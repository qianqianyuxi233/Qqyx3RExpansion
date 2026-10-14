using BlueprintCore.Blueprints.Configurators.Items;
using BlueprintCore.Blueprints.Configurators.Items.Ecnchantments;
using BlueprintCore.Blueprints.Configurators.Items.Equipment;
using BlueprintCore.Blueprints.Configurators.UnitLogic.ActivatableAbilities;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 两件增加「每日引导能量次数」的小物品（3.5《Complete Divine》等）。
///
/// 物品的机制和法术/专长完全不同，是**两层**结构（照原版
/// <c>Equipment/Rings/RingOfProtection1.jbp</c> + <c>Deflection1.jbp</c> 抄的）：
///
/// <code>
/// 物品蓝图（BlueprintItemEquipmentNeck / BlueprintItemEquipmentUsable / ...）
///   ├─ 名字 / 描述 / 图标 / 价格
///   └─ m_Enchantments → 附魔蓝图（BlueprintEquipmentEnchantment）
///                         └─ AddUnitFeatureEquipment → Feature ← 真正的效果写在这里
/// </code>
///
/// 两件物品都同时给**牧师的原版引导能量资源**和**神圣驱魔师的第二个槽位资源**
/// 加次数，所以对神圣驱魔师来说总次数同样翻倍。
/// </summary>
internal static class QqyxItems {

    /// <summary>Classes/Cleric/ChannelEnergyResource —— 牧师的引导能量次数</summary>
    private const string ClericChannelResourceGuid = "5e2bba3e07c37be42909a12945c27de7";

    // ---------- 本 mod 新增蓝图 GUID ----------
    private const string ReliquaryFeatureGuid = "186f28f9285647509c7b283422c50af1";
    private const string ReliquaryEnchantGuid = "de9f804b30c44d6aa84479845a18458e";
    private const string ReliquaryItemGuid = "9bd5b5c757464809a3860d027a746232";
    private const string NightstickBuffGuid = "d56fdeb6550c4d99a659adc6d65bfbea";
    private const string NightstickToggleGuid = "218724632b40468287df9533d66c6eb1";
    private const string NightstickItemGuid = "35a640243e204de6a4148bb1a3a95b67";

    // ---------- 图标（借原版的）----------
    /// <summary>Equipment/Amulets/AmuletOfNaturalArmor1 的图标（护符）</summary>
    private const string AmuletIconSourceGuid = "209960f8d60e23a4c8e75d14e2261263";
    /// <summary>Equipment/DLC6BonusItems/MetamagicRodNormalIntensified 的图标（超魔权杖）</summary>
    private const string MetamagicRodIconSourceGuid = "a35044c76afa45c69f4baedf13ad5ac9";

    // ---------- 商人表 ----------
    private const string JewelerChapter3TableGuid = "9f959fcff8d929042b1be6311d209580";
    private const string JewelerChapter5TableGuid = "fad4f5653f1d4943ad21a2dd85ee746b";
    private const string MagicVendorChapter2TableGuid = "f02cf582e915ae343aa489f11dba42aa";
    private const string MagicVendorChapter5TableGuid = "e1d21a0e6c9177d42a1b0fac1d6f8b21";

    internal static void Configure() {
        var holySymbol = ConfigureReliquaryHolySymbol();
        var nightstick = ConfigureNightstick();

        // 遗骨圣徽是颈部奇物 → 给珠宝商（第三章、第五章）
        AddToVendor("JewelerChapter3", JewelerChapter3TableGuid, holySymbol);
        AddToVendor("JewelerChapter5", JewelerChapter5TableGuid, holySymbol);

        // 夜枝是权杖 → 给魔法物品商人（第二章、第五章）
        AddToVendor("WarCampMagicVendor", MagicVendorChapter2TableGuid, nightstick);
        AddToVendor("Chapter5MagicVendor", MagicVendorChapter5TableGuid, nightstick);

        Main.Log?.Log("[物品] 已注册「遗骨圣徽」与「夜枝」，并挂到 4 张商人表");
    }

    // ==================== 遗骨圣徽（Reliquary Holy Symbol）====================

