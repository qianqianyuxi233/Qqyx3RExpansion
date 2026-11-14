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
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 猛禽面具（Raptor's Mask，3.5《魔法物品大全》），3500 GP，面部（眼镜）位。
///
/// <list type="bullet">
/// <item>侦察检定 +5</item>
/// <item>免疫目眩（<see cref="UnitCondition.Dazzled"/>）与目盲（<see cref="UnitCondition.Blindness"/>）</item>
/// </list>
///
/// <para>
/// 两个效果都用现成组件：
/// <c>AddStatBonus{Competence, SkillPerception, 5}</c>（照抄原版 PitchTunerFeature 的写法）
/// 与 <c>AddConditionImmunity{Blindness / Dazzled}</c>
/// （照抄原版「内视护符」AmuletOfInnerSightFeature）。
/// </para>
///
/// <para>
/// 部位用 <see cref="BlueprintItemEquipmentGlasses"/> —— WOTR 的「面部」槽就是眼镜槽，
/// 原版拉兹米尔面具（RazmirMask）、疾咬面具（MaskOfTheFastBites）都用这个类型。
/// 外观借「普通邪教徒面具」的模型（<c>KEE_Mask_MaskCommonCultist</c>），
/// 是原版面具里最不起眼的一款，戴在脸上不会抢戏。
/// 图标借「疾咬面具」—— 原版现成的面具图案。
/// </para>
/// </summary>
internal static class RaptorsMask {

    // ---------- 原版参照 ----------

    /// <summary>Equipment/DLC4BonusItems/MaskOfTheFastBitesItem —— 借它的图标（面具图案）。</summary>
    private const string MaskIconSourceGuid = "e71b0d384c7d47618fcf27be493ee3fd";

    /// <summary>EquipmentEntities/Masks/KEE_Mask_MaskCommonCultist —— 面部 3D 外观。</summary>
    private const string MaskModelGuid = "ba0c502fccb64bc59df51800ac15c96a";

    // ---------- 本 mod 新增蓝图 GUID ----------

    private const string FeatureGuid = "642fc63f7011482b98245d9b953f85a9";
    private const string EnchantGuid = "eac2f2e4724847ef8f34d9a601b3f7f8";
    private const string ItemGuid    = "ee57a441adc649d7a5a3ff563014dfdb";

    // ---------- 商人表 ----------

    private const string JewelerChapter3TableGuid = "9f959fcff8d929042b1be6311d209580";
    private const string GolemGoldJewelryTableGuid = "89567a21927845a0822d8bdaa724b8ce";

    /// <summary>第一章保卫者之心的沃尔吉夫（Woljif），见 QqyxItems 的说明。</summary>
    private const string WoljifShopTableGuid = "a8c181f20ae888b4aa7227c2903207e4";

    /// <summary>侦察（Perception）加值。</summary>
    private const int PerceptionBonus = 5;
    /// <summary>价格（GP）。</summary>
    private const int Cost = 3500;
    /// <summary>物品等级。</summary>
    private const int ItemLevel = 8;

    internal static void Configure() {
        var icon = BlueprintTool.Get<BlueprintItemEquipmentGlasses>(MaskIconSourceGuid).Icon;

        // 1) Feature：侦察 +5 + 免疫目眩 / 目盲
        var feature = FeatureConfigurator.New("RaptorsMaskFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("RaptorsMask.Feature.Name", "猛禽之眼"))
            .SetDescription(LocalizationTool.CreateString("RaptorsMask.Feature.Description",
                "佩戴者在侦察检定上获得 +" + PerceptionBonus + " 表现加值，"
                + "并且免疫目眩和目盲。"))
            .SetIcon(icon)
            .SetIsClassFeature()
            .AddComponent(new AddStatBonus {
                name = "$RaptorsMask$Perception",
                Stat = StatType.SkillPerception,
                Value = PerceptionBonus,
                Descriptor = ModifierDescriptor.Competence,
            })
            .AddComponent(new AddConditionImmunity {
                name = "$RaptorsMask$ImmuneBlindness",
                Condition = UnitCondition.Blindness,
            })
            .AddComponent(new AddConditionImmunity {
                name = "$RaptorsMask$ImmuneDazzled",
                Condition = UnitCondition.Dazzled,
            })
            .Configure();

        // 2) 附魔：装备时把 Feature 交给佩戴者
        var enchant = EquipmentEnchantmentConfigurator.New("RaptorsMaskEnchant", EnchantGuid)
            .AddComponent(new AddUnitFeatureEquipment {
                name = "$RaptorsMask$Feature",
                m_Feature = feature.ToReference<BlueprintFeatureReference>(),
            })
            .Configure();

        // 3) 物品本体（面部槽 → BlueprintItemEquipmentGlasses）
        var item = ItemEquipmentGlassesConfigurator.New("RaptorsMaskItem", ItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("RaptorsMask.Name", "猛禽面具"))
            .SetDescriptionText(LocalizationTool.CreateString("RaptorsMask.Description",
                "这副面具用灰白色的猛禽头骨和皮革制成，眼窝里嵌着两片打磨过的琥珀。\n"
                + "佩戴者在侦察检定上获得 +" + PerceptionBonus + " 表现加值，"
                + "并且免疫目眩和目盲。"))
            .SetFlavorText(LocalizationTool.CreateString("RaptorsMask.Flavor",
                "「它不再需要抬头，也看得见整片天空。」"))
            .SetIcon(icon)
            .SetCost(Cost)
            .SetCR(ItemLevel)
            .SetEnchantments(enchant.ToReference<BlueprintEquipmentEnchantmentReference>())
            .SetEquipmentEntity(BlueprintTool.GetRef<KingmakerEquipmentEntityReference>(MaskModelGuid))
            .Configure();

        // 4) 上架：第一章沃尔吉夫 + 第三章 Drezen 珠宝商两位
        AddToVendor("WoljifShop", WoljifShopTableGuid, ItemGuid);
        AddToVendor("JewelerChapter3", JewelerChapter3TableGuid, ItemGuid);
        AddToVendor("GolemGoldJewelry", GolemGoldJewelryTableGuid, ItemGuid);

        Main.Log?.Log("[物品] 已注册「猛禽面具」并挂到 3 张商人表");
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
