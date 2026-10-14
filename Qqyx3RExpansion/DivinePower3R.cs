using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.JsonSystem;
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
/// 3.5 规则法术「3r神能」（Divine Power）。
///
/// 数据来源（游戏内 blueprints.zip，路径与 guid 均实测确认）：
///   Spells/Level4/DivinePower.jbp      → ef16771cb05d1344989519e87f25b3c5
///   Spells/Level4/DivinePowerBuff.jbp  → 208ce0902f20b1e4896a85b2339468ff
///   Spells/Level2/BullsStrengthBuff.jbp → b175001b42b1a02479881b72fe132116（力量 +4 写法来源）
///
/// 位置：法术表中的归属由 AddToSpellList 登记，与原版神能完全相同（4 环）：
///   ClericSpellList       8443ce803d2d31347897a3d85cc32f53  牧师
///   WarDomainSpellList    e3c54da90a2b54b4a975a80b5d39c361  战争领域
///   GloryDomainSpellList  7b3506924ed8354419b7829736ab2c7e  荣耀领域
///   InquisitorSpellList   57c894665b7895c499b3dce058c284b3  审判者
///   ShamanSpelllist       c0c40e42f07ff104fa85492da464ac69  萨满
///   AngelClericSpelllist  c074062863fbc1e4bab02f9e6e4eb78d  天使道途（牧师表）
///   AeonSpellList         24b0c796f723a144e9891b6c4794c595  御衡者道途
///
/// 效果（3.5）：
///   基本攻击加值 = 人物等级 → RaiseBAB。它开启时把 (人物等级 − 当前 BAB) 作为 modifier 加在
///     Stats.BaseAttackBonus 上；而 RuleCalculateAttacksCount 正是按 BAB/5 递进算攻击次数，
///     所以攻击检定加值与「+20/+15/+10/+5」式多打都自动生效。
///   力量 +6 增强加值 → AddStatBonus（照抄牛之蛮力 BullsStrengthBuff 的写法，只把 4 改成 6）。
///   每施法者等级 1 点暂时生命值 → ContextRankConfig(CasterLevel) + AddContextStatBonus(TemporaryHitPoints)。
///
/// 两个已踩过的坑，这里都规避了：
///   1. CopyFrom 复制出来的组件是**与原版共享的实例**，游戏会给它重设 OwnerBlueprint，
///      造成原版蓝图里的组件"归属"变成我们的蓝图（WrathPatches 会报
///      "Non-matching OwnerBlueprint … THIS COMPONENT MAY NOT WORK AS EXPECTED"）。
///      所以这里 CopyFrom 一律传 <c>c =&gt; false</c>：只复制字段（图标、距离、施法时间、
///      持续时间文案、可用超魔…），组件全部自己 new。
///   2. ★运行时 new 出来的组件必须给唯一 name★。EntityFact.Setup() 是这样创建运行时组件的：
///        var provider = component as IRuntimeEntityFactComponentProvider;
///        if (provider != null
///            &amp;&amp; !this.Components.HasItem&lt;EntityFactComponent&gt;(
///                   i =&gt; i.SourceBlueprintComponentName == component.name)
///            &amp;&amp; !component.Disabled) { 创建 }
///      它按 <c>component.name</c> 去重，而 BlueprintComponent.name 默认为 null，
///      于是**第二个及之后的组件全部被当成"已存在"而跳过** —— 表现就是"永远只有第一个组件生效"
///      （BAB 与力量轮流失灵、诊断组件从不打印，都是这个原因）。
///      jbp 里每个组件的 name 形如 "$AddStatBonus$&lt;guid&gt;"，这里统一用固定字符串。
/// </summary>
internal static class DivinePower3R {

    // ---------- 原版蓝图 GUID ----------

    /// <summary>Spells/Level4/DivinePower —— 原版「神能」</summary>
    private const string DivinePowerGuid = "ef16771cb05d1344989519e87f25b3c5";
    /// <summary>Spells/Level4/DivinePowerBuff</summary>
    private const string DivinePowerBuffGuid = "208ce0902f20b1e4896a85b2339468ff";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string SpellGuid = "b1625f14b8c04c92b75b833f055de6c8";
    private const string BuffGuid = "5563dc60f3ac4c82a5aedfc2c89dfd0c";

    /// <summary>3r神能所在的法术环数（与原版神能一致）</summary>
    private const int SpellLevel = 4;

    // ---------- 原版神能登记的全部法术表 ----------

    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";
    private const string WarDomainSpellListGuid = "e3c54da90a2b54b4a975a80b5d39c361";
    private const string GloryDomainSpellListGuid = "7b3506924ed8354419b7829736ab2c7e";
    private const string InquisitorSpellListGuid = "57c894665b7895c499b3dce058c284b3";
    private const string ShamanSpellListGuid = "c0c40e42f07ff104fa85492da464ac69";
    private const string AngelClericSpellListGuid = "c074062863fbc1e4bab02f9e6e4eb78d";
    private const string AeonSpellListGuid = "24b0c796f723a144e9891b6c4794c595";

