using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 通用专长「快速拔刀（Quick Draw）」。
///
/// <para>
/// 原文效果是「以自由动作拔出武器」。游戏里拔出/收起武器本来就不消耗动作，
/// 所以这个专长在引擎里没有可实现的独立效果 —— 做成一张没有组件的专长，
/// 只用来满足「拔刀师」进阶职业的前提。描述里对玩家明说这一点，不藏。
/// </para>
///
/// <para>
/// 它会通过延迟配置加进 <c>BasicFeatSelection</c>，否则玩家根本选不到，进阶前提就永远无法满足。
/// </para>
/// </summary>
internal static class QuickDrawFeat {

    internal const string Guid = "c86006531e5148b39eb031e700f78438";

    private const string Name = "快速拔刀";

    internal static BlueprintFeature Configure() {
        return FeatureConfigurator.New("QuickDraw", Guid)
            .SetDisplayName(LocalizationTool.CreateString("QuickDraw.Name", Name))
            .SetDescription(LocalizationTool.CreateString("QuickDraw.Description",
                "你能够在瞬间拔出武器。\n"
                + "在游戏里拔出或收起武器本来就不消耗动作，因此本专长没有额外的战斗效果；"
                + "它的作用是满足「拔刀师」进阶职业的前提。"))
            .SetIcon(BlueprintTool.Get<BlueprintFeature>(
                FeatureRefs.WeaponFinesse.ToString()).Icon)
            .Configure();
    }
}
