using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.Utility;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 移位脚镯的<b>距离与落点限制</b>。3.5《魔法物品大全》原文要求三项：
/// 目标必须在 10 尺内、中间没有阻碍、不能落进被其他生物占据或坚固物体所在的位置。
///
/// <para>
/// 为什么必须自己写：原版<b>没有</b>"最大距离"接口。能力的 <c>Range</c>（这里用
/// <c>AbilityRange.Custom</c> + <c>CustomRange = 10 尺</c>）只决定 <b>UI 上画的圈</b>和提示文本，
/// <c>AbilityData.CanTarget()</c> 对"点选地面"这类目标<b>直接 return true</b>，
/// 全程不做距离检查（<c>AbilityData.cs</c> 的 <c>CanTarget</c> / <c>CantTargetReason</c>），
/// 只有 <c>IAbilityMinRangeProvider</c>（最小距离）而没有对应的最大距离接口。
/// 所以 10 尺这个硬上限只能靠一个 <see cref="IAbilityTargetRestriction"/> 组件来保证 ——
/// <c>CanTarget()</c> 会遍历 <c>BlueprintAbility.TargetRestrictions</c>，
/// 任何一项返回 false 就判定目标非法（UI 上点不了，执行时也不接受）。
/// </para>
///
/// <para>
/// "中间没有阻碍"和"不能进坚固物体"由原版 <see cref="Kingmaker.UnitLogic.Abilities.Components.AbilityCustomTeleportation"/>
/// 自带的目标限制覆盖（<c>IsPointInsideNavMesh</c> + 非战争迷雾 + 非禁用浮岛）；
/// 本组件补上它不管的两条：<b>距离</b>和<b>落点被占</b>。
/// </para>
/// </summary>
[TypeId("38d7cf1342a64dc8aaef28341204fb10")]
public class AnkletOfTranslocationRangeLimit : BlueprintComponent, IAbilityTargetRestriction {

    /// <summary>最大传送距离，默认 10 尺。</summary>
    public Feet MaxRange = 10.Feet();

    /// <summary>判定容差（米）；避免网格取整把"刚好 10 尺"判成超距。</summary>
    private const float Tolerance = 0.1f;

    public bool IsTargetRestrictionPassed(UnitEntityData caster, TargetWrapper target) {
        if (caster == null) return false;

        // ① 距离：从施法者身体边缘量起，大体积单位不吃亏。
        var maxMeters = MaxRange.Meters + caster.Corpulence + Tolerance;
        if (caster.DistanceTo(target.Point) > maxMeters) return false;

        // ② 落点不能有别的活着的生物。
        //    （NavMesh 检查已经保证了"不会落进坚固物体里"。）
        var state = Game.Instance == null ? null : Game.Instance.State;
        if (state == null) return true;

        var casterCorpulence = caster.Corpulence;
        foreach (var unit in state.Units) {
            if (unit == null || unit == caster) continue;
            if (unit.Descriptor != null && unit.Descriptor.State.IsDead) continue;
            if (unit.DistanceTo(target.Point) < unit.Corpulence + casterCorpulence) return false;
        }
        return true;
    }

    /// <summary>UI 上鼠标悬停在非法位置时显示的提示。</summary>
    public string GetAbilityTargetRestrictionUIText(UnitEntityData caster, TargetWrapper target) {
        return "目标位置超过 10 尺，或已被其他生物占据。";
    }
}
