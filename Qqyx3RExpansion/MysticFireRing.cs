using System.Collections.Generic;
using BlueprintCore.Blueprints.Configurators.Items;
using BlueprintCore.Blueprints.Configurators.Items.Ecnchantments;
using BlueprintCore.Blueprints.Configurators.Items.Equipment;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.Enums;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;

namespace Qqyx3RExpansion;

/// <summary>
/// 秘火戒指（Ring of Mystic Fire，3.5《魔法物品大全》），7500 GP / 物品等级 11，戒指位。
///
/// 两个效果：
/// <list type="number">
/// <item><b>持续生效</b>：施展带「火焰」描述符的法术时，施法者等级 +1 表现加值
/// —— 用原版 <see cref="IncreaseSpellDescriptorCasterLevel"/>（它按法术描述符筛选，
/// 判定点在 <c>RuleCalculateAbilityParams</c>）。</item>
/// <item><b>每日一次</b>：迅捷动作，让下一次火焰法术额外造成 4d6 火焰伤害
/// —— 给自己挂一个 1 轮的「秘火」buff，buff 上放
/// <see cref="MysticFireExtraDamage"/>（在 <c>RuleCalculateDamage</c> 给法术伤害加骰）。</item>
/// </list>
///
/// 关于"下一次"：因为灼热射线这类法术是**一次施法、多次伤害结算**，
/// buff 不能"第一次伤害后就移除"（否则第 2、3 条射线就吃不到了），
/// 所以做成**持续 1 轮** —— 每天只能用一次由资源保证。
///
/// 黑暗之炎是特例：它的伤害由 buff 触发，<c>Reason.Ability</c> 为 null，
/// 走不到 <see cref="MysticFireExtraDamage"/>；那条联动在 <c>Darkfire.cs</c> 里
/// 通过「秘火增幅版 buff」实现。
/// </summary>
internal static class MysticFireRing {

    // ---------- 原版参照 ----------

    /// <summary>
    /// Equipment/Rings/UniquePF2/Chapter3/RingOfPyromaniatItem（原版「纵火狂戒指」）
    /// —— 借它的图标，火焰主题最贴切。类型同为 BlueprintItemEquipmentRing。
    /// </summary>
    private const string RingIconSourceGuid = "e2f23f658afc6fe48a61f8aea173dc29";

    // ---------- 本 mod 新增蓝图 GUID ----------

    private const string FeatureGuid    = "24c9b806796642e7921e416c3be32276";
    private const string EnchantGuid    = "5ffacf78874e41eb99a4e9028f87081d";
    private const string ItemGuid       = "e02ad51379014d4d816b544f8e4f84da";
    private const string ChargeBuffGuid = "bc1b4dcf6e08414f9735ac397fc7f78e";
    private const string AbilityGuid    = "6e834326d04e459e850de92ce6ea0b7a";
    private const string ResourceGuid   = "23a54644e5c444ea947f29bd477e7b2a";

    /// <summary>增幅 buff 的 GUID 供 <c>Darkfire</c> 判定"是否处于秘火蓄能中"（见 Darkfire.cs）。</summary>
    internal const string ChargeBuffGuidPublic = ChargeBuffGuid;

    // ---------- 商人表 ----------
    //
    // ⚠ 只能挂到「真的被某个商人 NPC 引用」的表上，判定方法见 QqyxItems 的注释。
    //    原版的 Jeweler_Chapter5VendorTable / RE_Chapter5VendorTableMagic 都是孤儿表。
    private const string JewelerChapter3TableGuid  = "9f959fcff8d929042b1be6311d209580";
    private const string GolemGoldJewelryTableGuid = "89567a21927845a0822d8bdaa724b8ce";
    private const string VendorTieflingTableGuid   = "d0a54c0af02eed44488c15108fed0433";

    /// <summary>第一章保卫者之心的沃尔吉夫（Woljif），见 QqyxItems 的说明。</summary>
    private const string WoljifShopTableGuid = "a8c181f20ae888b4aa7227c2903207e4";

    /// <summary>额外伤害骰数。</summary>
    private const int ExtraDice = 4;
    /// <summary>「秘火」buff 的持续时间（轮）—— 覆盖下一次施法的全部伤害结算。</summary>
    private const int ChargeRounds = 1;

    private const string ItemName = "秘火戒指";

