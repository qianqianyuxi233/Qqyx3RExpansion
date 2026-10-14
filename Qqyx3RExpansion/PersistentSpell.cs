using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using TabletopTweaks.Core.MechanicsChanges;
using TabletopTweaks.Core.Utilities;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 版超魔专长「超魔（法术持久）」（Complete Arcane 的 Persistent Spell）：
/// 把有固定范围或针对自身的法术持续时间变成 24 小时，占用 +6 环法术位。
///
/// ⚠ 与游戏原版区分：原版把 PF1 的 Persistent Spell（豁免骰两次取低、+2 环）翻成
/// 「超魔（法术顽强）」（`Feats/Metamagic/PersistentSpellFeat`，guid cd26b9fa…），
/// 所以本专长按官方命名格式叫「超魔（法术持久）」，两者不重名。
///
/// **新超魔位、环位花费、标记图标、能否施加、UI 显示全部交给 TabletopTweaks-Core**
/// （本 mod 的前置）：调用一次
/// <c>TabletopTweaks.Core.MechanicsChanges.MetamagicExtention.RegisterMetamagic(...)</c>，
/// TTT 内部的这几个 patch 就会认这个位：
///
///   MetamagicHelper.DefaultCost / SpellIcon          → 查它的注册表拿花费与图标
///   RuleCollectMetamagic.AddMetamagic                → 允许施加
///   RuleApplyMetamagic.OnTrigger                     → 偏好超魔减免
///   SpellbookMetamagicSelectorVM.GetCost/Add/Remove  → 法术书界面
///   UIUtilityTexts.GetMetamagicList / GetMetamagicName → 法术名旁的标记（它把这块 patch
///                                                        延迟到 BlueprintsCache.Init 之后挂，
///                                                        正是为了避免 UIUtilityTexts 静态构造崩溃）
///
/// 用法与 TomeOfTheFirebird 的 `New Content/Feats/TwinSpell.cs` 相同。
/// 本项目自己只剩两件事：
///   1. 给法术蓝图放开这个位（TTT 要求使用方自己做，见 <see cref="OpenToSpells"/>）；
///   2. 「持续 24 小时」的效果本身（TTT 不管效果，见 <see cref="PersistentSpellEffect"/>）。
/// </summary>
internal static class PersistentSpell {

    /// <summary>
    /// 本专长使用的自定义超魔位。**发布后不可更改**（会写进存档的法术书数据）。
    ///
    /// 位分配现状：原版 1..2048（1&lt;&lt;0..1&lt;&lt;11）；
    /// TabletopTweaks-Core 的 `MetamagicExtention.CustomMetamagic` 占 4096、8192、65536 起
    /// （Intensified / Dazing / Rime…Twin），DarkCodex 用的是同一套；
    /// 所以这里取它们都跳过的 **1&lt;&lt;14 = 16384**。
    /// （TTT-Core 用 <c>IsNewMetamagic(m) =&gt; m &gt;= 4096</c> 判断「是不是 mod 加的超魔」。）
    /// </summary>
    public const Metamagic Persistent24h = (Metamagic)(1 << 14);

    /// <summary>比正常法术位高 6 级（3.5 原文）。</summary>
    public const int SpellLevelCost = 6;

    /// <summary>持久化的持续时间：24 小时。</summary>
    public const int DurationHours = 24;

    // ---------- 原版参照物 GUID ----------

    /// <summary>Feats/Metamagic/ExtendSpellFeat —— 超魔（法术延时），本专长的先决条件</summary>
    private const string ExtendSpellFeatGuid = "f180e72e4a9cbaa4da8be9bc958132ef";
    /// <summary>Feats/Metamagic/PersistentSpellFeat —— 原版「超魔（法术顽强）」，借它的图标</summary>
    private const string OriginalPersistentFeatGuid = "cd26b9fa3f734461a0fcedc81cafaaac";

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    private const string FeatGuid = "09d474d0109249e9b0f5bcbaba4319de";

    /// <summary>按原版超魔专长的官方命名格式：超魔（法术XX）。</summary>
    private const string FeatName = "超魔（法术持久）";

    private const string FeatDescription =
        "你可以使你的一个法术持续一天。\n"
        + "先决条件：法术延时。\n"
        + "专长效果：有固定范围的、或针对自身的法术，持续时间可以增加到 24 小时。"
        + "瞬间效果或能量散发的法术不能被该专长影响。\n"
        + "你无需在进行侦测法术（例如「侦测魔法」与「侦测思想」）时保持专注，"
        + "你只会注意到受侦测事物的出现或消失；但进一步的信息则正常要求专注。\n"
        + "持久法术使用比正常等级高 6 级的法术位。";

