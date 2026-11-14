using System.Collections.Generic;
using System.Linq;
using BlueprintCore.Blueprints.Configurators.Classes;
using BlueprintCore.Blueprints.Configurators.Root;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 缺陷（Drawbacks，3.5 / PF1 可选房规）。
///
/// <list type="bullet">
/// <item>只在创建角色（1 级）时可选</item>
/// <item>最多选 2 个；每选 1 个获得 1 个奖励专长</item>
/// <item>1 级之后无法再获得</item>
/// </list>
///
/// <para>
/// <b>挂载点</b>：<c>Classes/Basic/BasicFeatsProgression</c>（<c>5b72dd2ca2cb73b49903806ee8986325</c>）
/// —— 这是所有角色共享的 1 级进度表，原版「背景」（<c>BackgroundsBaseSelection</c>）
/// 就是挂在这里的。把两张缺陷选择表加进它的 <c>Level=1</c> 条目，
/// 角色创建时就会依次弹出选择。
/// </para>
///
/// <para>
/// <b>为什么是两个选择表</b>：<c>SelectionMode</c> 只有 Default / OnlyNew / OnlyRankUp，
/// 没有「多选」模式，所以「最多 2 个」只能拆成两个独立的 selection 槽位。
/// 每张表都带「无需缺陷」选项，于是 0 / 1 / 2 个都选得了。
/// </para>
///
/// <para>
/// <b>怎么防止同一个缺陷被选两次</b>：每个缺陷 Feature 上都带
/// <c>PrerequisiteNoFeature(自己)</c> —— 第一次选时角色还没有它，条件通过；
/// 选完之后角色拥有了该 Feature，第二个槽位里它就会自动变灰。
/// 一个前置同时解决了「槽 1 能选」和「槽 2 不重复」。
/// </para>
///
/// <para>
/// <b>奖励专长</b>：每个缺陷通过 <c>AddFacts</c> 授予一张专长选择表
/// （<c>DrawbackBonusFeatSelection</c>）。不能直接授予 <c>BasicFeatSelection</c> ——
/// 同一张 selection 被授予两次会按 name 去重，第二次就没了。所以这里
/// 复制一份 <c>BasicFeatSelection.AllFeatures</c> 做成独立蓝图。
/// </para>
///
/// <para>
/// ⚠ 注意：<c>AllFeatures</c> 是**配置时的快照**。本 mod 加载之后其他 mod 再往
/// <c>BasicFeatSelection</c> 里加的专长不会出现在奖励池里。要跟随的话得改成
/// 延迟配置（<c>ConfigureDelayedBlueprints</c>），目前没做。
/// </para>
/// </summary>
internal static class Drawbacks {

    // ---------- 原版参照 ----------

    /// <summary>Classes/Basic/BasicFeatsProgression —— 所有角色共享的 1 级进度表。</summary>
    private const string BasicFeatsProgressionGuid = "5b72dd2ca2cb73b49903806ee8986325";

    /// <summary>Classes/Basic/BasicFeatSelection —— 普通专长选择池，奖励专长从它复制。</summary>
    private const string BasicFeatSelectionGuid = "247a4068296e8be42890143f451b4b45";

    /// <summary>
    /// 图标：借原版「诅咒炸弹」发现（<c>Classes/Alchemist/CursedBombsFeature</c>）的图标，所有缺陷共用。
    /// ⚠ 这里必须是**蓝图**的 AssetId，不是它的 m_Icon.guid。
    /// </summary>
    private const string IconSourceGuid = "bf8f80f3a492e7946924e1cd9c4b0867";

    /// <summary>
    /// 取图标；失败返回 null 而不是抛异常。
    /// 图标只是外观，不能因为它让整套缺陷注册失败。
    /// </summary>
    private static Sprite SafeIcon() {
        try {
            return BlueprintTool.Get<BlueprintFeature>(IconSourceGuid).Icon;
        } catch (System.Exception e) {
            Main.Log?.Log("[缺陷] 取图标失败（不影响功能）：" + e.Message);
            return null;
        }
    }

