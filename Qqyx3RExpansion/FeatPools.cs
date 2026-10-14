using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;

namespace Qqyx3RExpansion;

/// <summary>
/// 把新专长登记进游戏的「专长选择池」。
///
/// ⚠ 关键点：升级 / 创建角色时能选的专长，来自
/// <c>Classes/Basic/BasicFeatSelection</c>（`247a4068296e8be42890143f451b4b45`）里那份
/// **静态清单**（原版 276 项）—— 光给专长设 <c>Groups = [Feat]</c> 是不会自动出现的。
/// `Groups` 只用于「已拥有之后在角色面板里归到哪一类」，不负责往选择池里登记。
///
/// 社区 mod 全都是手动登记的（Prestige-Plus / MysticalMayhem / DarkCodex）：
/// <code>FeatureSelectionConfigurator.For(FeatureSelectionRefs.BasicFeatSelection)
///           .AddToAllFeatures(feat).Configure();</code>
///
/// 法师的奖励专长是另一个池子 <c>Classes/Wizard/WizardFeatSelection</c>
/// （`8c3102c2ff3b69444b139a98521a4899`，原版 26 项）—— 原版超魔专长（含「法术延时」）
/// 两个池子里都有，所以本 mod 的超魔也照做。
/// </summary>
internal static class FeatPools {

    /// <summary>Classes/Basic/BasicFeatSelection —— 普通专长池</summary>
    private const string BasicFeatSelectionGuid = "247a4068296e8be42890143f451b4b45";
    /// <summary>Classes/Wizard/WizardFeatSelection —— 法师奖励专长池</summary>
    private const string WizardFeatSelectionGuid = "8c3102c2ff3b69444b139a98521a4899";

    /// <summary>登记进普通专长池。</summary>
    internal static void AddToBasicFeats(params Blueprint<BlueprintFeatureReference>[] feats) {
        if (feats == null || feats.Length == 0) return;

        FeatureSelectionConfigurator
            .For(BlueprintTool.GetRef<BlueprintFeatureSelectionReference>(BasicFeatSelectionGuid))
            .AddToAllFeatures(feats)
            .Configure();

        Main.Log?.Log($"[专长池] 已把 {feats.Length} 项登记进 BasicFeatSelection（普通专长）");
    }

    /// <summary>登记进法师奖励专长池。</summary>
    internal static void AddToWizardFeats(params Blueprint<BlueprintFeatureReference>[] feats) {
        if (feats == null || feats.Length == 0) return;

        FeatureSelectionConfigurator
            .For(BlueprintTool.GetRef<BlueprintFeatureSelectionReference>(WizardFeatSelectionGuid))
            .AddToAllFeatures(feats)
            .Configure();

        Main.Log?.Log($"[专长池] 已把 {feats.Length} 项登记进 WizardFeatSelection（法师奖励专长）");
    }
}
