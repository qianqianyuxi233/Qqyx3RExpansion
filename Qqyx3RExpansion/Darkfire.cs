using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators;
using BlueprintCore.Blueprints.Configurators.Items.Weapons;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则法术「黑暗之炎」（Darkfire，万法大全 / Spell Compendium，牧师 3 环）。
///
/// 原文：掌中出现一团不发光但灼热的火焰，持续 1 轮/等级；每轮可以把火焰投向 120 尺内的敌人
/// （远程接触攻击，无距离减值），造成每施法者等级 1d6 火焰伤害（最多 5d6）。
///
/// <para><b>实现形态（按用户口径定稿）</b>：法术直接给施法者装备一把**真正的武器**，
/// 因此可以标准动作投一次，也可以整轮攻击连投多次；次数不消耗，只看 buff 持续时间，
/// 于是「法术持久」能把 1轮/CL 拉成 24 小时，「强化超魔」能把 5d6 上限顶到 10d6。</para>
///
/// <para><b>为什么伤害不写在武器/附魔里</b>：念动之刃把伤害写在 <c>BlueprintWeaponEnchantment</c> 的
/// <c>ContextRankConfig</c> 里，而附魔的 MechanicsContext 是「角色」的，既拿不到施法者等级、
/// 也拿不到这次施法带的超魔。所以这里反过来：<b>武器伤害骰设为 0</b>，真正的伤害由
/// buff 上的 <c>AddInitiatorAttackWithWeaponTrigger</c> 在每次命中时结算
/// （它的 context 就是 buff 的 context，而 <c>ContextActionDealDamage</c> 读的是
/// <c>Context.HasMetamagic(...)</c>，见 ContextActionDealDamage.cs:209）。
/// buff 的 context 通过 <c>ContextActionApplyBuff</c> 继承施法上下文
/// （<c>MechanicsContext.Params</c> 在 m_Params 为 null 时回退 ParentContext，见 MechanicsContext.cs:220），
/// 所以 CasterLevel / Empower / Maximize / Intensified 全都成立。</para>
///
/// <para><b>组件清单</b>（4 个蓝图，1 个自定义组件 <see cref="AddDarkfireWeapon"/>）：</para>
/// <list type="number">
/// <item>DarkfireWeaponType —— 复制原版 RayType（RangedTouch），把射程从 50 尺改成 120 尺。</item>
/// <item>DarkfireWeapon —— 复制原版 RayItem，指向自己的类型，伤害骰 0，无模型无动画（火焰不可见）。</item>
/// <item>DarkfireBuff —— 生成武器 + 每次命中结算 D6 × Rank(CL) 火焰伤害。</item>
/// <item>Darkfire —— 牧师 3 环，标准动作、目标自己、1轮/CL。</item>
/// </list>
///
/// 图标：操念使「火操念射线」（FireBlastAbility）的图标 cbf5ad683ebf1aa4b84f32da4f8d6b5a。
/// </summary>
internal static class Darkfire {

    // ---------- 原版参照物 GUID ----------

    /// <summary>Weapons/Types/Special/RayType —— Category=Ray、m_AttackType=RangedTouch、射程 50 尺</summary>
    private const string RayTypeGuid = "1d39a22f206840e40b2255fc0175b8d0";
    /// <summary>Weapons/Types/Special/RayItem —— 法术射线用的武器（无模型、无动画、无附魔）</summary>
    private const string RayItemGuid = "f6ef95b1f7bb52b408a5b345a330ffe8";
    /// <summary>Classes/Kineticist/ElementalFocus/Fire/FireBlast/FireBlastAbility —— 火操念射线（借图标）</summary>
    private const string FireBlastAbilityGuid = "7b4f0c9a06db79345b55c39b2d5fb510";
    /// <summary>Spells/Level4/DivinePower —— 原版神能（目标自己的增益法术，借字段：Range=Personal 等）</summary>
    private const string DivinePowerGuid = "ef16771cb05d1344989519e87f25b3c5";

    // ---------- 法术表 GUID ----------

