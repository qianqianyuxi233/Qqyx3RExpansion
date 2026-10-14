using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;

namespace Qqyx3RExpansion;

/// <summary>
/// 「神圣超魔」buff 的组件：行为与游戏原版 <see cref="MetamagicOnNextSpell"/> 完全一致 ——
///
///   1. 开启时把自己注册进 <c>UnitPartSpecialMetamagic</c>（让 AbilityData 也知道这次要加超魔）；
///   2. <c>RuleCalculateAbilityParams</c> 阶段 <c>evt.AddMetamagic(...)</c>：
///      这是**法术参数计算**阶段，法术位早在施法前就定了，所以**不会升环**；
///   3. <c>RuleCastSpell</c> 之后自动 `Buff.Remove()`（用一次就消失）。
///
/// 唯一多出来的一条：**只认神术**（`Spellbook.Blueprint.IsArcane == false`），
/// 奥术法术书直接跳过 —— 原版组件不区分神术/奥术。
///
/// 实现方式是继承原版类并**重新实现** <c>IInitiatorRulebookHandler&lt;RuleCalculateAbilityParams&gt;</c>：
/// 这样既保留原版的注册/移除逻辑（<c>UnitPartSpecialMetamagic.Add</c> 只接受 MetamagicOnNextSpell 类型），
/// 又能用自己的判断取代原方法（C# 的接口重新实现会让 CLR 把接口映射指到子类方法）。
/// </summary>
[AllowedOn(typeof(BlueprintBuff), false)]
[TypeId("8949c972f4b74382be295c16c57c4a6d")]
public class SacredMetamagicOnNextSpell : MetamagicOnNextSpell,
    IInitiatorRulebookHandler<RuleCalculateAbilityParams> {

    public new void OnEventAboutToTrigger(RuleCalculateAbilityParams evt) {
        if (evt.Spell == null || evt.Spellbook == null || evt.Spell.Type != AbilityType.Spell
            || !CheckSpellLevel(evt.AbilityData)) {
            base.Data.Spell = null;
            return;
        }

        // ★ 神圣超魔只灌注给神术；奥术法术书（法师 / 术士 / 巫妖…）不生效
        var spellbook = evt.Spellbook.Blueprint;
        if (spellbook != null && spellbook.IsArcane) {
            base.Data.Spell = null;
            return;
        }

        evt.AddMetamagic(Metamagic);
        base.Data.Spell = evt.AbilityData;
    }

    public new void OnEventDidTrigger(RuleCalculateAbilityParams evt) {
    }
}
