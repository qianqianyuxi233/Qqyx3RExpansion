using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 卷宗学者的职业能力（**黑暗学识体系除外**，那一套需要知识检定与按生物类型加值，单独设计）。
///
/// 按 3.5《Heroes of Horror》职业表：
///   1 级          抄写卷轴（奖励专长）
///   2 级          学识掌握（选一项知识检定 +2）
///   4 级          心如止水（对抗附魔系法术的豁免 +2）
///   7 / 13 / 17 级 学识掌握（额外再选一项）
///   10 / 20 级     奖励专长（技能专攻(知识) / 法术专攻 / 超魔专长）
///
/// 实现取向：全部使用游戏现成组件，不写自定义代码。
///   技能加值   → AddStatBonus
///   对抗学派    → SavingThrowBonusAgainstSchool（School = Enchantment）
///   可选能力池  → FeatureSelectionConfigurator
/// </summary>
internal static class ArchivistFeatures {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string LoreMasterySelectGuid   = "0bfae95957a14482b92914f0f87a27c8";
    private const string LoreMasteryArcanaGuid   = "df173cf454b64f4895c5a7fa40bbc121";
    private const string LoreMasteryWorldGuid    = "8c08c42ecb104cb78fa37b072a70827b";
    private const string LoreMasteryReligionGuid = "c3acdc4373ba4ded9fdb231cc6467ace";
    private const string LoreMasteryNatureGuid   = "97981bf2123b482b89887767303230ab";
    private const string StillMindGuid           = "5687710f385a4568a7496489052a7670";
    private const string BonusFeatSelectGuid     = "873d9bb9f42e460889cec598a36d7ab7";

    /// <summary>学识掌握相关能力借用「博学大师」（Lore Master）的图标 —— 学识主题。</summary>
    private static UnityEngine.Sprite Icon => FeatureRefs.BardLoreMaster.Reference.Get().Icon;

    /// <summary>心如止水的图标 —— 借用战士的「无畏」（Bravery）。</summary>
    private static UnityEngine.Sprite BraveryIcon =>
        BlueprintTool.Get<BlueprintFeature>("f6388946f9f472f4585591b80e9f2452").Icon;

    /// <summary>奖励专长的图标 —— 借用法师的奖励专长选择（WizardFeatSelection）。</summary>
    private static UnityEngine.Sprite WizardFeatIcon =>
        BlueprintTool.Get<BlueprintFeatureSelection>("8c3102c2ff3b69444b139a98521a4899").Icon;

    // 注：「抄写卷轴」不再做成职业能力 —— 那只是个占位，实际机制已经由法术书的
    // CanCopyScrolls = true 提供（见 Archivist.ConfigureSpellbook）。

    // ---------- 2 / 7 / 13 / 17 级：学识掌握 ----------

    /// <summary>
    /// 学识掌握：选择一项知识，在该知识检定上获得 +2 加值。
    /// 2 级获得第一次，7 / 13 / 17 级各再获得一次（同一个能力池，多次选择）。
    /// </summary>
    internal static BlueprintFeatureSelection ConfigureLoreMastery() {
        var arcana = KnowledgeBonus("ArchivistLoreMasteryArcana", LoreMasteryArcanaGuid,
            "知识（神秘）", StatType.SkillKnowledgeArcana);
        var world = KnowledgeBonus("ArchivistLoreMasteryWorld", LoreMasteryWorldGuid,
            "知识（世界）", StatType.SkillKnowledgeWorld);
        var religion = KnowledgeBonus("ArchivistLoreMasteryReligion", LoreMasteryReligionGuid,
            "学识（宗教）", StatType.SkillLoreReligion);
        var nature = KnowledgeBonus("ArchivistLoreMasteryNature", LoreMasteryNatureGuid,
            "学识（自然）", StatType.SkillLoreNature);

        return FeatureSelectionConfigurator.New("ArchivistLoreMasterySelection", LoreMasterySelectGuid)
            .SetDisplayName(LocalizationTool.CreateString("ArchivistLoreMastery.Name", "学识掌握"))
            .SetDescription(LocalizationTool.CreateString("ArchivistLoreMastery.Description",
                "卷宗学者在所选的一项知识检定上获得 +2 加值。"
                + "2 级时获得第一次，7 级、13 级、17 级各再获得一次。"))
            .SetIcon(Icon)
            .AddToAllFeatures(arcana, world, religion, nature)
            .SetIsClassFeature()
            .Configure();
    }

