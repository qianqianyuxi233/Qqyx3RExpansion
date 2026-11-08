using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Enums;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 信念（Conviction）——《万法大全》，牧师 1。
///
/// 效果：受术者豁免检定获得 **+2 士气加值**，并且**每 6 个施法者等级再 +1**
/// （18 级时达到最高的 +5）。
///
/// 关键在「2 + CL/6」这个式子。原版的 <c>ContextRankProgression.StartPlusDivStep</c> 实现是：
/// <code>return 1 + Math.Max((value - m_StartLevel) / m_StepLevel, 0);</code>
/// 取 <c>m_StartLevel = -6, m_StepLevel = 6</c> 代入即可得 <c>1 + (CL + 6) / 6 = 2 + CL/6</c>：
///
///   CL 1  → 2      CL 6  → 3      CL 12 → 4      CL 18 → 5      CL 24 → 6（被 m_Max 截到 5）
///
/// 与 3.5 原文完全一致，不需要写自定义组件。三个豁免各挂一个 <c>AddContextStatBonus</c>，
/// 描述符统一用「士气」。
///
/// 法术本体以原版「提升抗力」为参照（同为触碰距离的豁免加值法术）。
/// </summary>
internal static class Conviction {

    /// <summary>Spells/Level0/Resistance —— 原版提升抗力（触碰 / 豁免加值）</summary>
    private const string ResistanceGuid = "7bc8e27cba24f0e43ae64ed201ad5785";

    /// <summary>Classes/Cleric/ClericSpellList</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    /// <summary>Classes/Shaman/ShamanSpelllist —— 规则 A1</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>Classes/Inquisitor/InquisitorSpellList —— 规则 A2</summary>
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    /// <summary>Classes/Warpriest/WarpriestSpellList —— 规则 A2</summary>
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";

    // ---------- 本 mod 新增蓝图 GUID ----------
    internal const string SpellGuid = "a4fe60b668ea405cbf498a2bfd33eab9";
    internal const string BuffGuid  = "d3f7a1222e774c6cb0c6f88be282bdd8";

    internal const int ClericSpellLevel = 1;
    /// <summary>加值上限（18 级 +5）</summary>
    private const int MaxBonus = 5;

    internal const string SpellName = "信念";
    internal const string SpellDescription =
        "你手持一张神圣的誓条碰触盟友并吟诵法术，海蓝色的光辉从他的头与肩迸发出来，加强了他的决心。\n"
        + "受术者的豁免检定获得 +2 士气加值，并且每 6 个施法者等级再 +1（18 级时达到最高的 +5）。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    /// <summary>信念 Buff：三个豁免的士气加值（可被群体信念复用）。</summary>
    internal static void ConfigureBuff(string blueprintName = "ConvictionBuff", string guid = BuffGuid,
                                        string nameKey = "Conviction") {
        var icon = BlueprintTool.Get<BlueprintAbility>(ResistanceGuid);

        BuffConfigurator.New(blueprintName, guid)
            .SetDisplayName(LocalizationTool.CreateString(nameKey + ".Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString(nameKey + ".Buff.Description", SpellDescription))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // rank = 2 + CL/6，上限 5（StartPlusDivStep: 1 + (CL - (-6)) / 6）
                    new ContextRankConfig {
                        name = "$Conviction$Rank",
                        m_Type = AbilityRankType.Default,
                        m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                        m_Progression = ContextRankProgression.StartPlusDivStep,
                        m_StartLevel = -6,
                        m_StepLevel = 6,
                        m_UseMax = true,
                        m_Max = MaxBonus,
                    },
                    new AddContextStatBonus {
                        name = "$Conviction$Fortitude",
                        Stat = StatType.SaveFortitude,
                        Descriptor = ModifierDescriptor.Morale,
                        Multiplier = 1,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Rank,
                            ValueRank = AbilityRankType.Default,
                        },
                    },
                    new AddContextStatBonus {
                        name = "$Conviction$Reflex",
                        Stat = StatType.SaveReflex,
                        Descriptor = ModifierDescriptor.Morale,
                        Multiplier = 1,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Rank,
                            ValueRank = AbilityRankType.Default,
                        },
                    },
                    new AddContextStatBonus {
                        name = "$Conviction$Will",
                        Stat = StatType.SaveWill,
                        Descriptor = ModifierDescriptor.Morale,
                        Multiplier = 1,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Rank,
                            ValueRank = AbilityRankType.Default,
                        },
                    },
                }.ToArray();
            })
            .Configure();
    }

    private static void ConfigureSpell() {
        AbilityConfigurator.New("Conviction", SpellGuid)
            // 只复制字段：Range = Touch、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(ResistanceGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$Conviction$SpellComponent",
                School = SpellSchool.Abjuration,
            })
            // 持续时间「10 分钟/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$Conviction$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$Conviction$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$Conviction$BuffAction",
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
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
                            ToCaster = false,   // 触碰目标
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
            .SetDisplayName(LocalizationTool.CreateString("Conviction.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Conviction.Description", SpellDescription))
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "Conviction.Duration", "10分钟/等级"))
            .Configure();
    }
}
