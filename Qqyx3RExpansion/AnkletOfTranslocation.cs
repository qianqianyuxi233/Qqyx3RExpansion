using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Items;
using BlueprintCore.Blueprints.Configurators.Items.Ecnchantments;
using BlueprintCore.Blueprints.Configurators.Items.Equipment;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.ResourceLinks;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;

namespace Qqyx3RExpansion;

/// <summary>
/// 移位脚镯（Anklet of Translocation，3.5《魔法物品大全》），1400 GP，鞋位。
///
/// <list type="bullet">
/// <item>激活：迅捷动作（指令词）</item>
/// <item>次数：每天 2 次</item>
/// <item>效果：瞬移 10 尺；目的地必须可见、中间无阻碍，
/// 不能落进其他生物占据的位置或坚固物体中</item>
/// </list>
///
/// <para>
/// 机制借用原版「恶魔传送」（<c>Mythic/Demon/DemonSpells/DemonTeleport.jbp</c>）的
/// <see cref="AbilityCustomTeleportation"/>：它负责真正的位移，并自带
/// 「目标点必须在 NavMesh 上、不在战争迷雾里」的校验 —— 正好对上
/// 「看得见、中间没阻碍、不是坚固物体」。
/// 本 mod 的版本<b>不要特效</b>（消失/现身 PrefabLink 留空、时长 0），
/// 所以视觉上就是「一闪即到」。
/// </para>
///
/// <para>
/// ⚠ 原版没有"最大距离"接口（只有 <c>IAbilityMinRangeProvider</c>），
/// <c>Range</c> 只影响 UI 上画的圈。所以 10 尺的<b>硬上限</b>由
/// <see cref="AnkletOfTranslocationRangeLimit"/> 这个自定义目标限制组件保证。
/// </para>
///
/// <para>
/// 外观：<b>不挂 3D 模型</b>。脚镯在游戏的外观库里没有对应物，而原版 95 双靴子里
/// 有 64 双同样不带模型 —— 穿上就显示角色原本的鞋，这是正常表现。
/// 图标借「变色龙戒指」—— 同为金属圆环，形状与原物最接近。
/// </para>
/// </summary>
internal static class AnkletOfTranslocation {

    // ---------- 原版参照 ----------

    /// <summary>
    /// Equipment/Rings/ChameleonRingItem（变色龙戒指）—— 借它的图标。
    /// 类型同为「圆环」类物品，且不是剧情独占用品。
    /// </summary>
    private const string RingIconSourceGuid = "072ddfdddd30f3c428e18cb70b566493";

    // ---------- 本 mod 新增蓝图 GUID ----------

    private const string FeatureGuid  = "f3a4ab72b0cd4241b8bc31d791b563a1";
    private const string ResourceGuid = "5ff02390e5534741b4c268a2f85016b9";
    private const string AbilityGuid  = "1f37a496a3384e73a4c3add4431380e1";
    private const string EnchantGuid  = "d80d101e98a0424b99eebbbffc8b59e5";
    private const string ItemGuid     = "960949ead5934a47a52aa883e1ca5333";

    // ---------- 商人表 ----------
    //
    // ⚠ 只能挂到「真的被某个商人 NPC 引用」的表上，判定方法见 QqyxItems 的注释。
    //    脚镯算饰品 → 珠宝商；同时挂裁缝（原版裁缝卖斗篷/手套/衬衫/护腕这类穿戴物）。
    private const string JewelerChapter3TableGuid = "9f959fcff8d929042b1be6311d209580";
    private const string GolemGoldJewelryTableGuid = "89567a21927845a0822d8bdaa724b8ce";
    private const string TailorChapter3TableGuid = "9b80d696f04aaef4cb4e13eadcc66731";

    /// <summary>第一章保卫者之心的沃尔吉夫（Woljif），见 QqyxItems 的说明。</summary>
    private const string WoljifShopTableGuid = "a8c181f20ae888b4aa7227c2903207e4";

    /// <summary>每日使用次数。</summary>
    private const int UsesPerDay = 2;
    /// <summary>传送距离（尺）。</summary>
    private const int RangeFeet = 10;
    /// <summary>价格（GP）。</summary>
    private const int Cost = 1400;

