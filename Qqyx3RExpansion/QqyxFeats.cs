using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.UnitLogic.ActivatableAbilities;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 三则 3.5 战斗专长：
///
/// <list type="bullet">
/// <item><b>影之刃 Shadow Blade</b>（《万法大全》）—— 娴熟武器 + 武器娴熟时，
/// 力量之外**额外**把敏捷调整值加到伤害上。</item>
/// <item><b>森林射手 Woodland Archer</b>（《PHB2》）——「距离修正」战术机动：
/// 远程失手后，本轮内对同一目标的后续远程攻击 +4。</item>
/// <item><b>突击尖兵</b> ——「轻率冲锋」战术机动：冲锋并启用猛力攻击时，
/// 把猛力攻击的攻击检定减值转移到 AC。</item>
/// </list>
///
/// 影之刃进普通专长池；森林射手与突击尖兵按原文进普通池 **和** 战士奖励专长池。
/// </summary>
internal static class QqyxFeats {

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string ShadowBladeGuid          = "de7faf4f94d54ddd9fd6833d0f70c36b";
    private const string WoodlandArcherGuid       = "3a9c58390f9d4397a9dbc69fce0582cd";
    private const string WoodlandArcherMarkerGuid = "6e1e74be5fda4a84a76d564ab6d97bad";
    private const string RecklessChargeGuid       = "fd323a7a7b2148ee94aa6017b6ea0f64";
    private const string RecklessChargeBuffGuid   = "8bcadd78aa06483c9652af11f7119a10";
    private const string RecklessChargeToggleGuid = "cafa233611a848399d94e1704cd2d418";

    // ---------- 原版参照 ----------
    /// <summary>Feats/WeaponFinesse —— 武器娴熟</summary>
    private const string WeaponFinesseGuid = "90e54424d682d104ab36436bd527af09";
    /// <summary>Feats/PointBlankShot —— 近程射击</summary>
    private const string PointBlankShotGuid = "0da0c194d6e1d43419eb8d990b28e0ab";
    /// <summary>Feats/PowerAttack/PowerAttackFeature —— 猛力攻击</summary>
    private const string PowerAttackFeatureGuid = "9972f33f977fc724c838e59641b2fca5";
    /// <summary>Feats/PowerAttack/PowerAttackBuffEffect —— 猛力攻击的效果 buff（-1 攻击 / +2 伤害，按 BAB 缩放）</summary>
    private const string PowerAttackBuffGuid = "bfe8300529214c7ca4ae0024f834f085";
    /// <summary>
    /// Root/SystemMechanics/Conditions/ChargeBuffEffect —— 冲锋的**效果** buff。
    /// ⚠ 不要用 ChargeBuff：那是 HiddenInUi 的容器 buff，冲锋动作一结束就卸掉，
    /// 而攻击结算 / AC 计算的时机更晚，查它永远查不到。
    /// 冲锋自己的 AC −2 与「持续 1 轮」的时长都挂在 ChargeBuffEffect 上。
    /// </summary>
    private const string ChargeBuffGuid = "4fb54a7eafb647d2a82347437a2b652d";

    // ---------- 图标来源 ----------
    // 都用与效果语义最接近的原版专长，而不是各自的前置专长 —— 否则专长列表里
    // 会出现两张相同的图（前置 + 本专长），玩家容易看错。
    /// <summary>Feats/FencingGrace —— 剑术优雅（PF 里同样是「敏捷加到伤害」）</summary>
    private const string ShadowBladeIconGuid = "47b352ea0f73c354aba777945760b441";
    /// <summary>Feats/ImprovedPreciseShot —— 精通精准射击</summary>
    private const string WoodlandArcherIconGuid = "46f970a6b9b5d2346b10892673fe6e74";
    /// <summary>Feats/MountedCombat/SpiritedCharge —— 勇猛冲锋</summary>
    private const string RecklessChargeIconGuid = "95ef0ff14771f2549897f300ce62c95c";
    /// <summary>Feats/AugmentSummoning/AugmentSummoning —— 原版增强召唤（借图标）</summary>
    private const string GreenboundIconGuid = "38155ca9e4055bb48a89240a2055dcc3";

