using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Root;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 进阶职业「荒野行者 Wildrunner」（《 races of the Wild 》）。
///
/// 结构照抄本项目已验证过的 <see cref="SacredExorcist"/>：
/// 同一个 <c>BlueprintCharacterClass</c> 类型，靠 <c>PrestigeClass = true</c> 标记
/// （游戏会在创建角色/招募时过滤掉它，只在升级时出现，且自动限 10 级）；
/// 最后必须把职业追加进 <c>BlueprintRoot.Progression.m_CharacterClasses</c>。
///
/// 生命骰 D10，满 BAB；强韧/反射用**进阶职业高进度**、意志用**低进度**，
/// 与 3.5 原始表格一致（+1/+2/+2/+0 → … → +10/+7/+7/+3）。
///
/// ⚠ 与 3.5 原表的差异（均为引擎限制，已与作者确认）：
/// <list type="bullet">
/// <item><b>无踪步</b>（1 级）—— 游戏没有实现这个能力，删除；</item>
/// <item><b>忍受元素伤害</b>（3 级）—— 游戏没有 <c>EndureElements</c> 法术，删除；</item>
/// <item><b>精之心的「种类变为精类」</b>（9 级）—— 游戏没有生物类型概念，删除，
/// 但保留 <b>DR 2/寒铁</b>；</item>
/// <item>作为补偿，<b>3 级与 9 级各获得一个奖励专长</b>（作者指定）；</item>
/// <item>进阶前置的「忍耐」专长游戏里不存在，改用<b>强韧加强</b>（+2 强韧豁免）。</item>
/// </list>
/// </summary>
internal static class Wildrunner {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string ClassGuid            = "008b26f4a378464db5e3ab5101efd8f5";
    private const string ProgressionGuid      = "501b1130d33f4c4094bdb0fafee3d4bb";
    private const string FastMovementGuid     = "51ab32c1ff604611ac8d69da65a136cc";
    private const string ScentGuid            = "2c14acf16c0d4f088468de6081b82bdf";
    private const string HideInPlainSightGuid = "1c89b6573f2043b1b1ad9d772c844ca8";
    private const string WoodlandStrideGuid   = "b7627ea24f004b80ab62ba7397b107cc";
    private const string FeyHeartGuid         = "7d7afb6ca7e843f0a375b93046c9e423";
    private const string BonusFeat3Guid       = "003a040f46964388a34ef87dfaf7b84f";
    private const string BonusFeat9Guid       = "7dab13a921aa4596af250d1c62faa713";

    // 原始之吼相关的 GUID 由 WildrunnerPrimalScream 使用
    internal const string PrimalScreamGuid = "23d268e3ac4045bbb02467ee462ba591";

    // ---------- 原版参照物 GUID ----------

    /// <summary>满 BAB 进度（战士用的那条）</summary>
    private const string BabHighGuid = "b3057560ffff3514299e8b93e7648a9d";
    /// <summary>进阶职业专用的低豁免进度（每 3 级 +1，从 0 起）</summary>
    private const string SavePrestigeLowGuid = "dc5257e1100ad0d48b8f3b9798421c72";
    /// <summary>进阶职业专用的高豁免进度（每 2 级 +1，从 +2 起）</summary>
    private const string SavePrestigeHighGuid = "1f309006cd2855e4e91a6c3707f3f700";

