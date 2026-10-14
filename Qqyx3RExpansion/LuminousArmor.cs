using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.ResourceLinks;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 / BoED 圣洁法术「辉耀护甲」（Luminous Armor）与「高等辉耀护甲」（Greater Luminous Armor）。
///
/// 结构照抄原版「法师护甲」（MageArmor）——同样是「接触一个盟友、挂一个 AC 盔甲加值的 buff」：
///
///   法术 LuminousArmor        Spells/Level1/MageArmor.jbp           9e1ad5d6f87d19e4d8883d63a6e35568
///     ├─ SpellComponent            咒法 → 这里改成防护系（Abjuration）
///     ├─ AbilityEffectRunAction    祭献（施法者 1d2 力量伤害）→ 给受术者挂 buff
///     └─ SpellListComponent ×8     8 张神术职业表（见下），环位 = 圣洁环位
///   Buff   LuminousArmorBuff   Spells/Level1/MageArmorBuff.jbp       a92acdf18049d784eaa8f2004f5d2304
///     ├─ AddStatBonus { Armor, AC, +5 }         ← 照抄原版法师护甲（它是 Armor/AC/+4）
///     └─ LuminousArmorMeleeAttackPenalty        ← 自定义：近战攻击受术者 -4（见 Components 文件）
///
/// 与法师护甲的差别（用户给的 BoED 原文）：
///   - 加值 +5（高等 +8），且魔法护甲不挡奥术失败率（游戏里 Armor 加值本就无失败率）
///   - 会放出昼明术的光亮：buff 的 FxOnStart 借用原版「昼明术」buff 的光效 prefab
///   - 成分「祭献」：施法者受到 1d2（高等 1d3）点力量伤害
///   - 圣洁法术：只有善良阵营的角色可以施展（自定义 IAbilityRestriction）
///
/// 法术表：原书里圣洁法术对所有**神术**施法者开放，所以登记进本 mod 使用的
/// 那 8 张神术职业表（与「卷宗学者」卷宗表的来源一致），环位就是「圣洁 N」的 N：
///   牧师 / 德鲁伊 / 萨满 / 审判者 / 战斗祭司 / 猎人 / 圣武士 / 游侠
/// （先知共用牧师表；圣武士、游侠最高 4 环，正好收得下圣洁 4。）
///
/// 组件写法遵循 MODDING_NOTES 的四条铁律：CopyFrom 只取字段（c =&gt; false）、
/// 组件全部自建、每个组件给唯一 name、固定属性加值用 AddStatBonus。
/// </summary>
internal static class LuminousArmor {

    // ---------- 原版参照物 GUID ----------

    /// <summary>Spells/Level1/MageArmor —— 原版法师护甲（结构、图标、接触法术字段来源）</summary>
    private const string MageArmorGuid = "9e1ad5d6f87d19e4d8883d63a6e35568";
    /// <summary>Spells/Level1/MageArmorBuff —— 原版法师护甲 buff（图标 + AddStatBonus 数值写法）</summary>
    private const string MageArmorBuffGuid = "a92acdf18049d784eaa8f2004f5d2304";
    /// <summary>Spells/Level3/DayLightBuff 的 FxOnStart —— 原版昼明术的光效 prefab</summary>
    private const string DayLightFxGuid = "72938ec0a6e4a10459ea374d65aecfa5";

    // ---------- 8 张神术职业法术表（与 Archivist.DivineSpellListGuids 同一批）----------

    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    private const string DruidSpellListGuid = "bad8638d40639d04fa2f80a1cac67d6b";
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";
    private const string HunterSpellListGuid = "d090b791bfe381740b98ed4ff909b1cf";
    private const string PaladinSpellListGuid = "9f5be2f7ea64fe04eb40878347b147bc";
    private const string RangerSpellListGuid = "29f3c338532390546bc5347826a655c4";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string BuffGuid = "07261488e19b41c7be0dcd8275917e19";
    private const string SpellGuid = "5058f289d761488888d7aa2c86963b19";
    private const string GreaterBuffGuid = "eed54c85f89149219acb28f55175791c";
    private const string GreaterSpellGuid = "a1318d93b7df4643b07cccea8272fb24";

    // ---------- 数值（用户给的 BoED 原文）----------

    private const int ArmorBonus = 5;
    private const int GreaterArmorBonus = 8;
    private const int MeleePenalty = -4;
    private const int SpellLevel = 2;
    private const int GreaterSpellLevel = 4;

    private const string SpellName = "辉耀护甲";
    private const string GreaterSpellName = "高等辉耀护甲";

    private const string CommonDescription =
        "\n「辉耀护甲」会放出相当于「昼明术」的光亮，"
        + "其光芒使敌人在以近战攻击受术者时受到 -4 减值。"
        + "\n祭献：施法者受到 {0} 点力量伤害。"
        + "\n只有善良阵营的角色可以施展该法术。";

    private const string SpellDescription =
        "一层闪烁着光芒的保护性灵气包裹住受术者，如同一套没有重量的耀目全身铠甲。"
        + "它提供等同于胸甲的防护：AC 获得 +5 盔甲加值，没有最大敏捷调整值、"
        + "防具检定减值，也没有奥术失败率。";
    private const string GreaterSpellDescription =
        "此法术与「辉耀护甲」相同，但提供等同于全身铠的防护：AC 获得 +8 盔甲加值。";

