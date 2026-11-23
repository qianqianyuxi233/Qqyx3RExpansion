using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Classes.Selection;
using BlueprintCore.Blueprints.Configurators.Root;using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Commands.Base;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 东方冒险（Oriental Adventures）进阶职业「拔刀师（Iaijutsu Master）」。
///
/// <para>
/// 职业表（用户提供，10 级进阶职业）：
///   生命骰 d10 ／ BAB 满 ／ 强韧低、反射高、意志低 ／ 技能点 4+智力
///   熟练：所有简单武器与军用武器；<b>不擅长任何防具和盾牌</b>
///   1 级 精明防御 ／ 2 级 出手如电 ／ 4 级 额外专长 ／ 5 级 虚空斩
///   8 级 二段斩 ／ 9 级 额外专长 ／ 10 级 心无凝滞
/// </para>
///
/// <para>
/// 前提：任意守序、BAB +6、精通先攻、快速拔刀、能使用拔刀术。
/// </para>
///
/// <para>
/// 与规则表不一致、需要说明的两处：
/// <list type="bullet">
/// <item><b>意志豁免</b>：用户表里 2 级写作 +2，与 1 级 +0、3 级 +1 不连贯，疑为笔误。
/// 这里按标准的「低」进度（0,0,1,1,1,2,2,2,3,3）实现，与 3.5 原文一致；</item>
/// <item><b>二段斩</b>：规则要求「手持武士刀」，游戏没有武士刀，放宽为「手持近战武器」。</item>
/// </list>
/// </para>
///
/// <para>
/// <b>心无凝滞（10 级）</b>：原规则是「进入战斗时若近战范围内有敌人，自动获得突袭轮」——
/// 突袭轮是遭遇开始、未察觉状态下的独立阶段，战斗一旦开始就没有「追加一个突袭轮」的入口，
/// 引擎里没有可靠的实现点。按用户要求改为「进行拔刀术检定时 +10 加值」。
/// </para>
/// </summary>
internal static class IaijutsuMaster {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string ClassGuid = "2bf172d9d85b4e019488b29dfaa953de";
    private const string ProgressionGuid = "b9801d4ebae84e4bad08fa34879e6f53";
    private const string ProficienciesGuid = "bb90468c681841d09c736989801af40f";

    private const string CannyDefenseGuid = "80fcaba3745e4291a4c44e4011357f16";
    private const string LightningBladeGuid = "6b5eddc581bc4f02981209352931fa4b";
    // 这两个特性由 IaijutsuFocusDamage 直接按「身上有没有这个特性」判断，
    // 所以 GUID 必须是 internal：控制台/存档工具单独塞进来的特性也要能生效，
    // 不能依赖「拔刀师职业等级」这种间接条件。
    internal const string StrikeFromTheVoidGuid = "1b9b393b5a234ad499eca5a37a3f93a9";
    internal const string StrikeWithNoThoughtGuid = "a6c741f7e40742cc9b3dfa35f30a29b7";
    private const string OneStrikeTwoCutsFeatureGuid = "691a5aa9416747bab4ff25eba34aec48";
    private const string OneStrikeTwoCutsAbilityGuid = "e6ddda258ea94b868ffd01cf2dc8e34d";

    /// <summary>心无凝滞：拔刀术检定加值。具体叠加在 <c>IaijutsuFocusDamage.RollIfNeeded</c> 里做。</summary>
    internal const int StrikeWithNoThoughtBonus = 10;

    // ---------- 原版参照 ----------

    private const string FighterClassGuid = "48ac8db94d5de7645906c7d0ad3bcfbd";
    private const string FighterFeatSelectionGuid = "41c8486641f7d6d4283ca9dae4147a9f";
    /// <summary>FeatureRefs.Improved_Initiative —— 精通先攻</summary>
    private const string ImprovedInitiativeGuid = "797f25d709f559546b29e7bcb181cc74";

    private const string BabFullGuid = "b3057560ffff3514299e8b93e7648a9d";
    private const string SavesHighGuid = "ff4662bde9e75f145853417313842751";
    private const string SavesLowGuid = "dc0c7c1aba755c54f96c089cdf7d14a3";

    private const string ClassName = "拔刀师";