    /// <summary>ClericSpellList（牧师；先知共用）</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    /// <summary>ShamanSpelllist —— 规则 A1：牧师表的法术自动同环登记到萨满表</summary>
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    /// <summary>InquisitorSpellList —— 规则 A2：牧师表 + &lt;6 环 + 战斗类型（用户确认）</summary>
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    /// <summary>WarpriestSpellList —— 规则 A2 同上</summary>
    private const string WarpriestSpellListGuid = "c5a1b8df32914d74c9b44052ba3e686a";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    /// <summary>Spells/Level2/ScorchingRay 用的投射物（借它的字段，只把速度调快）</summary>
    private const string ScorchingRayProjectileGuid = "8cc159ce94d29fe46a94b80ce549161f";

    /// <summary>
    /// 施法过后攻击日志 / 伤害飘字全靠它：远程武器的攻击日志会被游戏挂起
    /// （<c>RuleAttackWithWeapon.cs:161</c> 的 <c>SuspendCombatLog = Weapon.IsRanged</c>），
    /// 必须等投射物命中事件（<c>ProjectileHitLogThread</c>）来解除挂起并补写日志。
    /// 武器没有投射物时那段代码不会跑，于是**整场战斗一条记录都没有**。
    /// </summary>
    private const string ProjectileGuid = "34ef07064b97409bb8a70c13d482ad01";

    private const string WeaponTypeGuid = "04f6b89a7a994d8eac4471e23a544d41";
    private const string WeaponGuid = "4d90fb8a4d044f6fbaca964516c800d0";
    private const string BuffGuid = "175f348f1b054143bb85720f4a792b1c";
    private const string SpellGuid = "112bfce98e5f4de2b1483a07851b8490";

    /// <summary>法术环数：牧师 3 环</summary>
    private const int SpellLevel = 3;
    /// <summary>投掷射程（英尺）。原文 120 尺，原版 RayType 只有 50 尺，所以自建类型副本。</summary>
    private const float ThrowRangeFeet = 120f;
    /// <summary>伤害骰上限（每施法者等级 1d6，最多 5d6）。强化超魔在此之上 +5（ContextRankConfig.cs:623）。</summary>
    private const int MaxDamageDice = 5;

    private const string SpellName = "黑暗之炎";
    private const string WeaponName = "黑暗之炎";
    private const string SpellDescription =
        "你掌中出现一团怪异的火焰。它不发出任何光亮，却像普通火焰一样灼热，也不会灼伤你或你的装备。\n"
        + "法术生效期间，你可以把火焰投向 120 尺内的敌人：进行一次远程接触攻击（没有距离减值），"
        + "命中则造成每施法者等级 1d6 点火焰伤害（最多 5d6）。\n"
        + "你可以用标准动作投掷一次，也可以整轮攻击连续投掷；掷出后新的火焰立刻在掌中生成，"
        + "法术结束前掷多少次都不会耗尽。";
    private const string BuffDescription =
        "掌中握着黑暗之炎：可以投向 120 尺内的敌人，命中造成每施法者等级 1d6 点火焰伤害（最多 5d6）。";

    internal static void Configure() {
        ConfigureProjectile();
        ConfigureWeaponType();
        ConfigureWeapon();
        ConfigureBuff();
        ConfigureSpell();
    }

    /// <summary>
    /// 投射物：只为「让游戏产生投射物命中事件」而存在。
    /// 没有它，远程攻击的日志会被永久挂起，战斗日志与伤害飘字一个都不会出现。
    /// 速度 1000（原版 50）：120 尺在一帧内到达，视觉上几乎看不到飞行物。
    /// </summary>
    private static void ConfigureProjectile() {
        ProjectileConfigurator.New("DarkfireProjectile", ProjectileGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintProjectileReference>(ScorchingRayProjectileGuid), c => false)
            .OnConfigure(bp => {
                bp.Speed = 1000f;
                bp.MinTime = 0f;
            })
            .Configure();
    }

