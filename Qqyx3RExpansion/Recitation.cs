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
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则法术「赞颂」（Recitation）。
///
/// 结构与数值全部照抄游戏原版「祈祷术」（Prayer）——它同样是
/// 「以自己为中心、只给一片区域内的目标挂幸运加值」的法术，三层结构一模一样：
///
///   主法术 Recitation            Spells/Level3/Prayer.jbp          faabd2cc67efa4646ac58c7bb3e40fcc
///     ├─ SpellComponent          塑能→这里改成咒法系（Conjuration）
///     ├─ AbilityTargetsAround    以自己为中心 60 尺，m_TargetType = Ally（只选盟友）
///     └─ AbilityEffectRunAction → ContextActionCastSpell(子法术, MarkAsChild = true)
///   子法术 RecitationBuffAbility  Spells/Level3/PrayerBuffAbility.jbp c1f9524c409c4962afbf68ab28f9822e
///     └─ AbilityEffectRunAction → ContextActionApplyBuff(buff)
///   Buff   RecitationBuff        Spells/Level3/PrayerBuff.jbp      789bae3802e7b6b4c8097aaf566a1cf5
///     ├─ AddStatBonus { Luck, AC, 3 }                     ← AC +3
///     ├─ AddStatBonus { Luck, AdditionalAttackBonus, 3 }  ← 攻击检定 +3
///     └─ BuffAllSavesBonus { Luck, 3 }                    ← 强韧/反射/意志 +3
///
/// 与原版祈祷术的差别（用户给的 3.5 数据）：
///   - 光环半径 40 尺 → 60 尺
///   - 只影响盟友（祈祷术对敌人另挂一个 debuff，这里用 TargetType.Ally 直接筛掉敌人）
///   - 加值 1 → 3，且多了 AC
///
/// 图标、距离（个人）、施法时间（标准动作）、持续时间文案（1 轮/等级）、可用超魔
/// 都由 CopyFrom 从祈祷术继承（图标 guid d2d07f0dfe9fade448b004e36c4306a5）。
///
/// 法术表：
///   ClericSpellList 8443ce803d2d31347897a3d85cc32f53     4 环 —— 牧师与先知共用同一张表
///     （已核对 Classes/Oracle/OracleSpellbook.jbp 的 m_SpellList 就是这张）
///   ProtectionDomainSpellList 93228f4df23d2d448a0db59141af8aed  3 环 —— 保护领域
///     （原版没有 3.5 的「净化 Purification」领域，取主题最接近的保护领域）
///
/// 组件写法遵循 MODDING_NOTES 的四条铁律：CopyFrom 只取字段（c =&gt; false）、
/// 组件全部自建、每个组件给唯一 name、固定属性加值用 AddStatBonus。
/// </summary>
internal static class Recitation {

    // ---------- 原版参照物 GUID ----------

    /// <summary>Spells/Level3/Prayer —— 原版祈祷术（结构 + 图标来源）</summary>
    private const string PrayerGuid = "faabd2cc67efa4646ac58c7bb3e40fcc";
    /// <summary>Spells/Level3/PrayerBuffAbility</summary>
    private const string PrayerBuffAbilityGuid = "c1f9524c409c4962afbf68ab28f9822e";
    /// <summary>Spells/Level3/PrayerBuff</summary>
    private const string PrayerBuffGuid = "789bae3802e7b6b4c8097aaf566a1cf5";

    // ---------- 法术表 GUID ----------

    /// <summary>Classes/Cleric/ClericSpellList —— 牧师表（先知 OracleSpellbook 也用这张）</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    /// <summary>Classes/Cleric/Domains/Protection/ProtectionDomainSpellList —— 保护领域</summary>
    private const string ProtectionDomainSpellListGuid = "93228f4df23d2d448a0db59141af8aed";

    // ---------- 硬性规则（见 MODDING_NOTES §2.5）连带登记用的表 ----------

    /// <summary>Classes/Shaman/ShamanSpelllist —— 规则 A1：牧师表的法术同环进萨满表</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>Classes/Inquisitor/InquisitorSpellList —— 规则 A2</summary>
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    /// <summary>Classes/Warpriest/WarpriestSpellList —— 规则 A2</summary>
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string SpellGuid = "286bad46941d40209891d8c2d4b77750";
    private const string SubSpellGuid = "c3473fcedc934db5b08739cd41cd9366";
    private const string BuffGuid = "648791a88b0742bb83814c4d9e84f309";

    /// <summary>牧师表里的环数</summary>
    private const int ClericSpellLevel = 4;
    /// <summary>领域表里的环数</summary>
    private const int DomainSpellLevel = 3;
    /// <summary>幸运加值数值</summary>
    private const int BonusValue = 3;
    /// <summary>光环半径（尺）</summary>
    private const float RadiusFeet = 60f;

    // ---------- 本地化文本（待用户最终确认）----------

