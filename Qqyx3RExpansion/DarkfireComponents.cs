using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Newtonsoft.Json;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 运行时数据：记下这次 buff 插进手里的是哪个武器实体，移除时要用。
/// 照抄原版 <c>AddKineticistBladeData</c>。
/// </summary>
public class DarkfireWeaponData {
    [JsonProperty]
    public ItemEntityWeapon Applied;
}

/// <summary>
/// 法术「黑暗之炎」：施法后在**主手**生成一把武器（无形、无模型），并在 buff 期间锁住该手槽。
///
/// 为什么不直接用原版念动之刃的 <c>AddKineticistBlade</c>：
/// 它是「插不进去就放弃」——
/// <code>
/// if (!handSlot.CanInsertItem(item, false)) { PFLog.Error("Can't insert…"); return; }
/// </code>
/// 施法者手上只要有武器（牧师常见的钉头锤 / 法杖 / 盾），法术就等于白放。
/// 按用户要求改成「强制塞主手 + 锁住 + 原有物品回背包」：
///
/// <list type="bullet">
/// <item><c>ContextData&lt;ItemSlot.IgnoreLock&gt;.Request()</c> —— 让 <c>IsPossibleInsertItems()</c>
/// 与 <c>CanRemoveItem()</c> 在本次作用域内一律放行（ItemSlot.cs:230 / :283）。</item>
/// <item><c>ItemSlot.InsertItem</c> 自己就会处理原有物品：内部先
/// <c>RemoveItem(false, true)</c>，而 <c>autoMerge = true</c> 会调用
/// <c>item.TryMergeInCollection()</c> 把旧武器送回它原来的背包集合（ItemSlot.cs:317-321）。</item>
/// <item>随后 <c>Lock.Retain()</c> 锁住手槽，玩家不能再用 UI 把火焰武器换掉。</item>
/// </list>
/// </summary>
[AllowedOn(typeof(BlueprintBuff), false)]
[TypeId("d1ef34e107dc4c94b0f28fa841a024c0")]
public class AddDarkfireWeapon : UnitBuffComponentDelegate<DarkfireWeaponData> {

    /// <summary>要生成的武器蓝图（本 mod 的「黑暗之炎（投掷）」）。</summary>
    [SerializeField]
    public BlueprintItemWeaponReference m_Weapon;

    public override void OnActivate() {
        base.OnActivate();

        var weapon = m_Weapon == null ? null : m_Weapon.Get();
        if (weapon == null) {
            Main.Log?.Log("[黑暗之炎] m_Weapon 为空，跳过");
            return;
        }
        var owner = base.Owner;
        if (owner == null) return;

        // 念动之刃同款：带运行时武器的单位不参与存档优化（否则重载后武器会丢）。
        owner.MarkNotOptimizableInSave(true);

        var applied = weapon.CreateEntity<ItemEntityWeapon>();
        applied.MakeNotLootable();
        base.Data.Applied = applied;

        var handSlot = owner.Body.PrimaryHand;
        using (ContextData<ItemSlot.IgnoreLock>.Request()) {
            if (!handSlot.InsertItem(applied)) {
                base.Data.Applied = null;
                Main.Log?.Log("[黑暗之炎] 插入主手失败");
                return;
            }
        }

        Main.Log?.Log($"[黑暗之炎] {owner.CharacterName} 获得「{applied.Name}」"
            + $"（伤害骰 {applied.Blueprint.m_DamageDice}）");
    }

    public override void OnTurnOn() {
        var holdingSlot = base.Data.Applied == null ? null : base.Data.Applied.HoldingSlot;
        holdingSlot?.Lock.Retain();
    }

    public override void OnTurnOff() {
        var holdingSlot = base.Data.Applied == null ? null : base.Data.Applied.HoldingSlot;
        holdingSlot?.Lock.Release();
    }

    public override void OnDeactivate() {
        base.OnDeactivate();

        var applied = base.Data.Applied;
        if (applied == null) return;

        var holdingSlot = applied.HoldingSlot;
        if (holdingSlot != null) {
            // autoMerge：先摘下来（此时手槽仍被 Lock 锁着，必须靠 IgnoreLock 放行）
            using (ContextData<ItemSlot.IgnoreLock>.Request()) {
                holdingSlot.RemoveItem(true);
            }
        }
        using (ContextData<ItemsCollection.SuppressEvents>.Request()) {
            var collection = applied.Collection;
            if (collection != null) collection.Remove(applied);
        }
        base.Data.Applied = null;
    }
}
