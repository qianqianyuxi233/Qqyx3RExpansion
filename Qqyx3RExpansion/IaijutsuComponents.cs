using System;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 拔刀术的运行时状态：本次检定得到的骰数，以及虚空斩的「每骰加值」。
///
/// <para>
/// 放在 <see cref="UnitPart"/> 而不是 buff 组件字段上，是因为掷骰现在由
/// <see cref="ContextActionIaijutsuFocus"/> 在能力执行时完成 —— 它拿不到 buff 的运行时组件实例，
/// 只能通过 UnitPart 把结果传给伤害结算。
/// </para>
/// </summary>
public class UnitPartIaijutsuFocus : UnitPart {
    /// <summary>本次检定得到的额外 d6 数；0 表示没有额外伤害。</summary>
    public int DiceCount;
    /// <summary>虚空斩的每个骰子的固定加值（魅力调整值）；0 表示没有虚空斩。</summary>
    public int FlatBonusPerDie;
}

/// <summary>
/// 拔刀术能力的核心动作：掷检定 → 记结果 → 上 buff → 写战斗日志。
///
/// <para>
/// <b>为什么不用 buff 组件的 OnActivate 掷骰</b>：那条路径依赖
/// <c>EntityFact.Activate</c> 的调用时序，一旦组件没被激活（或 owner 尚未就绪），
/// 就会静默地什么都不做 —— 玩家看到的就是「用了能力，但没有任何检定」。
/// 放进 <c>AbilityEffectRunAction.Actions</c> 里则一定会在能力成功施放时执行。
/// </para>
///
/// <para>
/// 检定 = d20 + 角色等级 + 魅力调整值 + 各种加值（心无凝滞 / 拔刀专精 / 神话版）。
/// 加值全部按「身上有没有对应特性」判断，所以控制台单独添加也生效。
/// </para>
/// </summary>
[TypeId("095b5d4bbd7c4e5c91dc935ac662bdb6")]
public class ContextActionIaijutsuFocus : ContextAction {

    /// <summary>要挂上的拔刀术 buff</summary>
    public BlueprintBuffReference Buff;

    /// <summary>虚空斩（拔刀师 5 级）：每骰 + 魅力</summary>
    public BlueprintFeatureReference StrikeFromTheVoid;
    /// <summary>心无凝滞（拔刀师 10 级）：检定 +10</summary>
    public BlueprintFeatureReference StrikeWithNoThought;
    /// <summary>拔刀专精：检定 +4</summary>
    public BlueprintFeatureReference IaijutsuWeaponFocus;
    /// <summary>拔刀专精（神话）：检定 +6</summary>
    public BlueprintFeatureReference MythicIaijutsuWeaponFocus;

    public override string GetCaption() {
        return "拔刀术检定";
    }

