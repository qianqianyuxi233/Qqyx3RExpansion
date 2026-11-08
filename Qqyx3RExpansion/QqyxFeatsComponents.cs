using System;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.Utility;


namespace Qqyx3RExpansion;

// =====================================================================================
// 影之刃（Shadow Blade）
// =====================================================================================

/// <summary>
/// 影之刃：手持**娴熟武器**、且拥有「武器娴熟」专长时，把**敏捷调整值**加到伤害检定上。
///
/// 注意与盗贼的「巧手」（<c>UnitPartDamageGrace</c>）的区别：巧手是**按武器类别**登记的，
/// 影之刃要求覆盖**所有娴熟武器**，所以这里直接判 <c>WeaponSubCategory.Finessable</c>。
///
/// 原文特别注明「并非替代力量，而是额外把敏捷加上去」—— 所以这里只做追加，
/// 不去动力量的伤害加值（<c>AddDamageModifier</c> 就是纯追加）。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("e5f6a8e97b704caa82650537bb2d2810")]
public class ShadowBladeDamage : UnitFactComponentDelegate, IInitiatorRulebookHandler<RuleCalculateWeaponStats> {

    /// <summary>「武器娴熟」专长 —— 用它确认角色确实在以武器娴熟的方式攻击。</summary>
    public BlueprintUnitFactReference m_FinesseFeat;

    public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt) {
        if (evt.Weapon == null) return;
        var weapon = evt.Weapon.Blueprint;
        if (weapon == null || weapon.Type == null) return;
        // 只对娴熟武器（轻型武器、细剑、鞭子等）
        if (!weapon.Type.Category.HasSubCategory(WeaponSubCategory.Finessable)) return;

        var finesse = m_FinesseFeat == null ? null : m_FinesseFeat.Get();
        if (finesse != null && !evt.Initiator.Descriptor.HasFact(finesse)) return;

        int dexBonus = evt.Initiator.Stats.Dexterity.Bonus;
        if (dexBonus == 0) return;

        evt.AddDamageModifier(dexBonus, base.Fact, ModifierDescriptor.UntypedStackable);
    }

    public void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }
}

// =====================================================================================
// 突击尖兵（Reckless Charge）
// =====================================================================================

/// <summary>
/// 突击尖兵 ·「轻率冲锋」战术机动：
/// 冲锋且启用猛力攻击时，把猛力攻击的**攻击检定减值转移到 AC** ——
/// 也就是攻击检定照常（用等量加值抵消掉减值），改为承受等量 AC 减值。
///
/// 减值量必须和猛力攻击自己算的一致：它的
/// <c>WeaponParametersAttackBonus</c> 用 <c>ScaleByBasicAttackBonus</c>，
/// 公式是 <c>1 + BAB/4</c>（<c>WeaponParametersAttackBonus.cs</c> 里 <c>1 + BaseAttackBonus.ModifiedValue / 4</c>），
/// 这里照同一个公式复现。
///
/// ⚠ 「是否处于冲锋状态」的判定对象是原版的 <c>ChargeBuffEffect</c>，**不是** <c>ChargeBuff</c>：
/// 后者是 <c>HiddenInUi</c> 的容器 buff，冲锋动作一结束就卸掉，而攻击结算 / AC 计算发生的时机更晚，
/// 查它会永远查不到。冲锋自己的 AC −2 与「持续 1 轮」的时长都挂在 <c>ChargeBuffEffect</c> 上
/// （<c>Root/SystemMechanics/Conditions/ChargeBuffEffect.jbp</c>，用
/// <c>AddContextStatBonus { Stat = AC, Descriptor = Penalty, Value = -2 }</c>）。
///
/// 减值量必须和猛力攻击自己算的一致：它的
/// <c>WeaponParametersAttackBonus</c> 用 <c>ScaleByBasicAttackBonus</c>，
/// 公式是 <c>1 + BAB/4</c>（<c>WeaponParametersAttackBonus.cs</c> 里 <c>1 + BaseAttackBonus.ModifiedValue / 4</c>），
/// 这里照同一个公式复现。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("18f43f04bd7847b28ca7cbed8b18b1cc")]
public class RecklessChargeBonus : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleCalculateAttackBonus>, IRulebookHandler<RuleCalculateAttackBonus>,
    ITargetRulebookHandler<RuleCalculateAC>, IRulebookHandler<RuleCalculateAC>,
    ISubscriber, IInitiatorRulebookSubscriber, ITargetRulebookSubscriber {

    /// <summary>原版冲锋的**效果** buff（ChargeBuffEffect），不是容器 ChargeBuff。</summary>
    public BlueprintBuffReference m_ChargeBuff;
    /// <summary>猛力攻击的效果 buff（Feats/PowerAttack/PowerAttackBuffEffect）。</summary>
    public BlueprintBuffReference m_PowerAttackBuff;

    /// <summary>猛力攻击此时的攻击减值量 = 1 + BAB/4。</summary>
    private static int PowerAttackPenalty(UnitEntityData unit) {
        int bab = Math.Max(0, unit.Stats.BaseAttackBonus.ModifiedValue);
        return 1 + bab / 4;
    }

    // ---- 攻击检定：抵消掉猛力攻击的减值 ----

    public void OnEventAboutToTrigger(RuleCalculateAttackBonus evt) {
        if (!IsRecklessCharging(evt.Initiator)) return;
        int penalty = PowerAttackPenalty(evt.Initiator);
        evt.AddModifier(penalty, base.Fact, ModifierDescriptor.UntypedStackable);
        Main.Log?.Log("[突击尖兵] 抵消猛力攻击减值：攻击检定 +" + penalty);
    }

    public void OnEventDidTrigger(RuleCalculateAttackBonus evt) { }

    // ---- AC：承受等量减值（持续到下一轮，由冲锋状态自然结束）----

    public void OnEventAboutToTrigger(RuleCalculateAC evt) {
        if (!IsRecklessCharging(evt.Target)) return;
        int penalty = PowerAttackPenalty(evt.Target);
        // 描述符照抄原版冲锋的 AC 惩罚（ChargeBuffEffect 用的就是 ModifierDescriptor.Penalty）
        evt.AddModifier(-penalty, ModifierDescriptor.Penalty);
        Main.Log?.Log("[突击尖兵] AC 承受 −" + penalty);
    }

    public void OnEventDidTrigger(RuleCalculateAC evt) { }

    private bool IsRecklessCharging(UnitEntityData unit) {
        if (unit == null || unit.Descriptor == null) return false;
        var charge = m_ChargeBuff == null ? null : m_ChargeBuff.Get();
        var powerAttack = m_PowerAttackBuff == null ? null : m_PowerAttackBuff.Get();
        if (charge == null || powerAttack == null) {
            Main.Log?.Log("[突击尖兵] buff 引用未配置（charge=" + (charge != null)
                + " powerAttack=" + (powerAttack != null) + "）");
            return false;
        }
        bool isCharging = unit.Descriptor.HasFact(charge);
        bool hasPowerAttack = unit.Descriptor.HasFact(powerAttack);
        if (isCharging && !hasPowerAttack) {
            Main.Log?.Log("[突击尖兵] 检测到冲锋状态，但未开启猛力攻击，不转移");
        }
        return isCharging && hasPowerAttack;
    }
}

