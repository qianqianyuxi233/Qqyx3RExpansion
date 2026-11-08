using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 瑞雪之歌（Snowsong）—— 3.5《霜燃之书》，吟游诗人 6 环。
///
/// 附魔系（胁迫 / 影响心灵）。以自己为中心 30 尺，给区域内**所有盟友**挂一个 buff：
/// <list type="bullet">
/// <item>魅力 +4 士气加值 → <c>AddStatBonus { Charisma, Morale }</c></item>
/// <item>攻击检定 +4 士气加值 → <c>AddStatBonus { AdditionalAttackBonus, Morale }</c>（祝福术同款）</item>
/// <item>AC +4 洞察加值 → <c>AddStatBonus { AC, Insight }</c></item>
/// <item>快速治疗 1 → <c>AddEffectFastHealing { Heal = 1 }</c>（照原版 <c>CreatureAbilities/FastHealing/FastHealing1</c>）</item>
/// <item>寒冷抗力 15 → <c>AddDamageResistanceEnergy { Type = Cold }</c>（照 <c>Spells/Level2/ResistColdBuff</c>）</item>
/// <item>武器额外 1d6 寒冷伤害 → 命中触发器 + <c>ContextActionDealDamage</c>（火焰/寒冷都是能量伤害）</item>
/// </list>
///
/// ⚠ 两处实现说明：
/// <list type="number">
/// <item>最后那条原文是「their weapons deal an extra 1d6 points of cold damage」，
/// 游戏中没有"给任意武器临时附魔"的现成机制，所以用**命中触发一条独立寒冷伤害**代替 ——
/// 结算上它是**单独的伤害条目**（不并入武器伤害），且**任何命中**都会触发（不限于武器）。</item>
/// <item>3.5 原文的豁免是「意志（无害）」。对盟友的增益若设成需要豁免反而会被队友抵抗，
/// 所以这里用 <c>SavingThrowType.Unknown</c>（与「赞颂」等群体增益一致）。</item>
/// </list>
///
/// 法术本体以原版「祈祷术」为参照（同为以自己为中心给盟友挂加值的光环）。
/// </summary>
internal static class Snowsong {

    /// <summary>Spells/Level3/Prayer —— 原版祈祷术（光环结构 + 图标来源）</summary>
    private const string PrayerGuid = "faabd2cc67efa4646ac58c7bb3e40fcc";
    /// <summary>Spells/DLC4NewSpells/WinterGrasp —— 凛冬之握（借图标：冰 / 寒冬主题）</summary>
    private const string IconSourceGuid = "406c6e4a631b43ce8f7a77844b75bf75";

    /// <summary>Classes/Bard/BardSpellList</summary>
    private const string BardSpellListGuid = "25a5013493bdcf74bb2424532214d0c8";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string SpellGuid = "8c8c00d9156f4ed3ac9cdd44cc3bc962";
    private const string BuffGuid  = "87eaa841700d4135982699373ae0aa51";

    private const int BardSpellLevel = 6;
    /// <summary>魅力 / 攻击的士气加值、AC 的洞察加值</summary>
    private const int BonusValue = 4;
    /// <summary>寒冷抗力值</summary>
    private const int ColdResistance = 15;
    /// <summary>光环半径（尺）</summary>
    private const float RadiusFeet = 30f;

    private const string SpellName = "瑞雪之歌";
    private const string SpellDescription =
        "影响区域内的所有盟友会听到一阵柔和、轻快的歌声，鼓舞他们的士气和信心。\n"
        + "受术的盟友获得 +4 魅力士气加值、攻击检定 +4 士气加值、AC +4 洞察加值；"
        + "此外还获得快速治疗 1 与寒冷抗力 15，其攻击额外造成 1d6 点寒冷伤害。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    private static void ConfigureBuff() {
        var icon = BlueprintTool.Get<BlueprintAbility>(IconSourceGuid);

