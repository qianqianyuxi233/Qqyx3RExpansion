using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Classes.Spells;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Spells;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Mechanics.Properties;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则职业「卷宗学者」（Archivist，《Heroes of Horror》）。
///
/// 本文件目前只搭**骨架**：职业本体 + 等级进度 + 施法能力 + 熟练，全部职业能力
/// （黑暗学识、学识掌握、心如止水、奖励专长…）尚未实现。
///
/// 职业数据（用户提供的职业表）：
///   生命骰 D6 / 技能点 4+智力 / BAB 1/2（L20 = +10/+5）
///   豁免：强韧高、反射低、意志高
///   本职技能：知识（神秘、世界、宗教、自然）、察觉、沟通
///   熟练：所有简易武器、轻甲、中甲（**不熟练盾牌**）
///   施法：**神术**、**智力**施法属性、**准备式（记忆）**、法术来自牧师法术列表、
///         可抄录卷轴（对应「祈祷书 / Prayerbook」与「抄写卷轴」设定）
///
/// 每日法术位表是本职业自建的（与牧师表不同：卷宗学者 1 级就有 0 环 3 / 1 环 2）。
///
/// 参考实现：
///   基础职业骨架 —— Mods_backup_...\Prestige-Plus-main\Blueprint\Gunslinger\GunslingerMain.cs
///   施法职业     —— 同目录 Blueprint\PrestigeClass\ExaltedEvangelist.cs（法术书 + 法术位表）
/// </summary>
internal static class Archivist {

    // ---------- 原版引用 ----------

    /// <summary>Classes/Cleric/ClericSpellList —— 卷宗学者用法术来自牧师法术列表</summary>
    private const string ClericSpellListGuid = "8443ce803d2d31347897a3d85cc32f53";

    /// <summary>Classes/Cleric/ClericClass —— 借用它的外观实体与 UI 配色（原版职业都有这些字段）</summary>
    private const string ClericClassGuid = "67819271767a9dd4fbfd4ae700befea0";

    /// <summary>Classes/Cleric/ClericProficiencies —— 借用它的图标作为本职业「熟练」能力的图标</summary>
    private const string ClericProficienciesGuid = "8c971173613282844888dc20d572cfc9";

    /// <summary>BAB 1/2 进度（StatProgressionRefs.BABLow）</summary>
    private const string BabLowGuid = "0538081888b2d8c41893d25d098dee99";
    /// <summary>高豁免进度（StatProgressionRefs.SavesHigh）</summary>
    private const string SavesHighGuid = "ff4662bde9e75f145853417313842751";
    /// <summary>低豁免进度（StatProgressionRefs.SavesLow）</summary>
    private const string SavesLowGuid = "dc0c7c1aba755c54f96c089cdf7d14a3";

    /// <summary>BardLoreMaster —— 图标来源（学识主题）</summary>
    private const string IconSourceGuid = "4bea694e79a87cd4d8c14fb91578059e";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string ClassGuid = "5316ca03ae994323adb0996bc4d0f981";
    private const string ProgressionGuid = "f229665d302446f3af213619fe78f04c";
    internal const string SpellbookGuid = "7d5393e63b504cfab67f56f4a02b02df";
    private const string SpellsTableGuid = "a905a7c398604a3a92bf2cfb7a397eb9";
    private const string SpellcastingGuid = "1615de63587a473c88a69f3d1013ea6f";
    private const string ProficienciesGuid = "b426262bb8e84f42ae9bcfff6e88268a";

    /// <summary>
    /// 本 mod 新建的「卷宗表」——所有神术职业法术表的并集。
    /// 用它当卷宗学者的法术表，有三个好处：
    ///   1. 升级时「可学」范围直接覆盖全部神术；
    ///   2. CopyScroll 的 `SpellList.Contains(spell)` 会通过，所以**任何神术卷轴都能抄**；
    ///   3. 不必动 Harmony。
    /// </summary>
    private const string SpellListGuid = "ae17c73ac91d413a84cb8f98b336bb44";