    // ---------- 本地化文本（待用户最终确认）----------

    private const string SpellName = "3r神能";
    private const string SpellDescription =
        "呼唤你所信仰神祗的威力，为自己注入一股战斗力量和技能。"
        + "你的基本攻击加值变为等于你的人物等级（这也许可使你进行多次攻击），"
        + "你的力量获得+6的增强加值，并获得每施法者等级1点暂时生命值。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    /// <summary>
    /// 3r神能 Buff：字段（图标、Stacking = Replace、m_Flags = IsFromSpell、Frequency = Rounds）
    /// 沿用原版神能，组件全部换成 3.5 的效果。
    /// </summary>
    private static void ConfigureBuff() {
        BuffConfigurator.New("DivinePower3RBuff", BuffGuid)
            // 只复制字段，不复制任何组件 —— 避免与原版神能共享组件实例
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(DivinePowerBuffGuid), c => false)
            .SetDisplayName(LocalizationTool.CreateString("DivinePower3R.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("DivinePower3R.Buff.Description", SpellDescription))
            .OnConfigure(bp => {
                // 每个组件都必须有唯一的 name！见文件头注释「坑 2」：
                // EntityFact.Setup 按 component.name 去重，name 为 null 时第二个之后一律被跳过。
                bp.Components = new List<BlueprintComponent> {
                    // ① 力量 +6 增强加值 —— 逐字照抄牛之蛮力 BullsStrengthBuff
                    //    AddStatBonus{Enhancement, Strength, 4}，这里只把 4 改成 6。
                    new AddStatBonus {
                        name = "$3rDivinePower$Strength",
                        Stat = StatType.Strength,
                        Descriptor = ModifierDescriptor.Enhancement,
                        Value = 6,
                        ScaleByBasicAttackBonus = false,
                    },
                    // ② 基本攻击加值＝人物等级
                    new ContextRankConfig {
                        name = "$3rDivinePower$RankCharacterLevel",
                        m_Type = AbilityRankType.Default,
                        m_BaseValueType = ContextRankBaseValueType.CharacterLevel,
                        m_Progression = ContextRankProgression.AsIs,
                    },
                    new RaiseBAB {
                        name = "$3rDivinePower$RaiseBab",
                        TargetValue = new ContextValue {
                            ValueType = ContextValueType.Rank,
                            ValueRank = AbilityRankType.Default,
                        },
                    },
                    // ③ 每施法者等级 1 点暂时生命值
                    new ContextRankConfig {
                        name = "$3rDivinePower$RankCasterLevel",
                        m_Type = AbilityRankType.StatBonus,
                        m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                        m_Progression = ContextRankProgression.AsIs,
                    },
                    new AddContextStatBonus {
                        name = "$3rDivinePower$TemporaryHitPoints",
                        Stat = StatType.TemporaryHitPoints,
                        Descriptor = ModifierDescriptor.None,
                        Multiplier = 1,
                        Value = new ContextValue {
                            ValueType = ContextValueType.Rank,
                            ValueRank = AbilityRankType.StatBonus,
                        },
                    },
                }.ToArray();
            })
            .Configure();
    }

    /// <summary>
    /// 3r神能法术：CopyFrom 原版神能只取字段（距离 = 个人、施法时间 = 标准动作、
    /// 持续时间文案 = 1 轮/等级、图标、可用超魔…），组件自己搭。
    /// </summary>
    private static void ConfigureSpell() {
        AbilityConfigurator.New("DivinePower3R", SpellGuid)
            // 只复制字段，不复制组件（原版 7 个 SpellListComponent 由 AddToSpellList 负责）
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(DivinePowerGuid), c => false)
            // 法术学派与技能：塑能系（与原版神能一致）
            .AddComponent(new SpellComponent {
                name = "$3rDivinePower$SpellComponent",
                School = SpellSchool.Evocation,
            })
            // 持续时间「1 轮/等级」用的 rank：施法者等级
            .AddComponent(new ContextRankConfig {
                name = "$3rDivinePower$AbilityRankConfig",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.CasterLevel,
                m_Progression = ContextRankProgression.AsIs,
            })
            // 施放后给自己挂上 3r神能 Buff
            .AddComponent(new AbilityEffectRunAction {
                name = "$3rDivinePower$ApplyBuff",
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
                            IsFromSpell = true,
                            IsNotDispelable = false,
                            ToCaster = false,
                            AsChild = false,
                            SameDuration = false,
                            NotLinkToAreaEffect = false,
                        },
                    },
                },
            })
            // 与原版神能完全相同的 7 个法术表，均为 4 环
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ClericSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WarDomainSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(GloryDomainSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(InquisitorSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(ShamanSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(AngelClericSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(AeonSpellListGuid))
            // 名字/描述最后注册，确保覆盖 CopyFrom 带来的文本
            .SetDisplayName(LocalizationTool.CreateString("DivinePower3R.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("DivinePower3R.Description", SpellDescription))
            .Configure();
    }
}
