using BlueprintCore.Utils;
using HarmonyLib;
using Kingmaker.Blueprints.Classes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Commands.Base;

namespace Qqyx3RExpansion;

/// <summary>
/// 让「快速拔刀」系列专长改写拔刀术能力的动作类型。
///
/// <para>
/// <b>为什么 patch 属性的 getter 而不是 <c>GetDefaultActionType</c></b>：
/// <c>GetDefaultActionType</c> 只是 <c>ActionType</c> 内部的第一步，外面还有
/// <c>UnitPartAbilityModifiers</c> 覆盖、<c>RuntimeActionType</c> 的降级逻辑等等。
/// 只改第一步时，任何在 <c>ActionType</c> 之上再读一次的地方都可能拿到旧值，
/// 表现就是「有时是迅捷、有时又是移动」。直接 patch <c>get_ActionType</c> 则所有读取方都统一。
/// </para>
///
/// <para>
/// 蓝图缓存做了懒加载：第一次调用可能早于蓝图完全加载，此时不置 <c>s_Loaded</c>，
/// 下次再试，避免把 null 永久缓存下来（那也会表现成「有时生效有时不生效」）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(AbilityData), "get_ActionType")]
internal static class IaijutsuActionTypePatch {

    private static BlueprintFeature s_QuickDraw;
    private static BlueprintFeature s_MythicQuickDraw;
    private static bool s_Loaded;

    [HarmonyPostfix]
    public static void Postfix(AbilityData __instance, ref UnitCommand.CommandType __result) {
        if (__instance == null) return;
        var blueprint = __instance.Blueprint;
        if (blueprint == null) return;
        // 只处理拔刀术，其余能力原样放行
        if (blueprint.AssetGuid.ToString() != Iaijutsu.AbilityGuid) return;

        var caster = __instance.Caster;
        if (caster == null || caster.Unit == null) return;

        EnsureLoaded();

        // 神话版优先：自由动作
        if (s_MythicQuickDraw != null && caster.HasFact(s_MythicQuickDraw)) {
            __result = UnitCommand.CommandType.Free;
            return;
        }
        // 普通版：迅捷动作
        if (s_QuickDraw != null && caster.HasFact(s_QuickDraw)) {
            __result = UnitCommand.CommandType.Swift;
        }
    }

    private static void EnsureLoaded() {
        if (s_Loaded) return;
        try {
            s_QuickDraw = BlueprintTool.Get<BlueprintFeature>(IaijutsuFeats.QuickDrawGuid);
            s_MythicQuickDraw = BlueprintTool.Get<BlueprintFeature>(IaijutsuFeats.MythicQuickDrawGuid);
            // 两个都拿到才算加载完成；否则保持 false，下次重试
            s_Loaded = s_QuickDraw != null && s_MythicQuickDraw != null;
        } catch {
            // 蓝图尚未就绪，留待下次
        }
    }
}