    private const string ClassDescription =
        "拔刀师是拔刀术的大师：把拔刀的一瞬锤炼成决定胜负的艺术。"
        + "他们不依赖护甲，而是凭敏锐的洞察与迅捷的身手在战斗中占尽先机，"
        + "让每一次拔刀都成为致命的一击。";

    private static UnityEngine.Sprite Icon() {
        try {
            var sneak = BlueprintTool.Get<BlueprintFeature>(FeatureRefs.SneakAttack.ToString());
            return sneak == null ? null : sneak.Icon;
        } catch {
            return null;
        }
    }

    internal static void Configure() {
        var proficiencies = ConfigureProficiencies();
        var cannyDefense = ConfigureCannyDefense();
        var lightningBlade = ConfigureLightningBlade();
        var strikeFromTheVoid = ConfigureStrikeFromTheVoid();
        var oneStrikeTwoCuts = ConfigureOneStrikeTwoCuts();
        var strikeWithNoThought = ConfigureStrikeWithNoThought();

        var progression = ConfigureProgression(
            proficiencies, cannyDefense, lightningBlade, strikeFromTheVoid,
            oneStrikeTwoCuts, strikeWithNoThought);
        var charClass = ConfigureClass(progression);
        RegisterInCharacterCreation(charClass);
    }

    // ---------- 1. 武器熟练（无护甲、无盾牌）----------

