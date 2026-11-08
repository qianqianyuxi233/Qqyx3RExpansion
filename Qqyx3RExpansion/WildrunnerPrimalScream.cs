using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.ResourceLinks;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;

namespace Qqyx3RExpansion;

/// <summary>
/// 荒野行者的核心能力「原始之吼 Primal Scream」。
///
/// 3.5 原文：2 级起可用，自由动作、不引发借机攻击；每日 <b>3 + 魅力调整值</b> 次，
/// 持续 <b>3 + 体质调整值</b> 轮；期间力量 +2、敏捷 +6，并获得 1d6 的啮咬攻击。
/// 4 / 6 / 8 / 10 级再逐级叠加（见下）。
///
/// 实现结构：
/// <list type="bullet">
/// <item>每日次数：<c>ResourceAmountBuilder.New(3).IncreaseByStat(Charisma)</c>
/// —— 与牧师引导能量「3 + 魅力」同款公式；</item>
/// <item>持续轮数：<c>ContextRankConfig { StatBonus / Constitution, Progression = BonusValue, StepLevel = 3 }</c>
/// 得到「3 + 体质调整值」，再由 <c>ContextDurationValue.BonusValue</c> 取用；</item>
/// <item>啮咬：借原版 <c>RagePowers/AnimalFuryBite</c> 的天武武器 + <c>AddAdditionalLimb</c>
/// （游戏的次要天武自带 −5／−2 攻击惩罚）；</item>
/// <item><b>四个附加效果</b>：主 buff 挂载时，用 <c>AddBuffActions.Activated</c> +
/// <c>Conditional { ContextConditionCharacterClass(CheckCaster, MinLevel) }</c>
/// 按<b>荒野行者职业等级</b>把对应的附加 buff 挂到自己身上
/// （<c>SameDuration = true</c> 使其与吼叫同时结束）。用职业等级而不是角色等级，
/// 是为了兼职时不会提前解锁。</item>
/// </list>
///
/// 附加效果对照（与 3.5 原表的差异见括号）：
/// <list type="bullet">
/// <item>4 级 <b>力量强化</b>：吼叫期间力量额外 +2（合计 +4）——
/// 原文此处是「惊恐」，因引擎无法做带豁免的范围恐惧而由作者改为数值强化；</item>
/// <item>6 级 <b>猛扑</b>：冲锋后可进行整轮攻击；</item>
/// <item>8 级 <b>敏捷强化</b>：吼叫期间敏捷额外 +2（合计 +8）——原文此处是「破胆」；</item>
/// <item>10 级 <b>复苏</b>：吼叫期间只要还有 1 点 HP 就获得快速医疗 5。</item>
/// </list>
///
/// ⚠ 另一处引擎限制：「使用原始之吼需要吼叫，因此不能在不能说话时使用」——
/// 游戏没有"沉默"状态，已舍弃。
/// </summary>
internal static class WildrunnerPrimalScream {

    // ---------- 本 mod 新增蓝图 GUID ----------
    internal const string FeatureGuid  = "f640c243baee4204ae6bec42acb95871";
    private const string AbilityGuid   = "d5e2c0470af549c69bf2d221e60b344f";
    private const string ResourceGuid  = "425abfcf5c8148db8705f7e9503ab1b3";
    private const string BuffGuid      = "23d268e3ac4045bbb02467ee462ba591";

    /// <summary>4 级「力量强化」附加 buff</summary>
    private const string StrengthBuffGuid = "28f7424967464d0eb5dc068839158322";
    /// <summary>8 级「敏捷强化」附加 buff</summary>
    private const string DexterityBuffGuid = "3aac43c3987047bfac2173c9160d8c68";
    /// <summary>6 级「猛扑」附加 buff</summary>
    private const string PounceBuffGuid    = "527f26e9dbaf489aa4ba707b2bf8075a";
    /// <summary>10 级「复苏」附加 buff</summary>
    private const string ReviveBuffGuid    = "335477b8d6ff4e4aa9685023bf6ebd96";

