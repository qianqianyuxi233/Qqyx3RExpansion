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
/// 塞壬的恩典（Sirine's Grace）——《万法大全》，吟游诗人 4 / 德鲁伊 5。
///
/// 效果：魅力与敏捷 **+4 增强加值**；AC 获得等于**魅力调整值**的**偏斜加值**。
///
/// 两个技术点：
/// <list type="bullet">
/// <item>「AC = 某属性的**调整值**」用 <c>ContextRankConfig { BaseValueType = StatBonus, m_Stat = Charisma }</c>
/// 取魅力调整值（<c>StatBonus</c> 就是取调整值，<c>BaseStat</c> 才是取总值），
/// 再交给 <c>AddContextStatBonus</c> 以 Rank 形式加到 AC 上；</item>
/// <item>魅力变化会影响这个 AC 加值本身 —— 原版就是这么联动的（先算魅力、再取调整值），
/// 所以 +4 魅力会同时抬高 AC，符合原文。</item>
/// </list>
///
/// 法术本体以原版「猫之优雅」为参照（同为**个人**距离的属性增强法术）。
/// </summary>
internal static class SirinesGrace {

    /// <summary>Spells/Level2/CatsGrace —— 原版猫之优雅（个人 / 属性增强，借结构与图标）</summary>
    private const string CatsGraceGuid = "de7a025d48ad5da4991e7d3c682cf69d";

    /// <summary>Classes/Bard/BardSpellList</summary>
    private const string BardSpellListGuid = "25a5013493bdcf74bb2424532214d0c8";
    /// <summary>Classes/Druid/DruidSpellList</summary>
    private const string DruidSpellListGuid = "bad8638d40639d04fa2f80a1cac67d6b";
    /// <summary>Spells/Level2/Grace —— 原版「优雅」（借图标：名字与 Grace 直接对应）</summary>
    private const string IconSourceGuid = "464a7193519429f48b4d190acb753cf0";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string SpellGuid = "9271d48631674830922715470dd696d3";
    private const string BuffGuid  = "4cf143ca49a546928867ebd5950f8850";

    private const int BardSpellLevel  = 4;
    private const int DruidSpellLevel = 5;
    /// <summary>魅力 / 敏捷的增强加值</summary>
    private const int AbilityBonus = 4;

    private const string SpellName = "塞壬的恩典";
    private const string SpellDescription =
        "在完成这个法术后，你被注入了超凡脱俗的优雅与自信。\n"
        + "在法术持续时间里，你在魅力和敏捷上获得 +4 增强加值，"
        + "AC 获得等于你魅力调整值的偏斜加值。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    private static void ConfigureBuff() {
        var icon = BlueprintTool.Get<BlueprintAbility>(IconSourceGuid);

        BuffConfigurator.New("SirinesGraceBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("SirinesGrace.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("SirinesGrace.Buff.Description", SpellDescription))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // ① 魅力 +4 增强
                    new AddStatBonus {
                        name = "$SirinesGrace$Charisma",
                        Stat = StatType.Charisma,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ② 敏捷 +4 增强
                    new AddStatBonus {
                        name = "$SirinesGrace$Dexterity",
                        Stat = StatType.Dexterity,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ③ AC += **当前**魅力调整值（偏斜加值）
                    //    ⚠ 必须实时取：用 ContextRankConfig 的 rank 是快照，
                    //    本法术自己给的 +4 魅力会进不去。详见 SirinesGraceComponents.cs。
                    new SirinesGraceCharismaToAC {
                        name = "$SirinesGrace$Deflection",
                        Descriptor = ModifierDescriptor.Deflection,
                    },
                }.ToArray();
            })
            .Configure();
    }

    private static void ConfigureSpell() {
        AbilityConfigurator.New("SirinesGrace", SpellGuid)
            // 只复制字段：Range = Personal、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(CatsGraceGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$SirinesGrace$SpellComponent",
                School = SpellSchool.Evocation,
            })
            // 持续时间「1 轮/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$SirinesGrace$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$SirinesGrace$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$SirinesGrace$Buff",
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Rounds,
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
                            ToCaster = true,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .AddToSpellList(BardSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(BardSpellListGuid))
            .AddToSpellList(DruidSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(DruidSpellListGuid))
            // 法术本体的图标必须显式覆盖：CopyFrom(猫之优雅) 会把猫之优雅的图标一起带过来
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(IconSourceGuid)?.Icon)
            .SetDisplayName(LocalizationTool.CreateString("SirinesGrace.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("SirinesGrace.Description", SpellDescription))
            // 持续时间文案：猫之优雅是「1 分钟/等级」，本法术是「1 轮/等级」
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "SirinesGrace.Duration", "1轮/等级"))
            .Configure();
    }
}
