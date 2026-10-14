using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.FactLogic;

namespace Qqyx3RExpansion;

/// <summary>
/// 3.5 规则野蛮人变体「猛扑野蛮人」：1 级时以「猛扑」替换「快速移动」。
///
/// 猛扑机制直接复用原版：原版 Units/Monsters/Leopard/Pounce 就是一个
/// BlueprintFeature 挂 AddMechanicsFeature 组件、m_Feature = Pounce。
/// 这里使用同一个 MechanicsFeatureType.Pounce 枚举，不自行实现冲锋逻辑。
/// </summary>
internal static class PounceBarbarian {

    // ---------- 原版蓝图 GUID（取自游戏 blueprints.zip）----------

    /// <summary>Classes/Barbarian/BarbarianClass —— 野蛮人职业</summary>
    private const string BarbarianClassGuid = "f7d7eb166b3dd594fb330d085df41853";

    /// <summary>Classes/Barbarian/FastMovement —— 野蛮人「快速移动」</summary>
    private const string FastMovementGuid = "d294a5dddd0120046aae7d4eb6cbc4fc";

    // ---------- 本 mod 新增蓝图 GUID ----------
    // 固定值，一旦发布不可更改，否则已有存档会失效。

    private const string PounceFeatureGuid = "e0bfa0a1ba5e4a2b8aec3af930683814";
    private const string ArchetypeGuid = "fef1b7fa527e4d959cf4c3930f9e7c96";

    internal static void Configure() {
        // 1. 猛扑能力：BlueprintFeature + AddMechanicsFeature(Pounce)
        FeatureConfigurator.New("PounceFeature", PounceFeatureGuid)
            .SetDisplayName(LocalizationTool.CreateString("Pounce.Name", "猛扑"))
            .SetDescription(LocalizationTool.CreateString(
                "Pounce.Description",
                "冲锋之后可以进行全回合攻击。"))
            .AddMechanicsFeature(AddMechanicsFeature.MechanicsFeatureType.Pounce)
            .Configure();

        // 2. 野蛮人变体：1 级移除「快速移动」，加入「猛扑」
        ArchetypeConfigurator.New(
                "PounceBarbarianArchetype",
                ArchetypeGuid,
                BlueprintTool.GetRef<BlueprintCharacterClassReference>(BarbarianClassGuid))
            .SetLocalizedName(LocalizationTool.CreateString("PounceBarbarian.Name", "猛扑野蛮人"))
            .SetLocalizedDescription(LocalizationTool.CreateString(
                "PounceBarbarian.Description",
                "3r野蛮人变体：1 级时以「猛扑」替换「快速移动」，冲锋后可以进行全回合攻击。"))
            .SetRemoveFeaturesEntry(1, BlueprintTool.GetRef<BlueprintFeatureBaseReference>(FastMovementGuid))
            .SetAddFeatureEntry(1, BlueprintTool.GetRef<BlueprintFeatureBaseReference>(PounceFeatureGuid))
            .Configure();
    }
}