    private static BlueprintFeature KnowledgeBonus(string name, string guid, string display, StatType skill) {
        return FeatureConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "卷宗学者在" + display + "检定上获得 +2 加值。"))
            .SetIcon(Icon)
            .AddComponent(new AddStatBonus {
                // 自定义组件必须给唯一 name，否则 EntityFact.Setup 按 name 去重会跳过同名组件
                name = "$" + name + "$StatBonus",
                Stat = skill,
                Value = 2,
            })
            .SetIsClassFeature()
            .Configure();
    }

    // ---------- 4 级：心如止水 ----------

    /// <summary>
    /// 心如止水：对抗附魔系（Enchantment）法术时，三种豁免都 +2。
    /// 用现成的 <see cref="SavingThrowBonusAgainstSchool"/>：
    /// 它在 RuleSavingThrow 触发时比较来源法术的学派，命中就给三个豁免加值。
    /// （3.5 原文是「附魔系法术**和效果**」，非法术来源的附魔效果游戏里无法判定，此处只覆盖法术。）
    /// </summary>
    internal static BlueprintFeature ConfigureStillMind() {
        const string name = "ArchivistStillMind";
        return FeatureConfigurator.New(name, StillMindGuid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "心如止水"))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "从 4 级起，因为卷宗学者的专注和精神上的戒条，"
                + "他在对抗附魔系法术的豁免检定上获得 +2 加值。"))
            .SetIcon(BraveryIcon)
            .AddComponent(new SavingThrowBonusAgainstSchool {
                name = "$" + name + "$SaveBonus",
                School = SpellSchool.Enchantment,
                Value = 2,
                ModifierDescriptor = ModifierDescriptor.UntypedStackable,
            })
            .SetIsClassFeature()
            .Configure();
    }

    // ---------- 10 / 20 级：奖励专长 ----------

    /// <summary>
    /// 奖励专长：从「技能专攻（任何知识）」「法术专攻（任何学派）」「任何超魔专长」中选一个。
    /// 10 级与 20 级各获得一次。
    /// </summary>
    internal static BlueprintFeatureSelection ConfigureBonusFeat() {
        // 注意 AddToAllFeatures 的参数类型是 Blueprint<BlueprintFeatureReference>：
        // 单个蓝图对象能隐式转换，但 BlueprintFeature[] 数组不能，所以这里直接收集 guid
        // （Blueprint<T> 有 string 的隐式转换）。
        var feats = new List<Blueprint<BlueprintFeatureReference>>();
        foreach (var guid in BonusFeatGuids) {
            feats.Add(guid);
        }

        return FeatureSelectionConfigurator.New("ArchivistBonusFeatSelection", BonusFeatSelectGuid)
            .SetDisplayName(LocalizationTool.CreateString("ArchivistBonusFeat.Name", "奖励专长"))
            .SetDescription(LocalizationTool.CreateString("ArchivistBonusFeat.Description",
                "卷宗学者可以从「技能专攻（任何知识技能）」「法术专攻」以及任何超魔专长中"
                + "选择一个奖励专长。10 级与 20 级各获得一次。"))
            .SetIcon(WizardFeatIcon)
            .AddToAllFeatures(feats.ToArray())
            .SetIsClassFeature()
            .Configure();
    }

    /// <summary>奖励专长池：4 个知识技能专攻 + 8 个法术专攻 + 11 个超魔专长。</summary>
    private static readonly string[] BonusFeatGuids = {
        // 技能专攻（知识类）
        "cad1b9175e8c0e64583432a22134d33c",  // SkillFocusKnowledgeArcana  知识（神秘）
        "611e863120c0f9a4cab2d099f1eb20b4",  // SkillFocusKnowledgeWorld   知识（世界）
        "6507d2da389ed55448e0e1e5b871c013",  // SkillFocusLoreNature       学识（自然）
        "c541f80af8d0af4498e1abb6025780c7",  // SkillFocusLoreReligion     学识（宗教）
        // 法术专攻（8 个学派）
        "71a3f1c1ac77ae3488b9b3d6d2aac01a",  // 防护 Abjuration
        "d342cc595f499434687f9765f56d525c",  // 咒法 Conjuration
        "955e97411611d384db2cbc00d7ed5ead",  // 预言 Divination
        "c5bf645f128c39b40850cde005b8538f",  // 附魔 Enchantment
        "743d0106ee3076342839c6d550cdda25",  // 塑能 Evocation
        "e588279a80eb7a24b813fadad4bc83b5",  // 幻术 Illusion
        "8791da25011fd1844ad61a3fea6ece54",  // 死灵 Necromancy
        "49907a2e51b49d641aad3c9781a3a698",  // 变化 Transmutation
        // 超魔专长
        "a1de1e4f92195b442adb946f0e2b9d4e",  // EmpowerSpellFeat          法术强效
        "f180e72e4a9cbaa4da8be9bc958132ef",  // ExtendSpellFeat           法术延时
        "2f5d1e705c7967546b72ad8218ccf99c",  // HeightenSpellFeat         法术升阶
        "8ad7fd39abea4722b39eb5a67d606a41",  // IntensifiedSpell          法术强化
        "7f2b282626862e345935bbea5e66424b",  // MaximizeSpellFeat         法术极效
        "cd26b9fa3f734461a0fcedc81cafaaac",  // PersistentSpellFeat       法术持久
        "c101ad6879a94204a780506f7a554865",  // PiercingSpell             法术穿透
        "ef7ece7bb5bb66a41b256976b27f424e",  // QuickenSpellFeat          法术瞬发
        "46fad72f54a33dc4692d3b62eca7bb78",  // ReachSpellFeat            法术延伸
        "85f3340093d144dd944fff9a9adfd2f2",  // SelectiveSpellFeat        法术选择
        "094b6278f7b570f42aeaa98379f07cf2",  // CompletelyNormalSpellFeat 完全正常法术
    };
}
