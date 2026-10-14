using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.Configurators.UnitLogic.ActivatableAbilities;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Commands.Base;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则野蛮人变体「旋风狂暴」。
///
/// 结构完全照搬游戏内同类范例 InstinctualWarrior（本能战士，野蛮人变体替换狂暴）：
///   - 变体 1 级 RemoveFeatures 原版狂暴(RageFeature)，AddFeatures 自己的独立狂暴
///   - 独立狂暴 = BlueprintFeature(AddFacts) → [独立的 ActivatableAbility, 独立的狂暴资源]
///   - 能力 Group = Rage、ActivationType = Immediately、ActivateWithUnitCommand = Free
///     （Group = Rage 即「被视为狂暴」，狂暴之力等判定才认得它）
///
/// Buff 则直接在原版 StandartRageBuff 上 CopyFrom 后改造：
///   删临时HP、AC -2→+2闪避、意志豁免→反射豁免；其余（近战攻击/伤害、投掷伤害、
///   禁止施法、11/20 级加值提升）原样保留。
/// </summary>
internal static class WhirlingFrenzy {

    // ---------- 原版蓝图 GUID ----------

    private const string BarbarianClassGuid = "f7d7eb166b3dd594fb330d085df41853";
    /// <summary>Classes/Barbarian/Rage/RageFeature —— 原版狂暴</summary>
    private const string RageFeatureGuid = "2479395977cfeeb46b482bc3385f4647";
    /// <summary>Classes/Barbarian/Rage/StandartRageBuff</summary>
    private const string StandartRageBuffGuid = "da8ce41ac3cd74742b80984ccc3c9613";
    /// <summary>Classes/Barbarian/Rage/StandartRageActivateableAbility</summary>
    private const string StandartRageAbilityGuid = "df6a2cce8e3a9bd4592fb1968b83f730";
    /// <summary>Classes/Barbarian/Rage/StandartRageFact —— 提供狂暴次数资源</summary>
    private const string StandartRageFactGuid = "4b1f3dd0f61946249a654941fc417a89";
    /// <summary>Classes/Basic/BasicActions/FightDefensivelyToggleAbility</summary>
    private const string FightDefensivelyToggleGuid = "09d742e8b50b0214fb71acfc99cc00b3";
    /// <summary>Feats/PowerAttack/PowerAttackToggleAbility —— 额外攻击开关模板（图标与猛力攻击一致）</summary>
    private const string PowerAttackToggleGuid = "a7b339e4f6ff93a4697df5d7a87ff619";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string BuffGuid = "7980fd6ae0ce487e9c9182aaf21af187";
    private const string AbilityGuid = "05c8117ad3674c15bede0ab569f72c0c";
    private const string FeatureGuid = "4404c62bee94447eb6340a153fcac093";
    private const string ExtraBuffGuid = "2f40dcdc5303427abef3fac425cade21";
    private const string ExtraToggleGuid = "1c20f04e7dda4b10a4a7f79f9a61becc";
    private const string ArchetypeGuid = "b45d8c5da71c4a8d9f3aa2e8ce344c57";

    internal static void Configure() {
        ConfigureBuff();
        ConfigureAbility();
        ConfigureExtraAttackToggle();
        ConfigureFeature();
        ConfigureArchetype();
    }

