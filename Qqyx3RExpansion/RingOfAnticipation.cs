using BlueprintCore.Blueprints.Configurators.Items;
using BlueprintCore.Blueprints.Configurators.Items.Ecnchantments;
using BlueprintCore.Blueprints.Configurators.Items.Equipment;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 预知戒指（Ring of Anticipation，3.5《魔法物品大全》），6000 GP，戒指位。
///
/// <list type="bullet">
/// <item>侦察检定 +2 表现加值 —— 现成组件 <c>AddStatBonus</c></item>
/// <item>先攻检定双骰取高 —— 自写组件 <see cref="RerollInitiativeTakeBest"/>，
/// 原理见该类的注释</item>
/// </list>
///
/// <para>
/// 图标借「引导之星戒指」—— 星星/指引的意象最接近「预知」，
/// 配色也是原版戒指里少数偏冷紫的。
/// 戒指位不显示 3D 模型（原版 420 枚戒指无一有模型，见 docs 的调研记录）。
/// </para>
/// </summary>
internal static class RingOfAnticipation {

    // ---------- 原版参照 ----------

    /// <summary>Equipment/Rings/RingOfGuidingStarItem（引导之星戒指）—— 借它的图标。</summary>
    private const string RingIconSourceGuid = "b1d2843bfc4edd44cb26e4c45375a406";

    // ---------- 本 mod 新增蓝图 GUID ----------

    private const string FeatureGuid = "d3e073cc755a4d5ab08557babf9fa070";
    private const string EnchantGuid = "1fa4bfe9fb5640dd8e494c15e81637a4";
    private const string ItemGuid    = "1c357f80c6074576b3ca1ece069e5d39";

    // ---------- 商人表 ----------

    private const string JewelerChapter3TableGuid = "9f959fcff8d929042b1be6311d209580";
    private const string GolemGoldJewelryTableGuid = "89567a21927845a0822d8bdaa724b8ce";

    /// <summary>第一章保卫者之心的沃尔吉夫（Woljif），见 QqyxItems 的说明。</summary>
    private const string WoljifShopTableGuid = "a8c181f20ae888b4aa7227c2903207e4";

    /// <summary>侦察（Perception）加值。</summary>
    private const int PerceptionBonus = 2;
    /// <summary>价格（GP）。</summary>
    private const int Cost = 6000;

    internal static void Configure() {
        var icon = BlueprintTool.Get<BlueprintItemEquipmentRing>(RingIconSourceGuid).Icon;

        // 1) Feature：侦察 +2 + 先攻双骰取高
        var feature = FeatureConfigurator.New("RingOfAnticipationFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("RingOfAnticipation.Feature.Name", "预知"))
            .SetDescription(LocalizationTool.CreateString("RingOfAnticipation.Feature.Description",
                "佩戴者在侦察检定上获得 +" + PerceptionBonus + " 表现加值，"
                + "并且在投掷先攻检定时可以投两次骰，采用较好的那次结果。"))
            .SetIcon(icon)
            .SetIsClassFeature()
            .AddComponent(new AddStatBonus {
                name = "$RingOfAnticipation$Perception",
                Stat = StatType.SkillPerception,
                Value = PerceptionBonus,
                Descriptor = ModifierDescriptor.Competence,
            })
            .AddComponent(new RerollInitiativeTakeBest {
                name = "$RingOfAnticipation$Initiative",
            })
            .Configure();

        // 2) 附魔：装备时把 Feature 交给佩戴者
        var enchant = EquipmentEnchantmentConfigurator.New("RingOfAnticipationEnchant", EnchantGuid)
            .AddComponent(new AddUnitFeatureEquipment {
                name = "$RingOfAnticipation$Feature",
                m_Feature = feature.ToReference<BlueprintFeatureReference>(),
            })
            .Configure();

        // 3) 物品本体
        var item = ItemEquipmentRingConfigurator.New("RingOfAnticipationItem", ItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("RingOfAnticipation.Name", "预知戒指"))
            .SetDescriptionText(LocalizationTool.CreateString("RingOfAnticipation.Description",
                "这枚戒指由某种光滑的透明淡紫色玻璃制成，握在手里微微发凉。\n"
                + "佩戴者在侦察检定上获得 +" + PerceptionBonus + " 表现加值。\n"
                + "此外，佩戴者投掷先攻检定时可以投两次骰，并采用较好的那次结果。"))
            .SetFlavorText(LocalizationTool.CreateString("RingOfAnticipation.Flavor",
                "「它总是比你的手快一步地，知道你要往哪儿走。」"))
            .SetIcon(icon)
            .SetCost(Cost)
            .SetEnchantments(enchant.ToReference<BlueprintEquipmentEnchantmentReference>())
            .Configure();

        // 4) 上架：第一章沃尔吉夫 + 第三章 Drezen 珠宝商两位
        AddToVendor("WoljifShop", WoljifShopTableGuid, ItemGuid);
        AddToVendor("JewelerChapter3", JewelerChapter3TableGuid, ItemGuid);
        AddToVendor("GolemGoldJewelry", GolemGoldJewelryTableGuid, ItemGuid);

        Main.Log?.Log("[物品] 已注册「预知戒指」并挂到 3 张商人表");
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
