using System;
using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 黑暗学识（重做版）——只对**恶魔**生效，只需要一次知识（世界）检定。
///
///   1 级          能力本身；每日 3 次；变体「战术」（对恶魔攻击 +1/+2/+3）
///   3/6/9/12/15/18 级  每日次数各 +1（3 → 9 次）
///   5 级          解锁「影响」（对抗恶魔的豁免 +1/+2/+3）
///   8 级          解锁「对手」（对恶魔的伤害 +1d6/+2d6/+3d6）
///   14 级         解锁「预知」（面对恶魔时 AC +1/+2/+3）
///
/// 施放：移动动作，掷**一次** d20 + 知识（世界），出目 &gt;15 / &gt;25 / &gt;35 对应三档，
/// 给自己与 60 尺内的同伴挂上已解锁变体的对应档位 buff，持续 1 分钟。
/// </summary>
internal static class DarkKnowledge {

    /// <summary>
    /// 恶魔的 Fact —— 游戏 AI 的 <c>NoDemonConsideration</c> 判断「是否恶魔」用的就是它，
    /// 所以这是普通单位的通用恶魔标记，不是陆军专用。
    /// </summary>
    internal const string DemonFactGuid = "dc960a234d365cb4f905bdc5937e623a";

    private const string ResourceGuid = "d64a9a9903004d8f9d40b2c11cc9b208";
    private const string AbilityGuid  = "9ed1e2a2aac14e30b16a37ce76495572";

    // 四个变体各做成一个职业能力条目，分别放在 1 / 5 / 8 / 14 级。
    // 每个条目都会把同一个「黑暗学识」能力加进来（游戏对同一 fact 会自动去重），
    // 实际生效哪些变体由能力内部读卷宗学者等级决定。
    private const string TacticsFeatureGuid        = "4d135f7ac38e4c36b22af3d8194a2272";  // 1 级
    private const string PuissanceFeatureGuid      = "41e2393ac034478ab88ff12995d77ad3";  // 5 级
    private const string FoeFeatureGuid            = "52304a160d064970930ceda41cfb2fe2";  // 8 级
    private const string ForeknowledgeFeatureGuid  = "6bb6d31df4cf4dd79cdf47bef3a6f2e8";  // 14 级

    // 四个变体各三档（索引 0/1/2 = 第 1/2/3 档）
    private static readonly string[] TacticsGuids = {
        "2c0be1c847824bafa3ca5d7e5bbb1208", "7c516904bd64426082a84d4a7c67106b", "6d7829cc4c9246d1b513b933c76c6cec",
    };
    private static readonly string[] PuissanceGuids = {
        "ee33b4b6dfbb4861b53ec4c602a78d0f", "3feab940b15941adadc6f2b3b8b9302a", "9178e2263abe4e59b36b472371ad1785",
    };
    private static readonly string[] FoeGuids = {
        "4d8bdb2fdc5547b9b1ecd486f9a66e85", "28d9929fd6c94cbe8aba4d8e1612dbb0", "32e02b2c4b884b59b72e850cefb36b86",
    };
    private static readonly string[] ForeknowledgeGuids = {
        "9b05ea466de243aa96155b1dac73f649", "cf475c58192143ee8c9335e363f20e97", "91caf27f75984f668a9fdf0d951480ca",
    };

    /// <summary>3/6/9/12/15/18 级的每日次数 +1 能力。</summary>
    private static readonly string[] TimesGuids = {
        "2490a3cf72c542e4afbdf92e0342d3d7", "5507680f1714444eba6b02ce2238b6b2", "cc6c2e0eca1f41a589dbf8614b35309d",
        "252b3cc4d7934b9682ed6fcc2c79b6d7", "399f21fd17a24e1fa92eb46d08616fe3", "1d303c320cb44dc9ba9545b9367b31f3",
    };

    /// <summary>黑暗学识系列统一借用吟游诗人「勇气赞歌」（InspireCourageFeature）的图标。</summary>
    private static UnityEngine.Sprite Icon =>
        BlueprintTool.Get<BlueprintFeature>("acb4df34b25ca9043a6aba1a4c92bc69").Icon;

    /// <summary>1 级：黑暗学识（战术）—— 同时把能力本体加进职业。</summary>
    internal static BlueprintFeature TacticsFeature { get; private set; }
    /// <summary>5 级：黑暗学识（影响）。</summary>
    internal static BlueprintFeature PuissanceFeature { get; private set; }
    /// <summary>8 级：黑暗学识（对手）。</summary>
    internal static BlueprintFeature FoeFeature { get; private set; }
    /// <summary>14 级：黑暗学识（预知）。</summary>
    internal static BlueprintFeature ForeknowledgeFeature { get; private set; }

    /// <summary>每日次数提升能力，按 3/6/9/12/15/18 级顺序排列。</summary>
    internal static readonly List<BlueprintFeature> TimesFeatures = new List<BlueprintFeature>();

    internal static void Configure() {
        var demon = BlueprintTool.GetRef<BlueprintUnitFactReference>(DemonFactGuid);

        MakeTactics(demon);
        MakePuissance(demon);
        MakeFoe(demon);
        MakeForeknowledge(demon);

        var resource = ConfigureResource();
        ConfigureAbilityFeatures(resource);
        ConfigureTimesFeatures(resource);
    }

