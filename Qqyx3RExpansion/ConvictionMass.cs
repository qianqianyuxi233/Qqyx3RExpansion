using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 群体信念（Conviction, Mass）——《万法大全》，牧师 3。
///
/// 效果同信念，只是影响多位同伴。<b>复用信念的 buff</b>（原文就是「作用如同信念术」）——
/// 这样两个法术也不会互相叠加，同一次施法只保留一个。
///
/// ⚠ 与原文的差异：3.5 写的是「中距（100 尺 + 10 尺/等级）+ 半径 20 尺爆发」，
/// 但**游戏里所有群体 buff 法术都是自身光环**——原版 <c>FeatherStepMass</c>、
/// <c>EnlargePersonMass</c>、<c>BlessingOfLuckAndResolveMass</c> 无一例外都用
/// <c>AbilityTargetsAround { TargetType = Ally }</c>（以施法者为中心），
/// 没有「选一个点、再在落点爆发挂 buff」的现成机制。
/// 所以这里做成**以自己为中心 20 尺**，实际效果是「给身边的同伴加豁免」。
///
/// 法术本体以原版「群体羽步」为参照（3 环群体 buff）。
/// </summary>
internal static class ConvictionMass {

    /// <summary>Spells/Level3/FeatherStepMass —— 原版群体羽步（群体 buff 的结构参照）</summary>
    private const string FeatherStepMassGuid = "d219494150ac1f24f9ce14a3d4f66d26";

    /// <summary>Classes/Cleric/ClericSpellList</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    /// <summary>Classes/Shaman/ShamanSpelllist —— 规则 A1</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>Classes/Inquisitor/InquisitorSpellList —— 规则 A2</summary>
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    /// <summary>Classes/Warpriest/WarpriestSpellList —— 规则 A2</summary>
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string SpellGuid = "50ce610ce5c943d8bce740bb6f83269d";

    private const int ClericSpellLevel = 3;
    /// <summary>光环半径（尺）—— 3.5 原文的爆发半径</summary>
    private const float RadiusFeet = 20f;

    private const string SpellName = "群体信念";
    private const string SpellDescription =
        "你高举着神圣的誓条并吟诵着法术的咒语，你附近的盟友全都沐浴在海蓝色的光辉之中。\n"
        + "这项法术的作用如同信念术，但是可以影响多位同伴："
        + "受术者的豁免检定获得 +2 士气加值，并且每 6 个施法者等级再 +1（18 级时达到最高的 +5）。";

    internal static void Configure() {
        AbilityConfigurator.New("ConvictionMass", SpellGuid)
            // 只复制字段：图标、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(FeatherStepMassGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$ConvictionMass$SpellComponent",
                School = SpellSchool.Abjuration,
            })
            // 持续时间「10 分钟/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$ConvictionMass$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            // 以自己为中心 20 尺，只选盟友
            .AddComponent(new AbilityTargetsAround {
                name = "$ConvictionMass$TargetsAround",
                m_Radius = new Feet(RadiusFeet),
                m_TargetType = TargetType.Ally,
                m_IncludeDead = false,
                // 这个字段不能为 null：AbilityTargetsAround.Select() 会读 m_Condition.HasConditions
                m_Condition = new ConditionsChecker {
                    Operation = Operation.And,
                    Conditions = new Condition[0],
                },
                m_SpreadSpeed = new Feet(0f),
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$ConvictionMass$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$ConvictionMass$BuffAction",
                            // 复用信念的 buff
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(Conviction.BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.TenMinutes,
                                DiceType = DiceType.Zero,
                                DiceCountValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = 0,
                                },
                                BonusValue = new ContextValue {
                                    ValueType = ContextValueType.Rank,
                                    ValueRank = AbilityRankType.Default,
                                },
                                m_IsExtendable = true,
                            },
                            DurationSeconds = 0f,
                            IsFromSpell = true,
                            IsNotDispelable = false,
                            ToCaster = false,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            // 硬性规则 A1：牧师表 → 萨满表同环
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid))
            // 硬性规则 A2：牧师表 + <6 环 + 战斗支援类 → 审判者 / 战斗祭司
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid))
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarpriestSpellListGuid))
            .SetDisplayName(LocalizationTool.CreateString("ConvictionMass.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("ConvictionMass.Description", SpellDescription))
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "ConvictionMass.Duration", "10分钟/等级"))
            .Configure();
    }
}
