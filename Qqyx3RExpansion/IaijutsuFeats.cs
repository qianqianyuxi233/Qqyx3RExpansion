using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;

namespace Qqyx3RExpansion;

/// <summary>
/// 拔刀术系列的四个专长。
///
/// <para>
/// <list type="bullet">
/// <item><b>快速拔刀术</b>：可以用迅捷动作进行拔刀术（战士奖励专长 + 普通专长）；</item>
/// <item><b>快速拔刀术（神话）</b>：前置快速拔刀术，可以在自己的回合用自由动作进行拔刀术；</item>
/// <item><b>拔刀专精</b>：拔刀术检定 +4（战士奖励专长）；</item>
/// <item><b>拔刀专精（神话）</b>：前置拔刀专精，拔刀术检定额外 +6。</item>
/// </list>
/// </para>
///
/// <para>
/// 动作类型不是靠「挂组件」实现的：<c>AbilityData.ActionType</c> 虽然有
/// <c>UnitPartAbilityModifiers</c> 这个覆盖入口，但它是「取第一个匹配项」，
/// 同时拥有普通版与神话版时无法保证 Free 优先。所以改用
/// <see cref="IaijutsuActionTypePatch"/> 直接改写 <c>GetDefaultActionType</c> 的返回值，
/// 优先级写死为 神话 &gt; 普通 &gt; 默认（移动动作）。
/// </para>
///
/// <para>
/// 检定加值（+4 / +6）不在这里实现，由 <c>IaijutsuFocusDamage</c> 在掷检定时按特性查找叠加。
/// </para>
/// </summary>
internal static class IaijutsuFeats {

    // ---------- 本 mod 新增蓝图 GUID（发布后不可更改）----------

    internal const string QuickDrawGuid = "c86006531e5148b39eb031e700f78438";
    internal const string MythicQuickDrawGuid = "a4e2c77ecf954f688838a2391755b563";
    internal const string WeaponFocusGuid = "81fcc328f68c445b93c975854905f4b5";
    internal const string MythicWeaponFocusGuid = "a8792765bed040b8a6228375233b8fe9";

    /// <summary>拔刀专精的检定加值</summary>
    internal const int WeaponFocusBonus = 4;
    /// <summary>拔刀专精（神话）的检定加值（与普通版叠加）</summary>
    internal const int MythicWeaponFocusBonus = 6;

    // ---------- 原版参照 ----------

    /// <summary>FeatureSelectionRefs.FighterFeatSelection —— 战士奖励专长池</summary>
    private const string FighterFeatSelectionGuid = "41c8486641f7d6d4283ca9dae4147a9f";
    /// <summary>FeatureSelectionRefs.BasicFeatSelection —— 普通专长池</summary>
    private const string BasicFeatSelectionGuid = "247a4068296e8be42890143f451b4b45";
    /// <summary>FeatureSelectionRefs.MythicFeatSelection —— 神话专长池</summary>
    private const string MythicFeatSelectionGuid = "9ee0f6745f555484299b0a1563b99d81";

    private static UnityEngine.Sprite Icon() {
        try {
            var sneak = BlueprintTool.Get<BlueprintFeature>(FeatureRefs.SneakAttack.ToString());
            return sneak == null ? null : sneak.Icon;
        } catch {
            return null;
        }
    }

    internal static void Configure() {
        var quickDraw = ConfigureQuickDraw();
        var mythicQuickDraw = ConfigureMythicQuickDraw(quickDraw);
        var weaponFocus = ConfigureWeaponFocus();
        var mythicWeaponFocus = ConfigureMythicWeaponFocus(weaponFocus);

        RegisterInFeatLists(quickDraw, mythicQuickDraw, weaponFocus, mythicWeaponFocus);
    }

    // ---------- 快速拔刀术 ----------

    private static BlueprintFeature ConfigureQuickDraw() {
        return FeatureConfigurator.New("QuickDraw", QuickDrawGuid)
            .SetDisplayName(LocalizationTool.CreateString("QuickDraw.Name", "快速拔刀术"))
            .SetDescription(LocalizationTool.CreateString("QuickDraw.Description",
                "你可以使用迅捷动作进行拔刀术。"))
            .SetIcon(Icon())
            .OnConfigure(bp => { bp.Groups = new[] { FeatureGroup.Feat, FeatureGroup.CombatFeat }; })
            .Configure();
    }

    // ---------- 快速拔刀术（神话）----------

