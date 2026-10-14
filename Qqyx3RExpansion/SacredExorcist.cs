using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Root;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.CasterCheckers;
using Kingmaker.UnitLogic.Abilities.Components.TargetCheckers;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 进阶职业「神圣驱魔师 Sacred Exorcist」（Complete Divine）。
///
/// 结构对照原版进阶职业（`Classes/Duelist/DuelistClass.jbp` 等）：
///   - 同一个 <c>BlueprintCharacterClass</c> 类型，靠 <c>PrestigeClass = true</c> 标记
///     （游戏在创建角色/招募时过滤掉它，只在升级时出现；且自动限 10 级）
///   - 进阶职业的豁免用**专门的 Prestige 进度**（每 2 级 +1 那套），BAB 用 3/4 进度
///   - 最后要把职业追加进 <c>BlueprintRoot.Progression.m_CharacterClasses</c>
///
/// 能力实现要点见各方法注释。
/// </summary>
internal static class SacredExorcist {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string ClassGuid = "8aff7a8aeb0f4dbc9969250a85ed8432";
    private const string ProgressionGuid = "f8edcf4a11604ee0a6bc33737d3fa426";
    private const string ProficienciesGuid = "e7ab43f2d3f54268a05b7ab7e085de1a";

    internal const string ChannelResourceGuid = "5759983697614fe88ca95d01ffc4db92";
    private const string ChannelHealGuid = "bd7d943ab64d4c6e9142cbc9ea5d340c";
    private const string ChannelHarmGuid = "f0c34f544e234f18b0116df8ae12bf93";
    private const string ChannelFeatureGuid = "70b88c5a2cc34225aff9d4f977d1c55b";

    private const string ResistPossessionGuid = "8db16135ee3b4a7bbb67b9d78d2827bc";

    private const string FoeSelectionGuid = "15709b67f60b4e3c96f7da68ee693c8b";
    private const string FoeUndeadGuid = "2ac284b246d94757bfd4dfd777ead328";
    private const string FoeOutsiderGuid = "baf1035287fb474e8b2965e18096fb1a";

    private const string SmiteResourceGuid = "a962e1f0d52c441ea3cff7a684c259ca";
    private const string SmiteAbilityGuid = "18da25d89f944a1393309ab3c6be1acf";
    private const string SmiteBuffGuid = "dcca80992e4140d4bf47029ad101a10a";
    private const string SmiteFeature4Guid = "23f080945a8c4b2190f15bb54276c1eb";
    private const string SmiteFeature7Guid = "757da46878894909b175b5abcdf33faf";
    private const string SmiteFeature10Guid = "6b32458bfea34d6d9c7f8a4f19505670";

    private const string BonusFeatSelectionGuid = "d275b6cad7614681a3c7a0bcf9d654b9";
    private const string BonusFeat8Guid = "b764ea76acca4a82b0baafdd55981a0b";

    // ---------- 原版参照物 GUID ----------

    /// <summary>3/4 BAB 进度（龙脉术士用的那条）</summary>
    private const string BabMediumGuid = "4c936de4249b61e419a3fb775b9f2581";
    /// <summary>进阶职业专用的低豁免进度（每 3 级 +1）</summary>
    private const string SavePrestigeLowGuid = "dc5257e1100ad0d48b8f3b9798421c72";
    /// <summary>进阶职业专用的高豁免进度（每 2 级 +1，从 +2 起）</summary>
    private const string SavePrestigeHighGuid = "1f309006cd2855e4e91a6c3707f3f700";

    /// <summary>Classes/Basic/Proficiencies/SimpleWeaponProficiency</summary>
    private const string SimpleWeaponProficiencyGuid = "e70ecf1ed95ca2f40b754f1adb22bbdd";

    /// <summary>Classes/Cleric/ChannelEnergyResource —— 牧师的引导能量次数</summary>
    private const string ClericChannelResourceGuid = "5e2bba3e07c37be42909a12945c27de7";
    /// <summary>Classes/Cleric/ChannelEnergy —— 牧师「引导能量（治疗）」</summary>
    private const string ClericChannelHealGuid = "f5fc9a1a2a3c1a946a31b320d1dd31b2";
    /// <summary>Classes/Cleric/ChannelPositiveHarm —— 牧师「引导正能量（伤害不死）」</summary>
    private const string ClericChannelHarmGuid = "279447a6bf2d3544d93a0a39c3b8e91d";
    /// <summary>Classes/Cleric/ChannelEnergyFact —— 授予引导能量资源</summary>
    private const string ClericChannelFactGuid = "93f062bc0bf70e84ebae436e325e30e8";