    private static BlueprintFeature ConfigureProficiencies() {
        return FeatureConfigurator.New("IaijutsuMasterProficiencies", ProficienciesGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.Proficiencies.Name", "武器熟练"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.Proficiencies.Description",
                "拔刀师擅长所有简单武器和军用武器，但不擅长任何防具和盾牌。"))
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(
                FeatureRefs.MartialWeaponProficiency.ToString()).Icon)
            .SetIsClassFeature(true)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                FeatureRefs.SimpleWeaponProficiency.ToString(),
                FeatureRefs.MartialWeaponProficiency.ToString(),
                // 规则明确：不擅长任何防具和盾牌，所以这里不挂任何护甲 / 盾牌熟练
            })
            .Configure();
    }

    // ---------- 2. 1 级：精明防御 ----------

    private static BlueprintFeature ConfigureCannyDefense() {
        return FeatureConfigurator.New("IaijutsuMasterCannyDefense", CannyDefenseGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.CannyDefense.Name", "精明防御"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.CannyDefense.Description",
                "当你不穿着防具时，可以把智力调整值额外附加在防护等级上。\n"
                + "若你处于措手不及状态，本能力无法发挥作用。"))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .AddComponent(new IaijutsuMasterCannyDefense { name = "$IaijutsuMaster$CannyDefense" })
            .Configure();
    }

    // ---------- 3. 2 级：出手如电 ----------

    private static BlueprintFeature ConfigureLightningBlade() {
        return FeatureConfigurator.New("IaijutsuMasterLightningBlade", LightningBladeGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.LightningBlade.Name", "出手如电"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.LightningBlade.Description",
                "从 2 级起，你可以把魅力调整值额外附加在先攻检定上。"))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .AddComponent(new IaijutsuMasterLightningBlade { name = "$IaijutsuMaster$LightningBlade" })
            .Configure();
    }

    // ---------- 4. 5 级：虚空斩 ----------
    //
    // 这个特性本身没有组件：伤害加成由 IaijutsuFocusDamage 按「身上有没有虚空斩特性」判断，
    // 所以控制台/存档工具单独添加、或其它途径获得，都能正常生效。
    // 保留特性只是为了职业面板有显示。

    private static BlueprintFeature ConfigureStrikeFromTheVoid() {
        return FeatureConfigurator.New("IaijutsuMasterStrikeFromTheVoid", StrikeFromTheVoidGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.StrikeFromTheVoid.Name", "虚空斩"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.StrikeFromTheVoid.Description",
                "从 5 级起，你可以把魅力调整值附加在拔刀造成的每一个伤害骰上。\n"
                + "例如你的魅力调整值为 +2，而拔刀术检定结果为 28（4d6），"
                + "则造成的拔刀伤害为 4d6+8。"))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .Configure();
    }

    // ---------- 5. 8 级：二段斩 ----------

    private static BlueprintFeature ConfigureOneStrikeTwoCuts() {
        AbilityConfigurator.New("IaijutsuMasterOneStrikeTwoCutsAbility", OneStrikeTwoCutsAbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.OneStrikeTwoCuts.Name", "二段斩"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.OneStrikeTwoCuts.Description",
                "以一个标准动作对同一目标攻击两次。\n"
                + "需要手持近战武器。"))
            .SetIcon(Icon())
            .SetType(AbilityType.Extraordinary)
            .SetRange(AbilityRange.Weapon)
            // 规则原文：一个标准动作
            .SetActionType(UnitCommand.CommandType.Standard)
            .SetCanTargetEnemies(true)
            .SetCanTargetFriends(false)
            .SetCanTargetSelf(false)
            .AddComponent(new AbilityDeliverRepeatedWeaponAttacks {
                name = "$IaijutsuMaster$TwoCuts",
                AttackCount = 2,
            })
            .Configure();

        return FeatureConfigurator.New("IaijutsuMasterOneStrikeTwoCuts", OneStrikeTwoCutsFeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.OneStrikeTwoCuts.Name", "二段斩"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.OneStrikeTwoCuts.Description",
                "以一个标准动作对同一目标攻击两次。\n"
                + "需要手持近战武器。"))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(OneStrikeTwoCutsAbilityGuid),
            })
            .Configure();
    }

    // ---------- 5b. 10 级：心无凝滞 ----------
    //
    // 与虚空斩一样：特性本身没有组件，检定 +10 由 IaijutsuFocusDamage 按
    // 「身上有没有心无凝滞特性」判断，不依赖职业等级。保留特性是为了职业面板有显示。

    private static BlueprintFeature ConfigureStrikeWithNoThought() {
        return FeatureConfigurator.New("IaijutsuMasterStrikeWithNoThought", StrikeWithNoThoughtGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuMaster.StrikeWithNoThought.Name", "心无凝滞"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.StrikeWithNoThought.Description",
                "从 10 级起，你进行拔刀术检定时获得 +" + StrikeWithNoThoughtBonus + " 加值。"))
            .SetIcon(Icon())
            .SetIsClassFeature()
            .Configure();
    }

    // ---------- 6. 等级进度 ----------

    private static BlueprintProgression ConfigureProgression(
            BlueprintFeature proficiencies,
            BlueprintFeature cannyDefense,
            BlueprintFeature lightningBlade,
            BlueprintFeature strikeFromTheVoid,
            BlueprintFeature oneStrikeTwoCuts,
            BlueprintFeature strikeWithNoThought) {
        var fighterFeats = BlueprintTool.Get<BlueprintFeatureSelection>(FighterFeatSelectionGuid);

        return ProgressionConfigurator.New("IaijutsuMasterProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, proficiencies, cannyDefense)
            .AddToLevelEntry(2, lightningBlade)
            // 额外专长：4 级与 9 级，池子直接用战士的额外专长选择
            .AddToLevelEntry(4, fighterFeats)
            .AddToLevelEntry(5, strikeFromTheVoid)
            // 表里第二个「9」按正文「8 级或以上」理解为 8 级
            .AddToLevelEntry(8, oneStrikeTwoCuts)
            .AddToLevelEntry(9, fighterFeats)
            .AddToLevelEntry(10, strikeWithNoThought)
            .SetIsClassFeature(true)
            .SetRanks(1)
            .SetDisplayName("")
            .SetDescription(LocalizationTool.CreateString("IaijutsuMaster.Description", ClassDescription))
            .Configure();
    }

    // ---------- 7. 职业本体 ----------

    private static BlueprintCharacterClass ConfigureClass(BlueprintProgression progression) {
        var improvedInitiative = BlueprintTool.Get<BlueprintFeature>(ImprovedInitiativeGuid);
        var iaijutsu = BlueprintTool.Get<BlueprintFeature>(Iaijutsu.FeatureGuid);
        var quickDraw = BlueprintTool.Get<BlueprintFeature>(IaijutsuFeats.QuickDrawGuid);

        return CharacterClassConfigurator.New("IaijutsuMaster", ClassGuid)
            .SetLocalizedName(LocalizationTool.CreateString("IaijutsuMaster.Name", ClassName))
            .SetLocalizedDescription(LocalizationTool.CreateString("IaijutsuMaster.Description", ClassDescription))
            .SetLocalizedDescriptionShort(LocalizationTool.CreateString("IaijutsuMaster.DescriptionShort", ClassDescription))
            .SetSkillPoints(4)
            .SetHitDie(DiceType.D10)
            // 进阶职业
            .SetPrestigeClass(true)
            // 职业表：BAB 满；强韧低、反射高、意志低
            .SetBaseAttackBonus(BabFullGuid)
            .SetFortitudeSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetReflexSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetWillSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetProgression(progression)
            .SetClassSkills(new StatType[] {
                StatType.SkillAthletics,        // 攀爬 / 跳跃
                StatType.SkillMobility,         // 平衡 / 骑乘 / 翻滚
                StatType.SkillPersuasion,       // 交涉
                StatType.SkillPerception,       // 观言察色
                StatType.SkillKnowledgeWorld,   // 手艺 / 专业的近似
            })
            // 精明防御吃智力，拔刀术与虚空斩吃魅力
            .SetRecommendedAttributes(StatType.Charisma, StatType.Intelligence, StatType.Strength)
            .SetNotRecommendedAttributes(StatType.Wisdom)
            .SetDifficulty(1)
            .SetStartingGold(0)
            // ---- 进阶前提 ----
            .AddComponent(new PrerequisiteAlignment {
                name = "$IaijutsuMaster$Alignment",
                Alignment = AlignmentMaskType.Lawful,
            })
            .AddComponent(new PrerequisiteStatValue {
                name = "$IaijutsuMaster$Bab",
                Stat = StatType.BaseAttackBonus,
                Value = 6,
            })
            .AddComponent(new PrerequisiteFeature {
                name = "$IaijutsuMaster$ImprovedInitiative",
                m_Feature = improvedInitiative.ToReference<BlueprintFeatureReference>(),
            })
            .AddComponent(new PrerequisiteFeature {
                name = "$IaijutsuMaster$QuickDraw",
                m_Feature = quickDraw.ToReference<BlueprintFeatureReference>(),
            })
            .AddComponent(new PrerequisiteFeature {
                name = "$IaijutsuMaster$Iaijutsu",
                m_Feature = iaijutsu.ToReference<BlueprintFeatureReference>(),
            })
            .OnConfigure(bp => {
                bp.IsDivineCaster = false;
                bp.IsArcaneCaster = false;

                // 外观实体、UI 配色、默认 build：借用战士的（只读引用，不改原版）。
                // 进阶职业没有起始装备，m_StartingItems 保持为空。
                var fighter = BlueprintTool.Get<BlueprintCharacterClass>(FighterClassGuid);
                if (fighter != null) {
                    bp.MaleEquipmentEntities = fighter.MaleEquipmentEntities;
                    bp.FemaleEquipmentEntities = fighter.FemaleEquipmentEntities;
                    bp.PrimaryColor = fighter.PrimaryColor;
                    bp.SecondaryColor = fighter.SecondaryColor;
                    bp.m_DefaultBuild = fighter.m_DefaultBuild;
                }
            })
            .Configure();
    }

    // ---------- 8. 注册进职业列表 ----------

    /// <summary>
    /// 进阶职业同样要进 <c>m_CharacterClasses</c>：游戏用 <c>PrestigeClass</c> 标志
    /// 把它们从「创建角色」里过滤掉，只在升级选职业时出现（原版决斗者等进阶职业的注册方式也一样）。
    /// </summary>
    private static void RegisterInCharacterCreation(BlueprintCharacterClass charClass) {
        var classRef = charClass.ToReference<BlueprintCharacterClassReference>();

        Action<ProgressionRoot> register = root => {
            var classes = new BlueprintCharacterClassReference[root.m_CharacterClasses.Length + 1];
            for (int i = 0; i < root.m_CharacterClasses.Length; i++) {
                classes[i] = root.m_CharacterClasses[i];
            }
            classes[root.m_CharacterClasses.Length] = classRef;
            root.m_CharacterClasses = classes;
        };

        RootConfigurator.For(RootRefs.BlueprintRoot)
            .ModifyProgression(register)
            .Configure();

        Main.Log?.Log("[拔刀师] 已注册进职业列表（ProgressionRoot.m_CharacterClasses）");
    }
}
