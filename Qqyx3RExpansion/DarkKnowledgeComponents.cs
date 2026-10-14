using System;
using System.Collections.Generic;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 黑暗学识：掷**一次** d20 + 知识（世界），按出目决定档位，然后给自己与 60 尺内的同伴
/// 挂上对应档位的 buff（战术 / 影响 / 对手 / 预知，按卷宗学者等级解锁）。
/// 所有 buff 的效果都只对恶魔生效（见 DarkKnowledge.DemonFactGuid）。
///
/// 档位：出目 &gt; 35 → 第 3 档（+3）；&gt; 25 → 第 2 档（+2）；&gt; 15 → 第 1 档（+1）；否则无效果。
/// </summary>
public class ContextActionDarkKnowledge : ContextAction {

    public StatType Stat = StatType.SkillKnowledgeWorld;

    /// <summary>战术（1 级起）：攻击加值三档，索引 0/1/2 对应第 1/2/3 档。</summary>
    public BlueprintBuffReference[] TacticsBuffs;
    /// <summary>影响（5 级起）：豁免加值三档。</summary>
    public BlueprintBuffReference[] PuissanceBuffs;
    /// <summary>对手（8 级起）：伤害加值三档。</summary>
    public BlueprintBuffReference[] FoeBuffs;
    /// <summary>预知（14 级起）：AC 加值三档。</summary>
    public BlueprintBuffReference[] ForeknowledgeBuffs;

    public int PuissanceLevel = 5;
    public int FoeLevel = 8;
    public int ForeknowledgeLevel = 14;

    /// <summary>影响半径（尺）</summary>
    public int RadiusFeet = 60;
    /// <summary>持续时间（分钟）</summary>
    public int DurationMinutes = 1;

    public override void RunAction() {
        var caster = Context.MaybeCaster;
        if (caster == null) return;

        // 只掷一次骰
        int roll = RulebookEvent.Dice.D(1, DiceType.D20);
        int skill = caster.Stats.GetStat(Stat).ModifiedValue;
        int total = roll + skill;

        int tier = total > 35 ? 3 : total > 25 ? 2 : total > 15 ? 1 : 0;
        if (tier == 0) {
            Main.Log?.Log($"[黑暗学识] 知识（世界）检定 {total}（d20 {roll} + {skill}）未超过 15，无效果");
            return;
        }

        int classLevel = caster.Progression.GetClassLevel(
            BlueprintTool.Get<BlueprintCharacterClass>(Archivist.ClassGuid));

        var buffs = new List<BlueprintBuff>();
        Collect(buffs, TacticsBuffs, tier, 1, classLevel);
        Collect(buffs, PuissanceBuffs, tier, PuissanceLevel, classLevel);
        Collect(buffs, FoeBuffs, tier, FoeLevel, classLevel);
        Collect(buffs, ForeknowledgeBuffs, tier, ForeknowledgeLevel, classLevel);

        if (buffs.Count == 0) return;

        var duration = TimeSpan.FromMinutes(DurationMinutes);
        float radius = RadiusFeet.Feet().Meters;
        int affected = 0;

        foreach (var unit in GameHelper.GetTargetsAround(caster.Position, radius, true, false)) {
            if (unit == null || unit.IsEnemy(caster)) continue;   // 只给友军（自己也不是自己的敌人）

            foreach (var buff in buffs) {
                unit.Buffs.AddBuff(buff, caster, duration);
            }
            affected++;
        }

        Main.Log?.Log($"[黑暗学识] 知识（世界）检定 {total}（d20 {roll} + {skill}）→ 第 {tier} 档；"
            + $"卷宗学者 {classLevel} 级 → 施加 {buffs.Count} 个效果，覆盖 {affected} 个单位");
    }

    /// <summary>该变体已解锁则加入本次要施加的 buff 列表。</summary>
    private static void Collect(List<BlueprintBuff> into, BlueprintBuffReference[] refs,
                                int tier, int unlockLevel, int classLevel) {
        if (refs == null || refs.Length < tier || classLevel < unlockLevel) return;
        var buff = refs[tier - 1]?.Get();
        if (buff != null) into.Add(buff);
    }

    public override string GetCaption() {
        return "Dark Knowledge (d20 + Knowledge: World)";
    }
}

/// <summary>
/// 对拥有指定 Fact（恶魔）的目标额外造成 Nd6 伤害。
///
/// 游戏自带的 <c>DamageBonusAgainstFactOwner</c> 只能加**固定值**，
/// 所以这里照它的写法（<c>evt.Initiator.Stats.AdditionalDamage.AddModifier</c>）做了个掷骰版：
/// 每次命中时掷 Nd6，把结果加进额外伤害。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[AllowMultipleComponents]
[TypeId("3f8c1d2a5b6e4f7a9c0d1e2f3a4b5c6d")]
public class DarkKnowledgeDamageDice : UnitFactComponentDelegate, IInitiatorRulebookHandler<RuleAttackWithWeapon> {

    public BlueprintUnitFactReference m_CheckedFact;
    public int DiceCount = 1;
    public ModifierDescriptor Descriptor = ModifierDescriptor.UntypedStackable;

    public BlueprintUnitFact CheckedFact => m_CheckedFact == null ? null : m_CheckedFact.Get();

    public void OnEventAboutToTrigger(RuleAttackWithWeapon evt) {
        if (evt.Weapon == null) return;

        var fact = CheckedFact;
        if (fact == null) return;
        if (evt.Target == null || evt.Target.Descriptor == null) return;
        if (!evt.Target.Descriptor.HasFact(fact)) return;

        int roll = RulebookEvent.Dice.D(DiceCount, DiceType.D6);
        evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalDamage.AddModifier(
            roll, base.Runtime, Descriptor));
    }

    public void OnEventDidTrigger(RuleAttackWithWeapon evt) { }
}