        BuffConfigurator.New("SnowsongBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Snowsong.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Snowsong.Buff.Description", SpellDescription))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // ① 魅力 +4 士气
                    new AddStatBonus {
                        name = "$Snowsong$Charisma",
                        Stat = StatType.Charisma,
                        Descriptor = ModifierDescriptor.Morale,
                        Value = BonusValue,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ② 攻击检定 +4 士气（祝福术同款）
                    new AddStatBonus {
                        name = "$Snowsong$Attack",
                        Stat = StatType.AdditionalAttackBonus,
                        Descriptor = ModifierDescriptor.Morale,
                        Value = BonusValue,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ③ AC +4 洞察
                    new AddStatBonus {
                        name = "$Snowsong$AC",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.Insight,
                        Value = BonusValue,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ④ 快速治疗 1（照原版 FastHealing1）
                    new AddEffectFastHealing {
                        name = "$Snowsong$FastHealing",
                        Heal = 1,
                    },
                    // ⑤ 寒冷抗力 15（照原版 ResistColdBuff 的写法）
                    new AddDamageResistanceEnergy {
                        name = "$Snowsong$ColdResistance",
                        Type = DamageEnergyType.Cold,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Simple,
                            Value = ColdResistance,
                        },
                    },
                    // ⑥ 命中时额外造成 1d6 寒冷伤害
                    new AddInitiatorAttackWithWeaponTrigger {
                        name = "$Snowsong$ColdDamage",
                        TriggerBeforeAttack = false,
                        OnlyHit = true,
                        ActionsOnInitiator = false,   // 伤害作用于被打的人
                        Action = new ActionList {
                            Actions = new GameAction[] {
                                new ContextActionDealDamage {
                                    name = "$Snowsong$ColdDamageAction",
                                    m_Type = ContextActionDealDamage.Type.Damage,
                                    DamageType = new DamageTypeDescription {
                                        Type = DamageType.Energy,
                                        Energy = DamageEnergyType.Cold,
                                    },
                                    Value = new ContextDiceValue {
                                        DiceType = DiceType.D6,
                                        DiceCountValue = new ContextValue {
                                            ValueType = ContextValueType.Simple,
                                            Value = 1,
                                        },
                                        BonusValue = new ContextValue {
                                            ValueType = ContextValueType.Simple,
                                            Value = 0,
                                        },
                                    },
                                },
                            },
                        },
                    },
                }.ToArray();
            })
            .Configure();
    }

    private static void ConfigureSpell() {
        AbilityConfigurator.New("Snowsong", SpellGuid)
            // 只复制字段：图标（祈祷术）、Range = Personal、ActionType = Standard、动画、可用超魔
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(PrayerGuid), c => false)
            .AddComponent(new SpellComponent {
                name = "$Snowsong$SpellComponent",
                School = SpellSchool.Enchantment,
            })
            // 胁迫 + 影响心灵
            .AddComponent(new SpellDescriptorComponent {
                name = "$Snowsong$SpellDescriptor",
                Descriptor = SpellDescriptor.Compulsion | SpellDescriptor.MindAffecting,
            })
            // 持续时间「10 分钟/等级」的 rank = 施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$Snowsong$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            // 以自己为中心 30 尺，只选盟友
            .AddComponent(new AbilityTargetsAround {
                name = "$Snowsong$TargetsAround",
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
                name = "$Snowsong$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$Snowsong$BuffAction",
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
                            ToCaster = false,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .AddToSpellList(BardSpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(BardSpellListGuid))
            // 法术本体的图标必须显式覆盖：CopyFrom(祈祷术) 会把祈祷术的图标一起带过来
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(IconSourceGuid)?.Icon)
            .SetDisplayName(LocalizationTool.CreateString("Snowsong.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Snowsong.Description", SpellDescription))
            .OnConfigure(bp => bp.LocalizedDuration = LocalizationTool.CreateString(
                "Snowsong.Duration", "10分钟/等级"))
            .Configure();
    }
}