// =====================================================================================
// 绿缚召唤（Greenbound Summoning）
// =====================================================================================

/// <summary>
/// 绿缚召唤：监听召唤流程，给「召唤自然盟友」召来的生物挂上绿缚模板 buff。
///
/// 挂在 <c>RuleSummonUnit</c> 上，是因为这个事件自带 <c>AddBuff(buff, duration)</c> / <c>AddFact</c>，
/// 而召唤流程（<c>RuleSummonUnit.OnTrigger</c>）会把它们真正施加到召唤物身上。
///
/// ⚠ 必须用 <c>OnEventAboutToTrigger</c>、不能用 <c>OnEventDidTrigger</c>：
/// <c>OnTrigger</c> 里读取的正是那份"待施加"列表，等事件走完再加就晚了。
///
/// buff 的时长直接取 <c>evt.Duration</c>，因此它天然等于召唤持续时间。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("133a4198743640fb86de661e0c533c7a")]
public class GreenboundSummoning : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleSummonUnit>, IRulebookHandler<RuleSummonUnit>,
    ISubscriber, IInitiatorRulebookSubscriber {

    public BlueprintBuffReference m_GreenboundBuff;

    public void OnEventAboutToTrigger(RuleSummonUnit evt) {
        var buff = m_GreenboundBuff == null ? null : m_GreenboundBuff.Get();
        if (buff == null) return;
        if (!IsSummonNaturesAlly(evt)) return;
        evt.AddBuff(buff, evt.Duration.Seconds);
        Main.Log?.Log("[绿缚召唤] 已给召唤物挂上绿缚模板，时长 "
            + evt.Duration.Seconds.TotalSeconds + " 秒");
    }

    public void OnEventDidTrigger(RuleSummonUnit evt) { }

    /// <summary>
    /// 只对「召唤自然盟友」系列生效。该系列每级有 3~4 个变体（Single / d3 / d4plus1 …），
    /// 逐个列 GUID 不现实，所以比对来源法术的**内部名**前缀 —— 内部名是英文，不受本地化影响。
    /// </summary>
    private static bool IsSummonNaturesAlly(RuleSummonUnit evt) {
        var ability = evt.Context == null ? null : evt.Context.SourceAbility;
        if (ability == null) return false;
        string name = ability.name;
        return !string.IsNullOrEmpty(name) && name.StartsWith("SummonNaturesAlly");
    }
}