    /// <summary>Classes/Barbarian/RagePowers/ScentFeature —— 原版灵敏嗅觉（借实现）</summary>
    private const string ScentSourceGuid = "6e5d57a733d1eea46a9022a304f2c728";
    /// <summary>Classes/Ranger/HideInPlainSight —— 原版游侠的视线躲藏</summary>
    private const string HideInPlainSightSourceGuid = "6a0e4c8021fd6fa41ad28b54ba3355cb";
    /// <summary>Classes/Druid/DruidWoodlandStride —— 原版德鲁伊的林地 stride（= 无拘之步）</summary>
    private const string WoodlandStrideSourceGuid = "4c1419ef6cfc430a9071405788da4a73";
    /// <summary>Feats/GreatFortitude —— 强韧加强（+2 强韧豁免，作为进阶前置）</summary>
    private const string GreatFortitudeGuid = "79042cb55f030614ea29956177977c52";
    /// <summary>Races/ElfRace —— 精灵</summary>
    private const string ElfRaceGuid = "25a5878d125338244896ebd3238226c8";
    /// <summary>Races/HalfElfRace —— 半精灵</summary>
    private const string HalfElfRaceGuid = "b3646842ffbd01643ab4dac7479b20b0";
    /// <summary>Classes/Basic/BasicFeatSelection —— 普通专长池（奖励专长用）</summary>
    private const string BasicFeatSelectionGuid = "247a4068296e8be42890143f451b4b45";

    internal const string ClassName = "荒野行者";
    internal const string ClassDescription =
        "荒野行者几乎完全献身于自然，追求回归未驯服的本源，并最终成为精类生物。"
        + "尽管看起来野蛮，他们在发掘自身与大地内在的原始力量时，"
        + "也保留着文明社会的知识与道德判断。\n"
        + "生命骰：D10。本职技能：知识（自然）、潜行。技能点：4 + 智力调整值。\n"
        + "荒野行者不获得任何武器和盔甲擅长。\n"
        + "进阶条件：精灵或半精灵；任意善良或混乱阵营；知识（自然）5 级、潜行 5 级；强韧加强专长。";

    internal static void Configure() {
        var fastMovement = ConfigureFastMovement();
        var scent = ConfigureScent();
        WildrunnerPrimalScream.Configure();
        var hideInPlainSight = ConfigureHideInPlainSight();
        var woodlandStride = ConfigureWoodlandStride();
        var feyHeart = ConfigureFeyHeart();
        var bonusFeat3 = ConfigureBonusFeat("WildrunnerBonusFeat3", BonusFeat3Guid);
        var bonusFeat9 = ConfigureBonusFeat("WildrunnerBonusFeat9", BonusFeat9Guid);

        var progression = ProgressionConfigurator.New("WildrunnerProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, fastMovement)
            .AddToLevelEntry(2, scent, WildrunnerPrimalScream.FeatureGuid)
            .AddToLevelEntry(3, bonusFeat3)
            .AddToLevelEntry(4, WildrunnerPrimalScream.FrightfulGuid)
            .AddToLevelEntry(5, hideInPlainSight)
            .AddToLevelEntry(6, WildrunnerPrimalScream.PounceGuid)
            .AddToLevelEntry(7, woodlandStride)
            .AddToLevelEntry(8, WildrunnerPrimalScream.DreadGuid)
            .AddToLevelEntry(9, feyHeart, bonusFeat9)
            .AddToLevelEntry(10, WildrunnerPrimalScream.ReviveGuid)
            .SetRanks(1)
            .SetIsClassFeature(true)
            .SetDisplayName("")
            .SetDescription(LocalizationTool.CreateString("Wildrunner.Progression.Description", ClassDescription))
            .Configure();

        var characterClass = CharacterClassConfigurator.New("Wildrunner", ClassGuid)
            .SetLocalizedName(LocalizationTool.CreateString("Wildrunner.Name", ClassName))
            .SetLocalizedDescription(LocalizationTool.CreateString("Wildrunner.Description", ClassDescription))
            .SetSkillPoints(4)
            .SetHitDie(DiceType.D10)
            .SetPrestigeClass(true)
            // 满 BAB；强韧 / 反射高，意志低（与 3.5 原表一致）
            .SetBaseAttackBonus(BabHighGuid)
            .SetFortitudeSave(SavePrestigeHighGuid)
            .SetReflexSave(SavePrestigeHighGuid)
            .SetWillSave(SavePrestigeLowGuid)
            .SetProgression(progression)
            .SetClassSkills(new[] {
                // 游戏里没有「知识（自然）」技能，用「知识（世界）」代替（同为知识类）
                StatType.SkillKnowledgeWorld, StatType.SkillStealth,
            })
            // 前置：精灵**或**半精灵（二选一 —— 不能用两次 AddPrerequisiteFeature，
            //       那是 AND 关系，会变成"既要是精灵又要是半精灵"，永远无法满足）、
            //       善良或混乱阵营、知识（自然）5、潜行 5、强韧加强
            .AddPrerequisiteFeaturesFromList(new List<Blueprint<BlueprintFeatureReference>> {
                BlueprintTool.GetRef<BlueprintFeatureReference>(ElfRaceGuid),
                BlueprintTool.GetRef<BlueprintFeatureReference>(HalfElfRaceGuid),
            })
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(GreatFortitudeGuid))
            .AddPrerequisiteAlignment(AlignmentMaskType.Good | AlignmentMaskType.Chaotic)
            .AddPrerequisiteStatValue(StatType.SkillKnowledgeWorld, 5)
            .AddPrerequisiteStatValue(StatType.SkillStealth, 5)
            // ⚠ 不要加「本职不能再选本职业」之类的前置：那会把角色永久卡在 1 级（本项目踩过）
            .Configure();

        RegisterInCharacterCreation(characterClass);

        Main.Log?.Log("[荒野行者] 已注册（" + ClassGuid + "）");
    }

