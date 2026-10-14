using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;

namespace Qqyx3RExpansion;

/// <summary>
/// 神圣驱魔师的「每日法术 / 已知法术」——每级「现有施法等级 +1」。
///
/// 游戏原版的做法（照 `Classes/Loremaster/LoremasterDivineSpellbooks/*LevelUp.jbp`
/// 与 `Classes/MysticTheurge/...`）：**每个法术书一个 Feature**，
/// 组件是 <c>Kingmaker.Designers.Mechanics.Facts.AddSpellbookLevel</c>：
/// <code>OnActivate() =&gt; Owner.DemandSpellbook(Spellbook).AddBaseLevel();</code>
/// 再把它们放进一个选择池，让玩家每次升级自己选提升哪个法术书。
///
/// 这里每个选项额外带 `PrerequisiteClassLevel(对应职业, 1)`，
/// 所以只有真正拥有该施法职业的人才会看到它。
/// </summary>
internal static class SacredExorcistSpellbooks {

    /// <summary>本 mod 蓝图：法术书提升选择池</summary>
    internal const string SelectionGuid = "529962aab97b4cc69777fd5e5998a705";

    private sealed class Book {
        public string Key;
        public string Name;
        /// <summary>对应的职业（用于前置：必须拥有该职业的施法能力）</summary>
        public string ClassGuid;
        /// <summary>要提升的法术书</summary>
        public string SpellbookGuid;
        /// <summary>本 mod 新增的 Feature GUID</summary>
        public string Guid;
    }

    /// <summary>
    /// 原版全部施法职业的法术书（从各 `<职业>Class.jbp` 的 `m_Spellbook` 取），
    /// 外加本 mod 的卷宗学者。
    /// </summary>
    private static readonly Book[] Books = {
        new Book { Key = "Wizard", Name = "法师", Guid = "549df5bd259c45439e2951e003136d2b",
            ClassGuid = "ba34257984f4c41408ce1dc2004e342e", SpellbookGuid = "5a38c9ac8607890409fcb8f6342da6f4" },
        new Book { Key = "Sorcerer", Name = "术士", Guid = "8059d470c72143d4a5247c10060f5329",
            ClassGuid = "b3a505fb61437dc4097f43c3f8f9a4cf", SpellbookGuid = "b3db3766a4b605040b366265e2af0e50" },
        new Book { Key = "Cleric", Name = "牧师", Guid = "2b4d1755305f4930901a92eae0145689",
            ClassGuid = "67819271767a9dd4fbfd4ae700befea0", SpellbookGuid = "4673d19a0cf2fab4f885cc4d1353da33" },
        new Book { Key = "Druid", Name = "德鲁伊", Guid = "6cbeed1a089640c3b1c612072246c6ed",
            ClassGuid = "610d836f3a3a9ed42a4349b62f002e96", SpellbookGuid = "fc78193f68150454483a7eea8b605b71" },
        new Book { Key = "Bard", Name = "吟游诗人", Guid = "253a8fdd02244f7397f5d264d0382271",
            ClassGuid = "772c83a25e2268e448e841dcd548235f", SpellbookGuid = "bc04fc157a8801d41b877ad0d9af03dd" },
        new Book { Key = "Witch", Name = "巫师", Guid = "f86d4cda65994229bff2c8c596f78fbe",
            ClassGuid = "1b9873f1e7bfe5449bc84d03e9c8e3cc", SpellbookGuid = "dd04f9239f655ea438976742728e4909" },
        new Book { Key = "Magus", Name = "魔战士", Guid = "6b02081207674a59bac293628132e2d3",
            ClassGuid = "45a4607686d96a1498891b3286121780", SpellbookGuid = "5d8d04e76dff6c5439de99af0d57be63" },
        new Book { Key = "Inquisitor", Name = "审判者", Guid = "1bfb2b6445ed4dfca1718ffd6387dfc4",
            ClassGuid = "f1a70d9e1b0b41e49874e1fa9052a1ce", SpellbookGuid = "57fab75111f377248810ece84193a5a5" },
        new Book { Key = "Shaman", Name = "萨满", Guid = "1d475812a4c9457983deaa666af79d2f",
            ClassGuid = "145f1d3d360a7ad48bd95d392c81b38e", SpellbookGuid = "44f16931dabdff643bfe2a48138e769f" },
        new Book { Key = "Warpriest", Name = "战斗祭司", Guid = "91015526095f43378f1884870dfd919c",
            ClassGuid = "30b5e47d47a0e37438cc5a80c96cfb99", SpellbookGuid = "7d7d51be2948d2544b3c2e1596fd7603" },
        new Book { Key = "Hunter", Name = "猎人", Guid = "029c52afbc6f4f6a862c341a8094c9a3",
            ClassGuid = "34ecd1b5e1b90b9498795791b0855239", SpellbookGuid = "885cd422aa357e2409146b38bb1fec51" },
        new Book { Key = "Paladin", Name = "圣武士", Guid = "ccf62b37bdff4050acff3ac8f94c9195",
            ClassGuid = "bfa11238e7ae3544bbeb4d0b92e897ec", SpellbookGuid = "bce4989b070ce924b986bf346f59e885" },
        new Book { Key = "Ranger", Name = "游侠", Guid = "9d4cb8825de74b1c998164f899533555",
            ClassGuid = "cda0615668a6df14eb36ba19ee881af6", SpellbookGuid = "762858a4a28eaaf43aa00f50441d7027" },
        new Book { Key = "Alchemist", Name = "炼金术士", Guid = "e18a904e1abf434aad7f19f862d28f5f",
            ClassGuid = "0937bec61c0dabc468428f496580c721", SpellbookGuid = "027d37761f3804042afa96fe3e9086cc" },
        new Book { Key = "Bloodrager", Name = "血脉狂怒者", Guid = "0491468b1f7340e7a2be84525feea8c3",
            ClassGuid = "d77e67a814d686842802c9cfd8ef8499", SpellbookGuid = "e19484252c2f80e4a9439b3681b20f00" },
        new Book { Key = "Arcanist", Name = "奥能师", Guid = "b9c0cec367c74f568a6970e73660e9cc",
            ClassGuid = "52dbfd8505e22f84fad8d702611f60b7", SpellbookGuid = "33903fe5c4abeaa45bc249adb9d98848" },
        new Book { Key = "Skald", Name = "战歌者", Guid = "035410d8a89d46689dac471eb97d5d6f",
            ClassGuid = "6afa347d804838b48bda16acb0573dc0", SpellbookGuid = "8f159d2f22ced074ea32995eb5a446a0" },
        new Book { Key = "Oracle", Name = "先知", Guid = "ecb371a340b545d8987147bbb587291d",
            ClassGuid = "20ce9bf8af32bee4c8557a045ab499b1", SpellbookGuid = "6c03364712b415941a98f74522a81273" },
        // 本 mod 的卷宗学者
        new Book { Key = "Archivist", Name = "卷宗学者", Guid = "5f2f7d1e40a24d978fbdeacbbad89d0d",
            ClassGuid = Archivist.ClassGuid, SpellbookGuid = Archivist.SpellbookGuid },
    };