    /// <summary>旋风狂暴 Buff：复制原版狂暴 Buff 后逐项改造。</summary>
    private static void ConfigureBuff() {
        BuffConfigurator.New("WhirlingFrenzyBuff", BuffGuid)

            // 注意：CopyFrom(bp) 不传类型参数时只复制字段、一个组件都不复制（componentTypes 为空数组），
            // 必须用谓词重载把全部组件也复制过来。复制得到的组件是与原版共享的实例，
            // 因此下面凡是要改的组件都替换为新实例，避免污染原版狂暴。
            .CopyFrom(BlueprintTool.GetRef<BlueprintBuffReference>(StandartRageBuffGuid), c => true)

            // 覆盖原版的名字与描述（注册在 CopyFrom 之后，按注册顺序后执行）
            .SetDisplayName(LocalizationTool.CreateString("WhirlingFrenzyBuff.Name", "旋风狂暴"))
            .SetDescription(LocalizationTool.CreateString(
                "WhirlingFrenzyBuff.Description",
                "进入旋风狂暴后，如同正常的狂暴，除了以下内容，意志豁免加值和AC减值替换为AC和反射豁免上获得+2闪避加值，"
                + "当一个野蛮人处在旋风狂暴的状态下的时候，他可以在一轮中以进行一次最高基础攻击加值的额外攻击，"
                + "但是当他这么做的时候旋风狂暴野蛮人本轮内的所有攻击承受-2的减值。"))

            // 组件改造放在 OnConfigure（外部配置，最后执行），确保晚于 CopyFrom
            .OnConfigure(bp => {
                var rebuilt = new List<BlueprintComponent>();

                foreach (var c in bp.Components) {
                    // 1) 删除「每生命骰 N 点临时生命值」
                    if (c is TemporaryHitPointsPerLevel) continue;

                    // 2) AC -2 → +2 闪避加值（换新实例）
                    if (c is AddContextStatBonus ac && ac.Stat == StatType.AC) {
                        rebuilt.Add(new AddContextStatBonus {
                            Stat = StatType.AC,
                            Multiplier = 1,
                            Descriptor = ModifierDescriptor.Dodge,
                            Value = ac.Value,
                        });
                        continue;
                    }

                    // 3) 意志豁免 → 反射豁免（换新实例）
                    if (c is AddContextStatBonus will && will.Stat == StatType.SaveWill) {
                        rebuilt.Add(new AddContextStatBonus {
                            Stat = StatType.SaveReflex,
                            Multiplier = will.Multiplier,
                            Descriptor = will.Descriptor,
                            Value = will.Value,
                            HasMinimal = will.HasMinimal,
                            Minimal = will.Minimal,
                        });
                        continue;
                    }

                    rebuilt.Add(c);
                }

                bp.Components = rebuilt.ToArray();
            })

            .Configure();
    }

