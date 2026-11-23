using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 东方冒险（Oriental Adventures）规则「拔刀术」。
///
/// <para>
/// 规则原文：以一个移动动作进行一次拔刀术检定（d20 + 角色等级 + 魅力调整值）。
/// 在本轮内攻击措手不及或正被自己夹击的目标时，按检定结果获得额外 d6 精准伤害；
/// 免疫偷袭的生物同样免疫这些伤害。
/// </para>
///
/// <para>
/// 实现拆成三块：
/// <list type="bullet">
/// <item><b>能力</b>：移动动作、无目标、给自己挂 1 轮的 buff；</item>
/// <item><b>buff</b>：只承载 <see cref="IaijutsuFocusDamage"/>，由它负责掷骰与伤害结算；</item>
/// <item><b>特性</b>：把能力塞进 OA武士 的 1 级进度里，玩家在能力栏就能看到。</item>
/// </list>
/// buff 用 <c>StackingType.Replace</c>：重复使用时会先移除旧 buff 再加新的，
/// 因此每次都会重新掷检——这符合「每次使用都要重新检定」的规则。
/// </para>
/// </summary>
internal static class Iaijutsu {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string FeatureGuid = "2a96cd30370646b48156f87455d66c91";
    internal const string AbilityGuid = "fa3cd6db1b9649fdbb1ef47fa58125ee";
    internal const string BuffGuid    = "a78c064e41554c3f8382877e01492531";

    private const string FeatureName = "拔刀术";

    /// <summary>
    /// 职业特性名。规则里武士 1 级特性写作「拔刀斩」，其效果就是习得「拔刀术」能力，
    /// 所以特性与能力分两个名字：职业面板显示「拔刀斩」，能力栏里是可主动使用的「拔刀术」。
    /// </summary>
    private const string ClassFeatureName = "拔刀斩";

    /// <summary>
    /// 图标借原版「偷袭」的 —— 拔刀术与偷袭同为精准伤害，且共用同一套免疫判定，
    /// 视觉上让玩家能一眼联想到「精准、偷袭类」效果。
    /// </summary>
    private static UnityEngine.Sprite Icon() {
        try {
            var sneakAttack = BlueprintTool.Get<BlueprintFeature>(FeatureRefs.SneakAttack.ToString());
            return sneakAttack == null ? null : sneakAttack.Icon;
        } catch {
            return null;
        }
    }

    private const string Description =
        "以一个移动动作，你进行一次拔刀术检定：投掷 1d20，加上你的角色等级和魅力调整值。\n"
        + "\n"
        + "在本轮内，你攻击措手不及或正被你夹击的目标时，获得由拔刀术检定结果决定的额外伤害。"
        + "拔刀术伤害属于精准伤害，免疫偷袭的生物同样免疫拔刀术伤害。\n"
        + "\n"
        + "检定结果 → 额外伤害\n"
        + "5–9 → +1d6\n"
        + "10–14 → +2d6\n"
        + "15–19 → +3d6\n"
        + "20–24 → +4d6\n"
        + "25–29 → +5d6\n"
        + "30–34 → +6d6\n"
        + "35–39 → +7d6\n"
        + "40–44 → +8d6\n"
        + "45+ → +9d6\n"
        + "\n"
        + "检定结果低于 5 时没有额外伤害。";

    internal static void Configure() {
        var buff = ConfigureBuff();
        ConfigureAbility(buff);
        ConfigureFeature();
    }

    // ---------- buff ----------

    private static BlueprintBuff ConfigureBuff() {
        return BuffConfigurator.New("OaSamuraiIaijutsuBuff", BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Iaijutsu.Buff.Name", "拔刀术"))
            .SetDescription(LocalizationTool.CreateString("Iaijutsu.Buff.Description",
                "本轮内，你攻击措手不及或正被你夹击的目标时，额外造成精准伤害。\n"
                + "额外伤害取决于本次拔刀术检定的结果：5–9 为 1d6，之后每提高 5 点增加 1d6，"
                + "45 及以上为 9d6。\n"
                + "免疫偷袭的生物同样免疫这些伤害。"))
            .SetIcon(Icon())
            .OnConfigure(bp => {
                // 重复使用能力时先移除旧 buff 再加新的 —— 每次都会重新触发 OnActivate 重新掷检。
                bp.Stacking = StackingType.Replace;
            })
            .AddComponent(new IaijutsuFocusDamage {
                name = "$Iaijutsu$Damage",
                // 拔刀师 5 级起自动获得「虚空斩」（每骰 + 魅力），见组件注释
                IaijutsuMasterClass = BlueprintTool.GetRef<BlueprintCharacterClassReference>(
                    IaijutsuMaster.ClassGuid),
                // 拔刀专精系列的检定加值（真正叠加发生在 IaijutsuFocusDamage.RollIfNeeded）
                IaijutsuWeaponFocus = BlueprintTool.GetRef<BlueprintFeatureReference>(
                    IaijutsuFeats.WeaponFocusGuid),
                MythicIaijutsuWeaponFocus = BlueprintTool.GetRef<BlueprintFeatureReference>(
                    IaijutsuFeats.MythicWeaponFocusGuid),
            })
            .Configure();
    }

    // ---------- 启动能力（移动动作）----------

    private static BlueprintAbility ConfigureAbility(BlueprintBuff buff) {
        return AbilityConfigurator.New("OaSamuraiIaijutsuAbility", AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString("Iaijutsu.Ability.Name", FeatureName))
            .SetDescription(LocalizationTool.CreateString("Iaijutsu.Ability.Description", Description))
            .SetIcon(Icon())
            // 特异能力（Ex）—— 不引发法术抗力、不是法术
            .SetType(AbilityType.Extraordinary)
            .SetRange(AbilityRange.Personal)
            // 规则原文明确是「一个移动动作」
            .SetActionType(Kingmaker.UnitLogic.Commands.Base.UnitCommand.CommandType.Move)
            .AddComponent(new AbilityEffectRunAction {
                name = "$Iaijutsu$Apply",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$Iaijutsu$ApplyBuff",
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(BuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            // 1 轮（6 秒）
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

    // ---------- 特性（挂到职业进度里）----------

    private static void ConfigureFeature() {
        FeatureConfigurator.New("OaSamuraiIaijutsuFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("Iaijutsu.Feature.Name", ClassFeatureName))
            .SetDescription(LocalizationTool.CreateString("Iaijutsu.Feature.Description",
                "经过严苛的训练，你习得了拔刀术这一能力。\n\n" + Description))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(AbilityGuid),
            })
            .Configure();
    }
}