    internal static void Configure() {
        var options = new List<Blueprint<BlueprintFeatureReference>>();

        foreach (var book in Books) {
            var name = "SacredExorcistSpellbook" + book.Key;
            options.Add(FeatureConfigurator.New(name, book.Guid)
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name",
                    "施法者等级提升：" + book.Name))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "你的神圣驱魔师等级与" + book.Name + "等级相加，"
                    + "用以决定你的每日法术数量与已知法术。\n"
                    + "（只有拥有" + book.Name + "施法能力的人才能选择。）"))
                .SetIsClassFeature()
                // 可以选 10 次（对应神圣驱魔师的 10 个等级）
                .SetRanks(10)
                .AddPrerequisiteClassLevel(book.ClassGuid, 1)
                .AddSpellbookLevel(BlueprintTool.GetRef<BlueprintSpellbookReference>(book.SpellbookGuid))
                .Configure());
        }

        FeatureSelectionConfigurator.New("SacredExorcistSpellbookSelection", SelectionGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredExorcistSpellbook.Name", "每日法术"))
            .SetDescription(LocalizationTool.CreateString("SacredExorcistSpellbook.Description",
                "神圣驱魔师可以继续提升其原有的施法能力：每获得一级神圣驱魔师等级，"
                + "选择一个施法职业，其每日法术数量与已知法术提升 1 级"
                + "（但不会得到该职业的其它特殊能力）。\n"
                + "如果人物在成为神圣驱魔师之前有多个施法职业，需要自己决定把神圣驱魔师等级"
                + "和从前的哪一个施法职业相加。"))
            .SetIsClassFeature()
            .AddToAllFeatures(options.ToArray())
            .Configure();

        Main.Log?.Log($"[神圣驱魔师] 已注册 {options.Count} 个法术书提升选项");
    }
}