    /// <summary>
    /// 武器类型：复制原版射线类型（RemoteTouch = 远程接触），只把射程改成 120 尺。
    /// 不直接改原版蓝图（会污染所有法术射线，见 MODDING_NOTES 铁律 3）。
    /// </summary>
    private static void ConfigureWeaponType() {
        WeaponTypeConfigurator.New("DarkfireWeaponType", WeaponTypeGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintWeaponTypeReference>(RayTypeGuid), c => false)
            .OnConfigure(bp => {
                bp.m_AttackRange = new Feet(ThrowRangeFeet);
                bp.m_TypeNameText = LocalizationTool.CreateString("Darkfire.WeaponType.Name", WeaponName);
            })
            .Configure();
    }

    /// <summary>
    /// 武器：除了「指向自己的类型」和「图标」以外全部沿用原版 RayItem ——
    /// 它本来就无模型、无动画、无附魔，正好等于原文「看不见的火焰」。
    /// 伤害骰设为 0：真正的伤害由 buff 上的命中触发器结算（见类注释）。
    /// </summary>
    private static void ConfigureWeapon() {
        ItemWeaponConfigurator.New("DarkfireWeapon", WeaponGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintItemWeaponReference>(RayItemGuid), c => false)
            .SetDisplayNameText(LocalizationTool.CreateString("Darkfire.Weapon.Name", WeaponName))
            .SetDescriptionText(LocalizationTool.CreateString("Darkfire.Weapon.Description", BuffDescription))
            .OnConfigure(bp => {
                bp.m_Type = BlueprintTool.GetRef<BlueprintWeaponTypeReference>(WeaponTypeGuid);
                bp.m_OverrideDamageDice = true;
                bp.m_DamageDice = new DiceFormula(0, DiceType.Zero);
                bp.m_Enchantments = new BlueprintWeaponEnchantmentReference[0];
                bp.m_Icon = BlueprintTool.Get<BlueprintAbility>(FireBlastAbilityGuid).Icon;
                // 必须给投射物：没有它，远程攻击的战斗日志与伤害飘字都不会出现
                bp.m_VisualParameters.m_Projectiles = new[] {
                    BlueprintTool.GetRef<BlueprintProjectileReference>(ProjectileGuid),
                };
            })
            .Configure();
    }