    /// <summary>颈部奇物，2000 GP，每日引导能量次数 +3。</summary>
    private static string ConfigureReliquaryHolySymbol() {
        // 1) Feature：真正生效的效果（+3 次数，两个引导槽位各 +3）
        var feature = FeatureConfigurator.New("ReliquaryHolySymbolFeature", ReliquaryFeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("ReliquaryHolySymbol.Feature.Name", "引导能量次数 +3"))
            .SetDescription(LocalizationTool.CreateString("ReliquaryHolySymbol.Feature.Description",
                "遗骨圣徽使佩戴者每日可以额外引导能量 3 次。"))
            .SetIsClassFeature()
            .AddComponent(new IncreaseResourceAmount {
                name = "$ReliquaryHolySymbol$Cleric",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(ClericChannelResourceGuid),
                Value = 3,
            })
            .AddComponent(new IncreaseResourceAmount {
                name = "$ReliquaryHolySymbol$SacredExorcist",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(
                    SacredExorcist.ChannelResourceGuid),
                Value = 3,
            })
            .Configure();

        // 2) 附魔：装备时把上面的 Feature 给穿戴者
        var enchant = EquipmentEnchantmentConfigurator.New("ReliquaryHolySymbolEnchant", ReliquaryEnchantGuid)
            .AddComponent(new AddUnitFeatureEquipment {
                name = "$ReliquaryHolySymbol$Feature",
                m_Feature = feature.ToReference<BlueprintFeatureReference>(),
            })
            .Configure();

        // 3) 物品本体
        var item = ItemEquipmentNeckConfigurator.New("ReliquaryHolySymbolItem", ReliquaryItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("ReliquaryHolySymbol.Name", "遗骨圣徽"))
            .SetDescriptionText(LocalizationTool.CreateString("ReliquaryHolySymbol.Description",
                "这枚圣徽中嵌着一小块圣人的遗骨。佩戴者每日可以额外引导能量 3 次。"))
            .SetFlavorText(LocalizationTool.CreateString("ReliquaryHolySymbol.Flavor",
                "「骨头记得它生前侍奉的是谁。」"))
            .SetIcon(BlueprintTool.Get<BlueprintItemEquipmentNeck>(AmuletIconSourceGuid).Icon)
            .SetCost(2000)
            .SetEnchantments(enchant.ToReference<BlueprintEquipmentEnchantmentReference>())
            .Configure();

        return item.AssetGuid.ToString();
    }

    // ==================== 夜枝（Nightstick）====================

    /// <summary>
    /// 权杖类物品，7500 GP，每日引导能量次数 +4。
    ///
    /// 权杖在 WOTR 里**不是装备栏位**，原版超魔权杖的做法是
    /// <c>BlueprintItemEquipmentUsable</c> + 一个「激活开关」
    /// （<c>Equipment/DLC6BonusItems/MetamagicRodNormalIntensified.jbp</c>）：
    /// 物品放在背包里，用它自己的开关来激活。
    /// 所以夜枝照抄同一套：物品 → ActivatableAbility → Buff（Buff 上写 +4）。
    /// 开关设成**默认开启**，拿到手就生效。
    /// </summary>
    private static string ConfigureNightstick() {
        var icon = BlueprintTool.Get<BlueprintItemEquipmentUsable>(MetamagicRodIconSourceGuid).Icon;

        // 1) Buff：+4 次数（两个引导槽位各 +4）—— 开关打开时挂着
        var buff = BuffConfigurator.New("NightstickBuff", NightstickBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("Nightstick.Buff.Name", "夜枝"))
            .SetDescription(LocalizationTool.CreateString("Nightstick.Buff.Description",
                "夜枝使持有者每日可以额外引导能量 4 次。"))
            .SetIcon(icon)
            .AddComponent(new IncreaseResourceAmount {
                name = "$Nightstick$Cleric",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(ClericChannelResourceGuid),
                Value = 4,
            })
            .AddComponent(new IncreaseResourceAmount {
                name = "$Nightstick$SacredExorcist",
                m_Resource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(
                    SacredExorcist.ChannelResourceGuid),
                Value = 4,
            })
            .Configure();

        // 2) 激活开关（默认开启，拿到就生效）
        var toggle = ActivatableAbilityConfigurator.New("NightstickToggleAbility", NightstickToggleGuid)
            .SetDisplayName(LocalizationTool.CreateString("Nightstick.Toggle.Name", "夜枝"))
            .SetDescription(LocalizationTool.CreateString("Nightstick.Toggle.Description",
                "开启后，夜枝使持有者每日可以额外引导能量 4 次。"))
            .SetIcon(icon)
            .SetBuff(buff.ToReference<BlueprintBuffReference>())
            .SetIsOnByDefault(true)
            .Configure();

        // 3) 物品本体
        var item = ItemEquipmentUsableConfigurator.New("NightstickItem", NightstickItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("Nightstick.Name", "夜枝"))
            .SetDescriptionText(LocalizationTool.CreateString("Nightstick.Description",
                "这支用染成暗色的木头雕刻成的权杖中嵌入了多位神祇的标记。"
                + "你可以使用夜枝来增加 4 次每日引导能量次数。"))
            .SetFlavorText(LocalizationTool.CreateString("Nightstick.Flavor",
                "木质乌黑，触手微凉，仿佛刚从一个没有月亮的夜晚里取出来。"))
            .SetIcon(icon)
            .SetCost(7500)
            .SetActivatableAbility(toggle.ToReference<BlueprintActivatableAbilityReference>())
            .Configure();

        return item.AssetGuid.ToString();
    }

    // ==================== 加进商人表 ====================

    /// <summary>
    /// 往商人的贩卖表里加一件物品。
    ///
    /// <c>BlueprintSharedVendorTable</c> 本身没有"物品数组"字段 —— 商品是一个个
    /// <c>LootItemsPackFixed</c> 组件（<c>m_Item</c> + <c>m_Count</c>）堆出来的
    /// （见 <c>Items/Vendors/Chapter3/Jeweler_Chapter3VendorTable.jbp</c>：23 个组件全是它）。
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