    internal static void Configure() {
        var icon = BlueprintTool.Get<BlueprintItemEquipmentRing>(RingIconSourceGuid).Icon;

        // 1) 资源：每日 1 次
        var resource = AbilityResourceConfigurator.New("MysticFireRingResource", ResourceGuid)
            .SetMaxAmount(ResourceAmountBuilder.New(1))
            .Configure();

        // 2) 「秘火」buff：下一次火焰法术 +4d6
        var chargeBuff = BuffConfigurator.New("MysticFireChargeBuff", ChargeBuffGuid)
            .SetDisplayName(LocalizationTool.CreateString("MysticFireCharge.Name", "秘火"))
            .SetDescription(LocalizationTool.CreateString("MysticFireCharge.Description",
                "秘火在戒指中蓄势待发：下一次火焰法术额外造成 " + ExtraDice + "d6 点火焰伤害。"))
            .SetIcon(icon)
            .AddComponent(new MysticFireExtraDamage {
                name = "$MysticFire$ExtraDamage",
                DiceCount = ExtraDice,
                Dice = DiceType.D6,
                Descriptor = SpellDescriptor.Fire,
            })
            .Configure();

        // 3) 启动能力：迅捷动作，消耗每日 1 次
        AbilityConfigurator.New("MysticFireRingAbility", AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString("MysticFireAbility.Name", "秘火：增幅法术"))
            .SetDescription(LocalizationTool.CreateString("MysticFireAbility.Description",
                "以一个迅捷动作激活秘火戒指：你接下来施放的火焰法术会额外造成 " + ExtraDice + "d6 点火焰伤害。"
                + "\n每天可以使用 1 次。"))
            .SetIcon(icon)
            .SetType(AbilityType.Supernatural)
            .SetRange(AbilityRange.Personal)
            .SetActionType(UnitCommand.CommandType.Swift)
            .AddAbilityResourceLogic(amount: 1, isSpendResource: true, requiredResource: ResourceGuid)
            .AddComponent(new AbilityEffectRunAction {
                name = "$MysticFire$ApplyCharge",
                SavingThrowType = Kingmaker.EntitySystem.Stats.SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            m_Buff = BlueprintTool.GetRef<BlueprintBuffReference>(ChargeBuffGuid),
                            Permanent = false,
                            UseDurationSeconds = false,
                            DurationValue = new ContextDurationValue {
                                Rate = DurationRate.Rounds,
                                DiceType = DiceType.Zero,
                                DiceCountValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = 0,
                                },
                                BonusValue = new ContextValue {
                                    ValueType = ContextValueType.Simple,
                                    Value = ChargeRounds,
                                },
                            },
                            DurationSeconds = 0f,
                            IsFromSpell = false,
                            IsNotDispelable = false,
                            ToCaster = true,
                            AsChild = false,
                            SameDuration = false,
                        },
                    },
                },
            })
            .Configure();

        // 4) Feature：被动 +1 CL（火焰法术） + 授予启动能力与资源
        var feature = FeatureConfigurator.New("MysticFireRingFeature", FeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("MysticFireRing.Feature.Name", "秘火增幅"))
            .SetDescription(LocalizationTool.CreateString("MysticFireRing.Feature.Description",
                "佩戴者施展带有「火焰」描述符的法术时，施法者等级获得 +1 表现加值。"
                + "\n此外每天可以以一个迅捷动作，让下一次火焰法术额外造成 " + ExtraDice + "d6 点火焰伤害。"))
            .SetIcon(icon)
            .SetIsClassFeature()
            .AddComponent(new IncreaseSpellDescriptorCasterLevel {
                name = "$MysticFire$CasterLevel",
                Descriptor = SpellDescriptor.Fire,
                BonusCasterLevel = 1,
                ModifierDescriptor = ModifierDescriptor.Competence,
            })
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { AbilityGuid })
            .AddAbilityResources(resource: resource, restoreAmount: true)
            .Configure();

        // 5) 附魔：装备时把 Feature 交给佩戴者
        var enchant = EquipmentEnchantmentConfigurator.New("MysticFireRingEnchant", EnchantGuid)
            .AddComponent(new AddUnitFeatureEquipment {
                name = "$MysticFireRing$Feature",
                m_Feature = feature.ToReference<BlueprintFeatureReference>(),
            })
            .Configure();

        // 6) 物品本体
        var item = ItemEquipmentRingConfigurator.New("MysticFireRingItem", ItemGuid)
            .SetDisplayNameText(LocalizationTool.CreateString("MysticFireRing.Name", ItemName))
            .SetDescriptionText(LocalizationTool.CreateString("MysticFireRing.Description",
                "这枚银戒指看起来正被内部的热量闷燃着。\n"
                + "秘火戒指能增强你的火焰类法术：施展带有「火焰」描述符的法术时，"
                + "你的施法者等级获得 +1 表现加值。此能力持续生效，无需启动。\n"
                + "此外，每天一次，你可以用一个迅捷动作让下一次火焰法术额外造成 "
                + ExtraDice + "d6 点火焰伤害。"))
            .SetIcon(icon)
            .SetCost(7500)
            .SetEnchantments(enchant.ToReference<BlueprintEquipmentEnchantmentReference>())
            // ⚠ 物品触发的能力，动作类型由**物品**上的这个组件决定，不看能力蓝图的 ActionType
            //   （AbilityData.GetDefaultActionType：SourceItem != null 时若没有该组件就强制 Standard）。
            .AddComponent(new CustomItemAbilityActionType {
                name = "$MysticFireRing$SwiftAction",
                m_ActionType = UnitCommand.CommandType.Swift,
            })
            .Configure();

        // 7) 上架：第三章 Drezen 珠宝商两位 + 提夫林杂货商 + 第一章沃尔吉夫
        AddToVendor("JewelerChapter3", JewelerChapter3TableGuid, ItemGuid);
        AddToVendor("GolemGoldJewelry", GolemGoldJewelryTableGuid, ItemGuid);
        AddToVendor("VendorTiefling", VendorTieflingTableGuid, ItemGuid);
        AddToVendor("WoljifShop", WoljifShopTableGuid, ItemGuid);

        Main.Log?.Log("[物品] 已注册「" + ItemName + "」并挂到 4 张商人表");
    }

    /// <summary>
    /// 往商人的贩卖表里加一件物品（<c>BlueprintSharedVendorTable</c> 没有物品数组字段，
    /// 商品是一个个 <c>LootItemsPackFixed</c> 组件堆出来的，见 MODDING_NOTES §9.3）。
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
