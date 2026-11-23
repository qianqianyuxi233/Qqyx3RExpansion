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
/// 为什么不走 <c>UnitPartAbilityModifiers</c>：它的 <c>HasActionOverride</c> 是
/// 「按注册顺序取第一个匹配项」，而注册顺序取决于特性激活顺序，
/// 同时拥有普通版与神话版时无法保证「自由动作」优先生效。
/// 直接改写 <c>GetDefaultActionType</c> 的返回值，优先级由这里写死，行为确定。
/// </para>
///
/// <para>
/// 只影响拔刀术这一个能力（按蓝图 GUID 精确匹配），不影响任何其它能力。
/// </para>
/// </summary>
[HarmonyPatch(typeof(AbilityData), "GetDefaultActionType")]
internal static class IaijutsuActionTypePatch {

    [HarmonyPostfix]
    public static void Postfix(AbilityData __instance, ref UnitCommand.CommandType __result) {
        if (__instance == null) return;
        var blueprint = __instance.Blueprint;
        if (blueprint == null) return;
        // 只处理拔刀术
        if (blueprint.AssetGuid.ToString() != Iaijutsu.AbilityGuid) return;

        var caster = __instance.Caster;
        if (caster == null || caster.Unit == null) return;

        // 神话版优先：自由动作
        if (HasFeature(caster, IaijutsuFeats.MythicQuickDrawGuid)) {
            __result = UnitCommand.CommandType.Free;
            return;
        }
        // 普通版：迅捷动作
        if (HasFeature(caster, IaijutsuFeats.QuickDrawGuid)) {
            __result = UnitCommand.CommandType.Swift;
        }
    }

    private static bool HasFeature(UnitDescriptor caster, string guid) {
        try {
            var feature = BlueprintTool.Get<BlueprintFeature>(guid);
            return feature != null && caster.HasFact(feature);
        } catch {
            return false;
        }
    }
}
