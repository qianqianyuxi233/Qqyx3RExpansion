using System.Collections.Generic;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 专长「神圣超魔 Divine Metamagic」（Complete Divine）。
///
/// 规则：先决条件是「驱散/斥喝不死生物」；建卡时选择一个你已拥有的超魔专长；
/// 以一个自由动作消耗「1 + 该超魔多加的环数」次驱散次数，让**这一次施法**获得该超魔，
/// 而**法术位不变**。可多次选取，每次绑不同的超魔。
///
/// 游戏里这套机制是现成的 —— **和超魔权杖（Metamagic Rod）走的是同一条路**：
///
///   权杖 = 物品开关 + MetamagicRodMechanics(buff) + 消耗充能
///   神圣超魔 = 专长能力 + SacredMetamagicOnNextSpell(buff) + 消耗引导能量次数
///
/// 两者都在 <c>RuleCalculateAbilityParams</c> 阶段往这次施法里塞超魔
/// （见 <see cref="SacredMetamagicComponent"/>），所以法术位早在施法前就定好了，天然不升环。
///
/// 每个变体三个蓝图：
///   Feature  DM_X        先决条件 = 原版超魔专长 X（所以「选择你拥有的超魔」自动成立）
///   Ability  DM_X_Ability 自由动作 → 给自己挂 Buff_X，并消耗 1 + 环数 次引导能量
///   Buff     DM_X_Buff    SacredMetamagicOnNextSpell{ Metamagic = X }（施法后自动消失）
///
/// 引导能量（Channel Energy）就是 PF 版的驱散/斥喝不死生物：
/// 次数 = 3 + 魅力调整值（`Classes/Cleric/ChannelEnergyResource.jbp`），
/// 与 3.5 的 3 + Cha 完全一致。
/// </summary>
internal static class SacredMetamagic {

    /// <summary>Classes/Cleric/ChannelEnergyFeature —— 引导能量（先决条件）</summary>
    private const string ChannelEnergyFeatureGuid = "a79013ff4bcd4864cb669622a29ddafb";
    /// <summary>Classes/Cleric/ChannelEnergyResource —— 引导能量次数（燃料）</summary>
    private const string ChannelEnergyResourceGuid = "5e2bba3e07c37be42909a12945c27de7";
    /// <summary>Classes/Cleric/ChannelEnergy —— 只为借图标</summary>
    private const string ChannelEnergyAbilityGuid = "f5fc9a1a2a3c1a946a31b320d1dd31b2";

    /// <summary>本 mod 蓝图：神圣超魔选择池</summary>
    private const string SelectionGuid = "23a82eb8789b4388af5d4a8d4cff06f7";

    /// <summary>能力的施法时间：3.5 原文是 free action</summary>
    private const int BuffMinutes = 10;

    /// <summary>
    /// 一个「神圣超魔（X）」变体。
    ///
    /// ⚠ 所有 GUID 发布后不可更改。
    /// </summary>
    private sealed class Variant {
        /// <summary>内部名（BlueprintCore 用，也是本地化 key 前缀）</summary>
        public string Key;
        /// <summary>显示名后缀，例如「法术强效」</summary>
        public string Name;
        public Metamagic Metamagic;
        /// <summary>这个超魔要多加几环 —— 神圣超魔消耗 = 1 + 这个数</summary>
        public int LevelCost;
        /// <summary>对应的原版超魔专长（作为先决条件，也借它的图标）</summary>
        public string PrereqGuid;
        // 本 mod 新增蓝图
        public string FeatGuid;
        public string AbilityGuid;
        public string BuffGuid;
    }