    /// <summary>
    /// 把职业追加进 <c>BlueprintRoot.Progression.m_CharacterClasses</c>（进阶职业也放这个数组）。
    ///
    /// ⚠ 必须用 <c>Configure()</c> 的**返回值**，不能用 <c>BlueprintTool.Get&lt;T&gt;(guid)</c>：
    /// 在 <c>BlueprintsCache.Init</c> 期间刚 new 出来的蓝图还没进游戏缓存，
    /// <c>BlueprintTool.Get</c> 会抛 "Failed to fetch blueprint"（神圣驱魔师踩过这个坑）。
    /// </summary>
    private static void RegisterInCharacterCreation(BlueprintCharacterClass characterClass) {
        var classRef = characterClass.ToReference<BlueprintCharacterClassReference>();

        RootConfigurator.For(RootRefs.BlueprintRoot)
            .ModifyProgression(root => {
                var list = new List<BlueprintCharacterClassReference>(root.m_CharacterClasses);
                if (list.Contains(classRef)) return;
                list.Add(classRef);
                root.m_CharacterClasses = list.ToArray();
            })
            .Configure();

        Main.Log?.Log("[荒野行者] 已注册进升级菜单（ProgressionRoot.m_CharacterClasses）");
    }

    // ==================== 1 级：快速移动 ====================

