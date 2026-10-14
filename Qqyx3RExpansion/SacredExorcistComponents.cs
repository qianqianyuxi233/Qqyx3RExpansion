using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics;

namespace Qqyx3RExpansion;

/// <summary>
/// 「仇敌」：当法术目标是拥有指定 fact 的生物（不死生物 / 异界生物）时，
/// 施法者对抗其法术抗力的施法者等级检定获得加值。
///
/// 原版 <c>Kingmaker.Designers.Mechanics.Facts.SpellPenetrationBonus</c> 检查的是
/// **施法者**有没有某个 fact（<c>evt.Initiator.Descriptor.HasFact(...)</c>），
/// 而这里要检查**目标**（<c>evt.Target.Descriptor.HasFact(...)</c>），所以照它抄了一份。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("adbd0e61242c46f4b3eedd5e49b252f8")]
public class SpellPenetrationBonusAgainstTargetFact : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleSpellResistanceCheck>, IRulebookHandler<RuleSpellResistanceCheck>,
    ISubscriber, IInitiatorRulebookSubscriber {

    /// <summary>加值（可以用 Rank，这样同一个仇敌再选一次就 +1）。</summary>
    public ContextValue Value;
    public ModifierDescriptor Descriptor = ModifierDescriptor.UntypedStackable;
    public BlueprintUnitFactReference m_RequiredFact;

    public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt) {
        var fact = m_RequiredFact == null ? null : m_RequiredFact.Get();
        if (fact == null || evt.Target == null || evt.Target.Descriptor == null) return;
        if (!evt.Target.Descriptor.HasFact(fact)) return;

        evt.AddSpellPenetration(Value.Calculate(base.Context), Descriptor);
    }

    public void OnEventDidTrigger(RuleSpellResistanceCheck evt) { }
}