    /// <summary>
    /// 支持的超魔。按用户决定：排除 Heighten（要指定阶数）与 CompletelyNormal（−1 环，方向相反）；
    /// 其他 mod 的超魔不管；再加上本 mod 的「法术持久（3.5）」。
    /// </summary>
    private static readonly Variant[] Variants = {
        new Variant {
            Key = "Bolstered", Name = "法术增效", Metamagic = Metamagic.Bolstered, LevelCost = 1,
            PrereqGuid = "fbf5d9ce931f47f3a0c818b3f8ef8414",
            FeatGuid = "fffc6321b7db4983ad003eaadfab420b",
            AbilityGuid = "97fe79d47272439aaf0cf442ae03950b",
            BuffGuid = "04882b2ca5f3435297499d3e1e24bfac",
        },
        new Variant {
            Key = "Empower", Name = "法术强效", Metamagic = Metamagic.Empower, LevelCost = 2,
            PrereqGuid = "a1de1e4f92195b442adb946f0e2b9d4e",
            FeatGuid = "c348c3af59fe4c86aed454c9a886eae3",
            AbilityGuid = "5bb4e015cf3349aaa57d7b003f3f8948",
            BuffGuid = "1d6d3814e1a84af79357f1610a73135e",
        },
        new Variant {
            Key = "Extend", Name = "法术延时", Metamagic = Metamagic.Extend, LevelCost = 1,
            PrereqGuid = "f180e72e4a9cbaa4da8be9bc958132ef",
            FeatGuid = "142ee745402743b5b517d4afe86389e3",
            AbilityGuid = "eeb9a746e36d4d8f94970dd1c7684d4f",
            BuffGuid = "085edebf4f11405cac9d01ec6ef6f28a",
        },
        new Variant {
            Key = "Maximize", Name = "法术极效", Metamagic = Metamagic.Maximize, LevelCost = 3,
            PrereqGuid = "7f2b282626862e345935bbea5e66424b",
            FeatGuid = "977a022db56e40a7b149fc249e95cbdd",
            AbilityGuid = "5a95aeca3b4444f19d7422440c3f746b",
            BuffGuid = "b2a4c05cd0c74757818e055605c52e9b",
        },
        new Variant {
            Key = "Persistent", Name = "法术顽强", Metamagic = Metamagic.Persistent, LevelCost = 2,
            PrereqGuid = "cd26b9fa3f734461a0fcedc81cafaaac",
            FeatGuid = "8fc09ee95bc045bab31c99e9c18de7b4",
            AbilityGuid = "b2a6c39c20d441689e215e5ca46c77c4",
            BuffGuid = "49c66fa2e9b845a7bef0ae8dadc9c470",
        },
        new Variant {
            Key = "Quicken", Name = "法术瞬发", Metamagic = Metamagic.Quicken, LevelCost = 4,
            PrereqGuid = "ef7ece7bb5bb66a41b256976b27f424e",
            FeatGuid = "fa20c8c5a7254681bd468fcd3bddad26",
            AbilityGuid = "a83f1be5b85444d0a6b24221a1592d7b",
            BuffGuid = "d09712f10e9c43afb59234fcccdad657",
        },
        new Variant {
            Key = "Reach", Name = "法术及远", Metamagic = Metamagic.Reach, LevelCost = 1,
            PrereqGuid = "46fad72f54a33dc4692d3b62eca7bb78",
            FeatGuid = "13fafd8257574e0fac5e41c963ea49c5",
            AbilityGuid = "91869525843a4123962843869bcd51bb",
            BuffGuid = "f55f78e5352041f6a1ff97df8bfb4aba",
        },
        new Variant {
            Key = "Selective", Name = "法术甄选", Metamagic = Metamagic.Selective, LevelCost = 1,
            PrereqGuid = "85f3340093d144dd944fff9a9adfd2f2",
            FeatGuid = "e362b0d1296847f49353bb0934eeb19b",
            AbilityGuid = "f1b82f2e8c524b35819ce257f97662fe",
            BuffGuid = "9b38ab859b4842159581960a9f64d788",
        },
        // 法术加剧是 DLC6 内容；若玩家没装该 DLC，蓝图取不到会自动跳过（见 Configure）
        new Variant {
            Key = "Intensified", Name = "法术加剧", Metamagic = Metamagic.Intensified, LevelCost = 1,
            PrereqGuid = "8ad7fd39abea4722b39eb5a67d606a41",
            FeatGuid = "1d848e95d08848b1abe504d7860a196f",
            AbilityGuid = "3f1bf6a458114a7fbbf382bbff06f07c",
            BuffGuid = "b03cfa3755f04a7dad2fe0d86536ae55",
        },
        // 本 mod 自己的 3.5 超魔
        new Variant {
            Key = "Persistent3R", Name = "法术持久", Metamagic = PersistentSpell.Persistent24h,
            LevelCost = PersistentSpell.SpellLevelCost,
            PrereqGuid = "09d474d0109249e9b0f5bcbaba4319de",
            FeatGuid = "f3699b06f5ba4cf2a5052cb877dee459",
            AbilityGuid = "ea8390a8e43440409c54233cec5871d1",
            BuffGuid = "5f7aa05271674f4a9e6cad6c3e74d3a6",
        },
    };

    internal static void Configure() {
        var channelFeature = BlueprintTool.GetRef<BlueprintFeatureReference>(ChannelEnergyFeatureGuid);
        var channelResource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(ChannelEnergyResourceGuid);

        var options = new List<Blueprint<BlueprintFeatureReference>>();
        int built = 0;

        foreach (var variant in Variants) {
            var prerequisite = BlueprintTool.Get<BlueprintFeature>(variant.PrereqGuid);
            if (prerequisite == null) {
                Main.Log?.Log($"[神圣超魔] 跳过「{variant.Name}」：找不到对应超魔专长 {variant.PrereqGuid}"
                    + "（例如没装 DLC6）");
                continue;
            }

            var icon = prerequisite.Icon;
            var buff = ConfigureBuff(variant, icon);
            var ability = ConfigureAbility(variant, buff, channelResource, icon);
            options.Add(ConfigureFeat(variant, ability, icon));
            built++;
        }

        var selectionIcon = BlueprintTool.Get<BlueprintAbility>(ChannelEnergyAbilityGuid).Icon;

        var selection = FeatureSelectionConfigurator.New("SacredMetamagicSelection", SelectionGuid)
            .SetDisplayName(LocalizationTool.CreateString("SacredMetamagic.Name", "神圣超魔"))
            .SetDescription(LocalizationTool.CreateString("SacredMetamagic.Description",
                "你可以把引导能量的力量灌注进神术，让它更强。\n"
                + "先决条件：引导能量（驱散或斥喝不死生物）能力。\n"
                + "以一个自由动作，消耗「1 + 该超魔多加的环数」次引导能量次数，"
                + "你**下一次施展的神术**将获得所选超魔专长的效果，"
                + "并且**不占用比原法术更高的法术位**。\n"
                + "只有神术能受益；奥术法术不受影响。\n"
                + "这个专长可以被多次选取，每次必须选择一个不同的超魔专长。"))
            .SetIcon(selectionIcon)
            .AddPrerequisiteFeature(channelFeature)
            .AddToAllFeatures(options.ToArray())
            .SetGroups(FeatureGroup.Feat)
            .Configure();

        // 登记进普通专长池，否则升级 / 创建角色时看不到「神圣超魔」
        FeatPools.AddToBasicFeats(selection);

        Main.Log?.Log($"[神圣超魔] 已注册 {built} 个变体（选择池 {SelectionGuid}）");
    }