    internal static void Configure() {
        var icon = BlueprintTool.Get<BlueprintItemEquipmentRing>(RingIconSourceGuid).Icon;

        // 1) 资源：每天 2 次
        var resource = AbilityResourceConfigurator.New("AnkletOfTranslocationResource", ResourceGuid)
            .SetMaxAmount(ResourceAmountBuilder.New(UsesPerDay))
            .Configure();

        // 2) 能力：迅捷动作，点选地面，瞬移到 10 尺内
        var ability = AbilityConfigurator.New("AnkletOfTranslocationAbility", AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString("AnkletOfTranslocation.Ability.Name", "移位"))
            .SetDescription(LocalizationTool.CreateString("AnkletOfTranslocation.Ability.Description",
                "以一个迅捷动作，瞬间传送到 " + RangeFeet + " 尺内的一个位置。"
                + "\n目的地必须是你能看到的地方，且中间没有阻碍；"
                + "你不能传送到其他生物占据的位置，也不能传进坚固的物体中。"
                + "\n每天可以使用 " + UsesPerDay + " 次。"))
            .SetIcon(icon)
            .SetType(AbilityType.Supernatural)
            .SetRange(AbilityRange.Custom)
            .SetCustomRange(RangeFeet.Feet())
            .SetCanTargetPoint(true)
            .SetCanTargetEnemies(false)
            .SetCanTargetFriends(false)
            .SetCanTargetSelf(false)
            .SetActionType(UnitCommand.CommandType.Swift)
            .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: ResourceGuid)
            // 10 尺硬上限 + 落点不能有别人（原版没有最大距离接口，必须自己保证）
            .AddComponent(new AnkletOfTranslocationRangeLimit { name = "$Anklet$RangeLimit" })
            // 真正的位移。特效留空 → 视觉上直接瞬移，没有消失/现身动画。
            // ⚠ DisappearFx / AppearFx 在引擎里标注为 [ValidateNotNull]，但运行时允许为空：
            //    AbilityCustomTeleportation.CreateSettings 会调 .Load()，而 WeakResourceLink.Load()
            //    对空 AssetId 直接返回 null，随后 FxHelper.SpawnFxOnUnit(null) 内部有 null 判断，
            //    所以这里传空 PrefabLink 是安全的（传 null 反而会 NRE）。
            .AddComponent(new AbilityCustomTeleportation {
                name = "$Anklet$Teleport",
                DisappearFx = new PrefabLink(),
                DisappearDuration = 0f,
                AppearFx = new PrefabLink(),
                AppearDuration = 0f,
                AlongPath = false,
            })
            .Configure();

        // 3) Feature：授予能力与每日次数
        var feature = FeatureConfigurator.New("AnkletOfTranslocationFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("AnkletOfTranslocation.Feature.Name", "移位脚镯"))
            .SetDescription(LocalizationTool.CreateString("AnkletOfTranslocation.Feature.Description",
                "佩戴者每天可以两次瞬移 " + RangeFeet + " 尺。"))
            .SetIcon(icon)
            .SetIsClassFeature()
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { AbilityGuid })
            .AddAbilityResources(resource: resource, restoreAmount: true)
            .Configure();

        // 4) 附魔：装备时把 Feature 交给佩戴者
        var enchant = EquipmentEnchantmentConfigurator.New("AnkletOfTranslocationEnchant", EnchantGuid)
            .AddComponent(new AddUnitFeatureEquipment {
                name = "$Anklet$Feature",
                m_Feature = feature.ToReference<BlueprintFeatureReference>(),
            })
            .Configure();

        // 5) 物品本体（不设 EquipmentEntity → 穿上不改变角色外观）
        var item = ItemEquipmentFeetConfigurator.New("AnkletOfTranslocationItem", ItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("AnkletOfTranslocation.Name", "移位脚镯"))
            .SetDescriptionText(LocalizationTool.CreateString("AnkletOfTranslocation.Description",
                "这条银灰色的脚镯上刻着一圈极细的符文，戴在脚踝上几乎感觉不到重量。\n"
                + "每天两次，佩戴者可以以一个迅捷动作瞬间传送至多 " + RangeFeet + " 尺。"
                + "目的地必须是佩戴者能看到的位置，且中间没有阻碍；"
                + "他不能传送到其他生物占据的地方，也不能传进坚固的物体中。"))
            .SetFlavorText(LocalizationTool.CreateString("AnkletOfTranslocation.Flavor",
                "「步子迈出去的瞬间，脚下的路已经不在了。」"))
            .SetIcon(icon)
            .SetCost(Cost)
            .SetEnchantments(enchant.ToReference<BlueprintEquipmentEnchantmentReference>())
            // ⚠ 物品触发的能力，动作类型由**物品**上的这个组件决定，不看能力蓝图的 ActionType
            //   （AbilityData.GetDefaultActionType：SourceItem != null 时若没有该组件就强制 Standard）。
            .AddComponent(new CustomItemAbilityActionType {
                name = "$Anklet$SwiftAction",
                m_ActionType = UnitCommand.CommandType.Swift,
            })
            .Configure();

        // 6) 上架：第三章 Drezen 珠宝商两位 + 裁缝 + 第一章沃尔吉夫
        AddToVendor("JewelerChapter3", JewelerChapter3TableGuid, ItemGuid);
        AddToVendor("GolemGoldJewelry", GolemGoldJewelryTableGuid, ItemGuid);
        AddToVendor("TailorChapter3", TailorChapter3TableGuid, ItemGuid);
        AddToVendor("WoljifShop", WoljifShopTableGuid, ItemGuid);

        Main.Log?.Log("[物品] 已注册「移位脚镯」并挂到 4 张商人表");
    }

    /// <summary>
    /// 往商人的贩卖表里加一件物品（<c>BlueprintSharedVendorTable</c> 没有物品数组字段，
    /// 商品是一个个 <c>LootItemsPackFixed</c> 组件堆出来的，见 QqyxItems 的同名方法）。
    /// </summary>
    private static void AddToVendor(string name, string tableGuid, string itemGuid) {
        SharedVendorTableConfigurator.For(BlueprintTool.GetRef<BlueprintSharedVendorTableReference>(tableGuid))
            .AddComponent(new LootItemsPackFixed {
                name = "$Vendor$" + name + "$" + itemGuid,
                m_Item = new LootItem {
                    m_Type = LootItemType.Item,
                    m_Item = BlueprintTool.GetRef<BlueprintItemReference>(itemGuid),
                },
                m_Count = 1,
            })
            .Configure();
    }
}