    // ---------- 每日次数资源 ----------

    private static BlueprintAbilityResource ConfigureResource() {
        return AbilityResourceConfigurator.New("ArchivistDarkKnowledgeResource", ResourceGuid)
            .SetMaxAmount(ResourceAmountBuilder.New(3))
            .Configure();
    }

    /// <summary>3/6/9/12/15/18 级各 +1 次。</summary>
    private static void ConfigureTimesFeatures(BlueprintAbilityResource resource) {
        TimesFeatures.Clear();
        var levels = new[] { 3, 6, 9, 12, 15, 18 };

        for (int i = 0; i < levels.Length; i++) {
            var name = "ArchivistDarkKnowledgeTimes" + levels[i];
            TimesFeatures.Add(FeatureConfigurator.New(name, TimesGuids[i])
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "黑暗学识（次数提升）"))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "卷宗学者每日使用黑暗学识的次数增加 1 次。"))
                .SetIcon(Icon)
                .SetIsClassFeature()
                .AddComponent(new IncreaseResourceAmount {
                    name = "$" + name + "$Increase",
                    // Resource 是只读属性（内部 get 自 m_Resource），只能设 private 字段
                    m_Resource = resource.ToReference<BlueprintAbilityResourceReference>(),
                    Value = 1,
                })
                .Configure());
        }
    }

    // ---------- 四个变体 ----------

    private static void MakeTactics(BlueprintUnitFactReference demon) {
        var tiers = new[] { 1, 2, 3 };
        for (int i = 0; i < 3; i++) {
            var name = "ArchivistDarkKnowledgeTactics" + tiers[i];
            BuffConfigurator.New(name, TacticsGuids[i])
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name",
                    "黑暗学识：战术 +" + tiers[i]))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "卷宗学者看穿了恶魔的战斗方式。对恶魔的攻击检定获得 +" + tiers[i] + " 加值。"))
                .SetIcon(Icon)
                .AddComponent(new AttackBonusAgainstFactOwner {
                    name = "$" + name + "$Attack",
                    m_CheckedFact = demon,
                    AttackBonus = tiers[i],
                    Descriptor = ModifierDescriptor.UntypedStackable,
                })
                .Configure();
        }
    }

    private static void MakePuissance(BlueprintUnitFactReference demon) {
        var tiers = new[] { 1, 2, 3 };
        for (int i = 0; i < 3; i++) {
            var name = "ArchivistDarkKnowledgePuissance" + tiers[i];
            BuffConfigurator.New(name, PuissanceGuids[i])
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name",
                    "黑暗学识：影响 +" + tiers[i]))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "卷宗学者帮助同伴摆脱恶魔的影响。对抗恶魔的能力时，豁免检定获得 +"
                    + tiers[i] + " 加值。"))
                .SetIcon(Icon)
                .AddComponent(new SavingThrowBonusAgainstFactMultiple {
                    name = "$" + name + "$Save",
                    m_Facts = new[] { demon },
                    Value = tiers[i],
                    Descriptor = ModifierDescriptor.UntypedStackable,
                })
                .Configure();
        }
    }

    private static void MakeFoe(BlueprintUnitFactReference demon) {
        var tiers = new[] { 1, 2, 3 };
        for (int i = 0; i < 3; i++) {
            var name = "ArchivistDarkKnowledgeFoe" + tiers[i];
            BuffConfigurator.New(name, FoeGuids[i])
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name",
                    "黑暗学识：对手 +" + tiers[i] + "d6"))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "卷宗学者指引同伴攻击恶魔的弱点。对恶魔造成的伤害获得 +"
                    + tiers[i] + "d6 加值。"))
                .SetIcon(Icon)
                .AddComponent(new DarkKnowledgeDamageDice {
                    name = "$" + name + "$Damage",
                    m_CheckedFact = demon,
                    DiceCount = tiers[i],
                    Descriptor = ModifierDescriptor.UntypedStackable,
                })
                .Configure();
        }
    }

    private static void MakeForeknowledge(BlueprintUnitFactReference demon) {
        var tiers = new[] { 1, 2, 3 };
        for (int i = 0; i < 3; i++) {
            var name = "ArchivistDarkKnowledgeForeknowledge" + tiers[i];
            BuffConfigurator.New(name, ForeknowledgeGuids[i])
                .SetDisplayName(LocalizationTool.CreateString(name + ".Name",
                    "黑暗学识：预知 +" + tiers[i]))
                .SetDescription(LocalizationTool.CreateString(name + ".Description",
                    "卷宗学者让同伴对恶魔的行动有所准备。面对恶魔时 AC 获得 +"
                    + tiers[i] + " 洞察加值。"))
                .SetIcon(Icon)
                .AddComponent(new ACBonusAgainstFactOwner {
                    name = "$" + name + "$AC",
                    m_CheckedFact = demon,
                    Bonus = tiers[i],
                    Descriptor = ModifierDescriptor.Insight,
                })
                .Configure();
        }
    }

    // ---------- 主能力 ----------

    /// <summary>
    /// 能力本体 + 四个变体条目（分别放在 1 / 5 / 8 / 14 级）。
    /// 每个条目都把同一个「黑暗学识」能力加进职业 —— 游戏对同一 fact 会自动去重；
    /// 具体生效哪些变体由 <see cref="ContextActionDarkKnowledge"/> 读卷宗学者等级决定。
    /// </summary>
    private static void ConfigureAbilityFeatures(BlueprintAbilityResource resource) {
        const string name = "ArchivistDarkKnowledge";

        var action = new ContextActionDarkKnowledge {
            Stat = StatType.SkillKnowledgeWorld,
            TacticsBuffs = Refs(TacticsGuids),
            PuissanceBuffs = Refs(PuissanceGuids),
            FoeBuffs = Refs(FoeGuids),
            ForeknowledgeBuffs = Refs(ForeknowledgeGuids),
            RadiusFeet = 60,
            DurationMinutes = 1,
        };

        var abilityDesc = LocalizationTool.CreateString(name + ".Description",
            "卷宗学者以对恶魔的禁忌研究支援同伴。以一个移动动作，他每天可以尝试 3 次"
            + "黑暗学识（每 3 个卷宗学者等级再增加 1 次）。\n"
            + "使用后进行一次知识（世界）检定：结果超过 15、25、35 时，他以及 60 尺内的同伴"
            + "在接下来 1 分钟内获得对应强度的加值。\n"
            + "此能力只对恶魔生效。");

        AbilityConfigurator.New(name, AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "黑暗学识"))
            .SetDescription(abilityDesc)
            .SetIcon(Icon)
            .SetType(AbilityType.Supernatural)
            .SetRange(AbilityRange.Personal)
            .SetActionType(UnitCommand.CommandType.Move)
            .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: ResourceGuid)
            .AddComponent(new AbilityEffectRunAction {
                name = "$" + name + "$Effect",
                Actions = new ActionList {
                    Actions = new GameAction[] { action },
                },
            })
            .Configure();

        var facts = new List<Blueprint<BlueprintUnitFactReference>> { AbilityGuid };

        // 1 级的条目额外负责**授予**每日次数资源：
        // AbilityResourceLogic 只管消耗，资源本身必须由某个 Feature 交给角色
        // （社区范例 Prestige-Plus 的 Gunslinger 就是用 AddAbilityResources）。
        TacticsFeature = FeatureConfigurator.New("ArchivistDarkKnowledgeTactics", TacticsFeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString(
                "ArchivistDarkKnowledgeTactics.Name", "黑暗学识（战术）"))
            .SetDescription(LocalizationTool.CreateString(
                "ArchivistDarkKnowledgeTactics.Description",
                "卷宗学者获得「黑暗学识」能力：以一个移动动作，每天可以尝试 3 次"
                + "（每 3 个卷宗学者等级再增加 1 次），进行一次知识（世界）检定；"
                + "结果超过 15、25、35 时，他以及 60 尺内的同伴在接下来 1 分钟内获得对应强度的加值。"
                + "此能力只对恶魔生效。\n\n"
                + "战术：他以及同伴对恶魔的攻击检定获得 +1（超过 25 时 +2，超过 35 时 +3）。"))
            .SetIcon(Icon)
            .SetIsClassFeature()
            .AddFacts(facts)
            .AddAbilityResources(resource: resource, restoreAmount: true)
            .Configure();

        PuissanceFeature = UnlockFeature("ArchivistDarkKnowledgePuissance", PuissanceFeatureGuid,
            "黑暗学识（影响）",
            "影响：他以及同伴在对抗恶魔的能力时，豁免检定获得 +1"
            + "（超过 25 时 +2，超过 35 时 +3）。",
            facts);

        FoeFeature = UnlockFeature("ArchivistDarkKnowledgeFoe", FoeFeatureGuid,
            "黑暗学识（对手）",
            "对手：他以及同伴对恶魔造成的伤害获得 +1d6"
            + "（超过 25 时 +2d6，超过 35 时 +3d6）。",
            facts);

        ForeknowledgeFeature = UnlockFeature("ArchivistDarkKnowledgeForeknowledge", ForeknowledgeFeatureGuid,
            "黑暗学识（预知）",
            "预知：他以及同伴面对恶魔时 AC 获得 +1 洞察加值"
            + "（超过 25 时 +2，超过 35 时 +3）。",
            facts);
    }

    private static BlueprintFeature UnlockFeature(string name, string guid, string display, string desc,
                                                  List<Blueprint<BlueprintUnitFactReference>> facts) {
        return FeatureConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString(name + ".Description", desc))
            .SetIcon(Icon)
            .SetIsClassFeature()
            .AddFacts(facts)
            .Configure();
    }

    private static BlueprintBuffReference[] Refs(string[] guids) {
        var result = new BlueprintBuffReference[guids.Length];
        for (int i = 0; i < guids.Length; i++) {
            result[i] = BlueprintTool.GetRef<BlueprintBuffReference>(guids[i]);
        }
        return result;
    }
}
