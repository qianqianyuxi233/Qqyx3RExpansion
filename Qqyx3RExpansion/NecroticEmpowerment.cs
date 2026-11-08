using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
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
/// 腐囊强化（Necrotic Empowerment）——《死者之书》，牧师 8 / 术士·法师 8。
///
/// 死灵系 [邪恶]。目标自身，持续 1 轮/等级。获得：
/// <list type="bullet">
/// <item>敏捷 / 智力 / 感知 <b>+8 增强加值</b> → <c>AddStatBonus { Descriptor = Enhancement }</c></item>
/// <item>天生防御 <b>+8</b> → <c>AddStatBonus { Stat = AC, Descriptor = NaturalArmor }</c>
/// —— ⚠ 是**纯**天生防御加值，<b>不是</b>树肤术那种「天防**增强**加值」。3.5 里
/// 「natural armor bonus」与「enhancement bonus to natural armor」是**不同描述符、可以叠加**，
/// 腐囊强化的原文给的是前者；误用 Enhancement 会凭空多叠一层。</item>
/// <item>强韧豁免 <b>+5 环境加值</b> → <c>AddStatBonus { Stat = SaveFortitude, Descriptor = Circumstance }</c></item>
/// <item><b>100 点临时生命值</b> → <c>TemporaryHitPointsFromAbilityValue</c>，其 <c>Value</c> 是
/// <c>ContextValue</c>，直接给固定值 100 即可（原版拿它接一个共享值，这里不需要绕那圈）。</item>
/// </list>
///
/// 法术本体以原版「猫之优雅」为参照（同为**个人**距离的属性增强法术）。
/// </summary>
internal static class NecroticEmpowerment {

    /// <summary>Spells/Level2/CatsGrace —— 原版猫之优雅（个人 / 属性增强，借结构与图标）</summary>
    private const string CatsGraceGuid = "de7a025d48ad5da4991e7d3c682cf69d";

    /// <summary>Classes/Cleric/ClericSpellList</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    /// <summary>Classes/Shaman/ShamanSpelllist —— 规则 A1（牧师表的法术同环进萨满表）</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>Classes/Wizard/WizardSpellList —— 术士与法师共用</summary>
    private const string WizardSpellListGuid = "ba0401fdeb4062f40a7aa95b6f07fe89";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string SpellGuid = "051daa7ef55049b2acdfd1bcd3a5bc98";
    private const string BuffGuid  = "d2591c4af01b415a9eccc71d19ccc126";

    private const int ClericSpellLevel = 8;
    private const int WizardSpellLevel = 8;
    /// <summary>敏捷 / 智力 / 感知的增强加值</summary>
    private const int AbilityBonus = 8;
    /// <summary>天生防御加值</summary>
    private const int NaturalArmorBonus = 8;
    /// <summary>强韧豁免的环境加值</summary>
    private const int FortitudeBonus = 5;
    /// <summary>临时生命值</summary>
    private const int TemporaryHitPoints = 100;

    private const string SpellName = "腐囊强化";
    private const string SpellDescription =
        "你呼唤体内的腐囊母体，从它那里获得力量、精力、速度及坚定的邪恶意志。\n"
        + "法术时效内，你的敏捷、智力和感知获得 +8 增强加值；皮肤明显变粗变硬，"
        + "获得 +8 天生防御；强韧豁免获得 +5 环境加值；同时获得 100 点临时生命值。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    private static void ConfigureBuff() {
        var icon = BlueprintTool.Get<BlueprintAbility>(CatsGraceGuid);

        BuffConfigurator.New("NecroticEmpowermentBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("NecroticEmpowerment.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("NecroticEmpowerment.Buff.Description", SpellDescription))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // ① 敏捷 +8 增强
                    new AddStatBonus {
                        name = "$NecroticEmpowerment$Dexterity",
                        Stat = StatType.Dexterity,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ② 智力 +8 增强
                    new AddStatBonus {
                        name = "$NecroticEmpowerment$Intelligence",
                        Stat = StatType.Intelligence,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ③ 感知 +8 增强
                    new AddStatBonus {
                        name = "$NecroticEmpowerment$Wisdom",
                        Stat = StatType.Wisdom,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = AbilityBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ④ 天生防御 +8 —— 用**纯** NaturalArmor。
                    //    3.5 里「natural armor bonus」与「enhancement bonus to natural armor」
                    //    （树肤术那种）是不同描述符、可以叠加；腐囊强化的原文给的是前者。
                    new AddStatBonus {
                        name = "$NecroticEmpowerment$NaturalArmor",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.NaturalArmor,
                        Value = NaturalArmorBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ⑤ 强韧豁免 +5 环境加值
                    new AddStatBonus {
                        name = "$NecroticEmpowerment$Fortitude",
                        Stat = StatType.SaveFortitude,
                        Descriptor = ModifierDescriptor.Circumstance,
                        Value = FortitudeBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ⑥ 100 点临时生命值
                    new TemporaryHitPointsFromAbilityValue {
                        name = "$NecroticEmpowerment$TempHP",
                        Value = new ContextValue {
                            ValueType = ContextValueType.Simple,
                            Value = TemporaryHitPoints,
                        },
                        RemoveWhenHitPointsEnd = false,
                    },
                }.ToArray();
            })
            .Configure();
    }

    private static void ConfigureSpell() {
        AbilityConfigurator.New("NecroticEmpowerment", SpellGuid)
            // 只复制字段：Range = Personal、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(CatsGraceGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$NecroticEmpowerment$SpellComponent",
                School = SpellSchool.Necromancy,
            })
            // [邪恶]
            .AddComponent(new SpellDescriptorComponent {
                name = "$NecroticEmpowerment$SpellDescriptor",
                Descriptor = SpellDescriptor.Evil,
            })
            // 持续时间「1 轮/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$NecroticEmpowerment$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$NecroticEmpowerment$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$NecroticEmpowerment$BuffAction",
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
                            ToCaster = true,   // 目标自身
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            // 硬性规则 A1：牧师表 → 萨满表同环（8 环不适用 A2 的「<6 环」条件）
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid))
            .AddToSpellList(WizardSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WizardSpellListGuid))
            .SetDisplayName(LocalizationTool.CreateString("NecroticEmpowerment.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("NecroticEmpowerment.Description", SpellDescription))
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "NecroticEmpowerment.Duration", "1轮/等级"))
            .Configure();
    }
}