    /// <summary>
    /// 「卷宗表」的来源：所有神术职业的法术表。
    /// 合并时同一法术取**最低环**（例如治疗轻伤在牧师表是 1 环、游侠表是 2 环 → 收在 1 环）。
    /// 神话道途法术表（天使/御衡者/阿兹拉塔/恶魔）与领域法术表暂不纳入。
    /// </summary>
    private static readonly string[] DivineSpellListGuids = {
        ClericSpellListGuid,                  // ClericSpellList（牧师 / 先知）
        "bad8638d40639d04fa2f80a1cac67d6b",  // DruidSpellList（德鲁伊）
        "c0c40e42f07ff104fa85492da464ac69",  // ShamanSpelllist（萨满）
        "57c894665b7895c499b3dce058c284b3",  // InquisitorSpellList（审判者）
        "c5a1b8df32914d74c9b44052ba3e686a",  // WarpriestSpellList（战斗祭司）
        "d090b791bfe381740b98ed4ff909b1cf",  // HunterSpellList（猎人）
        "9f5be2f7ea64fe04eb40878347b147bc",  // PaladinSpellList（圣武士）
        "29f3c338532390546bc5347826a655c4",  // RangerSpellList（游侠）
    };

    /// <summary>
    /// 领域法术表（37 张，含子域）。是否并入「卷宗表」由 UMM 选项
    /// <see cref="Settings.AllowDomainSpells"/> 控制，默认开启。
    /// </summary>
    private static readonly string[] DomainSpellListGuids = {
        "9678d121f669f864d9da5dabf1ca1ce0",  // Air
        "d0ccda70fddc0e346a227068502771c1",  // Animal
        "5ef652f325d21414d8565fcfb19d8177",  // Artifice
        "2ff9831eb262758449287f820108428d",  // Chaos
        "31c742d02fc33204cad4e02dddf028dd",  // Charm
        "75576ed8cab010644a11f9ecd512a7f9",  // Community
        "fa79e8d1fe20b0e43bf3ebca4cef93b9",  // Darkness
        "436986e90d1e81b45a1accb6fa7261f0",  // Death
        "6f9fe425726026d4f9c28c32d5d03481",  // Destruction
        "df3bc5bda7deb9d46b0f177db3bb7876",  // Earth
        "81bff1165d9468a44b2f815f7c26a373",  // Evil
        "d8f30625d1b1f9d41a24446cbf7ac52e",  // Fire
        "7b3506924ed8354419b7829736ab2c7e",  // Glory
        "dc242eb60eed94a4eb0640d773780090",  // Good
        "033b2b6a8899be844ae8aa91d4dab477",  // Healing
        "c341ec569c6140bf9ba48f3335fe59f6",  // Ice（子域）
        "384627980c2a60a43800f14029fbb8a7",  // Knowledge
        "57b0bbdc1114ee846945f1808b13cff7",  // Law
        "73406d3b9f6290e499c5fa3951a24234",  // Liberation
        "9e756552e9b05ce459feac658dd2d8fb",  // Luck
        "1d1638d47e7f8404baeed23bc35ec2f2",  // Madness
        "f997258a28a9e5f4192d973433edec5f",  // Magic
        "b5eadf57c3c34f2ab656d14923089d0d",  // Murder（子域）
        "3de1e283971828f4896a4140acd3c84c",  // Nobility
        "bd7b088a54b79434f90ed53551ca2189",  // Plant
        "93228f4df23d2d448a0db59141af8aed",  // Protection
        "5376474a39713514ca2135d6f9584563",  // Repose
        "30076fe3d5f4ef845a7bafb0be57fe44",  // Rune
        "2af2f5129c984dcfbe161934a5b3dc58",  // Scalykind
        "03db76fd27428004482f9314c334d1ab",  // Strength
        "600ffed45d0c3ec43a75dc76bb9377b6",  // Sun
        "ab90308db82342f47bf0d636fe941434",  // Travel
        "2c09ae283ea3e41408665c46fcf0303f",  // Trickery
        "90a101834cff40f09069d5a8c34c96ad",  // Undead（子域）
        "e3c54da90a2b54b4a975a80b5d39c361",  // War
        "35e15cd1b353e2d47b507c445d2f8c6f",  // Water
        "eba577470b8ee8443bb4552433451990",  // Weather
    };

    /// <summary>
    /// 神术系神话道途的法术表（天使 / 御衡者 / 阿兹拉塔 / 恶魔，各含主表与神话表）。
    /// 奥术系道途（巫妖 / 诡术师 / 金龙）**不在**这里。
    /// 由 UMM 选项 <see cref="Settings.AllowMythicDivineSpells"/> 控制，默认关闭。
    /// </summary>
    private static readonly string[] MythicDivineSpellListGuids = {
        "c074062863fbc1e4bab02f9e6e4eb78d",  // AngelClericSpelllist（天使 主表）
        "deaffb4218ccf2f419ffd6e41603131a",  // AngelMythicSpelllist（天使 神话）
        "24b0c796f723a144e9891b6c4794c595",  // AeonSpellList（御衡者 主表）
        "ca8c6024bd2519f4b97162a3ad286920",  // AeonSpellMythicList（御衡者 神话）
        "db216faa0ff2b984399e7495755c7111",  // AzataMythicSpelllist（阿兹拉塔 主表）
        "10c634d2b386d8d41b18a889adb8cd49",  // AzataMythicSpellsSpelllist（阿兹拉塔 神话）
        "78721d556676f264da947bfe263b1da0",  // DemonUsualSpelllist（恶魔 主表）
        "abb1991bf6e996348bb743471ee7e1c1",  // DemonSpelllist（恶魔）
    };

