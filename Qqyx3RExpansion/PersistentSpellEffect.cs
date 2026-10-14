using System;
using HarmonyLib;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 「超魔（法术持久）」的效果本体：把挂到目标身上的 buff 时长改成 24 小时。
///
/// TabletopTweaks-Core 的 <c>MetamagicExtention</c> 只管「这个超魔花几环、图标、能不能施加、
/// UI 怎么显示」，**效果本身由使用方自己实现**（TomeOfTheFirebird 的 TwinSpell 也是自己写了
/// 一个 <c>TwinSpellMechanics</c>）。
///
/// 挂载点选 <c>ContextActionApplyBuff.CalculateDuration</c>：它是每个「给目标挂 buff」动作
/// 算持续时间的地方；原版「法术延时 Extend」则是在更底层的
/// <c>ContextDurationValue.Calculate</c> 里翻倍。选前者是因为它只影响 buff 的持续时间，
/// 不会波及同样用 ContextDurationValue 的临时负向等级之类的东西。
/// Permanent（返回 null）与 SameDuration（继承父 buff）都不动。
///
/// ⚠ 这个类型没有依赖 BlueprintRoot / Game.Instance 的静态字段，交给 PatchAll 打是安全的
/// （对照 MODDING_NOTES §4.4）。
/// </summary>
[HarmonyPatch]
internal static class PersistentSpellEffect {

    [HarmonyPatch(typeof(ContextActionApplyBuff), "CalculateDuration")]
    [HarmonyPostfix]
    private static void CalculateDuration_Postfix(MechanicsContext context, ref TimeSpan? __result) {
        if (__result == null) return;
        if (!HasPersistentMetamagic(context)) return;

        __result = TimeSpan.FromHours(PersistentSpell.DurationHours);
        Main.Log?.Log($"[法术持久] 持续时间改为 {PersistentSpell.DurationHours} 小时");
    }

    /// <summary>
    /// 沿着 <c>ParentContext</c> 往上找「法术持久」。
    ///
    /// ⚠ 不能只看当前 context：像「赞颂」这种**通过子法术挂 buff** 的法术
    /// （主法术 → <c>ContextActionCastSpell</c> → 子法术 → <c>ContextActionApplyBuff</c>），
    /// 子法术是用 <c>new AbilityData(spell, caster)</c> 单独造的（见 ContextActionCastSpell.RunAction），
    /// **不带父法术的超魔**；而 <c>MechanicsContext.Params</c> 只在自身 <c>m_Params</c> 为 null 时
    /// 才回退到父 context —— 子法术有自己的 Params，所以查不到超魔，持久就不生效。
    /// 往父链上找一层就能修复（祝福术 / 神恩 / 神能是直接挂 buff，本来就正常）。
    /// </summary>
    private static bool HasPersistentMetamagic(MechanicsContext context) {
        for (var current = context; current != null; current = current.ParentContext) {
            if (current.HasMetamagic(PersistentSpell.Persistent24h)) return true;
        }
        return false;
    }
}
