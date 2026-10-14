using System;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;

namespace Qqyx3RExpansion;

/// <summary>
/// 「神圣超魔」的资源消耗：**跨两个引导能量槽位**扣次数。
///
/// 神圣驱魔师会拿到第二个引导能量槽位（<see cref="SacredExorcist.ChannelResourceGuid"/>），
/// 用户要的效果是「消耗完一个槽位再用另一个」，总次数 = 两个槽之和。
///
/// ⚠ **必须继承 <c>AbilityResourceLogic</c>**，不能只实现 <c>IAbilityResourceLogic</c>：
/// <c>AbilityData.AbilityResourceLogic</c>（AbilityData.cs:515）是
/// <c>Blueprint.GetComponent&lt;AbilityResourceLogic&gt;()</c> —— **按具体类取**，
/// 只实现接口的组件取不到，结果是 <c>ResourceLogic</c> 为 null、**一点资源都不消耗**
/// （本项目踩过：第一版只实现接口 → 施法后次数完全不动）。
///
/// 主资源用自己加的 <c>m_PrimaryResource</c> 字段（基类的 <c>m_RequiredResource</c> 是 private，
/// 而且基类的 <c>RequiredResource</c> 不是 virtual），所以这里用 <c>new</c> 隐藏它，
/// 并重新实现 <c>IAbilityResourceLogic</c> 让接口调用也走我们这份。
/// </summary>
[AllowedOn(typeof(BlueprintAbility), false)]
[TypeId("8fb595283bd84d65bca61adc3e333e6b")]
public class SacredMetamagicResourceLogic : AbilityResourceLogic,
    IAbilityRestriction, IAbilityResourceLogic {

    /// <summary>主槽位：牧师的引导能量资源。</summary>
    public BlueprintAbilityResourceReference m_PrimaryResource;
    /// <summary>副槽位：神圣驱魔师的第二个引导能量资源。</summary>
    public BlueprintAbilityResourceReference m_SecondaryResource;

    public new BlueprintAbilityResource RequiredResource =>
        m_PrimaryResource == null ? null : m_PrimaryResource.Get();

    public override bool IsAbilityRestrictionPassed(AbilityData ability) {
        if (ability == null || ability.Caster == null) return false;

        var resources = ability.Caster.Resources;
        int available = GetAmount(resources, m_PrimaryResource) + GetAmount(resources, m_SecondaryResource);
        return available >= Amount;
    }

    public override void Spend(AbilityData ability) {
        if (ability == null || ability.Caster == null) return;

        var unit = ability.Caster.Unit;
        if (unit == null || unit.Blueprint.IsCheater) return;
        if (!IsAbilityRestrictionPassed(ability)) return;

        // 先扣主槽（牧师引导能量），不够的部分再扣副槽（神圣驱魔师的第二个槽位）
        var resources = unit.Descriptor.Resources;
        int remaining = Amount;
        remaining -= SpendFrom(resources, m_PrimaryResource, remaining);
        if (remaining > 0) SpendFrom(resources, m_SecondaryResource, remaining);
    }

    public override string GetAbilityRestrictionUIText() {
        return LocalizedTexts.Instance.Reasons.NoResources;
    }

    private static int GetAmount(UnitAbilityResourceCollection resources,
                                 BlueprintAbilityResourceReference resourceRef) {
        var resource = resourceRef == null ? null : resourceRef.Get();
        return resource == null ? 0 : resources.GetResourceAmount(resource);
    }

    private static int SpendFrom(UnitAbilityResourceCollection resources,
                                 BlueprintAbilityResourceReference resourceRef, int want) {
        var resource = resourceRef == null ? null : resourceRef.Get();
        if (resource == null || want <= 0) return 0;

        int have = resources.GetResourceAmount(resource);
        int spend = Math.Min(have, want);
        if (spend > 0) resources.Spend(resource, spend);
        return spend;
    }
}
