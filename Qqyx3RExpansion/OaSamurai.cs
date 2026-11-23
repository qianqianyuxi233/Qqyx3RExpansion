using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 东方冒险（Oriental Adventures）规则基础职业「OA武士」。
///
/// <para>
/// 目前只做到「骨架 + 1 级能力」：
/// <list type="bullet">
/// <item>生命骰 d10、BAB 满、强韧高 / 反射低 / 意志低、技能点 2+智力；</item>
/// <item>熟练所有简易与军用武器、轻/中/重甲与盾牌（对应武士的「全武器全护甲」）；</item>
/// <item>1 级：<b>拔刀术</b>（见 <see cref="Iaijutsu"/>）。</item>
/// </list>
/// 后续职业能力（武器专精、居合斩、挑战等）尚未实现，等规则文本到位再加。
/// </para>
///
/// <para>
/// 注册方式与 <see cref="Archivist"/> 完全一致：基础职业必须把自身引用追加到
/// <c>ProgressionRoot.m_CharacterClasses</c>，否则角色创建界面里根本看不到它。
/// </para>
/// </summary>
internal static class OaSamurai {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string ClassGuid = "c10d4d172f564384a9259bd46a377cde";
    private const string ProgressionGuid = "71193feb8e5a41cfbcbb2823f0f78892";
    private const string ProficienciesGuid = "e85ebde40fab4e9a8934defcb68b16f9";

    // ---------- 原版参照 ----------

    /// <summary>Classes/Fighter/FighterClass —— 借用它的外观实体、UI 配色、默认 build 与起始装备</summary>
    private const string FighterClassGuid = "48ac8db94d5de7645906c7d0ad3bcfbd";

    /// <summary>BAB 满进度</summary>
    private const string BabFullGuid = "b3057560ffff3514299e8b93e7648a9d";
    /// <summary>高豁免进度</summary>
    private const string SavesHighGuid = "ff4662bde9e75f145853417313842751";
    /// <summary>低豁免进度</summary>
    private const string SavesLowGuid = "dc0c7c1aba755c54f96c089cdf7d14a3";

    private const string ClassName = "OA武士";

    private const string ClassDescription =
        "OA武士是来自东方的剑士：以刀为魂，在拔刀的一瞬间决出胜负。"
        + "他们精于各类武器与护甲，并以拔刀术在敌人措手不及时造成致命打击。";

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
                "OA武士熟练所有简易武器与军用武器，以及轻甲、中甲、重甲和盾牌。"))
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(
                FeatureRefs.MartialWeaponProficiency.ToString()).Icon)
            .SetIsClassFeature(true)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                FeatureRefs.SimpleWeaponProficiency.ToString(),
                FeatureRefs.MartialWeaponProficiency.ToString(),
                FeatureRefs.LightArmorProficiency.ToString(),
                FeatureRefs.MediumArmorProficiency.ToString(),
                FeatureRefs.HeavyArmorProficiency.ToString(),
                FeatureRefs.ShieldsProficiency.ToString(),
            })
            .Configure();
    }

    // ---------- 2. 等级进度 ----------

    private static BlueprintProgression ConfigureProgression(BlueprintFeature proficiencies) {
        var iaijutsu = BlueprintTool.Get<BlueprintFeature>(Iaijutsu.FeatureGuid);

        return ProgressionConfigurator.New("OaSamuraiProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, proficiencies, iaijutsu)
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
            .SetSkillPoints(2)
            .SetHitDie(DiceType.D10)
            .SetPrestigeClass(false)
            // 职业表：BAB 满；强韧高、反射低、意志低（3.5 武士）
            .SetBaseAttackBonus(BabFullGuid)
            .SetFortitudeSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetReflexSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetWillSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetProgression(progression)
            .SetClassSkills(new StatType[] {
                StatType.SkillAthletics,        // 攀爬 / 游泳
                StatType.SkillPersuasion,       // 交涉 / 威吓
                StatType.SkillKnowledgeWorld,   // 知识（贵族）
                StatType.SkillMobility,         // 骑术
                StatType.SkillPerception,       // 察言观色
            })
            // 拔刀术的检定吃魅力，攻击吃力量，生存吃体质
            .SetRecommendedAttributes(StatType.Strength, StatType.Charisma, StatType.Constitution)
            .SetNotRecommendedAttributes(StatType.Intelligence, StatType.Wisdom)
            .SetDifficulty(1)
            .SetStartingGold(175)
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

        Main.Log?.Log("[OA武士] 已注册进角色创建菜单（ProgressionRoot.m_CharacterClasses）");
    }
}