    // ---------- 绿缚召唤 ----------
    private const string GreenboundSummoningGuid     = "1ca30a27a88846e78f762be20f3babcd";
    private const string GreenboundSummoningBuffGuid = "a8f45ec568d446269b31d243301edbd3";

    internal static void Configure() {
        var shadowBlade = ConfigureShadowBlade();
        var woodlandArcher = ConfigureWoodlandArcher();
        var recklessCharge = ConfigureRecklessCharge();

        var greenbound = ConfigureGreenboundSummoning();

        FeatPools.AddToBasicFeats(shadowBlade, woodlandArcher, recklessCharge, greenbound);
        // 原文注明森林射手与突击尖兵都可作为战士奖励专长
        FeatPools.AddToFighterFeats(woodlandArcher, recklessCharge);

        Main.Log?.Log("[专长] 已注册「影之刃」「森林射手」「突击尖兵」「绿缚召唤」");
    }

    // ==================== 影之刃 ====================

    private static BlueprintFeature ConfigureShadowBlade() {
        // 图标借「剑术优雅」—— 不和前置的武器娴熟撞图
        var iconSource = BlueprintTool.Get<BlueprintFeature>(ShadowBladeIconGuid);

        return FeatureConfigurator.New("ShadowBlade", ShadowBladeGuid).SkipAddToSelections()
            .SetDisplayName(LocalizationTool.CreateString("ShadowBlade.Name", "影之刃"))
            .SetDescription(LocalizationTool.CreateString("ShadowBlade.Description",
                "当你手持娴熟武器、并以「武器娴熟」的方式攻击时，"
                + "你可以把敏捷调整值作为加值加到伤害检定上。"))
            .SetIcon(iconSource == null ? null : iconSource.Icon)
            .AddComponent(new ShadowBladeDamage {
                name = "$ShadowBlade$Damage",
                m_FinesseFeat = BlueprintTool.GetRef<BlueprintUnitFactReference>(WeaponFinesseGuid),
            })
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(WeaponFinesseGuid))
            .AddPrerequisiteStatValue(StatType.Dexterity, 15)
            .AddFeatureTagsComponent(FeatureTag.ClassSpecific)
            .SetGroups(FeatureGroup.Feat)
            .Configure();
    }

    // ==================== 森林射手 ====================

    private static BlueprintFeature ConfigureWoodlandArcher() {
        // 图标借「精通精准射击」—— 不和前置的近程射击撞图
        var icon = BlueprintTool.Get<BlueprintFeature>(WoodlandArcherIconGuid);

        // 「距离修正」：**每次远程失手挂一层**，每层 +4 攻击加值、持续 1 轮。
        // Stacking = Stack 让各层独立生效 —— 失手两次就是 +8。
        // 这样整个专长只用「一个 buff + 一个失手触发器」就能实现，不需要任何自定义组件。
        var marker = BuffConfigurator.New("WoodlandArcherMarker", WoodlandArcherMarkerGuid)
            .SetDisplayName(LocalizationTool.CreateString("WoodlandArcherMarker.Name", "距离修正"))
            .SetDescription(LocalizationTool.CreateString("WoodlandArcherMarker.Description",
                "你的远程攻击刚刚失手。本轮剩余时间内，你的攻击检定获得 +4 加值。"))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => bp.Stacking = StackingType.Stack)
            .AddComponent(new AddStatBonus {
                name = "$WoodlandArcher$AttackBonus",
                Stat = StatType.AdditionalAttackBonus,
                Descriptor = ModifierDescriptor.UntypedStackable,
                Value = 4,
                ScaleByBasicAttackBonus = false,
            })
            .Configure();

        return FeatureConfigurator.New("WoodlandArcher", WoodlandArcherGuid).SkipAddToSelections()
            .SetDisplayName(LocalizationTool.CreateString("WoodlandArcher.Name", "森林射手"))
            .SetDescription(LocalizationTool.CreateString("WoodlandArcher.Description",
                "你获得「距离修正」战术机动：\n"
                + "当你进行一次远程攻击并失手后，在本轮剩余时间内，你的攻击检定获得 +4 加值。\n"
                + "多次失手可以累加。"))
            .SetIcon(icon == null ? null : icon.Icon)
            // 远程攻击失手 → 挂一层 1 轮的「距离修正」
            .AddComponent(new AddInitiatorAttackWithWeaponTrigger {
                name = "$WoodlandArcher$OnMiss",
                TriggerBeforeAttack = false,
                OnlyHit = false,
                OnMiss = true,
                CheckWeaponRangeType = true,
                RangeType = WeaponRangeType.Ranged,
                ActionsOnInitiator = true,
                Action = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$WoodlandArcher$MarkMiss",
                            m_Buff = marker.ToReference<BlueprintBuffReference>(),
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
                                    Value = 1,
                                },
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
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(PointBlankShotGuid))
            .AddPrerequisiteStatValue(StatType.BaseAttackBonus, 6)
            .AddFeatureTagsComponent(FeatureTag.ClassSpecific)
            .SetGroups(FeatureGroup.Feat)
            .Configure();
    }

    // ==================== 突击尖兵 ====================

    private static BlueprintFeature ConfigureRecklessCharge() {
        // 图标借「勇猛冲锋」—— 不和前置的猛力攻击撞图
        var iconSource = BlueprintTool.Get<BlueprintFeature>(RecklessChargeIconGuid);
        var icon = iconSource == null ? null : iconSource.Icon;

        // ① 效果 buff：真正做「加攻击加值 / 扣 AC」的组件挂在它上面
        var buff = BuffConfigurator.New("RecklessChargeBuff", RecklessChargeBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("RecklessChargeBuff.Name", "轻率冲锋"))
            .SetDescription(LocalizationTool.CreateString("RecklessChargeBuff.Description",
                "冲锋时把猛力攻击的攻击检定减值转移到 AC："
                + "攻击检定不再受该减值影响，改为承受等量 AC 减值。"))
            .SetIcon(icon)
            .AddComponent(new RecklessChargeBonus {
                name = "$RecklessCharge$Bonus",
                m_ChargeBuff = BlueprintTool.GetRef<BlueprintBuffReference>(ChargeBuffGuid),
                m_PowerAttackBuff = BlueprintTool.GetRef<BlueprintBuffReference>(PowerAttackBuffGuid),
            })
            .Configure();

        // ② 开关：原文是「你**可以**将……转移」，所以必须让玩家自己决定用不用。
        //    这是取舍型能力（拿 AC 换命中），默认关闭，要用时在能力栏打开。
        var toggle = ActivatableAbilityConfigurator.New("RecklessChargeToggle", RecklessChargeToggleGuid)
            .SetDisplayName(LocalizationTool.CreateString("RecklessChargeToggle.Name", "轻率冲锋"))
            .SetDescription(LocalizationTool.CreateString("RecklessChargeToggle.Description",
                "开启后：当你冲锋并使用猛力攻击时，把猛力攻击的攻击检定减值转移到 AC 上。\n"
                + "你每转移 1 点攻击检定减值，便承受 1 点 AC 减值；该 AC 减值持续到你下一轮开始。"))
            .SetIcon(icon)
            .SetBuff(buff.ToReference<BlueprintBuffReference>())
            .SetIsOnByDefault(false)
            .Configure();

        return FeatureConfigurator.New("RecklessCharge", RecklessChargeGuid).SkipAddToSelections()
            .SetDisplayName(LocalizationTool.CreateString("RecklessCharge.Name", "突击尖兵"))
            .SetDescription(LocalizationTool.CreateString("RecklessCharge.Description",
                "你获得「轻率冲锋」战术机动：\n"
                + "当你冲锋并使用猛力攻击时，你可以把猛力攻击造成的攻击检定减值转移到 AC 上。\n"
                + "你每转移 1 点攻击检定减值，便承受 1 点 AC 减值；"
                + "该 AC 减值持续到你下一轮开始。"))
            .SetIcon(icon)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                toggle.ToReference<BlueprintUnitFactReference>(),
            })
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(PowerAttackFeatureGuid))
            .AddPrerequisiteStatValue(StatType.BaseAttackBonus, 6)
            .AddFeatureTagsComponent(FeatureTag.ClassSpecific)
            .SetGroups(FeatureGroup.Feat)
            .Configure();
    }

    // ==================== 绿缚召唤 ====================

    private static BlueprintFeature ConfigureGreenboundSummoning() {
        var icon = BlueprintTool.Get<BlueprintFeature>(GreenboundIconGuid);

        // 绿缚模板 buff：由 GreenboundSummoning 组件在召唤时挂到召唤物身上，
        // 时长取召唤持续时间（见 QqyxFeatsComponents.cs 的类注释）。
        var buff = BuffConfigurator.New("GreenboundSummoningBuff", GreenboundSummoningBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("GreenboundBuff.Name", "绿缚模板"))
            .SetDescription(LocalizationTool.CreateString("GreenboundBuff.Description",
                "绿缚生物：力量 +6、敏捷 +2、体质 +4；天生防御 +6；"
                + "伤害减免 10/魔法与挥砍；寒冷抗力 10、电击抗力 10；快速医疗 3。"))
            .SetIcon(icon == null ? null : icon.Icon)
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // 属性加值（3.5 模板加值无类型，故用可叠加的无类型修正）
                    new AddStatBonus {
                        name = "$Greenbound$Strength",
                        Stat = StatType.Strength,
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        Value = 6,
                        ScaleByBasicAttackBonus = false,
                    },
                    new AddStatBonus {
                        name = "$Greenbound$Dexterity",
                        Stat = StatType.Dexterity,
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        Value = 2,
                        ScaleByBasicAttackBonus = false,
                    },
                    new AddStatBonus {
                        name = "$Greenbound$Constitution",
                        Stat = StatType.Constitution,
                        Descriptor = ModifierDescriptor.UntypedStackable,
                        Value = 4,
                        ScaleByBasicAttackBonus = false,
                    },
                    // 天生防御 +6（**纯**天防，不是树肤术那种"天防增强"）
                    new AddStatBonus {
                        name = "$Greenbound$NaturalArmor",
                        Stat = StatType.AC,
                        Descriptor = ModifierDescriptor.NaturalArmor,
                        Value = 6,
                        ScaleByBasicAttackBonus = false,
                    },
                    // DR 10/魔法与挥砍。
                    // AddDamageResistancePhysical 默认 Or = false，即"所有绕过条件都满足才穿透"，
                    // 正好对应「需要同时是魔法武器**且**挥砍伤害」。
                    new AddDamageResistancePhysical {
                        name = "$Greenbound$DR",
                        Value = new ContextValue {
                            ValueType = ContextValueType.Simple,
                            Value = 10,
                        },
                        BypassedByForm = true,
                        Form = PhysicalDamageForm.Slashing,
                        BypassedByMagic = true,
                        MinEnhancementBonus = 1,
                    },
                    // 寒冷抗力 10
                    new AddDamageResistanceEnergy {
                        name = "$Greenbound$ColdResistance",
                        Type = DamageEnergyType.Cold,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Simple,
                            Value = 10,
                        },
                    },
                    // 电击抗力 10
                    new AddDamageResistanceEnergy {
                        name = "$Greenbound$ElectricityResistance",
                        Type = DamageEnergyType.Electricity,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Simple,
                            Value = 10,
                        },
                    },
                    // 快速医疗 3
                    new AddEffectFastHealing {
                        name = "$Greenbound$FastHealing",
                        Heal = 3,
                    },
                }.ToArray();
            })
            .Configure();

        return FeatureConfigurator.New("GreenboundSummoning", GreenboundSummoningGuid).SkipAddToSelections()
            .SetDisplayName(LocalizationTool.CreateString("GreenboundSummoning.Name", "绿缚召唤"))
            .SetDescription(LocalizationTool.CreateString("GreenboundSummoning.Description",
                "你以「召唤自然盟友」召来的生物，在召唤法术持续期间获得绿缚模板：\n"
                + "力量 +6、敏捷 +2、体质 +4；天生防御 +6；"
                + "伤害减免 10/魔法与挥砍；寒冷抗力 10、电击抗力 10；快速医疗 3。"))
            .SetIcon(icon == null ? null : icon.Icon)
            .AddComponent(new GreenboundSummoning {
                name = "$GreenboundSummoning$Hook",
                m_GreenboundBuff = buff.ToReference<BlueprintBuffReference>(),
            })
            .AddFeatureTagsComponent(FeatureTag.ClassSpecific)
            .SetGroups(FeatureGroup.Feat)
            .Configure();
    }
}
