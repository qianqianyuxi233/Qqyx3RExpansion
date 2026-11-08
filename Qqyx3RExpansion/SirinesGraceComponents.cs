using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 塞壬的恩典：「AC 获得等于你魅力调整值的偏斜加值」。
///
/// ⚠ 为什么不用 <c>ContextRankConfig { BaseValueType = StatBonus, m_Stat = Charisma }</c>
/// 配 <c>AddContextStatBonus</c>：那条路算出来的 rank 是**快照** —— 在 buff 生效那一刻定死，
/// 于是**本法术自己给的 +4 魅力进不去**（AC 只按施法前的魅力算），之后魅力再变也不会更新。
///
/// 改成在 <c>RuleCalculateAC</c> 阶段**实时**读取当前魅力调整值，于是：
/// <list type="bullet">
/// <item>+4 魅力会正确计入 AC（魅力 20 → 24 时，AC 加值从 +5 变成 +6）；</item>
/// <item>期间魅力被其他效果改动，AC 也跟着动 —— 这才是「等于你魅力调整值」的字面意思。</item>
/// </list>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("ac7c5564152c4e528632744d5cad48a2")]
public class SirinesGraceCharismaToAC : UnitFactComponentDelegate,
    ITargetRulebookHandler<RuleCalculateAC>, IRulebookHandler<RuleCalculateAC>,
    ISubscriber, ITargetRulebookSubscriber {

    /// <summary>3.5 原文是偏斜加值。</summary>
    public ModifierDescriptor Descriptor = ModifierDescriptor.Deflection;

    public void OnEventAboutToTrigger(RuleCalculateAC evt) {
        var unit = evt.Target;
        if (unit == null || unit.Stats == null) return;
        int charismaBonus = unit.Stats.Charisma.Bonus;
        if (charismaBonus == 0) return;
        evt.AddModifier(charismaBonus, Descriptor);
    }

    public void OnEventDidTrigger(RuleCalculateAC evt) { }
}
