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
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则法术「高等提升抗力 / 进阶提升抗力」。
///
/// 完全照抄原版 0 环「提升抗力」：
///   Spells/Level0/Resistance.jbp      → 7bc8e27cba24f0e43ae64ed201ad5785
///   Spells/Level0/ResistanceBuff.jbp  → df680f6687f935e408eba6fb5124930e
/// 原版 buff 整个就一个组件：
///   BuffAllSavesBonus { Descriptor = Resistance, Value = 1 }
/// 本 mod 只把 Value 改成 3 / 6，并把持续时间从 1 分钟改成 24 小时。
///
/// 法术表（3.5 原文：吟游诗人/牧师/德鲁伊/术士法师，4 环与 6 环）：
///   BardSpellList   25a5013493bdcf74bb2424532214d0c8
///   ClericSpellList 8443ce803d2d31347897a3d85cc32f53
///   DruidSpellList  bad8638d40639d04fa2f80a1cac67d6b
///   WizardSpellList ba0401fdeb4062f40a7aa95b6f07fe89  ← 术士共用这张
///     （已核对 Classes/Sorcerer/SorcererSpellbook.jbp 的 m_SpellList）
///
/// 其余全部继承原版提升抗力：图标 b3355206cc666b543addf8d60df20299、距离 = 接触、
/// 施法时间 = 标准动作、防护系、buff 的 Stacking = Replace / m_Flags = StayOnDeath。
/// </summary>
internal static class ResistanceSpells {

    // ---------- 原版参照物 GUID ----------

    /// <summary>Spells/Level0/Resistance —— 原版 0 环提升抗力</summary>
    private const string ResistanceGuid = "7bc8e27cba24f0e43ae64ed201ad5785";
    /// <summary>Spells/Level0/ResistanceBuff</summary>
    private const string ResistanceBuffGuid = "df680f6687f935e408eba6fb5124930e";

    // ---------- 法术表 GUID ----------

    private const string BardSpellListGuid = "25a5013493bdcf74bb2424532214d0c8";
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    private const string DruidSpellListGuid = "bad8638d40639d04fa2f80a1cac67d6b";
    /// <summary>WizardSpellList —— 法师与术士共用</summary>
    private const string WizardSpellListGuid = "ba0401fdeb4062f40a7aa95b6f07fe89";

    // ---------- 硬性规则（见 MODDING_NOTES §2.5）连带登记用的表 ----------

    /// <summary>ShamanSpelllist —— 规则 A1</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>HunterSpellList —— 规则 A3（德鲁伊表且 &lt;6 环）</summary>
    private const string HunterSpellListGuid = "d090b791bfe381740b98ed4ff909b1cf";
    /// <summary>InquisitorSpellList —— 规则 A2</summary>
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    /// <summary>WarpriestSpellList —— 规则 A2</summary>
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string GreaterSpellGuid = "938b3c5058ca4fdca79be8566ad42559";
    private const string GreaterBuffGuid = "aed7a04195414c9388abc41702e97f16";
    private const string SuperiorSpellGuid = "c649af6cd90d4606810a2a891c3e6036";
    private const string SuperiorBuffGuid = "fece82bf5c584fd98409f4c37f7795f5";

    /// <summary>持续时间：24 小时</summary>
    private const int DurationHours = 24;

    internal static void Configure() {
        Build(
            blueprintName: "ResistanceGreater",
            spellGuid: GreaterSpellGuid,
            buffGuid: GreaterBuffGuid,
            displayName: "高等提升抗力",
            description: "该法术作用类似提升抗力，但是受术者得到的豁免加值为+3，且持续时间更长。",
            spellLevel: 4,
            saveBonus: 3);

        Build(
            blueprintName: "ResistanceSuperior",
            spellGuid: SuperiorSpellGuid,
            buffGuid: SuperiorBuffGuid,
            displayName: "进阶提升抗力",
            description: "该法术作用类似提升抗力，但是受术者得到的豁免加值为+6，且持续时间更长。",
            spellLevel: 6,
            saveBonus: 6);
    }

    /// <summary>两个法术结构完全一样，只有环数、加值和文本不同。</summary>
    private static void Build(string blueprintName, string spellGuid, string buffGuid,
                              string displayName, string description,
                              int spellLevel, int saveBonus) {
        ConfigureBuff(blueprintName, buffGuid, displayName, description, saveBonus);
        ConfigureSpell(blueprintName, spellGuid, buffGuid, displayName, description, spellLevel);
    }

