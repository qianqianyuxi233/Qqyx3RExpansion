using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 「祭献」法术成分（Book of Exalted Deeds 的圣洁法术）：施展法术时，**施法者自己**
/// 受到 NdX 点属性伤害（辉耀护甲 1d2 力量、高等辉耀护甲 1d3 力量）。
///
/// 为什么不用原版 <c>ContextActionDealDamage</c>：
/// 它的伤害目标恒为 <c>base.Target</c>（也就是法术的受术者）—— 原版没有任何字段能把它
/// 改成施法者自己（<c>ContextActionApplyBuff</c> 有 <c>ToCaster</c>，DealDamage 没有）。
/// 所以这里照原版 <c>ContextActionDealDamage.DealAbilityScoreDamage()</c> 的写法照抄了一遍
/// （同样是 <c>new RuleDealStatDamage(...)</c> + <c>Context.TriggerRule(...)</c>），
/// 只是把 target 换成 <c>Context.MaybeCaster</c>。
/// </summary>
public class ContextActionSacrificeStatDamage : ContextAction {

    /// <summary>受伤的属性（力量）。</summary>
    public StatType Stat = StatType.Strength;
    /// <summary>骰子个数。</summary>
    public int DiceCount = 1;
    /// <summary>骰子面数。</summary>
    public DiceType Dice = DiceType.D2;

    public override void RunAction() {
        var caster = Context.MaybeCaster;
        if (caster == null) return;

        var rule = new RuleDealStatDamage(caster, caster, Stat, new DiceFormula(DiceCount, Dice), 0);
        Context.TriggerRule(rule);

        Main.Log?.Log($"[辉耀护甲] 祭献：{caster.CharacterName} 受到 {rule.Result} 点{Stat}伤害"
            + $"（当前 {caster.Stats.GetStat(Stat).ModifiedValue}）");
    }

    public override string GetCaption() {
        return "Sacrifice: ability damage to caster";
    }
}

/// <summary>
/// 「辉耀护甲」的光芒：敌人以**近战**攻击受术者时，其攻击检定受 -4 减值。
///
/// 原版没有现成组件做这件事 —— 最接近的 <c>AttackBonusAgainstTarget</c> 只对
/// 「施法者本人及其同伴」生效（它的 ApplyValidation 强制要求 CheckCaster 或
/// CheckCasterFriend 为真），<c>ACBonusAgainstCaster</c> 也只针对施法者。
/// 所以这里照 <c>AttackBonusAgainstTarget</c> 的写法做了个不限定攻击者的版本：
/// buff 挂在受术者身上，它是攻击检定的 Target 时给这次攻击加一个负修正，
/// 并用 <c>WeaponRangeType.Melee.IsSuitableWeapon()</c>（原版同款扩展方法）筛掉远程攻击。
/// </summary>
[AllowedOn(typeof(BlueprintBuff), false)]
[TypeId("3f8c1d2aa8eb4a5ea679e8d8f9277816")]
public class LuminousArmorMeleeAttackPenalty : UnitBuffComponentDelegate,
    ITargetRulebookHandler<RuleCalculateAttackBonus>, IRulebookHandler<RuleCalculateAttackBonus>,
    ISubscriber, ITargetRulebookSubscriber {

    /// <summary>攻击检定修正（负数 = 减值）。</summary>
    public ContextValue Value = new ContextValue { ValueType = ContextValueType.Simple, Value = -4 };
    public ModifierDescriptor Descriptor = ModifierDescriptor.UntypedStackable;
    /// <summary>只对这类武器生效（近战）。</summary>
    public WeaponRangeType RangeType = WeaponRangeType.Melee;

    public void OnEventAboutToTrigger(RuleCalculateAttackBonus evt) {
        var weapon = evt.Weapon == null ? null : evt.Weapon.Blueprint;
        if (weapon == null || !RangeType.IsSuitableWeapon(weapon)) return;

        evt.AddModifier(Value.Calculate(base.Buff.Context), base.Fact, Descriptor);
    }

    public void OnEventDidTrigger(RuleCalculateAttackBonus evt) { }
}

/// <summary>
/// 「圣洁法术」的阵营限制：只有善良阵营（守序善良 / 中立善良 / 混乱善良）的角色才能施展。
///
/// 实现用的是游戏自带的 <see cref="IAbilityRestriction"/> 接口 —— 原版所有
/// 「这个能力现在能不能用」的判断都走它（<c>AbilityData.Restrictions</c>），
/// 检查不通过时法术在快捷栏里会变灰并显示 <see cref="GetAbilityRestrictionUIText"/>。
/// 原版用它做过武器 / 盾牌 / 装备等限制（AbilityRestrictionByWeapon 等），阵营限制没有先例。
/// </summary>
[AllowedOn(typeof(BlueprintAbility), false)]
[TypeId("3f8c1d2a2b8c439f943b9e842f23713e")]
public class AbilityRestrictionGoodAlignment : BlueprintComponent, IAbilityRestriction {

    public bool IsAbilityRestrictionPassed(AbilityData ability) {
        // AbilityData.Caster 是 UnitDescriptor（不是 UnitEntityData）
        var descriptor = ability == null ? null : ability.Caster;
        if (descriptor == null || descriptor.Alignment == null) return false;

        switch (descriptor.Alignment.ValueRaw) {
            case Alignment.LawfulGood:
            case Alignment.NeutralGood:
            case Alignment.ChaoticGood:
                return true;
            default:
                return false;
        }
    }

    public string GetAbilityRestrictionUIText() {
        return "只有善良阵营的角色可以施展该法术";
    }
}
