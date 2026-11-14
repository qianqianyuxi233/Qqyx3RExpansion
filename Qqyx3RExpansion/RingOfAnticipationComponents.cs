using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 预知戒指（Ring of Anticipation）的「先攻双骰取高」。
///
/// <para>
/// 为什么必须在 <c>OnEventDidTrigger</c> 里做，而不是 <c>OnEventAboutToTrigger</c>：
/// <see cref="RuleInitiativeRoll"/> 的骰子对象是**在 OnTrigger 里才创建**的 ——
/// <code>
/// public override void OnTrigger(RulebookEventContext context) {
///     this.Modifier = this.Initiator.Stats.Initiative;
///     this.D20 = RulebookEvent.Dice.D20;     // ← 这一刻才有 D20
/// }
/// </code>
/// 所以 AboutToTrigger 阶段 <c>evt.D20</c> 还是 null，没法重投。
/// </para>
///
/// <para>
/// 在 DidTrigger 里重投为什么仍然有效（两个条件同时成立）：
/// <list type="number">
/// <item><see cref="RuleInitiativeRoll.Result"/> 是动态属性 <c>D20 + Modifier</c>，不是缓存值 ——
/// D20 变了 Result 自动跟着变；</item>
/// <item>调用方 <c>UnitCombatPrepareController.Tick()</c> 是
/// <c>TriggerEvent(roll)</c> **返回之后**才读 <c>roll.Result</c> ——
/// 而 TriggerEvent 内部顺序是 AboutToTrigger → OnTrigger(投骰) → DidTrigger(本组件重投) → 返回。</item>
/// </list>
/// 原版 <c>RerollConcealment</c>（重投隐蔽检定）就是同样的写法：<c>evt.Roll.Reroll(base.Fact, true)</c>。
/// </para>
///
/// <para>
/// 副作用是好的：<c>RuleRollDice.ToString()</c> 会把两次投掷都写进战斗日志并用
/// <c>&lt;b&gt;&lt;u&gt;</c> 高亮采用的那颗，玩家能直接看到「双骰取高」生效了。
/// </para>
/// </summary>
[AllowedOn(typeof(BlueprintUnitFact), false)]
[TypeId("fb7310e71fb04fe5b43e3424ef808556")]
public class RerollInitiativeTakeBest : UnitFactComponentDelegate,
    IInitiatorRulebookHandler<RuleInitiativeRoll>, IRulebookHandler<RuleInitiativeRoll>,
    ISubscriber, IInitiatorRulebookSubscriber {

    public void OnEventAboutToTrigger(RuleInitiativeRoll evt) { }

    public void OnEventDidTrigger(RuleInitiativeRoll evt) {
        if (evt == null) return;
        // 先攻被外部指定（例如召唤物继承主人的先攻）时重投没有意义。
        if (evt.IsOverriden) return;
        if (evt.D20 == null) return;
        evt.D20.Reroll(base.Fact, takeBest: true);
    }
}