    private const string ClassName = "卷宗学者";
    private const string ClassDescription =
        "卷宗学者是追寻禁忌知识的学者：他不靠信仰，而是像法师研究魔法那样研读、抄录神术。"
        + "他以智力施展神术，法术主要来自牧师法术列表，并把它们记录在自己的祈祷书中。"
        + "他还能把神术卷轴抄入祈祷书，从而掌握他人无法企及的神术。";

    /// <summary>
    /// 每日法术位，索引 = 法术环（0 环起）。第 1..20 项对应角色 1..20 级。
    /// 数值来自用户提供的卷宗学者职业表，**但 0 环按 WOTR 本土规则改为 0（无限施放）**：
    /// 游戏里法师（Cantrips）与牧师（Orisions）的 0 环数量都是 0，表示不占法术位、可无限施放。
    /// </summary>
    private static readonly int[][] SpellsPerDayByLevel = {
        new[] { 0, 2 },                          //  1
        new[] { 0, 3 },                          //  2
        new[] { 0, 3, 2 },                       //  3
        new[] { 0, 4, 3 },                       //  4
        new[] { 0, 4, 3, 2 },                    //  5
        new[] { 0, 4, 4, 3 },                    //  6
        new[] { 0, 5, 4, 3, 2 },                 //  7
        new[] { 0, 5, 4, 4, 3 },                 //  8
        new[] { 0, 5, 5, 4, 3, 2 },              //  9
        new[] { 0, 5, 5, 4, 4, 3 },              // 10
        new[] { 0, 5, 5, 5, 4, 3, 2 },           // 11
        new[] { 0, 5, 5, 5, 4, 4, 3 },           // 12
        new[] { 0, 5, 5, 5, 5, 4, 3, 2 },        // 13
        new[] { 0, 5, 5, 5, 5, 4, 4, 3 },        // 14
        new[] { 0, 5, 5, 5, 5, 5, 4, 3, 2 },     // 15
        new[] { 0, 5, 5, 5, 5, 5, 4, 4, 3 },     // 16
        new[] { 0, 5, 5, 5, 5, 5, 5, 4, 3, 2 },  // 17
        new[] { 0, 5, 5, 5, 5, 5, 5, 4, 4, 3 },  // 18
        new[] { 0, 5, 5, 5, 5, 5, 5, 5, 4, 4 },  // 19
        new[] { 0, 5, 5, 5, 5, 5, 5, 5, 5, 5 },  // 20
    };

    internal static void Configure() {
        // 顺序有讲究：卷宗表要先建出来，法术书引用它
        var spellList = ConfigureSpellList();
        var spellsTable = ConfigureSpellsTable();
        var spellbook = ConfigureSpellbook(spellsTable);
        var proficiencies = ConfigureProficiencies();

        // 职业能力
        var loreMastery = ArchivistFeatures.ConfigureLoreMastery();
        var stillMind = ArchivistFeatures.ConfigureStillMind();
        var bonusFeat = ArchivistFeatures.ConfigureBonusFeat();

        // 黑暗学识体系（只对恶魔生效，详见 DarkKnowledge.cs）
        DarkKnowledge.Configure();

        var progression = ConfigureProgression(
            proficiencies, loreMastery, stillMind, bonusFeat);
        var charClass = ConfigureClass(progression, spellbook);
        RegisterInCharacterCreation(charClass);
    }

    // ---------- 0. 卷宗表：所有神术职业法术表的并集 ----------