    internal static void Configure() {
        var feat = ConfigureFeat();

        // 登记进专长池，否则升级 / 创建角色时看不到它
        FeatPools.AddToBasicFeats(feat);
        FeatPools.AddToWizardFeats(feat);

        // 交给 TTT-Core：花费、图标、超魔名、法术书界面、法术名标记全部由它接管
        MetamagicExtention.RegisterMetamagic(
            context: Main.Context,
            metamagic: Persistent24h,
            name: FeatName,
            icon: feat.Icon,
            defaultCost: SpellLevelCost,
            favoriteMetamagic: null,
            metamagicFeat: feat);

        Main.Log?.Log($"[法术持久] 已向 TabletopTweaks-Core 注册超魔位 {(int)Persistent24h}"
            + $"（{FeatName}，花费 +{SpellLevelCost}）");

        OpenToSpells();
    }

    /// <summary>
    /// 专长本体：结构照抄原版超魔专长
    /// （<c>Feats/Metamagic/ExtendSpellFeat.jbp</c>）——AddMetamagicFeat + FeatureTags +
    /// RecommendationRequiresSpellbook + Groups(WizardFeat, Feat)，
    /// 先决条件换成「法术延时」。
    /// </summary>
    private static BlueprintFeature ConfigureFeat() {
        return FeatureConfigurator.New("PersistentSpell3R", FeatGuid)
            .SetDisplayName(LocalizationTool.CreateString("PersistentSpell3R.Name", FeatName))
            .SetDescription(LocalizationTool.CreateString("PersistentSpell3R.Description", FeatDescription))
            // 图标照抄原版「超魔（法术顽强）」
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(OriginalPersistentFeatGuid).Icon)
            // 超魔本体（照原版超魔专长：组件自建，给唯一 name）
            .AddComponent(new AddMetamagicFeat {
                name = "$PersistentSpell3R$AddMetamagicFeat",
                Metamagic = Persistent24h,
            })
            // 先决条件：超魔（法术延时）
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(ExtendSpellFeatGuid))
            // 原版超魔专长都带这两个标签（决定它出现在专长筛选的「超魔」分类里）
            .AddFeatureTagsComponent(FeatureTag.Magic | FeatureTag.Metamagic)
            // 只有会施法的人才会被推荐（原版超魔专长同款）
            .AddComponent(new RecommendationRequiresSpellbook {
                name = "$PersistentSpell3R$RecommendSpellbook",
            })
            // 与所有原版超魔专长一样：法师奖励专长列表 + 普通专长列表
            .SetGroups(FeatureGroup.WizardFeat, FeatureGroup.Feat)
            .Configure();
    }

    /// <summary>
    /// 给法术蓝图放开这个超魔位。
    ///
    /// 原版 <c>RuleCollectMetamagic.AddMetamagic</c> 只允许 <c>Spell.AvailableMetamagic</c> 里
    /// 有的超魔，而法术蓝图里当然不会有我们新造的位；TTT 的做法就是让使用方自己写
    /// （TomeOfTheFirebird 的 <c>TwinSpell.UpdateSpells()</c> 遍历 <c>SpellTools.GetAllSpells()</c>）。
    /// 这里同样用 TTT 的 <see cref="SpellTools.GetAllSpells"/>，并借机落实 3.5 的范围限制。
    /// </summary>
    private static void OpenToSpells() {
        int opened = 0;
        int skipped = 0;

        foreach (var spell in SpellTools.GetAllSpells()) {
            if (spell == null) continue;
            if ((spell.AvailableMetamagic & Persistent24h) == Persistent24h) continue;

            if (!CanBePersisted(spell)) {
                skipped++;
                continue;
            }

            spell.AvailableMetamagic |= Persistent24h;
            opened++;
        }

        Main.Log?.Log($"[法术持久] 已对 {opened} 个法术放开该超魔（另有 {skipped} 个因瞬间/接触被排除）");
    }

    /// <summary>
    /// 哪些法术可以被持久化。按用户确认的判定：
    ///
    ///   ✓ **自身**（<see cref="AbilityRange.Personal"/>）—— 例：神能
    ///   ✓ **固定范围**—— 例：加速术（游戏里这类「以施法者为中心、影响一片区域」的法术
    ///     多半标成 Close，所以 Close / Medium / Long / Custom 一律放行）
    ///   ✗ **接触**（<see cref="AbilityRange.Touch"/>）—— 例：虔诚护盾、树肤术
    ///   ✗ **瞬间效果**（没有持续时间文案）
    ///
    /// ⚠ 这里**不做环位判断**：能不能用得看「基础环位 + 6 是否超过 10 环」，
    /// 而那是由游戏的 <c>RuleCollectMetamagic</c>（<c>m_SpellLevel + DefaultCost &gt; 10</c> 才拒绝）
    /// 以及减耗机制决定的 —— 在这里挡掉会让 5 环以上的法术永远没机会（哪怕有减耗）。
    /// </summary>
    public static bool CanBePersisted(BlueprintAbility spell) {
        if (spell == null) return false;

        // 瞬间效果：没有持续时间
        if (spell.LocalizedDuration == null || !spell.LocalizedDuration.IsSet()) return false;

        // 接触法术不能被持久
        if (spell.Range == AbilityRange.Touch) return false;

        return true;
    }
}
