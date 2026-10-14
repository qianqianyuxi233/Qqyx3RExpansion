using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则法术「鬼击术」（Wraithstrike）。
///
/// 效果「近战攻击变成近战接触攻击」照抄原版魔战士奥能「次元打击」DimensionStrike：
///   Classes/Magus/Arcanas/DimensionStrikeAbility.jbp  cf7c4eaa2b47d7242b2c734df567cefb
///   Classes/Magus/Arcanas/DimensionStrikeBuff.jbp     c25e4bf29c7baa24aa1d6f630a6c1fc3
/// 它本来就是一个「迅捷动作 + 个人 + 持续 1 轮」的能力，与鬼击术同构；
/// 核心组件只有一个：
///
///   AttackTypeChange {
///       NeedsWeapon    = true,             // 只对持用武器的攻击生效
///       ChangeAllTypes = false,
///       OriginalType   = AttackType.Melee,
///       NewType        = AttackType.Touch,
///   }
///   // 实现：OnEventAboutToTrigger(RuleAttackRoll) 里直接 evt.AttackType = NewType
///
/// 法术表（3.5 原文是刺客 2 / 法师术士 2；WOTR 的刺客职业没有施法能力，
/// 已核对 Classes/Assassin/ 下没有任何 Spell 文件，按用户意见改给炼金术士 2 环）：
///   WizardSpellList    ba0401fdeb4062f40a7aa95b6f07fe89   2 环（术士共用）
///   AlchemistSpellList f60d0cd93edc65c42ad31e34a905fb2f   2 环
///
/// 图标用「暗影打击」UmbralStrike 的图标 cdf2a182c73e7ef45ab4f38d724904de。
/// 组件写法遵循 MODDING_NOTES 的四条铁律。
/// </summary>
internal static class Wraithstrike {

    // ---------- 原版参照物 GUID ----------

    /// <summary>Classes/Magus/Arcanas/DimensionStrikeAbility —— 次元打击（迅捷 + 1 轮 + 接触攻击）</summary>
    private const string DimensionStrikeAbilityGuid = "cf7c4eaa2b47d7242b2c734df567cefb";
    /// <summary>Classes/Magus/Arcanas/DimensionStrikeBuff</summary>
    private const string DimensionStrikeBuffGuid = "c25e4bf29c7baa24aa1d6f630a6c1fc3";

    /// <summary>
    /// Spells/Level6/UmbralStrike（暗影打击）—— 图标取它的 <c>Icon</c> 属性。
    /// 注意不能写成 <c>.SetIcon("图标的guid")</c>：SetIcon 的 string 重载走的是游戏
    /// BundlesLoadService（AssetBundle 里的资源名），传 m_Icon.guid 会抛
    /// "Unable to fetch asset w/ ID …" 并把整个 Configure 中断。
    /// </summary>
    private const string UmbralStrikeGuid = "474ed0aa656cc38499cc9a073d113716";

    // ---------- 法术表 GUID ----------

    /// <summary>WizardSpellList —— 法师与术士共用</summary>
    private const string WizardSpellListGuid = "ba0401fdeb4062f40a7aa95b6f07fe89";
    /// <summary>AlchemistSpellList —— 炼金术士</summary>
    private const string AlchemistSpellListGuid = "f60d0cd93edc65c42ad31e34a905fb2f";
    /// <summary>MagusSpellList —— 规则 B：&lt;6 环问魔战士（用户已确认加）</summary>
    private const string MagusSpellListGuid = "4d72e1e7bd6bc4f4caaea7aa43a14639";
    /// <summary>BloodragerSpellList —— 规则 B：&lt;4 环问血脉狂怒者（用户已确认加）</summary>
    private const string BloodragerSpellListGuid = "98c05aeff6e3d384f8aec6d584973642";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string SpellGuid = "e1f28a1864b14e87a8f855e2b2aa03f5";
    private const string BuffGuid = "d5db2429b10b442183baac8a82257eb4";