    private static BlueprintSpellList ConfigureSpellList() {
        var byLevel = new List<BlueprintAbilityReference>[10];
        for (int lvl = 0; lvl <= 9; lvl++) {
            byLevel[lvl] = new List<BlueprintAbilityReference>();
        }

        // 来源 = 8 张基础神术职业表 + 按 UMM 选项追加的领域表 / 神术系神话表
        var sources = new List<string>(DivineSpellListGuids);
        bool withDomain = Main.ModSettings == null || Main.ModSettings.AllowDomainSpells;
        bool withMythic = Main.ModSettings != null && Main.ModSettings.AllowMythicDivineSpells;
        if (withDomain) sources.AddRange(DomainSpellListGuids);
        if (withMythic) sources.AddRange(MythicDivineSpellListGuids);

        var seen = new HashSet<string>();

        // 外层按法术环、内层按来源表遍历 —— 这样同一个法术会被收在它出现的**最低环**。
        for (int lvl = 0; lvl <= 9; lvl++) {
            foreach (var listGuid in sources) {
                try {
                    var source = BlueprintTool.Get<BlueprintSpellList>(listGuid);
                    if (source?.SpellsByLevel == null) continue;

                    foreach (var level in source.SpellsByLevel) {
                        if (level == null || level.SpellLevel != lvl) continue;

                        foreach (var spell in level.SpellsRefs) {
                            if (spell == null) continue;
                            if (seen.Add(spell.ToString())) {
                                byLevel[lvl].Add(spell);
                            }
                        }
                    }
                } catch (Exception e) {
                    Main.Log?.Log($"[卷宗学者] 读取法术表 {listGuid} 失败：{e.Message}");
                }
            }
        }

        var spellsByLevel = new SpellLevelList[10];
        int total = 0;
        for (int lvl = 0; lvl <= 9; lvl++) {
            var entry = new SpellLevelList(lvl);
            foreach (var spell in byLevel[lvl]) {
                entry.SpellsRefs.Add(spell);
            }
            spellsByLevel[lvl] = entry;
            total += byLevel[lvl].Count;
        }

        Main.Log?.Log($"[卷宗学者] 卷宗表合并完成：{total} 个法术，来自 {sources.Count} 张法术表"
            + $"（基础神术 {DivineSpellListGuids.Length}"
            + (withDomain ? $" + 领域 {DomainSpellListGuids.Length}" : "（未含领域）")
            + (withMythic ? $" + 神话神术 {MythicDivineSpellListGuids.Length}" : "（未含神话）")
            + "）");

        return SpellListConfigurator.New("ArchivistSpellList", SpellListGuid)
            .OnConfigure(bp => { bp.SpellsByLevel = spellsByLevel; })
            // 注意：这里**不能**用 Configure(delayed: true)。
            // BlueprintCore 的延迟队列由它自己的 patch 在蓝图加载后 flush（默认优先级），
            // 而本 mod 的 Init_Postfix 是 Priority.Last，晚于它 —— 排进去就没人执行了。
            .Configure();
    }

    // ---------- 1. 每日法术位表 ----------

    private static BlueprintSpellsTable ConfigureSpellsTable() {
        // Levels[0] = 0 级角色，Levels[n] = n 级；游戏支持到 40 级（Legend 道途），
        // 21 级以后沿用 20 级的数值。
        var entries = new List<SpellsLevelEntry>();
        for (int charLevel = 0; charLevel <= 40; charLevel++) {
            int[] counts;
            if (charLevel == 0) {
                counts = new int[0];
            } else if (charLevel <= SpellsPerDayByLevel.Length) {
                counts = SpellsPerDayByLevel[charLevel - 1];
            } else {
                counts = SpellsPerDayByLevel[SpellsPerDayByLevel.Length - 1];
            }
            entries.Add(new SpellsLevelEntry { Count = counts });
        }

        return SpellsTableConfigurator.New("ArchivistSpellsTable", SpellsTableGuid)
            .SetLevels(entries.ToArray())
            .Configure();
    }

    // ---------- 2. 法术书 ----------