    /// <summary>4 级「力量强化」解锁标记</summary>
    internal const string FrightfulGuid = "6036a8b3adf94253adb3e7a180555f79";
    /// <summary>6 级「猛扑」解锁标记</summary>
    internal const string PounceGuid    = "cc4903a9b8e44c08b5e0d3f5256b9356";
    /// <summary>8 级「敏捷强化」解锁标记</summary>
    internal const string DreadGuid     = "0828a337f7384cc8a3019a1a65538096";
    /// <summary>10 级「复苏」解锁标记</summary>
    internal const string ReviveGuid    = "18079c2e13e4485da067df050d6dee34";

    // ---------- 原版参照 ----------
    private const string ClassGuid = "008b26f4a378464db5e3ab5101efd8f5";
    /// <summary>Classes/Barbarian/RagePowers/AnimalFuryBite —— 原版狂暴之力的啮咬天武</summary>
    private const string AnimalFuryBiteGuid = "8c01e7fccbb829947bc5894f63fb389a";
    /// <summary>Classes/Barbarian/BarbarianRage —— 原版狂暴（借它的图标）</summary>
    private const string RageFeatureGuid = "b0e482a92c83481a8a65ec3f6df9ef1f";
    /// <summary>狂暴 buff 上的那个光效预制体（自带音效），照搬来做启动表现</summary>
    private const string RageFxAssetId = "a536a9fb639035140b8771b2e3a21ffd";

    /// <summary>原始之吼自己没有图标，借原版狂暴的。</summary>
    private static UnityEngine.Sprite RageIcon() {
        var rage = BlueprintTool.Get<BlueprintFeature>(RageFeatureGuid);
        return rage == null ? null : rage.Icon;
    }

    /// <summary>4 级：吼叫期间力量额外 +2</summary>
    private const int StrengthBoost = 2;
    /// <summary>8 级：吼叫期间敏捷额外 +2</summary>
    private const int DexterityBoost = 2;
    /// <summary>10 级：快速医疗量</summary>
    private const int FastHealing = 5;
    /// <summary>持续轮数的固定部分（3 + 体质调整值）</summary>
    private const int ExtraRounds = 3;

    private const string AbilityName = "原始之吼";

