using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 龙之力量（Draconic Might）——《万法大全》/《巨龙之书》，圣武士 4 / 法师·术士 5。
///
/// 效果：力量、体质、魅力 **+5 增强加值**；天生防御 **+4 增强加值**；
/// 免疫魔法睡眠与麻痹。
///
/// 技术点：
/// <list type="bullet">
/// <item>天生防御用 <c>AddStatBonus { Stat = AC, Descriptor = NaturalArmorEnhancement }</c>
/// —— 这就是原版「树肤术」的写法（<c>Spells/Level2/BarkskinBuff</c>）；</item>
/// <item>免疫麻痹 = <c>AddConditionImmunity { Condition = Paralyzed }</c>（原版「自由行动」同款）；
/// 免疫魔法睡眠同时给 <c>Condition = Sleeping</c> 与
/// <c>SpellImmunityToSpellDescriptor { Sleep }</c>，照原版
/// <c>CreatureAbilities/Immunities/ImmunityToSleep</c> 的做法。</item>
/// </list>
///
/// 法术本体以原版「牛之力量」为参照（同为**接触**距离、1 分钟/等级）。
/// </summary>
internal static class DraconicMight {

    /// <summary>Spells/Level2/BullsStrength —— 原版牛之力量（接触 / 属性增强）</summary>
    private const string BullsStrengthGuid = "4c3d08935262b6544ae97599b3a9556d";

    /// <summary>Classes/Paladin/PaladinSpellList</summary>
    private const string PaladinSpellListGuid = "9f5be2f7ea64fe04eb40878347b147bc";
    /// <summary>Classes/Wizard/WizardSpellList</summary>
    private const string WizardSpellListGuid = "ba0401fdeb4062f40a7aa95b6f07fe89";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string SpellGuid = "975070e0beb8426cb7e0eb4c3416caf6";
    private const string BuffGuid  = "fb3803a7dd9a4ed2a362ab1d81c20c73";

    private const int PaladinSpellLevel = 4;
    private const int WizardSpellLevel  = 5;
    /// <summary>力量 / 体质 / 魅力的增强加值</summary>
    private const int AbilityBonus = 5;
    /// <summary>天生防御的增强加值</summary>
    private const int NaturalArmorBonus = 4;

    private const string SpellName = "龙之力量";
    private const string SpellDescription =
        "在施展这个法术时，受术者的面孔暂时被一层黄色的精细鳞片纹理所覆盖；鳞片褪去，而金色的光辉仍在。\n"
        + "此法术的目标在力量、体质和魅力上获得 +5 增强加值，"
        + "在天生防御上获得 +4 增强加值。"
        + "最后，他免疫魔法睡眠和麻痹效果。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    private static void ConfigureBuff() {
        var icon = BlueprintTool.Get<BlueprintAbility>(BullsStrengthGuid);

        BuffConfigurator.New("DraconicMightBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("DraconicMight.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("DraconicMight.Buff.Description", SpellDescription))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    new AddStatBonus {
                        name = "$DraconicMight$Strength",
                        Stat = StatType.Strength,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    new AddStatBonus {
                        name = "$DraconicMight$Constitution",
                        Stat = StatType.Constitution,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    new AddStatBonus {
                        name = "$DraconicMight$Charisma",
                        Stat = StatType.Charisma,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 天生防御 +4 增强（树肤术同款写法）
                    new AddStatBonus {
                        name = "$DraconicMight$NaturalArmor",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.NaturalArmorEnhancement,
                        Value = NaturalArmorBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 免疫睡眠（状态）
                    new AddConditionImmunity {
                        name = "$DraconicMight$SleepImmunity",
                        Condition = UnitCondition.Sleeping,
                    },
                    // 免疫睡眠（法术描述符「魔法睡眠」）
                    new SpellImmunityToSpellDescriptor {
                        name = "$DraconicMight$SleepSpellImmunity",
                        Descriptor = SpellDescriptor.Sleep,
                    },
                    // 免疫麻痹
                    new AddConditionImmunity {
                        name = "$DraconicMight$ParalysisImmunity",
                        Condition = UnitCondition.Paralyzed,
                    },
                }.ToArray();
            })
            .Configure();
    }

    private static void ConfigureSpell() {
        AbilityConfigurator.New("DraconicMight", SpellGuid)
            // 只复制字段：Range = Touch、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(BullsStrengthGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$DraconicMight$SpellComponent",
                School = SpellSchool.Transmutation,
            })
            // 持续时间「1 分钟/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$DraconicMight$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$DraconicMight$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$DraconicMight$Buff",
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Minutes,
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
                            IsNotDispelable = false,   // 原文「可消解」
                            ToCaster = false,          // 接触目标，不是自己
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .AddToSpellList(PaladinSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(PaladinSpellListGuid))
            .AddToSpellList(WizardSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WizardSpellListGuid))
            .SetDisplayName(LocalizationTool.CreateString("DraconicMight.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("DraconicMight.Description", SpellDescription))
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "DraconicMight.Duration", "1分钟/等级"))
            .Configure();
    }
}