    private static BlueprintFeature ConfigureMythicQuickDraw(BlueprintFeature quickDraw) {
        return FeatureConfigurator.New("MythicQuickDraw", MythicQuickDrawGuid)
            .SetDisplayName(LocalizationTool.CreateString("MythicQuickDraw.Name", "快速拔刀术（神话）"))
            .SetDescription(LocalizationTool.CreateString("MythicQuickDraw.Description",
                "你可以在自己的回合用自由动作进行拔刀术。"))
            .SetIcon(Icon())
            .OnConfigure(bp => { bp.Groups = new[] { FeatureGroup.MythicFeat }; })
            .AddComponent(new Kingmaker.Blueprints.Classes.Prerequisites.PrerequisiteFeature {
                name = "$MythicQuickDraw$Prerequisite",
                m_Feature = quickDraw.ToReference<Kingmaker.Blueprints.BlueprintFeatureReference>(),
            })
            .Configure();
    }

    // ---------- 拔刀专精 ----------

    private static BlueprintFeature ConfigureWeaponFocus() {
        return FeatureConfigurator.New("IaijutsuWeaponFocus", WeaponFocusGuid)
            .SetDisplayName(LocalizationTool.CreateString("IaijutsuWeaponFocus.Name", "拔刀专精"))
            .SetDescription(LocalizationTool.CreateString("IaijutsuWeaponFocus.Description",
                "当你使用拔刀术时，拔刀术检定获得 +" + WeaponFocusBonus + " 加值。"))
            .SetIcon(Icon())
            .OnConfigure(bp => { bp.Groups = new[] { FeatureGroup.Feat, FeatureGroup.CombatFeat }; })
            .Configure();
    }

    // ---------- 拔刀专精（神话）----------

    private static BlueprintFeature ConfigureMythicWeaponFocus(BlueprintFeature weaponFocus) {
        return FeatureConfigurator.New("MythicIaijutsuWeaponFocus", MythicWeaponFocusGuid)
            .SetDisplayName(LocalizationTool.CreateString("MythicIaijutsuWeaponFocus.Name", "拔刀专精（神话）"))
            .SetDescription(LocalizationTool.CreateString("MythicIaijutsuWeaponFocus.Description",
                "当你使用拔刀术时，拔刀术检定额外获得 +" + MythicWeaponFocusBonus + " 加值。"))
            .SetIcon(Icon())
            .OnConfigure(bp => { bp.Groups = new[] { FeatureGroup.MythicFeat }; })
            .AddComponent(new Kingmaker.Blueprints.Classes.Prerequisites.PrerequisiteFeature {
                name = "$MythicWeaponFocus$Prerequisite",
                m_Feature = weaponFocus.ToReference<Kingmaker.Blueprints.BlueprintFeatureReference>(),
            })
            .Configure();
    }

    // ---------- 注册进各个专长池 ----------

    /// <summary>
    /// 全部走延迟配置：这三张表都是全局共享的，必须在所有蓝图加载完成后再改，
    /// 否则会被后续加载覆盖（与缺陷系统同一处理，见 <see cref="Drawbacks"/>）。
    /// </summary>
    private static void RegisterInFeatLists(
            BlueprintFeature quickDraw, BlueprintFeature mythicQuickDraw,
            BlueprintFeature weaponFocus, BlueprintFeature mythicWeaponFocus) {
        // 战士奖励专长池：快速拔刀术 + 拔刀专精
        FeatureSelectionConfigurator.For(
                BlueprintTool.GetRef<Kingmaker.Blueprints.BlueprintFeatureSelectionReference>(
                    FighterFeatSelectionGuid))
            .AddToAllFeatures(quickDraw, weaponFocus)
            .Configure(false);

        // 普通专长池：快速拔刀术（通用专长）
        FeatureSelectionConfigurator.For(
                BlueprintTool.GetRef<Kingmaker.Blueprints.BlueprintFeatureSelectionReference>(
                    BasicFeatSelectionGuid))
            .AddToAllFeatures(quickDraw)
            .Configure(false);

        // 神话专长池：两个神话版
        FeatureSelectionConfigurator.For(
                BlueprintTool.GetRef<Kingmaker.Blueprints.BlueprintFeatureSelectionReference>(
                    MythicFeatSelectionGuid))
            .AddToAllFeatures(mythicQuickDraw, mythicWeaponFocus)
            .Configure(false);

        Main.Log?.Log("[拔刀术专长] 已注册 4 个专长并排入延迟配置队列");
    }
}
