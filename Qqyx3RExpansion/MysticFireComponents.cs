using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 秘火戒指（Ring of Mystic Fire）的「下一次火焰法术额外造成 Nd6 伤害」。
///
/// 为什么不用原版的 <c>AddSpellDiceBonusTrigger</c>：
/// 它在「原法术骰面 ≥ 本组件的骰面」时会把骰子改成**每骰固定加值**
/// （<c>Rolls × DiceBonuses</c>）—— 对灼热射线（3d6）会变成固定 +12 而不是 4d6。
/// 本 mod 需要的是**真掷 4d6**，所以自己实现一份，其余逻辑与它一致：
///
/// <list type="bullet">
/// <item>只在 <c>RuleCalculateDamage</c> 阶段动手，因此**不会被暴击/豁免影响**，
/// 但会与法术自身的伤害一起被强效 / 极效放大（这是用户明确要的联动）；</item>
/// <item>遍历 <c>DamageBundle</c> 里**每一条**伤害，所以灼热射线这种一次施法多次结算的法术，
/// 每条射线都会各 +Nd6；</item>
/// <item>判定用 <c>Reason.Ability</c> 的法术描述符 —— 只认"法术造成的伤害"，
/// 因此火焰武器、火焰陷阱之类不会被误加成。</item>
/// </list>
///
/// ⚠ 黑暗之炎的伤害 <c>Reason.Ability</c> 为 null（伤害由 buff 触发），走不到这里，
/// 所以它交给 <c>Darkfire</c> 的「秘火增幅版」buff 单独处理。
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("81bee73b0efe4b3e9268ddd95432bd90")]
public class MysticFireExtraDamage : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleCalculateDamage>, IRulebookHandler<RuleCalculateDamage>,
    ISubscriber, IInitiatorRulebookSubscriber {

    /// <summary>额外骰数（秘火戒指 = 4）。</summary>
    public int DiceCount = 4;
    /// <summary>骰面。</summary>
    public DiceType Dice = DiceType.D6;
    /// <summary>只对带该描述符的法术生效（火焰）。</summary>
    public SpellDescriptorWrapper Descriptor = SpellDescriptor.Fire;

    public void OnEventAboutToTrigger(RuleCalculateDamage evt) {
        var ability = evt.Reason == null ? null : evt.Reason.Ability;
        if (ability == null || ability.Blueprint == null) return;
        if (!ability.SpellDescriptor.HasFlag(Descriptor)) return;

        var dice = new DiceFormula(DiceCount, Dice);
        foreach (BaseDamage damage in evt.DamageBundle) {
            damage.Dice.Modify(dice, base.Fact);
        }
    }

    public void OnEventDidTrigger(RuleCalculateDamage evt) { }
}