    // ---------- 框架蓝图 GUID ----------

    private const string Selection1Guid = "c3f5daea7fd9415a9bf73af76960c2f8";
    private const string Selection2Guid = "5d29f1422bbf41f8a81b8de1bc300807";
    private const string BonusFeatSelectionGuid = "21f38f8f8a2c4c4484353dfba873a854";
    private const string NoDrawbackGuid = "e19ae514d346481197f88af81b58ee94";

    // ---------- 18 个缺陷 GUID ----------

    private const string FeebleGuid = "c1860b97f7d84903a697cbfff0084f94";
    private const string FrailGuid = "2287cf79d31d4aef8e698b813dc4fbdf";
    private const string InattentiveGuid = "c151afcb59744e0a90e1df544f917d54";
    private const string MeagerFortitudeGuid = "24996faff30649e7b062c0c7bfacec77";
    private const string MurkyEyedGuid = "32ad655be0e54fa58fbb04116d24ef92";
    private const string NoncombatantGuid = "9042cefa4e5c418189c52c940af979f7";
    private const string PatheticStrGuid = "ac29a6f8ded74950a765cd3b0bd57f79";
    private const string PatheticDexGuid = "3f4c0bab87864154923154c115a74651";
    private const string PatheticConGuid = "e5b6abd2347b409a9e5c5bf9d4d83649";
    private const string PatheticIntGuid = "e7f95a2bb0804c86947e73abbb485a90";
    private const string PatheticWisGuid = "b276a0a1513a4f61b1752343fbe9bb06";
    private const string PatheticChaGuid = "1a87781a15644bec88b003d93bfbeda6";
    private const string PoorReflexesGuid = "78eba5d681da4cb3af80ea519840b9b7";
    private const string ShakyGuid = "829a64e6ae424ed487a243c5bd03cd5e";
    private const string SlowGuid = "00bc315107f8423fae9283a8fb3d9abc";
    private const string UnreactiveGuid = "e388857e8eaa457ba9b373943868d0e5";
    private const string VulnerableGuid = "791ef55c167941c2b65664f282e2af41";
    private const string WeakWillGuid = "631e0fe22e9944919b62d15a2e49aa24";