    /// <summary>
    /// Buff：施法期间在手槽生成武器，并在每次用这把武器命中时结算火焰伤害。
    /// 两个组件共用同一个 rank 槽（Default）＝施法者等级。
    /// </summary>
    private static void ConfigureBuff() {
        BuffConfigurator.New("DarkfireBuff", BuffGuid)
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(FireBlastAbilityGuid).Icon)
            .SetDisplayName(LocalizationTool.CreateString("Darkfire.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Darkfire.Buff.Description", BuffDescription))
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    // ① 把手槽里的武器（强制塞主手 + 锁定 + 原有物品回背包）
                    new AddDarkfireWeapon {
                        name = "$Darkfire$Weapon",
                        m_Weapon = BlueprintTool.GetRef<BlueprintItemWeaponReference>(WeaponGuid),
                    },
                    // ② 火焰描述符（元素抗性/免疫等按火焰处理）
                    new SpellDescriptorComponent {
                        name = "$Darkfire$Descriptor",
                        Descriptor = SpellDescriptor.Fire,
                    },
                    // ③ rank = 施法者等级（上限 5；强化超魔 +5）
                    new ContextRankConfig {
                        name = "$Darkfire$RankCasterLevel",
                        m_Type = AbilityRankType.Default,
                        m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                        m_Progression = ContextRankProgression.AsIs,
                        m_StartLevel = 0,
                        m_StepLevel = 0,
                        m_UseMax = true,
                        m_Max = MaxDamageDice,
                        m_AffectedByIntensifiedMetamagic = true,
                    },
                    // ④ 每次用这把武器命中 → 掷 rank 个 d6 火焰伤害
                    new AddInitiatorAttackWithWeaponTrigger {
                        name = "$Darkfire$OnHit",
                        TriggerBeforeAttack = false,
                        OnlyHit = true,
                        CheckWeaponBlueprint = true,
                        m_WeaponBlueprint = BlueprintTool.GetRef<BlueprintItemWeaponReference>(WeaponGuid),
                        ActionsOnInitiator = false,   // 伤害作用于被打的人，不是施法者
                        Action = new ActionList {
                            Actions = new GameAction[] {
                                new ContextActionDealDamage {
                                    name = "$Darkfire$Damage",
                                    m_Type = ContextActionDealDamage.Type.Damage,
                                    DamageType = new DamageTypeDescription {
                                        Type = DamageType.Energy,
                                        Energy = DamageEnergyType.Fire,
                                    },
                                    Value = new ContextDiceValue {
                                        DiceType = DiceType.D6,
                                        DiceCountValue = new ContextValue {
                                            ValueType = ContextValueType.Rank,
                                            ValueRank = AbilityRankType.Default,
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

    /// <summary>法术本体：标准动作、目标自己、持续 1 轮/等级。</summary>
    private static void ConfigureSpell() {
        AbilityConfigurator.New("Darkfire", SpellGuid)
            // 只复制字段：Range = Personal、Animation = Self、AnimationStyle = CastActionSelf
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(DivinePowerGuid), c => false)
            .SetType(AbilityType.Spell)
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(FireBlastAbilityGuid).Icon)
            .AddComponent(new SpellComponent {
                name = "$Darkfire$SpellComponent",
                School = SpellSchool.Evocation,
            })
            .AddComponent(new SpellDescriptorComponent {
                name = "$Darkfire$SpellDescriptor",
                Descriptor = SpellDescriptor.Fire,
            })
            // 持续时间的 rank = 施法者等级（1 轮/等级）
            .AddComponent(new ContextRankConfig {
                name = "$Darkfire$DurationRank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$Darkfire$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            // 1 轮/等级：固定部分写 BonusValue，骰子部分留 0
                            // （把固定时长写进 DiceCountValue 会变成 0 秒，见 MODDING_NOTES 4.2）
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
                            IsFromSpell = true,
                            IsNotDispelable = false,   // 原文「可消解」
                            ToCaster = true,           // 火焰出现在自己手上
                            AsChild = false,
                            SameDuration = false,
                            IgnoreParentContext = false,   // 必须继承施法上下文（超魔 / CL）
                        },
                    },
                },
            })
            // 牧师 3 环（先知共用同一张表）
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            // 规则 A1：牧师表 → 萨满表同环
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid))
            // 规则 A2：牧师表 + <6 环 + 战斗类型（用户确认加）
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarpriestSpellListGuid))
            .SetDisplayName(LocalizationTool.CreateString("Darkfire.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Darkfire.Description", SpellDescription))
            .OnConfigure(bp => {
                bp.Range = AbilityRange.Personal;
                bp.ActionType = UnitCommand.CommandType.Standard;
                bp.CanTargetSelf = true;
                bp.CanTargetFriends = false;
                bp.CanTargetEnemies = false;
                bp.CanTargetPoint = false;
                bp.SpellResistance = true;      // 原文「法术抗力：可」
                bp.EffectOnEnemy = AbilityEffectOnUnit.None;
                bp.EffectOnAlly = AbilityEffectOnUnit.None;
                bp.NotOffensive = false;
                bp.LocalizedDuration = LocalizationTool.CreateString("Darkfire.Duration", "1轮/等级");
                bp.LocalizedSavingThrow = LocalizationTool.CreateString("Darkfire.SavingThrow", "无");
                // 用户口径：要能叠超魔（强效 / 极效 / 强化顶上限 / 延时拉长 / 持久整天）
                // ★ 强化这里必须给两个位：2048 是原版 Metamagic.Intensified（DLC6 那个专长），
                //   4096 是 TabletopTweaks 的「强化法术」——只给 2048 的话，装了 TTT 的玩家
                //   在超魔栏里根本勾不上强化（能否勾选由 AvailableMetamagic 决定）。
                bp.AvailableMetamagic = Metamagic.Empower | Metamagic.Maximize | Metamagic.Quicken
                    | Metamagic.Extend | Metamagic.Heighten | Metamagic.Intensified
                    | ContextRankConfigTttIntensifiedPatch.TttIntensified
                    | Metamagic.CompletelyNormal;
            })
            .Configure();
    }
}
