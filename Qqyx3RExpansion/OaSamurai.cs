using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Alignments;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 东方冒险（Oriental Adventures）规则基础职业「武士（Samurai）」。
///
/// <para>
/// 职业表（用户提供）：
///   生命骰 d10 ／ BAB 满 ／ 强韧高、反射低、<b>意志高</b> ／ 技能点 4+智力
///   熟练：所有简单武器与军用武器、轻甲与中甲；<b>不熟练重甲与盾牌</b>
///   1 级：拔刀斩（授予「拔刀术」能力，见 <see cref="Iaijutsu"/>）
///   2/4/7/10/13/16/19 级：奖励专长，<b>只能从战士奖励专长中选</b>
///   阵营：任意守序
/// </para>
///
/// <para>
/// 几处本地化取舍：
/// <list type="bullet">
/// <item>「攀爬／跳跃／游泳」合并为游戏的运动（Athletics）；
/// 「交涉／威吓」合并为沟通（Persuasion）；「骑乘」对应机动（Mobility）；
/// 「观言察色」对应察觉（Perception）；</item>
/// <item>游戏没有「手艺／表演／专业」三项技能，用知识（世界）作为贵族教养的近似，
/// 让 4 点技能点有足够去处；「拔刀术」不是游戏技能，已实现为主动能力；</item>
/// <item>阵营限制用 <see cref="PrerequisiteAlignment"/>，与圣武士同一套机制。</item>
/// </list>
/// </para>
///
/// <para>
/// 尚未实现：「杰作剑刃（Ancestral Daisho）」——职业表中没有给出对应的等级与规则数值，
/// 等规则文本到位再补。
/// </para>
/// </summary>
internal static class OaSamurai {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string ClassGuid = "c10d4d172f564384a9259bd46a377cde";
    private const string ProgressionGuid = "71193feb8e5a41cfbcbb2823f0f78892";
    private const string ProficienciesGuid = "e85ebde40fab4e9a8934defcb68b16f9";

    // ---------- 原版参照 ----------

    /// <summary>Classes/Fighter/FighterClass —— 借用外观实体、UI 配色、默认 build 与起始装备</summary>
    private const string FighterClassGuid = "48ac8db94d5de7645906c7d0ad3bcfbd";

    /// <summary>战士的奖励专长池（额外专长选择）</summary>
    private const string FighterFeatSelectionGuid = "41c8486641f7d6d4283ca9dae4147a9f";

    /// <summary>BAB 满进度</summary>
    private const string BabFullGuid = "b3057560ffff3514299e8b93e7648a9d";
    /// <summary>高豁免进度</summary>
    private const string SavesHighGuid = "ff4662bde9e75f145853417313842751";
    /// <summary>低豁免进度</summary>
    private const string SavesLowGuid = "dc0c7c1aba755c54f96c089cdf7d14a3";

    private const string ClassName = "武士";

    private const string ClassDescription =
        "武士是专业的战士，是受过战争技艺训练的贵族。他们为服从、忠诚与荣誉的准则而生，"
        + "这准则影响着他们一生中的每一步。脱离阶级与地位的武士不再是武士，"
        + "只是没有荣誉、也没有社会地位的浪人。\n"
        + "\n"
        + "特征：武士与普通战士的区别在于他们坚守武士道——一种荣誉、忠诚与服从的准则。"
        + "他们受过严苛的战斗训练，并拥有可与战士媲美的额外专长。"
        + "只要保持着荣誉与行为准则，他代代相传的杰作剑刃便可能在他手中苏醒，"
        + "随着武士等级的提高与精神能量的投入，武器会展现出越来越强的魔法能力。"
        + "对一个武士来说，耻辱比死亡更严重，失去佩剑可能是最可耻的耻辱。\n"
        + "\n"
        + "阵营：只有守序的角色才能遵守武士道并自称武士。";

    internal static void Configure() {
        var proficiencies = ConfigureProficiencies();
        var progression = ConfigureProgression(proficiencies);
        var charClass = ConfigureClass(progression);
        RegisterInCharacterCreation(charClass);
    }

    // ---------- 1. 武器与护甲熟练 ----------