    /// <summary>
    /// 旋风狂暴能力：从原版狂暴能力复制（因此图标、Group=Rage 都一致），
    /// 只把指向的 Buff 换成旋风狂暴的，并显式确保它是「按一下启动」而非切换。
    /// </summary>
    private static void ConfigureAbility() {
        ActivatableAbilityConfigurator.New("WhirlingFrenzyAbility", AbilityGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintActivatableAbilityReference>(StandartRageAbilityGuid))
            .SetBuff(BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid))
            // Group = Rage（原版即为 Rage，显式写一遍以防 CopyFrom 变化）
            .SetGroup(ActivatableAbilityGroup.Rage)
            // 按一下立即进入狂暴，自由动作
            .SetActivationType(AbilityActivationType.Immediately)
            .SetActivateWithUnitCommand(UnitCommand.CommandType.Free)
            .SetDeactivateIfCombatEnded(true)
            .SetDeactivateIfOwnerUnconscious(true)
            .Configure();
    }

    /// <summary>额外攻击开关的 Buff + 图标开关（仿「防御式攻击」的可切换选项）。</summary>
    private static void ConfigureExtraAttackToggle() {
        BuffConfigurator.New("WhirlingExtraAttackBuff", ExtraBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("WhirlingExtra.Name", "旋风狂暴：额外攻击"))
            .SetDescription(LocalizationTool.CreateString(
                "WhirlingExtra.Description",
                "全回合攻击时所有攻击检定 -2，并额外获得一次以最高基础攻击加值进行的攻击。"))
            // 全回合攻击时所有攻击 -2（原版快速射击同款组件）
            .AddComponent(new WeaponParametersAttackBonus {
                AttackBonus = -2,
                OnlyForFullAttack = true,
                Ranged = false,
                Descriptor = ModifierDescriptor.UntypedStackable,
            })
            // 以最高 BAB 额外攻击一次（Haste=true → HasteAttacks 分支）
            .AddComponent(new BuffExtraAttack {
                Number = 1,
                Haste = true,
                Penalized = false,
            })
            .Configure();

        ActivatableAbilityConfigurator.New("WhirlingExtraAttackToggle", ExtraToggleGuid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintActivatableAbilityReference>(PowerAttackToggleGuid))
            .SetDisplayName(LocalizationTool.CreateString("WhirlingExtraToggle.Name", "旋风狂暴：额外攻击"))
            .SetDescription(LocalizationTool.CreateString(
                "WhirlingExtraToggle.Description",
                "全回合攻击时所有攻击检定 -2，并额外获得一次以最高基础攻击加值进行的攻击。"))
            .SetBuff(BlueprintTool.GetRef<BlueprintBuffReference>(ExtraBuffGuid))
            .SetGroup(ActivatableAbilityGroup.None)
            .SetIsOnByDefault(false)
            .SetDeactivateIfCombatEnded(true)
            .Configure();
    }

    /// <summary>旋风狂暴 Feature：AddFacts → [旋风狂暴能力, 额外攻击开关, 原版狂暴次数资源]。</summary>
    private static void ConfigureFeature() {
        FeatureConfigurator.New("WhirlingFrenzyFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("WhirlingFrenzy.Name", "旋风狂暴"))
            .SetDescription(LocalizationTool.CreateString(
                "WhirlingFrenzy.Description",
                "以「旋风狂暴」替换野蛮人的狂暴。进入旋风狂暴后，如同正常的狂暴，除了以下内容，"
                + "意志豁免加值和AC减值替换为AC和反射豁免上获得+2闪避加值，"
                + "当一个野蛮人处在旋风狂暴的状态下的时候，他可以在一轮中以进行一次最高基础攻击加值的额外攻击，"
                + "但是当他这么做的时候旋风狂暴野蛮人本轮内的所有攻击承受-2的减值。"))
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(AbilityGuid),
                BlueprintTool.GetRef<BlueprintUnitFactReference>(ExtraToggleGuid),
                BlueprintTool.GetRef<BlueprintUnitFactReference>(StandartRageFactGuid),
            })
            .Configure();
    }

    /// <summary>
    /// 野蛮人变体：1 级以旋风狂暴替换原版狂暴。
    ///
    /// 这里用游戏自身的 <see cref="LevelEntry.SetFeatures"/> 构造条目，
    /// 而不是直接给 m_Features 赋值——后者在本环境实测会产生空列表。
    /// </summary>
    private static void ConfigureArchetype() {
        var rageFeature = BlueprintTool.Get<BlueprintFeature>(RageFeatureGuid);
        var whirlingFeature = BlueprintTool.Get<BlueprintFeature>(FeatureGuid);

        ArchetypeConfigurator.New(
                "WhirlingFrenzyArchetype",
                ArchetypeGuid,
                BlueprintTool.GetRef<BlueprintCharacterClassReference>(BarbarianClassGuid))
            .SetLocalizedName(LocalizationTool.CreateString("WhirlingArchetype.Name", "旋风狂暴野蛮人"))
            .SetLocalizedDescription(LocalizationTool.CreateString(
                "WhirlingArchetype.Description",
                "3r野蛮人变体：以「旋风狂暴」替换狂暴。"))
            .OnConfigure(bp => {
                var removeEntry = new LevelEntry { Level = 1 };
                removeEntry.SetFeatures(new List<BlueprintFeatureBase> { rageFeature });
                bp.RemoveFeatures = new[] { removeEntry };

                var addEntry = new LevelEntry { Level = 1 };
                addEntry.SetFeatures(new List<BlueprintFeatureBase> { whirlingFeature });
                bp.AddFeatures = new[] { addEntry };
            })
            .Configure();
    }
}