    /// <summary>Buff：强韧 / 反射 / 意志 +N 抗力加值（原版提升抗力同款组件）。</summary>
    private static void ConfigureBuff(string blueprintName, string buffGuid,
                                      string displayName, string description, int saveBonus) {
        BuffConfigurator.New(blueprintName + "Buff", buffGuid)
            // 只复制字段：图标（提升抗力图标）、Stacking = Replace、m_Flags = StayOnDeath
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(ResistanceBuffGuid), c => false)
            .SetDisplayName(LocalizationTool.CreateString(blueprintName + ".Buff.Name", displayName))
            .SetDescription(LocalizationTool.CreateString(blueprintName + ".Buff.Description", description))
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    new BuffAllSavesBonus {
                        name = "$" + blueprintName + "$AllSaves",
                        Descriptor = ModifierDescriptor.Resistance,
                        Value = saveBonus,
                    },
                }.ToArray();
            })
            .Configure();
    }

    /// <summary>法术：接触目标，挂上持续 24 小时的 buff。</summary>
    private static void ConfigureSpell(string blueprintName, string spellGuid, string buffGuid,
                                       string displayName, string description, int spellLevel) {
        var config = AbilityConfigurator.New(blueprintName, spellGuid)
            // 只复制字段：图标、距离 = 接触、施法时间 = 标准动作
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(ResistanceGuid), c => false)
            // 防护系
            .AddComponent(new SpellComponent {
                name = "$" + blueprintName + "$SpellComponent",
                School = SpellSchool.Abjuration,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$" + blueprintName + "$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.Get<BlueprintBuff>(buffGuid)
                                .ToReference<BlueprintBuffReference>(),
                            Permanent = false,
                            UseDurationSeconds = false,
                            // 24 小时：Rate = Hours，数值必须放在 BonusValue
                            // 注意 ContextValueHelper.CalculateDiceValue 的语义：
                            //   DiceType  = 骰子「面数」
                            //   DiceCountValue = 骰子「个数」（会去掷 DiceCountValue 个 DiceType 面骰）
                            //   BonusValue     = 固定加值
                            // 所以 DiceType.Zero + DiceCountValue 24 = 掷 24 个 0 面骰 = 0，
                            // 持续时间会变成 0 秒、buff 一挂上就消失。固定时长一律写 BonusValue
                            // （原版提升抗力就是 DiceCountValue 0 + BonusValue 1）。
                            // BonusValue 非零同时也让 ContextDurationValue.IsExtendable 成立，
                            // 「延时」超魔可以把 24 小时变成 48 小时。
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Hours,
                                DiceType = DiceType.Zero,
                                DiceCountValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = 0,
                                },
                                BonusValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = DurationHours,
                                },
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
            // 吟游诗人 / 牧师 / 德鲁伊 / 法师（术士共用法师表）
            .AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(BardSpellListGuid))
            .AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            .AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(DruidSpellListGuid))
            .AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WizardSpellListGuid))
            // 硬性规则 A1：进牧师表 → 萨满表同环（无环数限制）
            .AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid));

        if (spellLevel < 6) {
            // 硬性规则 A3：进德鲁伊表且 < 6 环 → 猎人表同环
            config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(HunterSpellListGuid));
            // 硬性规则 A2：进牧师表 + < 6 环 + 战斗类 → 审判者表、战斗祭司表同环
            config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid));
            config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarpriestSpellListGuid));
        }

        config
            // 名字/描述/持续时间文案最后设置，覆盖 CopyFrom 带来的原版文本
            .SetDisplayName(LocalizationTool.CreateString(blueprintName + ".Name", displayName))
            .SetDescription(LocalizationTool.CreateString(blueprintName + ".Description", description))
            .OnConfigure(bp => {
                // 原版 0 环提升抗力没有可用超魔（AvailableMetamagic = 0），这里给高位版本放开常用超魔
                bp.AvailableMetamagic = Metamagic.Quicken | Metamagic.Extend
                    | Metamagic.Heighten | Metamagic.CompletelyNormal;
                // 持续时间文案：原版是「1 分钟」，必须覆盖成 24 小时
                bp.LocalizedDuration = LocalizationTool.CreateString(
                    blueprintName + ".Duration", DurationHours + "小时");
            })
            .Configure();
    }
}