    public override void RunAction() {
        var caster = Context == null ? null : Context.MaybeCaster;
        if (caster == null) {
            Main.Log?.Log("[拔刀术] 施法者为空，检定跳过");
            return;
        }

        try {
            int d20 = Rulebook.Trigger(new RuleRollD20(caster)).Result;
            int level = caster.Progression.CharacterLevel;
            int charisma = caster.Stats.Charisma.Bonus;

            int extra = 0;
            string extraText = "";

            if (HasFeature(caster, StrikeWithNoThought)) {
                extra += IaijutsuMaster.StrikeWithNoThoughtBonus;
                extraText += " + 心无凝滞 " + IaijutsuMaster.StrikeWithNoThoughtBonus;
            }
            if (HasFeature(caster, IaijutsuWeaponFocus)) {
                extra += IaijutsuFeats.WeaponFocusBonus;
                extraText += " + 拔刀专精 " + IaijutsuFeats.WeaponFocusBonus;
            }
            if (HasFeature(caster, MythicIaijutsuWeaponFocus)) {
                extra += IaijutsuFeats.MythicWeaponFocusBonus;
                extraText += " + 拔刀专精（神话） " + IaijutsuFeats.MythicWeaponFocusBonus;
            }

            int total = d20 + level + charisma + extra;
            int dice = IaijutsuFocusDamage.DiceForResult(total);
            int perDie = HasFeature(caster, StrikeFromTheVoid)
                ? Math.Max(0, charisma)
                : 0;

            // 把结果交给伤害结算
            var part = caster.Ensure<UnitPartIaijutsuFocus>();
            part.DiceCount = dice;
            part.FlatBonusPerDie = perDie;

            // 挂 buff（1 轮 ≈ 6 秒，取 7 秒留一点余量）
            var buffBlueprint = Buff == null ? null : Buff.Get();
            if (buffBlueprint != null) {
                caster.Descriptor.Buffs.AddBuff(buffBlueprint, Context, TimeSpan.FromSeconds(7));
            } else {
                Main.Log?.Log("[拔刀术] buff 蓝图解析失败，只结算伤害不显示图标");
            }

            string effect = dice > 0
                ? "获得额外 " + dice + "d6"
                    + (perDie > 0 ? "+" + (dice * perDie) : "")
                    + " 精准伤害"
                : "没有额外伤害（检定结果低于 5）";
            string text = "拔刀术检定：d20 " + d20
                + " + 等级 " + level
                + " + 魅力 " + charisma
                + extraText
                + " = " + total + "，" + effect + "。";

            EventBus.RaiseEvent<ILogMessageUIHandler>(h => h.HandleLogMessage(text));
            Main.Log?.Log("[拔刀术] " + text);
        } catch (Exception e) {
            Main.Log?.Log("[拔刀术] 检定异常：" + e);
        }
    }

    private static bool HasFeature(UnitEntityData unit, BlueprintFeatureReference reference) {
        if (reference == null) return false;
        try {
            var feature = reference.Get();
            return feature != null && unit.HasFact(feature);
        } catch {
            return false;
        }
    }
}

/// <summary>
/// 拔刀术的伤害结算：每次攻击结算时，若目标措手不及或正被夹击，就再塞一份精准伤害。
///
/// <para>
/// <b>免疫不用单独写</b>：把 <c>BaseDamage.Precision</c> 置 true 后，目标身上的
/// <c>AddImmunityToPrecisionDamage</c> 会在 <c>RuleCalculateDamage</c> 里给这份伤害挂
/// <c>DamageDecline(Totals)</c> —— 免疫偷袭的生物自动免疫拔刀术。
/// </para>
///
/// <para>
/// <b>时序</b>：<c>RuleDealDamage.OnTrigger</c> 里先 <c>Trigger&lt;RulePrepareDamage&gt;</c>
/// 再 <c>Trigger&lt;RuleCalculateDamage&gt;</c>，所以在这里加进 bundle 的伤害一定能被精准免疫处理。
/// </para>
///
/// <para>
/// 骰数与每骰加值来自 <see cref="UnitPartIaijutsuFocus"/>，由
/// <see cref="ContextActionIaijutsuFocus"/> 在能力执行时写入。
/// </para>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("936ee54265bb4772b3db2bcdb4e4fed8")]
public class IaijutsuFocusDamage : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RulePrepareDamage>, IRulebookHandler<RulePrepareDamage>,
    ISubscriber, IInitiatorRulebookSubscriber {

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
        if (evt == null) return;

        var owner = base.Owner;
        if (owner == null) return;

        var part = owner.Get<UnitPartIaijutsuFocus>();
        if (part == null || part.DiceCount <= 0) return;

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

        // 虚空斩：每个骰子额外 +魅力调整值（= 骰数 × 每骰加值的固定伤害）
        int flatBonus = part.DiceCount * part.FlatBonusPerDie;

        BaseDamage damage = typeDescription
            .GetDamageDescriptor(new DiceFormula(part.DiceCount, DiceType.D6), flatBonus)
            .CreateDamage();
        damage.Precision = true;

        evt.Add(damage);
    }

    public void OnEventDidTrigger(RulePrepareDamage evt) { }
}