    /// <summary>
    /// 快速移动：不穿甲、或穿轻甲/中甲时陆地速度 +10 尺，且与类似加值叠加。
    ///
    /// 配置照抄原版野蛮人的 <c>Classes/Barbarian/FastMovement</c>：
    /// <c>SpeedBonusInArmorCategory.CheckArmor()</c> 的实际判定是
    /// 「没穿着 <c>Category</c> 里列出的甲就生效」—— 所以 <c>Category = [Heavy]</c>
    /// 表达的是「**除了**重甲以外都生效」，正好是 3.5 的"无甲/轻甲/中甲"。
    /// <c>UntypedStackable</c> 则保证它与野蛮人快速移动等加值叠加。
    /// </summary>
    private static BlueprintFeature ConfigureFastMovement() {
        return FeatureConfigurator.New("WildrunnerFastMovement", FastMovementGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.FastMovement.Name", "快速移动"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.FastMovement.Description",
                "当你不穿甲、或只穿着轻甲或中甲时，你的陆地移动速度获得 +10 尺加值。\n"
                + "此加值与其他类似加值（例如野蛮人的快速移动）叠加。"))
            .SetIsClassFeature()
            .AddComponent(new SpeedBonusInArmorCategory {
                name = "$Wildrunner$FastMovement",
                Category = new[] { ArmorProficiencyGroup.Heavy },
                Bonus = 10,
                Descriptor = ModifierDescriptor.UntypedStackable,
                NoArmor = true,
            })
            .Configure();
    }

    // ==================== 2 级：灵敏嗅觉 ====================

    /// <summary>灵敏嗅觉：直接给原版 <c>RagePowers/ScentFeature</c>（野蛮人狂暴之力同款实现）。</summary>
    private static BlueprintFeature ConfigureScent() {
        return FeatureConfigurator.New("WildrunnerScent", ScentGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.Scent.Name", "灵敏嗅觉"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.Scent.Description",
                "你获得灵敏嗅觉：可以嗅到 30 尺内的生物（顺风 60 尺、逆风 15 尺）。"))
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(ScentSourceGuid),
            })
            .Configure();
    }

    // ==================== 5 级：视线躲藏 ====================

    /// <summary>视线躲藏：直接给原版游侠的 <c>Classes/Ranger/HideInPlainSight</c>。</summary>
    private static BlueprintFeature ConfigureHideInPlainSight() {
        return FeatureConfigurator.New("WildrunnerHideInPlainSight", HideInPlainSightGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.HideInPlainSight.Name", "视线躲藏"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.HideInPlainSight.Description",
                "你获得视线躲藏能力：即使正被人注视，也能使用潜行技能藏匿身形。"))
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(HideInPlainSightSourceGuid),
            })
            .Configure();
    }

    // ==================== 7 级：无拘之步 ====================

    /// <summary>
    /// 无拘之步：直接给原版德鲁伊的 <c>DruidWoodlandStride</c> —— 3.5 的
    /// "无视困难地形"与 PF 的 Woodland Stride 是同一能力。
    /// </summary>
    private static BlueprintFeature ConfigureWoodlandStride() {
        return FeatureConfigurator.New("WildrunnerWoodlandStride", WoodlandStrideGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.WoodlandStride.Name", "无拘之步"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.WoodlandStride.Description",
                "你可以无障碍地穿过困难地形，无视它对移动与技能检定造成的减值。"
                + "此能力对冰面、沼泽、碎石、灌木丛和自然的岩石表面起作用。"))
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                BlueprintTool.GetRef<BlueprintUnitFactReference>(WoodlandStrideSourceGuid),
            })
            .Configure();
    }

    // ==================== 9 级：精之心（保留 DR 部分）====================

    /// <summary>
    /// 精之心：3.5 原文是「种类变为精类，并获得 DR 2/寒铁」。
    /// 游戏没有生物类型概念，所以只保留 <b>DR 2/寒铁</b>。
    /// </summary>
    private static BlueprintFeature ConfigureFeyHeart() {
        return FeatureConfigurator.New("WildrunnerFeyHeart", FeyHeartGuid)
            .SetDisplayName(LocalizationTool.CreateString("Wildrunner.FeyHeart.Name", "精之心"))
            .SetDescription(LocalizationTool.CreateString("Wildrunner.FeyHeart.Description",
                "你的身心愈发接近精类，获得伤害减免 2/寒铁。"))
            .SetIsClassFeature()
            .AddComponent(new AddDamageResistancePhysical {
                name = "$Wildrunner$FeyHeartDR",
                Value = new Kingmaker.UnitLogic.Mechanics.ContextValue {
                    ValueType = Kingmaker.UnitLogic.Mechanics.ContextValueType.Simple,
                    Value = 2,
                },
                BypassedByMaterial = true,
                Material = Kingmaker.Enums.Damage.PhysicalDamageMaterial.ColdIron,
            })
            .Configure();
    }

    // ==================== 3 / 9 级：奖励专长 ====================

    /// <summary>
    /// 奖励专长：3 级与 9 级各一个，从普通专长池里选。
    /// （作为删掉「无踪步 / 忍受元素伤害 / 精之心的种类变化」的补偿，由作者指定。）
    /// </summary>
    private static BlueprintFeature ConfigureBonusFeat(string name, string guid) {
        return FeatureConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "奖励专长"))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "荒野行者获得一个奖励专长。"))
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { BasicFeatSelectionGuid })
            .Configure();
    }
}