    /// <summary>Traits/Races/Common/UndeadType</summary>
    private const string UndeadTypeGuid = "734a29b693e9ec346ba2951b27987e33";
    /// <summary>Traits/Races/Common/OutsiderType</summary>
    private const string OutsiderTypeGuid = "9054d3988d491d944ac144e27b6bc318";

    /// <summary>超魔/法术专攻/知识专攻的现成选择池（奖励专长用）</summary>
    private const string BasicFeatGuid = "247a4068296e8be42890143f451b4b45";

    private const string ClassName = "神圣驱魔师";
    private const string ClassDescription =
        "神圣驱魔师通过仪式、驱魔舞蹈、击鼓与大量圣水来驱除邪恶的灵力，"
        + "避免其侵害人们的身体和心灵。他们大多是为这项工作受过教团专门训练的牧师或圣武士，"
        + "偶尔也有具备资格的法师。\n"
        + "生命骰：D8。本职技能：知识（神秘）、知识（位面）、知识（宗教）。技能点：2 + 智力调整值。\n"
        + "擅长所有简易武器，不擅长任何盔甲和盾牌。\n"
        + "每日法术：每获得一级神圣驱魔师等级，现有施法职业的每日法术数量与已知法术都提升 1 级"
        + "（不获得原职业的其它能力）。";