    /// <summary>变体的 buff：下一次神术施法带上该超魔，用完即弃。</summary>
    private static BlueprintBuff ConfigureBuff(Variant variant, Sprite icon) {
        var name = "SacredMetamagic" + variant.Key + "Buff";
        return BuffConfigurator.New(name, variant.BuffGuid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "神圣超魔：" + variant.Name))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "你的下一次神术施法将获得「" + variant.Name + "」的效果，且不占用更高的法术位。"
                + "施放后消失；奥术法术不受影响。"))
            .SetIcon(icon)
            .AddComponent(new SacredMetamagicOnNextSpell {
                name = "$" + name + "$Sacred",
                Metamagic = variant.Metamagic,
            })
            .Configure();
    }

    /// <summary>变体的能力：自由动作挂 buff，并消耗 1 + 环数 次引导能量。</summary>
    private static BlueprintAbility ConfigureAbility(Variant variant, BlueprintBuff buff,
                                                     BlueprintAbilityResourceReference channelResource,
                                                     Sprite icon) {
        var name = "SacredMetamagic" + variant.Key + "Ability";
        int cost = 1 + variant.LevelCost;

        return AbilityConfigurator.New(name, variant.AbilityGuid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "神圣超魔（" + variant.Name + "）"))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "以一个自由动作，消耗 " + cost + " 次引导能量次数，"
                + "让你下一次施展的神术获得「" + variant.Name + "」的效果，"
                + "并且不占用比原法术更高的法术位。"))
            .SetIcon(icon)
            .SetType(AbilityType.Supernatural)
            .SetRange(AbilityRange.Personal)
            .SetActionType(UnitCommand.CommandType.Free)
            // 消耗 1 + 该超魔的环数 次引导能量 —— 用自定义组件跨两个槽位扣：
            // 先扣牧师引导能量，不够的部分再扣神圣驱魔师的第二个槽位
            .AddComponent(new SacredMetamagicResourceLogic {
                name = "$" + name + "$ResourceLogic",
                m_PrimaryResource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(
                    ChannelEnergyResourceGuid),
                m_SecondaryResource = BlueprintTool.GetRef<BlueprintAbilityResourceReference>(
                    SacredExorcist.ChannelResourceGuid),
                Amount = cost,
            })
            .AddComponent(new AbilityEffectRunAction {
                name = "$" + name + "$Effect",
                SavingThrowType = SavingThrowType.Unknown,
                Actions = new ActionList {
                    Actions = new GameAction[] {
                        new ContextActionApplyBuff {
                            name = "$" + name + "$ApplyBuff",
                            m_Buff = buff.ToReference<BlueprintBuffReference>(),
                            Permanent = false,
                            UseDurationSeconds = true,
                            DurationSeconds = BuffMinutes * 60f,
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
    }

    /// <summary>
    /// 变体的专长：先决条件是「已拥有对应的超魔专长」——
    /// 这样选择池里只会列出玩家真正拥有对应超魔的选项（和 Weapon Focus 各武器版本同一个套路）。
    /// </summary>
    private static Blueprint<BlueprintFeatureReference> ConfigureFeat(Variant variant, BlueprintAbility ability,
                                                                     Sprite icon) {
        var name = "SacredMetamagic" + variant.Key;
        int cost = 1 + variant.LevelCost;

        return FeatureConfigurator.New(name, variant.FeatGuid)
            .SetDisplayName(LocalizationTool.CreateString(name + ".Name", "神圣超魔（" + variant.Name + "）"))
            .SetDescription(LocalizationTool.CreateString(name + ".Description",
                "以一个自由动作，消耗 " + cost + " 次引导能量次数，"
                + "让你下一次施展的神术获得「" + variant.Name + "」的效果，"
                + "并且不占用比原法术更高的法术位。\n"
                + "只有神术能受益；奥术法术不受影响。"))
            .SetIcon(icon)
            .AddPrerequisiteFeature(BlueprintTool.GetRef<BlueprintFeatureReference>(variant.PrereqGuid))
            .AddFacts(new List<Blueprint<BlueprintUnitFactReference>> { ability })
            .Configure();
    }
}
