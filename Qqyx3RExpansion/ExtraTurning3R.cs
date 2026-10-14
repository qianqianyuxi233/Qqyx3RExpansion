using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 专长「额外驱散 Extra Turning」：每天可以额外驱散/斥喝不死生物 4 次，可多次选取。
///
/// 游戏原版就有 PF1 版的「额外引导 Extra Channel」（`Feats/ExtraChannel.jbp`，
/// guid `cd9f19775bd9d3343a31a065e93f0c47`），结构与本专长完全同构，只是每次 +2 次（PF1 数值）、
/// 前置是 `ChannelEnergyFeature`、`Ranks = 10`（可选取 10 次）。
///
/// 本专长照抄它，只把 `IncreaseResourceAmount.Value` 从 2 改成 **4**（3.5 的 Extra Turning 数值）。
/// 图标沿用原版「额外引导」。
/// </summary>
internal static class ExtraTurning3R {

    /// <summary>本 mod 新增蓝图 GUID（发布后不可更改）</summary>
    private const string FeatGuid = "90128c96adf74aff851e415f9421c38d";

    /// <summary>Feats/ExtraChannel —— 原版「额外引导」，借它的图标（+2 次）</summary>
    private const string ExtraChannelGuid = "cd9f19775bd9d3343a31a065e93f0c47";
    /// <summary>Classes/Cleric/ChannelEnergyFeature —— 引导能量（驱散/斥喝不死生物）</summary>
    private const string ChannelEnergyFeatureGuid = "a79013ff4bcd4864cb669622a29ddafb";
    /// <summary>Classes/Cleric/ChannelEnergyResource —— 引导能量次数</summary>
    private const string ChannelEnergyResourceGuid = "5e2bba3e07c37be42909a12945c27de7";

    internal static void Configure() {
        var source = BlueprintTool.Get<BlueprintFeature>(ExtraChannelGuid);

        var feat = FeatureConfigurator.New("ExtraTurning3R", FeatGuid)
            .SetDisplayName(LocalizationTool.CreateString("ExtraTurning3R.Name", "额外驱散（3r）"))
            .SetDescription(LocalizationTool.CreateString("ExtraTurning3R.Description",
                "你可以比平常更频繁地驱散或斥喝不死生物。\n"
                + "先决条件：引导能量（驱散或斥喝不死生物）能力。\n"
                + "专长效果：你每天可以额外驱散或斥喝不死生物 4 次。\n"
                + "特殊：你可以多次选取本专长，效果累加。"))
            // 图标照抄原版「额外引导」
            .SetIcon(source == null ? null : source.Icon)
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(ChannelEnergyFeatureGuid))
            // 3.5 是 +4 次（原版额外引导是 +2 次）
            .AddComponent(new IncreaseResourceAmount {
                name = "$ExtraTurning3R$Increase",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(ChannelEnergyResourceGuid),
                Value = 4,
            })
            // 神圣驱魔师的第二个引导槽位：同一个「增加引导次数」的效果也要加到它上面
            // （这样总次数才会翻倍，例如牧师 + 额外驱散 = 两个槽各 +4）
            .AddComponent(new IncreaseResourceAmount {
                name = "$ExtraTurning3R$SacredExorcistSlot",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(
                    SacredExorcist.ChannelResourceGuid),
                Value = 4,
            })
            .AddFeatureTagsComponent(FeatureTag.ClassSpecific)
            // 原版额外引导可以选 10 次，这里保持一致
            .SetRanks(10)
            .SetGroups(FeatureGroup.Feat)
            .Configure();

        FeatPools.AddToBasicFeats(feat);
        Main.Log?.Log("[额外驱散] 已注册：每次 +4 次引导能量，最多可选取 10 次");
    }
}