    internal static void Configure() {
        var resource = AbilityResourceConfigurator.New("WildrunnerPrimalScreamResource", ResourceGuid)
            // 每日 3 + 魅力调整值（与牧师引导能量同款公式）
            .SetMaxAmount(ResourceAmountBuilder.New(3).IncreaseByStat(StatType.Charisma))
            .Configure();

        // 按等级挂载的四个附加状态
        var strengthBuff = ConfigureStatBuff("WildrunnerPrimalScreamStrengthBuff", StrengthBuffGuid,
            "原始之吼：力量强化", "原始之吼期间，你的力量额外获得 +2 加值。",
            StatType.Strength, StrengthBoost);
        var pounceBuff = ConfigurePounceBuff();
        var dexterityBuff = ConfigureStatBuff("WildrunnerPrimalScreamDexterityBuff", DexterityBuffGuid,
            "原始之吼：敏捷强化", "原始之吼期间，你的敏捷额外获得 +2 加值。",
            StatType.Dexterity, DexterityBoost);
        var reviveBuff = ConfigureReviveBuff();

        var buff = ConfigureBuff(strengthBuff, pounceBuff, dexterityBuff, reviveBuff);
        ConfigureAbility(resource, buff);

        FeatureConfigurator.New("WildrunnerPrimalScream", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.PrimalScream.Name", AbilityName))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.PrimalScream.Description",
                "你可以用内心的原始力量发出原始之吼。\n"
                + "以一个自由动作启动（不引发借机攻击），持续 3 + 体质调整值轮"
                + "（至少 1 轮），每日可用 3 + 魅力调整值次（至少 1 次）。\n"
                + "吼叫期间你的力量获得 +2 加值、敏捷获得 +6 加值，"
                + "并获得一次 1d6 的啮咬攻击（可作为次要天武，承受 −5 的攻击惩罚，"
                + "有多重攻击时改为 −2）。\n"
                + "随着荒野行者等级提升，吼叫还会附带：4 级力量强化、6 级猛扑、"
                + "8 级敏捷强化、10 级复苏。"))
            .SetIcon(RageIcon())
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(AbilityGuid),
            })
            .AddAbilityResources(resource: resource, restoreAmount: true)
            .Configure();

        // 四个解锁标记：能力列表里的可见提示，实际效果由主 buff 按职业等级施加
        ConfigureUnlock("WildrunnerPrimalScreamStrength", FrightfulGuid, "原始之吼·力量强化",
            "4 级起，你启动原始之吼时，力量额外获得 +2 加值（与基础加值合计 +4）。");
        ConfigureUnlock("WildrunnerPrimalScreamPounce", PounceGuid, "原始之吼·猛扑",
            "6 级起，启动原始之吼时你获得猛扑能力：冲锋后可以进行整轮攻击。");
        ConfigureUnlock("WildrunnerPrimalScreamDexterity", DreadGuid, "原始之吼·敏捷强化",
            "8 级起，你启动原始之吼时，敏捷额外获得 +2 加值（与基础加值合计 +8）。");
        ConfigureUnlock("WildrunnerPrimalScreamRevive", ReviveGuid, "原始之吼·复苏",
            "10 级起，在原始之吼持续期间，只要你还有 1 点生命值，就获得快速医疗 5。");
    }

    /// <summary>解锁标记：只负责在能力列表里显示说明，效果在主 buff 上。</summary>
    private static void ConfigureUnlock(string name, string guid, string display, string description) {
        FeatureConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description", description))
            .SetIcon(RageIcon())
            .SetIsClassFeature()
            .Configure();
    }

    // ==================== 附加状态 ====================

    /// <summary>一项属性强化（4 级力量 / 8 级敏捷）。</summary>
    private static BlueprintBuff ConfigureStatBuff(string name, string guid, string display,
            string description, StatType stat, int value) {
        return BuffConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description", description))
            .SetIcon(RageIcon())
            .AddComponent(new AddStatBonus {
                name = "$" + name + "$Bonus",
                Stat = stat,
                Descriptor = ModifierDescriptor.UntypedStackable,
                Value = value,
                ScaleByBasicAttackBonus = false,
            })
            .Configure();
    }

    /// <summary>6 级：猛扑。</summary>
    private static BlueprintBuff ConfigurePounceBuff() {
        return BuffConfigurator.New("WildrunnerPrimalScreamPounceBuff", PounceBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.PounceBuff.Name", "原始之吼：猛扑"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.PounceBuff.Description",
                "原始之吼期间，你获得猛扑能力：冲锋后可以进行整轮攻击。"))
            .SetIcon(RageIcon())
            .AddComponent(new AddMechanicsFeature {
                name = "$PrimalScream$Pounce",
                m_Feature = AddMechanicsFeature.MechanicsFeatureType.Pounce,
            })
            .Configure();
    }

    /// <summary>10 级：快速医疗 5。</summary>
    private static BlueprintBuff ConfigureReviveBuff() {
        return BuffConfigurator.New("WildrunnerPrimalScreamReviveBuff", ReviveBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.ReviveBuff.Name", "原始之吼：复苏"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.ReviveBuff.Description",
                "原始之吼期间，只要你还有 1 点生命值，就获得快速医疗 5。"))
            .SetIcon(RageIcon())
            .AddComponent(new AddEffectFastHealing {
                name = "$PrimalScream$FastHealing",
                Heal = FastHealing,
            })
            .Configure();
    }

    // ==================== 主 buff ====================

    private static BlueprintBuff ConfigureBuff(BlueprintBuff strength, BlueprintBuff pounce,
            BlueprintBuff dexterity, BlueprintBuff revive) {
        return BuffConfigurator.New("WildrunnerPrimalScreamBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.PrimalScreamBuff.Name", AbilityName))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.PrimalScreamBuff.Description",
                "原始之吼：力量 +2、敏捷 +6，并获得 1d6 的啮咬攻击。"))
            .SetIcon(RageIcon())
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    new AddStatBonus {
                        name = "$PrimalScream$Strength",
                        Stat = StatType.Strength,
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        Value = 2,
                        ScaleByBasicAttackBonus = false,
                    },
                    new AddStatBonus {
                        name = "$PrimalScream$Dexterity",
                        Stat = StatType.Dexterity,
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        Value = 6,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 啮咬：借原版狂暴之力的天武武器
                    new AddAdditionalLimb {
                        name = "$PrimalScream$Bite",
                        m_Weapon = BlueprintTool.GetRef<BlueprintItemWeaponReference>(AnimalFuryBiteGuid),
                    },
                    // 按荒野行者职业等级挂上四个附加状态
                    new AddBuffActions {
                        name = "$PrimalScream$SubStates",
                        Activated = new ActionList {
                            Actions = new GameAction[] {
                                // 启动表现照搬狂暴：这个 FX 预制体自带红色光效与吼叫音效
                                new ContextActionSpawnFx {
                                    name = "$PrimalScream$Fx",
                                    PrefabLink = new PrefabLink { AssetId = RageFxAssetId },
                                },
                                LevelConditional("Strength", 4, strength.ToReference<BlueprintBuffReference>()),
                                LevelConditional("Pounce", 6, pounce.ToReference<BlueprintBuffReference>()),
                                LevelConditional("Dexterity", 8, dexterity.ToReference<BlueprintBuffReference>()),
                                LevelConditional("Revive", 10, revive.ToReference<BlueprintBuffReference>()),
                            },
                        },
                    },
                }.ToArray();
            })
            .Configure();
    }

    /// <summary>
    /// 「若荒野行者职业等级 ≥ <paramref name="minLevel"/> 则给自己挂 <paramref name="buff"/>」。
    /// <c>SameDuration = true</c> 让附加状态与原始之吼同时结束。
    /// </summary>
    private static Conditional LevelConditional(string tag, int minLevel, BlueprintBuffReference buff) {
        return new Conditional {
            name = "$PrimalScream$" + tag,
            ConditionsChecker = new ConditionsChecker {
                Conditions = new Condition[] {
                    new ContextConditionCharacterClass {
                        name = "$PrimalScream$" + tag + "Level",
                        CheckCaster = true,
                        m_Class = BlueprintTool.GetRef<BlueprintCharacterClassReference>(ClassGuid),
                        MinLevel = minLevel,
                    },
                },
            },
            IfTrue = new ActionList {
                Actions = new GameAction[] {
                    new ContextActionApplyBuff {
                        name = "$PrimalScream$" + tag + "Apply",
                        m_Buff = buff,
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
                                ValueType = ContextValueType.Simple,
                                Value = 0,
                            },
                        },
                        DurationSeconds = 0f,
                        IsFromSpell = false,
                        IsNotDispelable = true,
                        ToCaster = true,
                        AsChild = false,
                        SameDuration = true,
                    },
                },
            },
            IfFalse = new ActionList { Actions = new GameAction[0] },
        };
    }

    // ==================== 启动能力 ====================

    private static void ConfigureAbility(BlueprintAbilityResource resource, BlueprintBuff buff) {
        AbilityConfigurator.New("WildrunnerPrimalScreamAbility", AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.PrimalScreamAbility.Name", AbilityName))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.PrimalScreamAbility.Description",
                "发出原始之吼：持续 3 + 体质调整值轮，期间力量 +2、敏捷 +6 并获得 1d6 啮咬攻击。"))
            .SetIcon(RageIcon())
            .SetType(AbilityType.Supernatural)
            .SetRange(AbilityRange.Personal)
            // 自由动作（原文为 free action）
            .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Free)
            .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: ResourceGuid)
            // rank = 3 + 体质调整值（BonusValue 这个 progression 的语义就是 value + StepLevel）
            .AddComponent(new ContextRankConfig {
                name = "$PrimalScream$ConRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.StatBonus,
                m_Stat = StatType.Constitution,
                m_Progression = ContextRankProgression.BonusValue,
                m_StepLevel = ExtraRounds,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$PrimalScream$Apply",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$PrimalScream$ApplyBuff",
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            // 3 + 体质调整值（rank）
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
                                m_IsExtendable = false,
                            },
                            DurationSeconds = 0f,
                            IsFromSpell = false,
                            IsNotDispelable = true,
                            ToCaster = true,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .Configure();
    }
}