    internal static void Configure() {
        var icon = SafeIcon();

        // 1) 奖励专长选择表：复制普通专长池的内容。
        var basicFeatSelection = BlueprintTool.Get<BlueprintFeatureSelection>(BasicFeatSelectionGuid);
        var bonusFeats = basicFeatSelection.AllFeatures
            .Select(f => (Blueprint<BlueprintFeatureReference>)f.ToReference<BlueprintFeatureReference>())
            .ToArray();
        // 诊断：如果这里是 0，说明 BasicFeatSelection 还没加载完，奖励专长会没有可选项。
        Main.Log?.Log("[缺陷] 奖励专长池 " + bonusFeats.Length + " 项");

        FeatureSelectionConfigurator.New("DrawbackBonusFeatSelection", BonusFeatSelectionGuid)
            .SetDisplayName(LocalizationTool.CreateString("Drawback.BonusFeat.Name", "缺陷补偿专长"))
            .SetDescription(LocalizationTool.CreateString("Drawback.BonusFeat.Description",
                "因为承受了一项缺陷，你可以额外选择一个专长。"))
            .SetIcon(icon)
            .SetGroup(FeatureGroup.Feat)
            .SetAllFeatures(bonusFeats)
            .Configure(false);

        // 2) 「无需缺陷」选项
        FeatureConfigurator.New("DrawbackNone", NoDrawbackGuid)
            .SetDisplayName(LocalizationTool.CreateString("Drawback.None.Name", "无需缺陷"))
            .SetDescription(LocalizationTool.CreateString("Drawback.None.Description",
                "不承受任何缺陷，也不获得补偿专长。"))
            .SetIcon(icon)
            .SetIsClassFeature()
            .Configure(false);

        // 3) 18 个缺陷
        ConfigureFeeble(icon);
        ConfigureFrail(icon);
        ConfigureInattentive(icon);
        ConfigureMeagerFortitude(icon);
        ConfigureMurkyEyed(icon);
        ConfigureNoncombatant(icon);
        ConfigurePathetic(icon);
        ConfigurePoorReflexes(icon);
        ConfigureShaky(icon);
        ConfigureSlow(icon);
        ConfigureUnreactive(icon);
        ConfigureVulnerable(icon);
        ConfigureWeakWill(icon);

        // 4) 两张选择表（内容相同，靠 PrerequisiteNoFeature 防重复）
        var all = new[] {
            FeebleGuid, FrailGuid, InattentiveGuid, MeagerFortitudeGuid, MurkyEyedGuid,
            NoncombatantGuid, PatheticStrGuid, PatheticDexGuid, PatheticConGuid,
            PatheticIntGuid, PatheticWisGuid, PatheticChaGuid, PoorReflexesGuid,
            ShakyGuid, SlowGuid, UnreactiveGuid, VulnerableGuid, WeakWillGuid,
            NoDrawbackGuid,
        };
        var refs = all
            .Select(g => (Blueprint<BlueprintFeatureReference>)BlueprintTool.GetRef<BlueprintFeatureReference>(g))
            .ToArray();

        FeatureSelectionConfigurator.New("DrawbackSelection1", Selection1Guid)
            .SetDisplayName(LocalizationTool.CreateString("Drawback.Slot1.Name", "缺陷（第一个）"))
            .SetDescription(LocalizationTool.CreateString("Drawback.Slot1.Description",
                "选择第一个缺陷以换取一个奖励专长，或选择「无需缺陷」。最多可以选择两个缺陷。"))
            .SetIcon(icon)
            .SetAllFeatures(refs)
            .Configure(false);

        FeatureSelectionConfigurator.New("DrawbackSelection2", Selection2Guid)
            .SetDisplayName(LocalizationTool.CreateString("Drawback.Slot2.Name", "缺陷（第二个）"))
            .SetDescription(LocalizationTool.CreateString("Drawback.Slot2.Description",
                "选择第二个缺陷以再换取一个奖励专长，或选择「无需缺陷」。"))
            .SetIcon(icon)
            .SetAllFeatures(refs)
            .Configure(false);

        // 5) 挂到共享 1 级进度表（背景也是挂在这里的）。
        ProgressionConfigurator.For(BlueprintTool.GetRef<BlueprintProgressionReference>(BasicFeatsProgressionGuid))
            .ModifyLevelEntries(entry => {
                if (entry.Level != 1) return;
                entry.Features.Add(BlueprintTool.GetRef<BlueprintFeatureBaseReference>(Selection1Guid));
                entry.Features.Add(BlueprintTool.GetRef<BlueprintFeatureBaseReference>(Selection2Guid));
            })
            .Configure(false);

        // 6) 把 BlueprintRoot 的专长进度重新指向这张表。
        //
        // 照抄 Path of War（Common/MainProgression.cs）的做法：只改 BasicFeatsProgression
        // 还不够，Root 里那份引用也必须重新赋值，否则角色创建时不读新的 LevelEntries。
        RootConfigurator.For(RootRefs.BlueprintRoot)
            .ModifyProgression(c => {
                c.m_FeatsProgression = BlueprintTool.GetRef<BlueprintProgressionReference>(BasicFeatsProgressionGuid);
            })
            .Configure(false);

        Main.Log?.Log("[缺陷] 已注册 18 个缺陷与两个选择槽，并挂到 1 级进度（延迟配置）");
    }

    // ==================== 缺陷定义 ====================