    private static BlueprintFeature ConfigureProficiencies() {
        return FeatureConfigurator.New("OaSamuraiProficiencies", ProficienciesGuid)
            .SetDisplayName(LocalizationTool.CreateString("OaSamurai.Proficiencies.Name", "武器与护甲熟练"))
            .SetDescription(LocalizationTool.CreateString("OaSamurai.Proficiencies.Description",
                "武士熟练所有简单武器和军用武器，以及轻型和中型防具。武士并不擅长盾牌，"
                + "也不擅长重型防具。"))
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(
                FeatureRefs.MartialWeaponProficiency.ToString()).Icon)
            .SetIsClassFeature(true)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                FeatureRefs.SimpleWeaponProficiency.ToString(),
                FeatureRefs.MartialWeaponProficiency.ToString(),
                FeatureRefs.LightArmorProficiency.ToString(),
                FeatureRefs.MediumArmorProficiency.ToString(),
                // 规则明确「并不擅长盾牌」，故不挂 ShieldsProficiency；
                // 也明确只到中型防具，故不挂 HeavyArmorProficiency。
            })
            .Configure();
    }

    // ---------- 2. 等级进度 ----------

    private static BlueprintProgression ConfigureProgression(BlueprintFeature proficiencies) {
        var iaijutsu = BlueprintTool.Get<BlueprintFeature>(Iaijutsu.FeatureGuid);
        var fighterFeats = BlueprintTool.Get<Kingmaker.Blueprints.Classes.Selection.BlueprintFeatureSelection>(
            FighterFeatSelectionGuid);

        return ProgressionConfigurator.New("OaSamuraiProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, proficiencies, iaijutsu)
            // 奖励专长：2/4/7/10/13/16/19 级，池子直接用战士的额外专长选择
            .AddToLevelEntry(2, fighterFeats)
            .AddToLevelEntry(4, fighterFeats)
            .AddToLevelEntry(7, fighterFeats)
            .AddToLevelEntry(10, fighterFeats)
            .AddToLevelEntry(13, fighterFeats)
            .AddToLevelEntry(16, fighterFeats)
            .AddToLevelEntry(19, fighterFeats)
            .SetIsClassFeature(true)
            .SetRanks(1)
            .SetDisplayName("")
            .SetDescription(LocalizationTool.CreateString("OaSamurai.Description", ClassDescription))
            .Configure();
    }

    // ---------- 3. 职业本体 ----------

    private static BlueprintCharacterClass ConfigureClass(BlueprintProgression progression) {
        return CharacterClassConfigurator.New("OaSamurai", ClassGuid)
            .SetLocalizedName(LocalizationTool.CreateString("OaSamurai.Name", ClassName))
            .SetLocalizedDescription(LocalizationTool.CreateString("OaSamurai.Description", ClassDescription))
            .SetLocalizedDescriptionShort(LocalizationTool.CreateString("OaSamurai.DescriptionShort", ClassDescription))
            // 初始技能点 = (4 + 智力调整值) × 4，每级 4 + 智力调整值
            .SetSkillPoints(4)
            .SetHitDie(DiceType.D10)
            .SetPrestigeClass(false)
            // 职业表：BAB 满；强韧高、反射低、意志高
            .SetBaseAttackBonus(BabFullGuid)
            .SetFortitudeSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetReflexSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetWillSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetProgression(progression)
            .SetClassSkills(new StatType[] {
                StatType.SkillAthletics,        // 攀爬 / 跳跃 / 游泳
                StatType.SkillPersuasion,       // 交涉 / 威吓
                StatType.SkillPerception,       // 观言察色
                StatType.SkillMobility,         // 骑乘
                StatType.SkillKnowledgeWorld,   // 手艺 / 专业的近似（贵族教养）
            })
            // 拔刀术检定吃魅力，攻击吃力量，生命与强韧吃体质
            .SetRecommendedAttributes(StatType.Strength, StatType.Charisma, StatType.Constitution)
            .SetNotRecommendedAttributes(StatType.Intelligence)
            .SetDifficulty(1)
            .SetStartingGold(175)
            .AddComponent(new PrerequisiteAlignment {
                name = "$OaSamurai$Alignment",
                // 任意守序（LawfulGood | LawfulNeutral | LawfulEvil）
                Alignment = AlignmentMaskType.Lawful,
            })
            .OnConfigure(bp => {
                // 非施法职业
                bp.IsDivineCaster = false;
                bp.IsArcaneCaster = false;

                // 外观实体、UI 配色、默认 build、起始装备：借用战士的（只读引用，不改原版）。
                // 缺失这些字段会导致角色创建界面不显示外观。
                var fighter = BlueprintTool.Get<BlueprintCharacterClass>(FighterClassGuid);
                if (fighter != null) {
                    bp.MaleEquipmentEntities = fighter.MaleEquipmentEntities;
                    bp.FemaleEquipmentEntities = fighter.FemaleEquipmentEntities;
                    bp.PrimaryColor = fighter.PrimaryColor;
                    bp.SecondaryColor = fighter.SecondaryColor;
                    bp.m_DefaultBuild = fighter.m_DefaultBuild;
                    bp.m_StartingItems = fighter.m_StartingItems;
                }
            })
            .Configure();
    }

    // ---------- 4. 注册进角色创建菜单 ----------

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

        // 同步注册（原因见 Archivist.RegisterInCharacterCreation 的注释：
        // 本 mod 的 patch 是 Priority.Last，晚于 BlueprintCore 自己的延迟队列 flush）。
        RootConfigurator.For(RootRefs.BlueprintRoot)
            .ModifyProgression(register)
            .Configure();

        Main.Log?.Log("[武士] 已注册进角色创建菜单（ProgressionRoot.m_CharacterClasses）");
    }
}