    internal static void Configure() {
        // 图标：原版「法师护甲」的图标（法术与 buff 共用同一个 Sprite）
        var icon = BlueprintTool.Get<BlueprintBuff>(MageArmorBuffGuid).Icon;

        ConfigureBuff("LuminousArmorBuff", BuffGuid, SpellName,
            SpellDescription + string.Format(CommonDescription, "1d2"), ArmorBonus, icon);
        ConfigureSpell("LuminousArmor", SpellGuid, BuffGuid, SpellName,
            SpellDescription + string.Format(CommonDescription, "1d2"), SpellLevel, DiceType.D2, icon);

        ConfigureBuff("GreaterLuminousArmorBuff", GreaterBuffGuid, GreaterSpellName,
            GreaterSpellDescription + string.Format(CommonDescription, "1d3"), GreaterArmorBonus, icon);
        ConfigureSpell("GreaterLuminousArmor", GreaterSpellGuid, GreaterBuffGuid, GreaterSpellName,
            GreaterSpellDescription + string.Format(CommonDescription, "1d3"),
            GreaterSpellLevel, DiceType.D3, icon);
    }

    /// <summary>
    /// 辉耀护甲 Buff：AC 盔甲加值 + 近战攻击惩罚 + 昼明术光效。
    /// 字段（IsFromSpell / Stacking = Replace / Frequency）照抄原版法师护甲 buff。
    /// </summary>
    private static void ConfigureBuff(string blueprintName, string guid, string displayName,
                                      string description, int armorBonus, Sprite icon) {
        BuffConfigurator.New(blueprintName, guid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(MageArmorBuffGuid), c => false)
            .SetDisplayName(LocalizationTool.CreateString(blueprintName + ".Name", displayName))
            .SetDescription(LocalizationTool.CreateString(blueprintName + ".Description", description))
            .SetIcon(icon)
            .OnConfigure(bp => {
                // 昼明术的光效（原版昼明术 buff 的 FxOnStart 就是这个 prefab）
                bp.FxOnStart = new PrefabLink { AssetId = DayLightFxGuid };

                // 每个组件都要唯一 name，否则 EntityFact.Setup 只会创建第一个（见 MODDING_NOTES 铁律 2）
                bp.Components = new BlueprintComponent[] {
                    // AC +5 / +8 盔甲加值（原版法师护甲是 Armor/AC/+4）
                    new AddStatBonus {
                        name = "$" + blueprintName + "$ArmorClass",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.Armor,
                        Value = armorBonus,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 敌人以近战攻击受术者时 -4
                    new LuminousArmorMeleeAttackPenalty {
                        name = "$" + blueprintName + "$MeleePenalty",
                        Value = new ContextValue { ValueType = ContextValueType.Simple, Value = MeleePenalty },
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        RangeType = WeaponRangeType.Melee,
                    },
                };
            })
            .Configure();
    }

    /// <summary>
    /// 主法术：接触一个盟友 → 先结算「祭献」力量伤害，再给受术者挂 buff（1 小时/等级）。
    /// 距离（接触）、目标（只能选友军 / 自己）、施法时间、动画都从原版法师护甲继承。
    /// </summary>
    private static void ConfigureSpell(string blueprintName, string guid, string buffGuid,
                                       string displayName, string description, int spellLevel,
                                       DiceType sacrificeDice, Sprite icon) {
        var config = AbilityConfigurator.New(blueprintName, guid)
            // 只复制字段：图标、Range = Touch、ActionType = Standard、CanTargetFriends/Self、
            // 动画、可用超魔、法术抗力 = false（无害法术）
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(MageArmorGuid), c => false)
            // 防护系
            .AddComponent(new SpellComponent {
                name = "$" + blueprintName + "$SpellComponent",
                School = SpellSchool.Abjuration,
            })
            // 圣洁法术：只有善良阵营能施展
            .AddComponent(new AbilityRestrictionGoodAlignment {
                name = "$" + blueprintName + "$GoodOnly",
            })
            // 持续时间「1 小时/等级」用的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$" + blueprintName + "$RankConfig",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$" + blueprintName + "$RunAction",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        // 祭献先结算：先付代价，再拿到护甲
                        new ContextActionSacrificeStatDamage {
                            Stat = StatType.Strength,
                            DiceCount = 1,
                            Dice = sacrificeDice,
                        },
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.Get<BlueprintBuff>(buffGuid)
                                .ToReference<BlueprintBuffReference>(),
                            Permanent = false,
                            UseDurationSeconds = false,
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Hours,
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
            });

        // 圣洁法术对所有神术职业开放：8 张神术表都登记在「圣洁 N」对应的环位上
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(DruidSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarpriestSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(HunterSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(PaladinSpellListGuid));
        config.AddToSpellList(spellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(RangerSpellListGuid));

        config
            .SetIcon(icon)
            .SetDisplayName(LocalizationTool.CreateString(blueprintName + ".Name", displayName))
            .SetDescription(LocalizationTool.CreateString(blueprintName + ".Description", description))
            .OnConfigure(bp => {
                // 持续时间文案：原版法师护甲是「1小时/等级」，这里显式写中文
                bp.LocalizedDuration = LocalizationTool.CreateString(
                    blueprintName + ".Duration", "1小时/等级");
            })
            .Configure();
    }
}