    /// <summary>环数（法师/术士 2、炼金术士 2）</summary>
    private const int SpellLevel = 2;
    /// <summary>持续时间：1 轮</summary>
    private const int DurationRounds = 1;

    // ---------- 本地化文本（待用户最终确认）----------

    private const string SpellName = "鬼击术";
    private const string SpellDescription =
        "你的武器变得半透明，几乎无法看见，但你依旧能感受到手中武器的重量。"
        + "法术生效期间，你的近战攻击变为近战接触攻击。";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureSpell();
    }

    /// <summary>Buff：把近战攻击改成近战接触攻击。</summary>
    private static void ConfigureBuff() {
        BuffConfigurator.New("WraithstrikeBuff", BuffGuid)
            // 只复制字段：Stacking = Replace、Frequency = Rounds
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(DimensionStrikeBuffGuid), c => false)
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(UmbralStrikeGuid).Icon)                 // 图标：暗影打击
            .SetDisplayName(LocalizationTool.CreateString("Wraithstrike.Buff.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Wraithstrike.Buff.Description", SpellDescription))
            .OnConfigure(bp => {
                bp.Components = new List<BlueprintComponent> {
                    new AttackTypeChange {
                        name = "$Wraithstrike$AttackTypeChange",
                        NeedsWeapon = true,
                        ChangeAllTypes = false,
                        OriginalType = AttackType.Melee,
                        NewType = AttackType.Touch,
                    },
                }.ToArray();
            })
            .Configure();
    }

    /// <summary>法术：迅捷动作、目标自己、持续 1 轮。</summary>
    private static void ConfigureSpell() {
        AbilityConfigurator.New("Wraithstrike", SpellGuid)
            // 只复制字段：Range = Personal、CanTargetSelf、Animation = Self、
            // AnimationStyle = CastActionSelf
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(DimensionStrikeAbilityGuid), c => false)
            // 次元打击是 Extraordinary 能力，法术这里要改成 Spell
            .SetType(AbilityType.Spell)
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(UmbralStrikeGuid).Icon)
            // 变化系
            .AddComponent(new SpellComponent {
                name = "$Wraithstrike$SpellComponent",
                School = SpellSchool.Transmutation,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$Wraithstrike$ApplyBuff",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.Get<BlueprintBuff>(BuffGuid)
                                .ToReference<BlueprintBuffReference>(),
                            Permanent = false,
                            UseDurationSeconds = false,
                            // 1 轮：固定时长写 BonusValue（写 DiceCountValue 会变成 0 秒，见 MODDING_NOTES 4.2）
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Rounds,
                                DiceType = DiceType.Zero,
                                DiceCountValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = 0,
                                },
                                BonusValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = DurationRounds,
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
            // 法师/术士 2 环、炼金术士 2 环
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(WizardSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(AlchemistSpellListGuid))
            // 硬性规则 B（用户已确认）：2 环 < 6 → 魔战士；2 环 < 4 → 血脉狂怒者；不加巫师表
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(MagusSpellListGuid))
            .AddToSpellList(SpellLevel, BlueprintTool.GetRef<BlueprintSpellListReference>(BloodragerSpellListGuid))
            .SetDisplayName(LocalizationTool.CreateString("Wraithstrike.Name", SpellName))
            .SetDescription(LocalizationTool.CreateString("Wraithstrike.Description", SpellDescription))
            .OnConfigure(bp => {
                // 迅捷动作（3.5 原文：施法动作 = 迅捷动作）
                bp.ActionType = UnitCommand.CommandType.Swift;
                // 持续时间文案：次元打击写的是它的奥能说明，这里覆盖成 1 轮
                bp.LocalizedDuration = LocalizationTool.CreateString(
                    "Wraithstrike.Duration", DurationRounds + "轮");
                bp.AvailableMetamagic = Metamagic.Extend | Metamagic.Heighten
                    | Metamagic.CompletelyNormal;
            })
            .Configure();
    }
}
