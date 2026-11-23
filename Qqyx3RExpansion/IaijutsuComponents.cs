using System;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 拔刀术（Iaijutsu Focus）的伤害结算。
///
/// <para>
/// 挂在拔刀术 buff 上：buff 激活时掷一次检定（d20 + 角色等级 + 魅力调整值），
/// 把档位对应的 d6 数记在 <see cref="DiceCount"/>；之后每次攻击结算伤害时，
/// 若目标是「措手不及」或「正被我方夹击」，就往这次伤害里再塞一份精准伤害。
/// </para>
///
/// <para>
/// <b>为什么免疫不用单独写</b>：只要把 <c>BaseDamage.Precision</c> 置为 true，
/// 目标身上的 <c>AddImmunityToPrecisionDamage</c> 就会在
/// <c>RuleCalculateDamage.OnEventAboutToTrigger</c> 里给这份伤害挂
/// <c>DamageDecline(Totals)</c> —— 免疫偷袭的生物自动免疫拔刀术。
/// </para>
///
/// <para>
/// <b>为什么事件顺序是对的</b>：<c>RuleDealDamage.OnTrigger</c> 里先
/// <c>Trigger&lt;RulePrepareDamage&gt;</c>（第 209 行）再 <c>Trigger&lt;RuleCalculateDamage&gt;</c>
/// （第 211 行）。本组件在 PrepareDamage 阶段把伤害塞进 <c>ParentRule</c> 的 bundle，
/// 所以紧接着的 CalculateDamage 一定能看见它，精准免疫也就能正确生效。
/// </para>
///
/// <para>
/// <b>措手不及/夹击的判定</b>与游戏自身的偷袭判定完全一致：
/// <c>attackRoll.IsTargetFlatFooted || Target.CombatState.IsFlanked</c>
/// （见 <c>RuleAttackRoll.cs:383</c>）。直接复用攻击检定上已经算好的结果，
/// 不重复 <c>RuleCheckTargetFlatFooted</c>，避免多触发一轮事件。
/// </para>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("936ee54265bb4772b3db2bcdb4e4fed8")]
public class IaijutsuFocusDamage : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RulePrepareDamage>, IRulebookHandler<RulePrepareDamage>,
    ISubscriber, IInitiatorRulebookSubscriber {

    /// <summary>本次拔刀术检定对应的额外 d6 数量；0 表示没有额外伤害。</summary>
    public int DiceCount;

    /// <summary>
    /// 已经掷过骰的标记。buff 被 Replace 时是新的运行时对象（字段重新为 false），
    /// 所以每次重新使用能力都会重掷；读档时字段丢失，靠 <see cref="OnPostLoad"/> 补掷。
    /// </summary>
    private bool m_Rolled;

    public override void OnActivate() {
        RollIfNeeded();
    }

    public override void OnPostLoad() {
        RollIfNeeded();
    }

    private void RollIfNeeded() {
        if (m_Rolled) return;
        var owner = base.Owner;
        if (owner == null) return;
        m_Rolled = true;

        try {
            // d20 + 角色等级 + 魅力调整值
            int d20 = Rulebook.Trigger(new RuleRollD20(owner)).Result;
            int level = owner.Progression.CharacterLevel;
            int charisma = owner.Stats.Charisma.Bonus;
            int total = d20 + level + charisma;

            DiceCount = DiceForResult(total);

            string effect = DiceCount > 0
                ? "获得额外 " + DiceCount + "d6 精准伤害"
                : "没有额外伤害（检定结果低于 5）";
            string text = "拔刀术检定：d20 " + d20
                + " + 等级 " + level
                + " + 魅力 " + charisma
                + " = " + total + "，" + effect + "。";

            EventBus.RaiseEvent<ILogMessageUIHandler>(h => h.HandleLogMessage(text));
            Main.Log?.Log("[拔刀术] " + text);
        } catch (Exception e) {
            Main.Log?.Log("[拔刀术] 检定失败：" + e);
        }
    }

    /// <summary>
    /// 检定结果 → 额外 d6 数。
    /// 5–9 / 10–14 / 15–19 / 20–24 / 25–29 / 30–34 / 35–39 / 40–44 / 45+。
    /// </summary>
    internal static int DiceForResult(int result) {
        if (result >= 45) return 9;
        if (result >= 40) return 8;
        if (result >= 35) return 7;
        if (result >= 30) return 6;
        if (result >= 25) return 5;
        if (result >= 20) return 4;
        if (result >= 15) return 3;
        if (result >= 10) return 2;
        if (result >= 5) return 1;
        return 0;
    }

    public void OnEventAboutToTrigger(RulePrepareDamage evt) {
        if (evt == null || DiceCount <= 0) return;

        // 只处理「一次攻击对同一目标造成的伤害」。法术等没有攻击检定的伤害不触发拔刀术。
        var attackRoll = evt.ParentRule.AttackRoll;
        if (attackRoll == null) return;
        if (attackRoll.Target != evt.Target) return;

        // 与偷袭完全一致的判定：目标措手不及，或目标正被夹击。
        if (!attackRoll.IsTargetFlatFooted && !evt.Target.CombatState.IsFlanked) return;

        var bundle = evt.DamageBundle;
        if (bundle == null) return;
        var first = bundle.FirstOrDefault<BaseDamage>();
        if (first == null) return;

        // 跟随武器原本的伤害类型（物理/能量），再叠加一份精准伤害。
        DamageTypeDescription typeDescription = first.CreateTypeDescription();
        if (typeDescription == null) return;

        BaseDamage damage = typeDescription
            .GetDamageDescriptor(new DiceFormula(DiceCount, DiceType.D6), 0)
            .CreateDamage();
        damage.Precision = true;

        evt.Add(damage);
    }

    public void OnEventDidTrigger(RulePrepareDamage evt) { }
}