    internal static void Configure() {
        SacredExorcistSpellbooks.Configure();

        ConfigureProficiencies();

        var channelFeature = ConfigureChannel();
        var resist = ConfigureResistPossession();
        var foe = ConfigureChosenFoe();
        var smite4 = ConfigureSmiteEvil(4, SmiteFeature4Guid, 1);
        var smite7 = ConfigureSmiteEvil(7, SmiteFeature7Guid, 1);
        var smite10 = ConfigureSmiteEvil(10, SmiteFeature10Guid, 1);
        var bonusFeat5 = ConfigureBonusFeat("SacredExorcistBonusFeat5", BonusFeatSelectionGuid);
        var bonusFeat8 = ConfigureBonusFeat("SacredExorcistBonusFeat8", BonusFeat8Guid);

        // 每级都给一次「提升哪个法术书」的选择
        var spellbook = SacredExorcistSpellbooks.SelectionGuid;

        var progression = ProgressionConfigurator.New("SacredExorcistProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, ProficienciesGuid, channelFeature, spellbook)
            .AddToLevelEntry(2, resist, spellbook)
            .AddToLevelEntry(3, foe, spellbook)
            .AddToLevelEntry(4, smite4, spellbook)
            .AddToLevelEntry(5, bonusFeat5, spellbook)
            .AddToLevelEntry(6, foe, spellbook)
            .AddToLevelEntry(7, smite7, spellbook)
            .AddToLevelEntry(8, bonusFeat8, spellbook)
            .AddToLevelEntry(9, foe, spellbook)
            .AddToLevelEntry(10, smite10, spellbook)
            .SetRanks(1)
            .SetIsClassFeature(true)
            .SetDisplayName("")
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.Progression.Description", ClassDescription))
            .Configure();

        var characterClass = CharacterClassConfigurator.New("SacredExorcist", ClassGuid)
            .SetLocalizedName(LocalizationTool.CreateString("SacredExorcist.Name", ClassName))
            .SetLocalizedDescription(LocalizationTool.CreateString("SacredExorcist.Description", ClassDescription))
            .SetSkillPoints(2)
            .SetHitDie(DiceType.D8)
            // ★ 进阶职业标记：自动限 10 级，且创建角色/招募时会被过滤掉
            .SetPrestigeClass(true)
            .SetBaseAttackBonus(BabMediumGuid)
            .SetFortitudeSave(SavePrestigeLowGuid)
            .SetReflexSave(SavePrestigeLowGuid)
            .SetWillSave(SavePrestigeHighGuid)
            .SetProgression(progression)
            .SetClassSkills(new[] {
                // 游戏里没有「知识（位面）」技能，用「知识（世界）」代替（同为知识类）
                StatType.SkillKnowledgeArcana, StatType.SkillKnowledgeWorld, StatType.SkillLoreReligion,
            })
            // 前置：善良阵营（负能量牧师不可能是善良，天然排除）、知识（宗教）7 级
            .AddPrerequisiteAlignment(AlignmentMaskType.Good)
            .AddPrerequisiteStatValue(StatType.SkillLoreReligion, 7)
            // ⚠ 不要加「本职不能再选本职业」之类的前置：那会变成「已有 1 级就不许再升」，
            // 把角色永久卡在 1 级（本项目踩过）。游戏本身就不允许重复选同一个职业。
            .Configure();

        RegisterInCharacterCreation(characterClass);

        AdaptChannelBonuses();

        Main.Log?.Log($"[神圣驱魔师] 已注册（{ClassGuid}）");
    }

    /// <summary>
    /// 让「增加引导次数」的效果**同时加到第二个槽位**，从而实现用户要的「总次数翻倍」。
    ///
    /// 需要适配的来源：
    ///   - 原版 <c>Feats/ExtraChannel</c>（额外引导，+2 次）
    ///   - 本 mod 的 <c>ExtraTurning3R</c>（额外驱散 3r，+4 次）—— 它自己创建时就补了第二份
    ///
    /// 不需要适配的：装备上的 `PositiveChanneling1/2/4Feature` —— 名字有误导性，
    /// 它们的组件是 `IncreaseSpellDescriptorCasterLevel`（正能量**法术**的施法者等级），与引导次数无关。
    /// </summary>
    private static void AdaptChannelBonuses() {
        const string extraChannelGuid = "cd9f19775bd9d3343a31a065e93f0c47";

        FeatureConfigurator.For(BlueprintTool.GetRef<BlueprintFeatureReference>(extraChannelGuid))
            .AddComponent(new IncreaseResourceAmount {
                name = "$ExtraChannel$SacredExorcistSlot",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(ChannelResourceGuid),
                Value = 2,
            })
            .Configure();

        Main.Log?.Log("[神圣驱魔师] 已把「额外引导」的加成同时接到第二个引导槽位");
    }

    /// <summary>
    /// 把职业追加进 <c>BlueprintRoot.Progression.m_CharacterClasses</c>
    /// （进阶职业也放这个数组，靠 <c>PrestigeClass</c> 字段区分）。
    ///
    /// ⚠ 必须用 <c>Configure()</c> 的**返回值**，不能用 <c>BlueprintTool.Get&lt;T&gt;(guid)</c>：
    /// 在 <c>BlueprintsCache.Init</c> 期间，刚 new 出来的蓝图还没进游戏缓存，
    /// <c>BlueprintTool.Get</c> 会抛 "Failed to fetch blueprint"
    /// （本项目踩过：神圣驱魔师第一版因此完全没进菜单）。
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

        Main.Log?.Log("[神圣驱魔师] 已注册进升级菜单（ProgressionRoot.m_CharacterClasses）");
    }

