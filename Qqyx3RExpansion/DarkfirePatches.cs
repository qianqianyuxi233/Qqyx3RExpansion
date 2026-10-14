using System;
using HarmonyLib;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;

namespace Qqyx3RExpansion;

/// <summary>
/// 让 TabletopTweaks 的「强化法术」也能顶高 <see cref="ContextRankConfig"/> 的骰数上限。
///
/// 背景（本项目实测踩到）：
/// <list type="bullet">
/// <item>原版 <c>Metamagic.Intensified = 2048</c>，DLC6 的强化法术专长
/// （<c>Feats/DLC6/IntensifiedSpell.jbp</c>）用的就是这一位；</item>
/// <item>但 TabletopTweaks-Core 的 <c>MetamagicExtention</c> 自己另开了一位：
/// <c>Intensified = 4096</c>，并用 <c>IsNewMetamagic(m) =&gt; m &gt;= 4096</c> 判断；</item>
/// <item>而原版 <c>ContextRankConfig.ApplyMinMax</c> 里写死了
/// <c>if (m_AffectedByIntensifiedMetamagic &amp;&amp; context.HasMetamagic(Metamagic.Intensified)) num += 5;</c>
/// —— 只认 2048。</item>
/// </list>
///
/// 后果有两个，第二个更致命：
/// <list type="number">
/// <item>玩法术的法术蓝图 <c>AvailableMetamagic</c> 里没有 4096 时，超魔栏里
/// **根本勾不上**「强化法术」（能否勾选由 <c>RuleCollectMetamagic.AddMetamagic</c> 决定）；</item>
/// <item>就算勾上了，骰数上限也不会涨（本项目的黑暗之炎 5d6 永远变不成 10d6）。</item>
/// </list>
///
/// 本 Postfix 处理第 2 点：在「本次施法带 4096、且没带 2048」时补上同样的 +5。
/// 已带 2048 时直接返回，避免与原版逻辑重复叠加。
/// 第 1 点在法术自己的 <c>AvailableMetamagic</c> 里加上 <see cref="TttIntensified"/> 解决。
/// </summary>
[HarmonyPatch(typeof(ContextRankConfig), "ApplyMinMax")]
internal static class ContextRankConfigTttIntensifiedPatch {

    /// <summary>
    /// TabletopTweaks-Core 的「强化法术」占用的位（`MetamagicExtention.Intensified = 4096`）。
    /// 值来自 TTT-Core.dll 的反编译结果，发布后不要改。
    /// </summary>
    internal const Metamagic TttIntensified = (Metamagic)4096;

    private static void Postfix(ContextRankConfig __instance, int value, bool unlimited,
                                MechanicsContext context, ref int __result) {
        if (!__instance.m_AffectedByIntensifiedMetamagic || context == null) return;
        if (!context.HasMetamagic(TttIntensified)) return;
        // 原版位已经 +5 过了，别重复
        if (context.HasMetamagic(Metamagic.Intensified)) return;

        int result = value;
        if (__instance.m_UseMin) result = Math.Max(result, __instance.m_Min);
        if (__instance.m_UseMax && !unlimited) result = Math.Min(result, __instance.m_Max + 5);
        __result = result;
    }
}