    private static BlueprintSpellbook ConfigureSpellbook(BlueprintSpellsTable spellsTable) {
        return SpellbookConfigurator.New("ArchivistSpellbook", SpellbookGuid)
            .SetName(LocalizationTool.CreateString("Archivist.Name", ClassName))
            .SetSpellsPerDay(spellsTable)
            .SetSpellList(SpellListGuid)                  // 用本 mod 的「卷宗表」（全部神术的并集）
            .SetCharacterClass(ClassGuid)
            .SetCastingAttribute(StatType.Intelligence)   // 智力施法（不是感知）
            // 下面三项照抄法师的法术书（WizardSpellbook.jbp: AllSpellsKnown=False /
            // CanCopyScrolls=True / Cantrips=Cantrips），而不是牧师的
            // （ClericSpellbook.jbp: AllSpellsKnown=True / CanCopyScrolls=False / Orisions）——
            // 这样才能体现「祈祷书 + 抄录卷轴 + 必须提前准备」的设定。
            .SetAllSpellsKnown(false)                     // 只有学过的法术才能准备
            .SetSpontaneous(false)                        // 准备式（记忆）施法
            .SetIsArcane(false)                           // 神术
            .SetCantripsType(CantripsType.Cantrips)       // 0 环有限（与职业表的 3/4/4… 一致）
            .OnConfigure(bp => {
                // 抄录卷轴：把神术卷轴抄进祈祷书
                bp.CanCopyScrolls = true;
                // 「可知法术」：法师是每级自动学 2 个（WizardSpellbook 的 SpellsPerLevel = 2），
                // 没有 SpellsKnown 表（那是术士那种递增曲线才用）。
                // 抄录卷轴是在此之外额外增加可知。
                bp.SpellsPerLevel = 2;
            })
            .Configure();   // 同步执行（不要 delayed，原因见 ConfigureSpellList 的注释）
    }

    // 注：**不需要**单独做一个「施法」能力。
    // 原版牧师的 1 级进度里就没有施法能力（只有 ClericProficiencies、ClericOrisonsFeature、
    // ChannelEnergySelection），施法是靠职业本体的 m_Spellbook 直接生效的（见 ConfigureClass）。
    // 把它做成能力反而会在职业面板里多出一项，与所有原版施法职业都不一致。

    // ---------- 4. 武器与护甲熟练（1 级） ----------

