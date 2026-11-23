using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Controllers.Projectiles;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 拔刀师 1 级：精明防御（Canny Defense，EX）。
///
/// <para>
/// 3.5 原文：不穿着防具时，可以把智力调整值额外加在防护等级上；措手不及时失效。
/// </para>
///
/// <para>
/// 实现要点：
/// <list type="bullet">
/// <item>用 <c>RuleCalculateAC</c> 的 Target 侧 handler 动态加值，所以智力被buff/伤害改变时 AC 会跟着动；</item>
/// <item><c>Body.Armor.HasArmor</c> 只看护甲槽，盾牌不算「防具」，与 3.5 措辞一致；</item>
/// <item><c>RuleCalculateAC.IsTargetFlatFooted</c> 要到它自己的 OnTrigger 才算出来，
/// 在 AboutToTrigger 阶段还是 false，所以这里自己触发一次
/// <c>RuleCheckTargetFlatFooted</c> —— 与游戏判定偷袭用的是同一个规则。</item>
/// </list>
/// </para>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("bcfb70929629450aa1be097cb12fe7cb")]
public class IaijutsuMasterCannyDefense : UnitFactComponentDelegate,
    ITargetRulebookHandler<RuleCalculateAC>, IRulebookHandler<RuleCalculateAC>,
    ISubscriber, ITargetRulebookSubscriber {

    public void OnEventAboutToTrigger(RuleCalculateAC evt) {
        var unit = evt.Target;
        if (unit == null) return;

        // 穿着防具则不生效
        if (unit.Body.Armor.HasArmor) return;

        // 措手不及时不生效
        if (Rulebook.Trigger(new RuleCheckTargetFlatFooted(evt.Initiator, unit)).IsFlatFooted) return;

        int intelligence = unit.Stats.Intelligence.Bonus;
        if (intelligence == 0) return;

        evt.AddModifier(intelligence, ModifierDescriptor.UntypedStackable);
    }

    public void OnEventDidTrigger(RuleCalculateAC evt) { }
}

/// <summary>
/// 拔刀师 2 级：出手如电（Lightning Blade，EX）。
///
/// <para>
/// 3.5 原文：把魅力调整值额外加到先攻检定上。
/// </para>
///
/// <para>
/// <c>RuleInitiativeRoll.OnTrigger</c> 才把 <c>Modifier</c> 赋成先攻总值、才掷 D20，
/// 而 <c>Result</c> 是动态属性 <c>D20 + Modifier</c>；调用方在 TriggerEvent 返回后才读 Result，
/// 所以在 <c>OnEventDidTrigger</c> 里改 Modifier 仍然有效（与预知戒指改 D20 是同一套时序）。
/// </para>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("84253e6f60dd42eab782115796c3bcfb")]
public class IaijutsuMasterLightningBlade : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleInitiativeRoll>, IRulebookHandler<RuleInitiativeRoll>,
    ISubscriber, IInitiatorRulebookSubscriber {

    public void OnEventAboutToTrigger(RuleInitiativeRoll evt) { }

    public void OnEventDidTrigger(RuleInitiativeRoll evt) {
        if (evt == null) return;
        // 先攻被外部指定时（例如召唤物继承主人先攻）加值没有意义
        if (evt.IsOverriden) return;

        int charisma = base.Owner.Stats.Charisma.Bonus;
        if (charisma == 0) return;

        evt.Modifier = evt.Modifier + charisma;
    }
}

/// <summary>
/// 拔刀师 8 级「二段斩（One Strike, Two Cuts）」的交付逻辑：
/// 用一个标准动作对同一目标做两次武器攻击。
///
/// <para>
/// 直接照抄原版 <c>AbilityDeliverAttackWithWeapon</c>（它只做一次单次攻击），
/// 在外面套一层 <see cref="AttackCount"/> 次循环。用 <c>UnitAttack</c> 命令而不是直接
/// trigger <c>RuleAttackWithWeapon</c>，是为了让攻击动画、命中时机、投射物都按游戏原本的流程走。
/// </para>
///
/// <para>
/// 规则原文要求「手持武士刀」。游戏里没有武士刀这类武器，
/// 这里放宽为「手持近战武器」；远程武器直接中断，避免出现用弓连射两次的怪状。
/// </para>
/// </summary>
[AllowedOn(typeof(Kingmaker.UnitLogic.Abilities.Blueprints.BlueprintAbility), false)]
[TypeId("05de9c25f88347ed96674c2edd758d1b")]
public class AbilityDeliverRepeatedWeaponAttacks : AbilityDeliverEffect {

    /// <summary>攻击次数（二段斩 = 2）。</summary>
    public int AttackCount = 2;

    public override bool IsEngageUnit {
        get { return true; }
    }

    public override IEnumerator<AbilityDeliveryTarget> Deliver(
            AbilityExecutionContext context, TargetWrapper target) {
        if (target.Unit == null) {
            Main.Log?.Log("[拔刀师] 二段斩：目标单位缺失");
            yield break;
        }

        for (int i = 0; i < AttackCount; i++) {
            UnitAttack command = new UnitAttack(target.Unit, null) {
                IsSingleAttack = true,
            };
            command.Init(context.Caster);
            command.Start();

            AttackHandInfo hand = command.AllAttacks.FirstOrDefault<AttackHandInfo>();
            ItemEntityWeapon weapon = hand != null ? hand.Weapon : null;
            if (weapon == null) {
                Main.Log?.Log("[拔刀师] 二段斩：没有可用的武器");
                command.Interrupt(true);
                yield break;
            }
            if (!weapon.Blueprint.IsMelee) {
                // 非近战武器不能使用二段斩
                command.Interrupt(true);
                yield break;
            }

            bool hitHandled = false;
            while (true) {
                if (command.IsFinished
                        && !command.LaunchedProjectiles.HasItem<Projectile>(
                            p => (p.OnTheWay || p.HitThisFrame) && !p.IsHitWall)) {
                    break;
                }

                bool wasActed = command.IsActed;
                if (!command.IsFinished) {
                    command.Tick();
                }

                if (command.LaunchedProjectiles.HasItem<Projectile>(p => p.HitThisFrame)) {
                    hitHandled = true;
                    yield return new AbilityDeliveryTarget(target);
                }
                if (!wasActed && command.IsActed) {
                    hitHandled = true;
                    yield return new AbilityDeliveryTarget(target);
                }
                yield return null;
            }

            if (!hitHandled) {
                var lastAttack = command.LastAttackRule;
                if (lastAttack != null && lastAttack.AttackRoll.IsHit) {
                    yield return new AbilityDeliveryTarget(target);
                }
            }
        }
    }
}