    /// <summary>擅长所有简易武器（不擅长盔甲盾牌）。</summary>
    private static void ConfigureProficiencies() {
        FeatureConfigurator.New("SacredExorcistProficiencies", ProficienciesGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.Proficiencies.Name", "武器与护甲熟练"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.Proficiencies.Description",
                "神圣驱魔师擅长所有简易武器。她不擅长任何盔甲和盾牌。"))
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                SimpleWeaponProficiencyGuid,
            })
            .Configure();
    }

    // ==================== 1 级：引导能量槽位 ====================

    /// <summary>
    /// 1 级「引导能量槽位」：再给一份引导能量（第二个槽）。
    ///
    /// 用户的设计：神圣驱魔师拿到**第二个引导能量槽位**，任何"增加引导次数"的效果
    /// （额外引导 / 额外驱散 / 奇物）都会同时加到两个槽上 → 总次数翻倍。
    /// 所以这里新建一个与牧师引导资源**同公式**（3 + 魅力）的资源，
    /// 并把原版两个引导能力（治疗 / 伤害不死）复制一份改用新资源、等级按神圣驱魔师等级算。
    /// </summary>
    private static string ConfigureChannel() {
        // 资源：与 Classes/Cleric/ChannelEnergyResource 同公式（3 + 魅力调整值）
        AbilityResourceConfigurator.New("SacredExorcistChannelResource", ChannelResourceGuid)
            .SetMaxAmount(ResourceAmountBuilder.New(3).IncreaseByStat(StatType.Charisma))
            .Configure();

        ConfigureChannelAbility("SacredExorcistChannelEnergy", ChannelHealGuid,
            ClericChannelHealGuid, "引导能量", "神圣驱魔师可以像牧师一样引导正能量：治疗活物、伤害不死生物。");
        ConfigureChannelAbility("SacredExorcistChannelPositiveHarm", ChannelHarmGuid,
            ClericChannelHarmGuid, "引导正能量（伤害不死）", "神圣驱魔师以正能量冲击不死生物。");

        return FeatureConfigurator.New("SacredExorcistChannelFeature", ChannelFeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.Channel.Name", "引导能量槽位"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.Channel.Description",
                "神圣驱魔师获得第二个引导能量槽位：她可以像牧师一样引导正能量"
                + "（治疗活物、伤害不死生物），次数 = 3 + 魅力调整值。\n"
                + "任何「增加引导次数」的效果（例如额外引导、额外驱散、引导护符）"
                + "都会同时加到两个槽位上，使总次数翻倍。"))
            .SetIcon(BlueprintTool.Get<BlueprintAbility>(ClericChannelHealGuid).Icon)
            .SetIsClassFeature()
            .AddAbilityResources(resource: BlueprintTool.Get<BlueprintAbilityResource>(ChannelResourceGuid),
                restoreAmount: true)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { ChannelHealGuid, ChannelHarmGuid })
            .Configure()
            .AssetGuid.ToString();
    }

    /// <summary>
    /// 复制一份原版引导能量能力：除「资源消耗」与「等级计算」外，其它组件沿用原版
    /// （条件树很长，重建不现实），并把等级来源改成神圣驱魔师等级。
    /// </summary>
    private static void ConfigureChannelAbility(string name, string guid, string sourceGuid,
                                               string display, string description) {
        // ⚠ 用 GetRef（引用）而不是 Get（实例）：这些能力是在职业蓝图创建**之前**建的，
        // 那时 BlueprintTool.Get 会抛 "Failed to fetch blueprint"。
        var classRef = BlueprintTool.GetRef<BlueprintCharacterClassReference>(ClassGuid);

        AbilityConfigurator.New(name, guid)
            .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>(sourceGuid),
                c => !(c is AbilityResourceLogic) && !(c is ContextRankConfig))
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description", description))
            // 改用我们的第二个资源
            .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: ChannelResourceGuid)
            // 等级按神圣驱魔师等级（原版是按牧师等级）
            .AddComponent(new ContextRankConfig {
                name = "$" + name + "$Rank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.ClassLevel,
                m_Class = new[] { classRef },
                m_Progression = ContextRankProgression.AsIs,
                m_UseMin = true,
                m_Min = 0,
                m_UseMax = false,
                m_Max = 20,
            })
            .Configure();
    }

    // ==================== 2 级：抵抗附身 ====================

    /// <summary>2 级「抵抗附身」：对抗魅惑 / 胁迫法术或效果时豁免 +2 神圣加值。</summary>
    private static string ConfigureResistPossession() {
        return FeatureConfigurator.New("SacredExorcistResistPossession", ResistPossessionGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.ResistPossession.Name", "抵抗附身"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.ResistPossession.Description",
                "2 级以上的神圣驱魔师在对抗带有「魅惑」或「胁迫」描述符的法术与效果时，"
                + "豁免检定获得 +2 神圣加值。"))
            .SetIcon(BlueprintTool.Get<BlueprintFeature>("f6388946f9f472f4585591b80e9f2452").Icon)
            .SetIsClassFeature()
            .AddComponent(new SavingThrowBonusAgainstDescriptor {
                name = "$SacredExorcist$ResistPossession",
                Value = 2,
                ModifierDescriptor = ModifierDescriptor.Sacred,
                SpellDescriptor = Kingmaker.Blueprints.Classes.Spells.SpellDescriptor.Charm
                    | Kingmaker.Blueprints.Classes.Spells.SpellDescriptor.Compulsion,
            })
            .Configure()
            .AssetGuid.ToString();
    }

    // ==================== 3 / 6 / 9 级：仇敌 ====================

    /// <summary>
    /// 3 级「仇敌」：选择不死生物或异界生物；对抗仇敌的法术抗力检定 +1。
    /// 6 / 9 级各再给一次同一个选择池 —— 选项 <c>Ranks = 3</c>，加值用 Rank，所以再选一次就是 +2 / +3。
    /// </summary>
    private static string ConfigureChosenFoe() {
        ConfigureFoeOption("SacredExorcistChosenFoeUndead", FoeUndeadGuid, "仇敌：不死生物",
            "神圣驱魔师深入研究并专门训练对付不死生物的方法。"
            + "对抗不死生物的法术抗力进行施法者等级检定时获得 +1 能力加值（6 级 +2，9 级 +3）。",
            UndeadTypeGuid);
        ConfigureFoeOption("SacredExorcistChosenFoeOutsider", FoeOutsiderGuid, "仇敌：异界生物",
            "神圣驱魔师深入研究并专门训练对付异界生物的方法。"
            + "对抗异界生物的法术抗力进行施法者等级检定时获得 +1 能力加值（6 级 +2，9 级 +3）。",
            OutsiderTypeGuid);

        return FeatureSelectionConfigurator.New("SacredExorcistChosenFoe", FoeSelectionGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.ChosenFoe.Name", "仇敌"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.ChosenFoe.Description",
                "从 3 级开始，神圣驱魔师可以选择不死生物或异界生物作为自己的仇敌。"
                + "对抗仇敌的法术抗力进行施法者等级检定时具有 +1 能力加值；6 级时增加为 +2，9 级时增加为 +3。"))
            .SetIsClassFeature()
            .AddToAllFeatures(FoeUndeadGuid, FoeOutsiderGuid)
            .Configure()
            .AssetGuid.ToString();
    }

    private static void ConfigureFoeOption(string name, string guid, string display, string description,
                                           string typeFactGuid) {
        FeatureConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description", description))
            .SetIsClassFeature()
            // 可以选择 3 次（3 / 6 / 9 级各一次），加值取 Rank
            .SetRanks(3)
            .AddComponent(new SpellPenetrationBonusAgainstTargetFact {
                name = "$" + name + "$SpellPen",
                Value = new ContextValue {
                    ValueType = ContextValueType.Rank,
                    ValueRank = AbilityRankType.Default,
                },
                Descriptor = ModifierDescriptor.UntypedStackable,
                m_RequiredFact = BlueprintTool.GetRef<BlueprintUnitFactReference>(typeFactGuid),
            })
            .AddComponent(new ContextRankConfig {
                name = "$" + name + "$Rank",
                m_Type = AbilityRankType.Default,
                m_BaseValueType = ContextRankBaseValueType.FeatureRank,
                m_Progression = ContextRankProgression.AsIs,
            })
            .Configure();
    }

    // ==================== 4 / 7 / 10 级：反制邪恶 ====================

    /// <summary>
    /// 4 / 7 / 10 级「反制邪恶」：如同圣武士的制裁邪恶，但把神圣驱魔师等级视为圣武士等级；
    /// 每日 1 / 2 / 3 次。
    ///
    /// 做法照抄原版 <c>Classes/Paladin/SmiteEvil/*</c> 的组件组合：
    ///   - 能力：Conditional（非友军）→ 给目标挂 buff；两个 rank（魅力调整值 / 职业等级）
    ///   - buff：挂在**敌人**身上，<c>AttackBonusAgainstTarget</c> / <c>DamageBonusAgainstTarget</c>
    ///     在「施法者攻击该目标」时生效
    /// </summary>
    private static string ConfigureSmiteEvil(int level, string featureGuid, int addUses) {
        var classRef = BlueprintTool.GetRef<BlueprintCharacterClassReference>(ClassGuid);

        // buff / 资源 / 能力都只在 4 级那一次创建；7 / 10 级只加 feature。
        // ⚠ 不能每次都建：同一个 blueprint 名 + guid 建第二次会抛 "Duplicate GuidByName"。
        if (level == 4) {
        // buff：挂在敌人身上
        BuffConfigurator.New("SacredExorcistSmiteEvilBuff", SmiteBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.SmiteEvil.Buff.Name", "反制邪恶"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.SmiteEvil.Buff.Description",
                "该目标被神圣驱魔师反制：神圣驱魔师对其攻击检定获得魅力加值、伤害获得职业等级加值。"))
            .SetIcon(BlueprintTool.Get<BlueprintBuff>(
                "b6570b8cbb32eaf4ca8255d0ec3310b0").Icon)
            .AddComponent(new AttackBonusAgainstTarget {
                name = "$SacredExorcistSmite$Attack",
                Value = new ContextValue { ValueType = ContextValueType.Rank, ValueRank = AbilityRankType.Default },
                CheckCaster = true,
                Descriptor = ModifierDescriptor.Sacred,
            })
            .AddComponent(new DamageBonusAgainstTarget {
                name = "$SacredExorcistSmite$Damage",
                Value = new ContextValue { ValueType = ContextValueType.Rank, ValueRank = AbilityRankType.DamageBonus },
                CheckCaster = true,
            })
            .AddComponent(new UniqueBuff { name = "$SacredExorcistSmite$Unique" })
            .Configure();

        // 资源：1 次起
            AbilityResourceConfigurator.New("SacredExorcistSmiteEvilResource", SmiteResourceGuid)
                .SetMaxAmount(ResourceAmountBuilder.New(1))
                .Configure();
        }

        // 能力
        if (level == 4) {
            AbilityConfigurator.New("SacredExorcistSmiteEvil", SmiteAbilityGuid)
                .CopyFrom(BlueprintTool.GetRef<BlueprintAbilityReference>("7bb9eb2042e67bf489ccd1374423cdec"),
                    c => false)
                .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.SmiteEvil.Name", "反制邪恶"))
                .SetDescription(LocalizationTool.CreateString("SacredExorcist.SmiteEvil.Description",
                    "神圣驱魔师得到能力「反制邪恶」，如同圣武士的制裁邪恶一般生效，"
                    + "并且把自己的神圣驱魔师等级视为圣武士等级。每日可使用 1 次（7 级 2 次，10 级 3 次）。"))
                .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: SmiteResourceGuid)
                .AddComponent(new AbilityEffectRunAction {
                    name = "$SacredExorcistSmite$Effect",
                    SavingThrowType = SavingThrowType.Unknown,
                    Actions = new ActionList {
                        Actions = new GameAction[] {
                            new ContextActionApplyBuff {
                                name = "$SacredExorcistSmite$ApplyBuff",
                                m_Buff = BlueprintTool.Get<BlueprintBuff>(SmiteBuffGuid)
                                    .ToReference<BlueprintBuffReference>(),
                                Permanent = true,
                                UseDurationSeconds = false,
                                ToCaster = false,
                                AsChild = false,
                                SameDuration = false,
                            },
                        },
                    },
                })
                // 魅力调整值（命中加值）
                .AddComponent(new ContextRankConfig {
                    name = "$SacredExorcistSmite$RankCha",
                    m_Type = AbilityRankType.Default,
                    m_BaseValueType = ContextRankBaseValueType.StatBonus,
                    m_Stat = StatType.Charisma,
                    m_Progression = ContextRankProgression.AsIs,
                    m_UseMin = true, m_Min = 0,
                    m_UseMax = false, m_Max = 20,
                })
                // 职业等级（伤害加值）—— 替代原版的圣武士等级
                .AddComponent(new ContextRankConfig {
                    name = "$SacredExorcistSmite$RankLevel",
                    m_Type = AbilityRankType.DamageBonus,
                    m_BaseValueType = ContextRankBaseValueType.OwnerSummClassLevelWithArchetype,
                    m_Class = new[] { classRef },
                    m_Progression = ContextRankProgression.AsIs,
                    m_DisableRankBonus = true,
                })
                .AddComponent(new AbilityCasterAlignment {
                    name = "$SacredExorcistSmite$Align",
                    Alignment = AlignmentMaskType.Good,
                })
                .AddComponent(new AbilityTargetIsAlly {
                    name = "$SacredExorcistSmite$NotAlly",
                    Not = true,
                })
                .Configure();
        }

        // 各等级的次数提升（7 / 10 级各 +1）
        var feature = FeatureConfigurator.New("SacredExorcistSmiteEvil" + level, featureGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcist.SmiteEvil" + level + ".Name",
                "反制邪恶（每日 " + (level == 4 ? 1 : level == 7 ? 2 : 3) + " 次）"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcist.SmiteEvil" + level + ".Description",
                "反制邪恶的每日使用次数增加到 " + (level == 4 ? 1 : level == 7 ? 2 : 3) + " 次。"))
            // 图标用原版制裁邪恶的（我们自己刚建的能力此时还取不到，不用 BlueprintTool.Get）
            .SetIcon(BlueprintTool.Get<BlueprintAbility>("7bb9eb2042e67bf489ccd1374423cdec").Icon)
            .SetIsClassFeature()
            .AddComponent(new IncreaseResourceAmount {
                name = "$SacredExorcistSmite" + level + "$Increase",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(SmiteResourceGuid),
                Value = addUses,
            });
        if (level == 4) feature = feature.AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { SmiteAbilityGuid });

        return feature.Configure().AssetGuid.ToString();
    }

    // ==================== 5 / 8 级：奖励专长 ====================

    /// <summary>
    /// 5 / 8 级奖励专长：**只**能选超魔专长、法术专攻专长、知识专攻专长（与描述一致）。
    ///
    /// 选项必须是**平铺的固定清单**：游戏的选择池 `m_AllFeatures` 是静态数组，
    /// 没有"运行时按类别过滤"的机制（这也是为什么原版博学者的法术书要逐个列）。
    ///
    /// ⚠ 不要拿「整个普通专长池」当唯一选项：那样玩家能选到任何专长，与描述不符。
    /// </summary>
    private static readonly string[] BonusFeatGuids = {
        // ---- 超魔专长（原版 11 + 本 mod 1）----
        "fbf5d9ce931f47f3a0c818b3f8ef8414",   // 法术增效 Bolstered
        "a1de1e4f92195b442adb946f0e2b9d4e",   // 法术强效 Empower
        "f180e72e4a9cbaa4da8be9bc958132ef",   // 法术延时 Extend
        "7f2b282626862e345935bbea5e66424b",   // 法术极效 Maximize
        "cd26b9fa3f734461a0fcedc81cafaaac",   // 法术顽强 Persistent
        "ef7ece7bb5bb66a41b256976b27f424e",   // 法术瞬发 Quicken
        "46fad72f54a33dc4692d3b62eca7bb78",   // 法术及远 Reach
        "85f3340093d144dd944fff9a9adfd2f2",   // 法术甄选 Selective
        "8ad7fd39abea4722b39eb5a67d606a41",   // 法术加剧 Intensified（DLC6）
        "2f5d1e705c7967546b72ad8218ccf99c",   // 法术升阶 Heighten
        "094b6278f7b570f42aeaa98379f07cf2",   // 超正常法术 CompletelyNormal
        "09d474d0109249e9b0f5bcbaba4319de",   // 超魔（法术持久）—— 本 mod
        // ---- 法术专攻（8 学派）----
        "71a3f1c1ac77ae3488b9b3d6d2aac01a",   // 防护
        "d342cc595f499434687f9765f56d525c",   // 咒法
        "955e97411611d384db2cbc00d7ed5ead",   // 预言
        "c5bf645f128c39b40850cde005b8538f",   // 附魔
        "743d0106ee3076342839c6d550cdda25",   // 塑能
        "e588279a80eb7a24b813fadad4bc83b5",   // 幻术
        "8791da25011fd1844ad61a3fea6ece54",   // 死灵
        "49907a2e51b49d641aad3c9781a3a698",   // 变化
        // ---- 元素专攻（4，属"法术专攻"一类）----
        "52135eada006e9045a848cd659749608",   // 酸
        "2ed9d8bf76412ba4a8afe38fa9925fca",   // 冷
        "d439691f37d17804890bd9c263ae1e80",   // 电
        "13bdf8d542811ac4ca228a53aa108145",   // 火
        // ---- 知识专攻（4）----
        "cad1b9175e8c0e64583432a22134d33c",   // 知识（神秘）
        "611e863120c0f9a4cab2d099f1eb20b4",   // 知识（世界）
        "6507d2da389ed55448e0e1e5b871c013",   // 学识（自然）
        "c541f80af8d0af4498e1abb6025780c7",   // 学识（宗教）
    };

    private static string ConfigureBonusFeat(string name, string guid) {
        var options = new List<Blueprint<BlueprintFeatureReference>>();
        foreach (var featGuid in BonusFeatGuids) {
            options.Add(BlueprintTool.GetRef<BlueprintFeatureReference>(featGuid));
        }

        return FeatureSelectionConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "奖励专长"))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "神圣驱魔师可以选择任意的超魔专长、法术专攻专长和知识专攻专长作为奖励专长。"))
            .SetIsClassFeature()
            .AddToAllFeatures(options.ToArray())
            .Configure()
            .AssetGuid.ToString();
    }
}