    private static BlueprintFeature ConfigureProficiencies() {
        return FeatureConfigurator.New("ArchivistProficiencies", ProficienciesGuid)
            .SetDisplayName(LocalizationTool.CreateString("ArchivistProficiencies.Name", "武器与护甲熟练"))
            .SetDescription(LocalizationTool.CreateString("ArchivistProficiencies.Description",
                "卷宗学者熟练所有简易武器、轻甲和中甲，但不熟练盾牌。"))
            // 图标照抄牧师的「熟练」能力
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(ClericProficienciesGuid).Icon)
            .SetIsClassFeature(true)
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> {
                // 所有简易武器 + 轻甲 + 中甲；**不熟练盾牌**
                // 注意这里要传 guid 字符串（Blueprint<T> 有 string 隐式转换），
                // 直接传 FeatureRefs.X 会因泛型类型不匹配编译失败。
                FeatureRefs.SimpleWeaponProficiency.ToString(),
                FeatureRefs.LightArmorProficiency.ToString(),
                FeatureRefs.MediumArmorProficiency.ToString(),
            })
            .Configure();
    }

    // ---------- 5. 等级进度 ----------

    private static BlueprintProgression ConfigureProgression(
            BlueprintFeature proficiencies,
            BlueprintFeatureSelection loreMastery,
            BlueprintFeature stillMind,
            BlueprintFeatureSelection bonusFeat) {
        // 按 3.5 职业表逐级给能力。
        // 「黑暗学识」体系（1/3/5/6/8/9/11/12/14/15/18 级）尚未实现，另行设计。
        return ProgressionConfigurator.New("ArchivistProgression", ProgressionGuid)
            .SetClasses(ClassGuid)
            .AddToLevelEntry(1, proficiencies, DarkKnowledge.TacticsFeature)
            .AddToLevelEntry(2, loreMastery)                   // 学识掌握（第一次）
            .AddToLevelEntry(3, DarkKnowledge.TimesFeatures[0])   // 黑暗学识 4 次/天
            .AddToLevelEntry(4, stillMind)                     // 心如止水
            .AddToLevelEntry(5, DarkKnowledge.PuissanceFeature)   // 黑暗学识：影响
            .AddToLevelEntry(6, DarkKnowledge.TimesFeatures[1])   // 5 次/天
            .AddToLevelEntry(7, loreMastery)                   // 学识掌握
            .AddToLevelEntry(8, DarkKnowledge.FoeFeature)         // 黑暗学识：对手
            .AddToLevelEntry(9, DarkKnowledge.TimesFeatures[2])   // 6 次/天
            .AddToLevelEntry(10, bonusFeat)                    // 奖励专长
            .AddToLevelEntry(12, DarkKnowledge.TimesFeatures[3])  // 7 次/天
            .AddToLevelEntry(13, loreMastery)                  // 学识掌握
            .AddToLevelEntry(14, DarkKnowledge.ForeknowledgeFeature)  // 黑暗学识：预知
            .AddToLevelEntry(15, DarkKnowledge.TimesFeatures[4])  // 8 次/天
            .AddToLevelEntry(17, loreMastery)                  // 学识掌握
            .AddToLevelEntry(18, DarkKnowledge.TimesFeatures[5])  // 9 次/天
            .AddToLevelEntry(20, bonusFeat)                    // 奖励专长
            .SetIsClassFeature(true)
            .SetRanks(1)
            .SetDisplayName("")
            .SetDescription(LocalizationTool.CreateString("Archivist.Description", ClassDescription))
            .Configure();
    }

    // ---------- 6. 职业本体 ----------

    private static BlueprintCharacterClass ConfigureClass(
            BlueprintProgression progression, BlueprintSpellbook spellbook) {
        return CharacterClassConfigurator.New("Archivist", ClassGuid)
            // 本地化：key 必须用 CreateString 绑定文本，否则界面显示 [unknown key: …]
            .SetLocalizedName(LocalizationTool.CreateString("Archivist.Name", ClassName))
            .SetLocalizedDescription(LocalizationTool.CreateString("Archivist.Description", ClassDescription))
            .SetLocalizedDescriptionShort(LocalizationTool.CreateString("Archivist.DescriptionShort", ClassDescription))
            .SetSkillPoints(4)
            .SetHitDie(DiceType.D6)
            .SetPrestigeClass(false)
            // 职业表：BAB 1/2；强韧高、反射低、意志高
            .SetBaseAttackBonus(BabLowGuid)
            .SetFortitudeSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetReflexSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesLowGuid))
            .SetWillSave(BlueprintTool.GetRef<BlueprintStatProgressionReference>(SavesHighGuid))
            .SetProgression(progression)
            .SetClassSkills(new StatType[] {
                StatType.SkillKnowledgeArcana,     // 知识（神秘）
                StatType.SkillKnowledgeWorld,      // 知识（世界）
                StatType.SkillLoreReligion,        // 知识（宗教）
                StatType.SkillLoreNature,          // 知识（自然）
                StatType.SkillPerception,          // 察觉
                StatType.SkillPersuasion,          // 沟通
            })
            .SetRecommendedAttributes(StatType.Intelligence, StatType.Wisdom, StatType.Constitution)
            .SetDifficulty(1)
            .SetStartingGold(105)
            // 职业本体**不设图标**：原版职业在角色创建列表里并不显示图标
            // （野蛮人的 m_Icon 就是 null），设了反而突兀。
            // 核心属性是智力（DC 与奖励法术都由它决定）→ 放进推荐；力量/魅力用不到
            .SetNotRecommendedAttributes(StatType.Strength, StatType.Charisma)
            .OnConfigure(bp => {
                bp.IsDivineCaster = true;
                bp.IsArcaneCaster = false;

                // 职业本体也挂法术书 —— 原版施法职业（牧师 / 法师…）的 m_Spellbook 都有值
                bp.m_Spellbook = spellbook.ToReference<BlueprintSpellbookReference>();

                // 外观实体、UI 配色、默认 build、起始装备：原版每个职业都有这些字段，
                // 缺失会导致创建界面不显示外观等问题。直接借用牧师的（只读引用，不改原版）。
                var cleric = BlueprintTool.Get<BlueprintCharacterClass>(ClericClassGuid);
                if (cleric != null) {
                    bp.MaleEquipmentEntities = cleric.MaleEquipmentEntities;
                    bp.FemaleEquipmentEntities = cleric.FemaleEquipmentEntities;
                    bp.PrimaryColor = cleric.PrimaryColor;
                    bp.SecondaryColor = cleric.SecondaryColor;
                    bp.m_DefaultBuild = cleric.m_DefaultBuild;
                    bp.m_StartingItems = cleric.m_StartingItems;
                }
            })
            .Configure();
    }

    // ---------- 7. 注册进角色创建菜单（基础职业必须，否则界面里根本看不到） ----------

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

        // 同步注册。社区范例（Prestige-Plus Gunslinger）写的是 Configure(delayed: true)，
        // 但那依赖 BlueprintCore 自己的延迟队列 flush；本 mod 的 patch 是 Priority.Last，
        // 晚于那个 flush，所以这里必须同步执行，否则职业根本不会进创建菜单。
        RootConfigurator.For(RootRefs.BlueprintRoot)
            .ModifyProgression(register)
            .Configure();

        Main.Log?.Log("[卷宗学者] 已注册进角色创建菜单（ProgressionRoot.m_CharacterClasses）");
    }
}