    /// <summary>
    /// 缺陷的公共骨架。
    ///
    /// <para>
    /// <b>为什么是 <see cref="BlueprintProgression"/> 而不是 <see cref="BlueprintFeature"/></b>：
    /// 「选缺陷 → 额外得一个专长」必须让引擎排入一次新的选择请求。
    /// 用 <c>AddFacts(selection)</c> 是不行的 —— <c>AddFacts</c> 组件里有明确校验：
    /// <c>if (fact is BlueprintFeatureSelection) context.AddError("not allowed here")</c>，
    /// 它只会把 selection 加进角色的 feature 列表，不会触发选择界面。
    ///
    /// 而 <c>LevelUpHelper.AddFeaturesFromProgression</c> 里对
    /// <c>blueprintFeature as BlueprintProgression</c> 有特殊处理：
    /// <code>
    /// Feature feature = unit.AddFact(blueprintFeature);
    /// if (blueprintFeature is BlueprintProgression p)
    ///     LevelUpHelper.UpdateProgression(state, unit, p);   // ← 再处理它的 LevelEntries
    /// </code>
    /// 而 <c>UpdateProgression</c> → <c>AddFeaturesFromProgression</c> →
    /// 发现 selection 就 <c>state.AddSelection(...)</c>，选择界面才会出现。
    /// </para>
    ///
    /// <para>
    /// <c>BlueprintProgression.CalcLevel()</c> 在 <c>m_Classes</c> / <c>m_Archetypes</c> 都为空时
    /// 直接返回角色等级，所以缺陷 progression 的 1 级条目在 1 级角色身上必然被处理。
    /// </para>
    /// </summary>
    private static ProgressionConfigurator NewDrawback(string name, string guid, string display, string description) {
        var levelEntries = LevelEntryBuilder.New()
            .AddEntry(1, new Blueprint<BlueprintFeatureBaseReference>[] {
                BlueprintTool.GetRef<BlueprintFeatureBaseReference>(BonusFeatSelectionGuid),
            })
            .GetEntries();

        return ProgressionConfigurator.New(name, guid)
            .SetDisplayName(LocalizationTool.CreateString("Drawback." + name + ".Name", display))
            .SetDescription(LocalizationTool.CreateString("Drawback." + name + ".Description", description))
            .SetIcon(SafeIcon())
            .SetIsClassFeature()
            // ← 被选中时，引擎会进这个条目并排队一次「缺陷补偿专长」选择
            .SetLevelEntries(levelEntries)
            // 选过之后同一缺陷在第二个槽位里变灰。
            .AddPrerequisiteNoFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(guid));
    }

    private static AddStatBonus StatPenalty(string name, StatType stat, int value) {
        return new AddStatBonus {
            name = "$" + name + "$" + stat,
            Stat = stat,
            Value = value,
            Descriptor = ModifierDescriptor.Penalty,
        };
    }

    /// <summary>虚弱：力量 / 敏捷 / 体质相关技能 -2。</summary>
    private static void ConfigureFeeble(Sprite icon) {
        // WOTR 没有独立的「属性检定」，只保留「相关技能」这一半。
        NewDrawback("DrawbackFeeble", FeebleGuid, "虚弱",
                "你不强壮，身体协调性也很差。\n"
                + "你的力量、敏捷、体质相关技能检定受到 -2 惩罚"
                + "（运动、隐匿、巧手、灵巧）。")
            .AddComponent(StatPenalty("DrawbackFeeble", StatType.SkillAthletics, -2))
            .AddComponent(StatPenalty("DrawbackFeeble", StatType.SkillStealth, -2))
            .AddComponent(StatPenalty("DrawbackFeeble", StatType.SkillThievery, -2))
            .AddComponent(StatPenalty("DrawbackFeeble", StatType.SkillMobility, -2))
            .Configure(false);
    }

    /// <summary>脆弱：每级 HP -1；需要体质 ≥ 4。</summary>
    private static void ConfigureFrail(Sprite icon) {
        NewDrawback("DrawbackFrail", FrailGuid, "脆弱",
                "你瘦弱而体质差。\n"
                + "每个等级你所获得的 HP -1。这个缺陷可以使你的 HP 减少到 0（不会到 0 以下）。\n"
                + "特殊：你必须拥有 4 点或以上的体质才能选择这个缺陷。")
            .AddComponent(new FrailHitPoints { name = "$DrawbackFrail$HitPoints" })
            .AddPrerequisiteStatValue(StatType.Constitution, 4)
            .Configure(false);
    }

    /// <summary>漫不经心：侦察 -4（WOTR 的 Perception 合并了聆听与侦察）。</summary>
    private static void ConfigureInattentive(Sprite icon) {
        NewDrawback("DrawbackInattentive", InattentiveGuid, "漫不经心",
                "你毫不关心周围的情况。\n"
                + "你的感知（侦察）技能检定受到 -4 惩罚。")
            .AddComponent(StatPenalty("DrawbackInattentive", StatType.SkillPerception, -4))
            .Configure(false);
    }

    /// <summary>病弱体质：强韧豁免 -3。</summary>
    private static void ConfigureMeagerFortitude(Sprite icon) {
        NewDrawback("DrawbackMeagerFortitude", MeagerFortitudeGuid, "病弱体质",
                "你多病且挑食。\n"
                + "你的强韧豁免受到 -3 惩罚。")
            .AddComponent(StatPenalty("DrawbackMeagerFortitude", StatType.SaveFortitude, -3))
            .Configure(false);
    }

    /// <summary>老花眼：对隐蔽目标攻击时投两次失手骰，任一次失手即失手。</summary>
    private static void ConfigureMurkyEyed(Sprite icon) {
        NewDrawback("DrawbackMurkyEyed", MurkyEyedGuid, "老花眼",
                "你看东西浑浊不堪。\n"
                + "战斗中，每次你攻击隐蔽下的敌人时会投掷两次失手骰；"
                + "只要其中有一次失手，这次攻击就会失手。")
            .AddComponent(new MurkyEyedConcealment { name = "$DrawbackMurkyEyed$Concealment" })
            .Configure(false);
    }

    /// <summary>文职人员：所有近战攻击 -2。</summary>
    private static void ConfigureNoncombatant(Sprite icon) {
        NewDrawback("DrawbackNoncombatant", NoncombatantGuid, "文职人员",
                "你完全不擅长使用近战战斗。\n"
                + "你的所有近战攻击承受 -2 攻击减值。")
            .AddComponent(AttackPenalty("DrawbackNoncombatant", WeaponRangeType.Melee, -2))
            .Configure(false);
    }

    /// <summary>手抖：所有远程攻击 -2。</summary>
    private static void ConfigureShaky(Sprite icon) {
        NewDrawback("DrawbackShaky", ShakyGuid, "手抖",
                "你的远程战斗是个渣。\n"
                + "你的所有远程攻击承受 -2 攻击减值。")
            .AddComponent(AttackPenalty("DrawbackShaky", WeaponRangeType.Ranged, -2))
            .Configure(false);
    }

    private static AttackTypeAttackBonus AttackPenalty(string name, WeaponRangeType type, int value) {
        return new AttackTypeAttackBonus {
            name = "$" + name + "$" + type,
            Type = type,
            AllTypesExcept = false,
            AttackBonus = 1,
            Descriptor = ModifierDescriptor.Penalty,
            Value = new ContextValue { ValueType = ContextValueType.Simple, Value = value },
        };
    }

    /// <summary>可怜虫：某项属性 -2（拆成六项）；属性修正合计 ≥ 8 时不可选。</summary>
    private static void ConfigurePathetic(Sprite icon) {
        var options = new (string name, string guid, string display, StatType stat)[] {
            ("PatheticStrength",     PatheticStrGuid, "力量", StatType.Strength),
            ("PatheticDexterity",    PatheticDexGuid, "敏捷", StatType.Dexterity),
            ("PatheticConstitution", PatheticConGuid, "体质", StatType.Constitution),
            ("PatheticIntelligence", PatheticIntGuid, "智力", StatType.Intelligence),
            ("PatheticWisdom",       PatheticWisGuid, "感知", StatType.Wisdom),
            ("PatheticCharisma",     PatheticChaGuid, "魅力", StatType.Charisma),
        };
        foreach (var o in options) {
            NewDrawback("Drawback" + o.name, o.guid, "可怜虫（" + o.display + "）",
                    "你的某个方面比你能做到的还要糟。\n"
                    + "你的" + o.display + "属性 -2。\n"
                    + "特殊：当你的所有属性调整值（加值减值都算）合计在 8 或以上时，不能选择这个缺陷。")
                .AddComponent(StatPenalty("Drawback" + o.name, o.stat, -2))
                .AddComponent(new PrerequisiteStatModifierSumBelow {
                    name = "$Drawback" + o.name + "$ModSum",
                    MaxSum = 8,
                })
                .Configure(false);
        }
    }

    /// <summary>反应慢：反射豁免 -3。</summary>
    private static void ConfigurePoorReflexes(Sprite icon) {
        NewDrawback("DrawbackPoorReflexes", PoorReflexesGuid, "反应慢",
                "你经常在急转向的时候转错方向。\n"
                + "你的反射豁免受到 -3 惩罚。")
            .AddComponent(StatPenalty("DrawbackPoorReflexes", StatType.SaveReflex, -3))
            .Configure(false);
    }

    /// <summary>短腿：基本陆地移动速度减半（至少减 5 尺）；需要基础速度 ≥ 20 尺。</summary>
    private static void ConfigureSlow(Sprite icon) {
        NewDrawback("DrawbackSlow", SlowGuid, "短腿",
                "你跑得很慢。\n"
                + "你的基本陆地移动速度减半（至少减少 5 尺的速度）。\n"
                + "特殊：你的基本陆地移动速度必须至少有 20 尺才能选择这个缺陷。")
            .AddComponent(new SlowMovementPenalty { name = "$DrawbackSlow$Speed", MinPenalty = 5 })
            .AddComponent(new PrerequisiteBaseSpeedAtLeast {
                name = "$DrawbackSlow$SpeedPrereq",
                MinSpeed = 20,
            })
            .Configure(false);
    }

    /// <summary>反应迟钝：先攻 -6。</summary>
    private static void ConfigureUnreactive(Sprite icon) {
        NewDrawback("DrawbackUnreactive", UnreactiveGuid, "反应迟钝",
                "你对于察觉危险总是慢一拍。\n"
                + "你的先攻检定承受 -6 惩罚。")
            .AddComponent(StatPenalty("DrawbackUnreactive", StatType.Initiative, -6))
            .Configure(false);
    }

    /// <summary>易受伤：AC -1。</summary>
    private static void ConfigureVulnerable(Sprite icon) {
        NewDrawback("DrawbackVulnerable", VulnerableGuid, "易受伤",
                "你不擅长保护自己。\n"
                + "你的 AC -1。")
            .AddComponent(StatPenalty("DrawbackVulnerable", StatType.AC, -1))
            .Configure(false);
    }

    /// <summary>意志不坚定：意志豁免 -3。</summary>
    private static void ConfigureWeakWill(Sprite icon) {
        NewDrawback("DrawbackWeakWill", WeakWillGuid, "意志不坚定",
                "你很容易动摇并且容易被欺骗。\n"
                + "你的意志豁免受到 -3 惩罚。")
            .AddComponent(StatPenalty("DrawbackWeakWill", StatType.SaveWill, -3))
            .Configure(false);
    }
}