    private const string SpellName = "赞颂";
    private const string SpellDescription =
        "该法术会影响释放瞬间所有身处法术区域内的盟友。"
        + "你的盟友在AC、攻击检定和豁免检定上都有+3的幸运加值。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSubSpell();
        ConfigureSpell();
    }

    /// <summary>赞颂 Buff：AC / 攻击检定 / 全部豁免 +3 幸运加值。</summary>
    private static void ConfigureBuff() {
        BuffConfigurator.New("RecitationBuff", BuffGuid)
            // 只复制字段：图标（祈祷术图标）、Stacking = Replace、Frequency = Rounds
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(PrayerBuffGuid), c => false)
            .SetDisplayName(LocalizationTool.CreateString("Recitation.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Recitation.Buff.Description", SpellDescription))
            .OnConfigure(bp => {
                // 每个组件都要唯一 name，否则 EntityFact.Setup 只会创建第一个（见 MODDING_NOTES 铁律 2）
                bp.Components = new List<BlueprintComponent> {
                    // AC +3 幸运加值
                    new AddStatBonus {
                        name = "$Recitation$ArmorClass",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.Luck,
                        Value = BonusValue,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 攻击检定 +3 幸运加值
                    new AddStatBonus {
                        name = "$Recitation$AttackBonus",
                        Stat = StatType.AdditionalAttackBonus,
                        Descriptor = ModifierDescriptor.Luck,
                        Value = BonusValue,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 强韧 / 反射 / 意志 +3 幸运加值（原版祈祷术同款组件）
                    new BuffAllSavesBonus {
                        name = "$Recitation$AllSaves",
                        Descriptor = ModifierDescriptor.Luck,
                        Value = BonusValue,
                    },
                }.ToArray();
            })
            .Configure();
    }

    /// <summary>
    /// 子法术：区域法术会对每个被选中的目标各跑一次这个动作，用来给目标挂 Buff。
    /// 玩家看不到它（没有登记进任何法术表）。
    /// </summary>
    private static void ConfigureSubSpell() {
        AbilityConfigurator.New("RecitationBuffAbility", SubSpellGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(PrayerBuffAbilityGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$Recitation$SubSpellComponent",
                School = SpellSchool.Conjuration,
            })
            // 持续时间「1 轮/等级」用的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$Recitation$SubRankConfig",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$Recitation$SubApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.Get<BlueprintBuff>(BuffGuid)
                                .ToReference<BlueprintBuffReference>(),
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
                            },
                            DurationSeconds = 0f,
                            // 与祈祷术子法术一致：由子法术施加、作为子效果
                            IsFromSpell = false,
                            IsNotDispelable = false,
                            ToCaster = false,
                            AsChild = true,
                            SameDuration = false,
                        },
                    },
                },
            })
            .Configure();
    }

    /// <summary>
    /// 主法术：以自己为中心 60 尺，**直接**给范围内每个盟友挂 Buff。
    ///
    /// ⚠ 原版祈祷术是「主法术 → ContextActionCastSpell 子法术 → 子法术挂 buff」两层结构，
    /// 第一版照抄了它，但**子法术会让「法术持久」失效**：
    /// <c>ContextActionCastSpell.RunAction</c> 用 <c>new AbilityData(spell, caster)</c> 单独造子法术，
    /// 不带父法术的超魔；而 <c>MechanicsContext.Params</c> 只在自身 m_Params 为 null 时才回退到父
    /// —— 子法术有自己的 Params，于是超魔查不到，24 小时不生效
    /// （祝福术 / 神恩 / 神能是直接挂 buff，所以它们正常）。
    ///
    /// <c>AbilityTargetsAround</c> + <c>AbilityEffectRunAction</c> 本身就会对**每个**选中目标各跑一次动作，
    /// 所以这里不需要子法术：去掉一层，施法上下文就是玩家这次施法（带超魔），持久自然生效。
    /// 子法术蓝图（<see cref="SubSpellGuid"/>）仍然保留注册，避免旧存档里的引用失效。
    /// </summary>
    private static void ConfigureSpell() {
        AbilityConfigurator.New("Recitation", SpellGuid)
            // 只复制字段：图标（祈祷术）、Range = Personal、ActionType = Standard、
            // 可用超魔、持续时间文案（1 轮/等级）
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(PrayerGuid), c => false)
            // 咒法系（3.5 原文是「咒法系（创造）」，游戏的 SpellComponent 只记学派）
            .AddComponent(new SpellComponent {
                name = "$Recitation$SpellComponent",
                School = SpellSchool.Conjuration,
            })
            // 持续时间「1 轮/等级」用的 rank = 施法者等级（原来在子法术上）
            .AddComponent(new ContextRankConfig {
                name = "$Recitation$RankConfig",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            // 以自己为中心的 60 尺光环，只选盟友
            .AddComponent(new AbilityTargetsAround {
                name = "$Recitation$TargetsAround",
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
            // 对每个选中的目标直接挂 buff
            .AddComponent(new AbilityEffectRunAction {
                name = "$Recitation$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.Get<BlueprintBuff>(BuffGuid)
                                .ToReference<BlueprintBuffReference>(),
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
                            ToCaster = false,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            // 牧师 4 环（先知共用这张表）
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            // 保护领域 3 环
            .AddToSpellList(DomainSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ProtectionDomainSpellListGuid))
            // 硬性规则 A1：进牧师表 → 萨满表同环
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid))
            // 硬性规则 A2：牧师表 + 4 环（<6）+ 战斗类（赞颂给盟友 AC/攻击/豁免）
            //            → 审判者表、战斗祭司表同环
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid))
            .AddToSpellList(ClericSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarpriestSpellListGuid))
            // 名字/描述最后注册，覆盖 CopyFrom 带来的祈祷术文本
            .SetDisplayName(LocalizationTool.CreateString("Recitation.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Recitation.Description", SpellDescription))
            .Configure();
    }
}
