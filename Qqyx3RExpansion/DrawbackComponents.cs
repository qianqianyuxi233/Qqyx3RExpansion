using System;
using System.Text;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using UnityEngine;

namespace Qqyx3RExpansion;

// ============================================================================
// 缺陷（Drawbacks）用到的自定义组件。
//
// 大部分缺陷只用原版现成组件（AddStatBonus / AttackTypeAttackBonus）就够了，
// 这里放的是原版没有对应物、必须自己写的四个半：
//   1. 脆弱   —— 每级 HP -1（原版 ToughnessLogic 只会加，不会减）
//   2. 老花眼 —— 隐蔽失手骰投两次取低（原版 RerollConcealment 是取高）
//   3. 短腿   —— 移动速度减半（原版 BuffMovementSpeed 只有加减，没有乘算）
//   4. 可怜虫 —— 「所有属性修正合计 ≥ 8 时不能选」的前置
//   5. 短腿   —— 「基础速度 ≥ 20 尺才能选」的前置
// ============================================================================

/// <summary>
/// 脆弱（Frail）：每个等级获得的 HP -1。
/// 照抄原版 <c>ToughnessLogic</c>（「健壮」专长）的结构，只是把加号换成减号。
/// </summary>
[TypeId("3802f245fb594d218efe1673c10719ed")]
public class FrailHitPoints : UnitFactComponentDelegate, IOwnerGainLevelHandler, IUnitSubscriber, ISubscriber {

    public override void OnTurnOn() {
        Apply();
    }

    public override void OnTurnOff() {
        base.Owner.Stats.HitPoints.RemoveModifiersFrom(base.Runtime);
    }

    public void HandleUnitGainLevel() {
        Apply();
    }

    private void Apply() {
        base.Owner.Stats.HitPoints.RemoveModifiersFrom(base.Runtime);
        int level = base.Owner.Progression.CharacterLevel;
        if (level <= 0) return;
        base.Owner.Stats.HitPoints.AddModifier(-level, base.Runtime, ModifierDescriptor.UntypedStackable);
    }
}

/// <summary>
/// 老花眼（Murky-Eyed）：攻击隐蔽目标时投两次失手骰，任一次失手即失手。
///
/// 照抄原版 <c>RerollConcealment</c> 的结构，但 <c>takeBest: false</c> ——
/// <see cref="RuleRollDice.Reroll"/> 在 takeBest=false 时走 <c>Math.Min</c>，
/// 即取两颗里较差的那颗，于是更容易低于失手阈值而失手。
/// </summary>
[TypeId("0b3088c814d44ce1b0d5bd44f532283c")]
public class MurkyEyedConcealment : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleConcealmentCheck>, IRulebookHandler<RuleConcealmentCheck>,
    ISubscriber, IInitiatorRulebookSubscriber {

    public void OnEventAboutToTrigger(RuleConcealmentCheck evt) { }

    public void OnEventDidTrigger(RuleConcealmentCheck evt) {
        if (evt == null || evt.Roll == null) return;
        // 没有隐蔽就不用掷失手骰，没什么可重投的。
        if (!evt.ShouldRoll()) return;
        evt.Roll.Reroll(base.Fact, takeBest: false);
    }
}

/// <summary>
/// 短腿（Slow）：基本陆地移动速度减半，但至少减少 5 尺。
///
/// 原版 <c>BuffMovementSpeed</c> 只支持「加/减固定值」和「封顶」，没有乘算，
/// 所以这里在 OnTurnOn 时按当时的 BaseValue 算出惩罚值。
/// 注意：速度在升级/装备变化时不会自动重算 —— 对 1 级选定的缺陷来说，
/// 基础速度通常就是种族速度，重启游戏/重新装备后仍是同一个值，可以接受。
/// </summary>
[TypeId("074a78b01e6542a39b6bdee9d1c734b6")]
public class SlowMovementPenalty : UnitFactComponentDelegate {

    /// <summary>至少要减少的尺数（正数）。</summary>
    public int MinPenalty = 5;

    public override void OnTurnOn() {
        Apply();
    }

    public override void OnTurnOff() {
        base.Owner.Stats.Speed.RemoveModifiersFrom(base.Runtime);
    }

    private void Apply() {
        base.Owner.Stats.Speed.RemoveModifiersFrom(base.Runtime);
        int baseSpeed = base.Owner.Stats.Speed.BaseValue;
        if (baseSpeed <= 0) return;
        int half = -(baseSpeed / 2);
        int penalty = Math.Min(-MinPenalty, half);
        base.Owner.Stats.Speed.AddModifier(penalty, base.Runtime, ModifierDescriptor.UntypedStackable);
    }
}

/// <summary>
/// 前置：六项属性修正值之和必须小于 <see cref="MaxSum"/>。
/// 用于「可怜虫」（所有属性调整值合计在 8 或以上时不能选）。
/// </summary>
[TypeId("2dfe19a9c2004e65843dd5c695352022")]
public class PrerequisiteStatModifierSumBelow : Prerequisite {

    /// <summary>允许的上限（不含）。修正值合计 ≥ 此数时不可选。</summary>
    public int MaxSum = 8;

    private static readonly StatType[] Attributes = {
        StatType.Strength, StatType.Dexterity, StatType.Constitution,
        StatType.Intelligence, StatType.Wisdom, StatType.Charisma,
    };

    public override bool CheckInternal(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state) {
        if (unit == null) return false;
        int sum = 0;
        foreach (var stat in Attributes) {
            sum += ModifierOf(unit, stat);
        }
        return sum < MaxSum;
    }

    private static int ModifierOf(UnitDescriptor unit, StatType stat) {
        int value = unit.Stats.GetStat(stat).PermanentValue;
        // 3.5 的属性修正 = floor((属性值 - 10) / 2)，负数也要向下取整。
        return Mathf.FloorToInt((value - 10) / 2f);
    }

    public override string GetUITextInternal(UnitDescriptor unit) {
        var sb = new StringBuilder();
        sb.Append("所有属性调整值合计必须小于 ").Append(MaxSum);
        if (unit != null) {
            int sum = 0;
            foreach (var stat in Attributes) sum += ModifierOf(unit, stat);
            sb.Append("\n当前合计：").Append(sum);
        }
        return sb.ToString();
    }
}

/// <summary>
/// 前置：基本陆地移动速度必须至少 <see cref="MinSpeed"/> 尺。用于「短腿」。
/// </summary>
[TypeId("00c57ca63e5c4600a83609ba86c6c8e2")]
public class PrerequisiteBaseSpeedAtLeast : Prerequisite {

    public int MinSpeed = 20;

    public override bool CheckInternal(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state) {
        if (unit == null) return false;
        return unit.Stats.Speed.BaseValue >= MinSpeed;
    }

    public override string GetUITextInternal(UnitDescriptor unit) {
        var sb = new StringBuilder();
        sb.Append("基本陆地移动速度必须至少 ").Append(MinSpeed).Append(" 尺");
        if (unit != null) {
            sb.Append("\n当前速度：").Append(unit.Stats.Speed.BaseValue);
        }
        return sb.ToString();
    }
}
