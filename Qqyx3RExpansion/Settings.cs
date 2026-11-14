using UnityModManagerNet;

namespace Qqyx3RExpansion;

/// <summary>
/// UMM 设置页面的数据。UMM 会把它序列化到 mod 目录下的 Settings.xml。
/// 字段的初始值就是「默认值」。
/// </summary>
public class Settings : UnityModManager.ModSettings {

    /// <summary>
    /// 允许卷宗学者抄录领域法术：把 37 张领域法术表一并注入「卷宗表」。默认开启。
    /// </summary>
    public bool AllowDomainSpells = true;

    /// <summary>
    /// 允许卷宗学者抄录神话法术：把神术系道途（天使 / 御衡者 / 阿兹拉塔 / 恶魔）的
    /// 法术表注入「卷宗表」。默认关闭。
    /// </summary>
    public bool AllowMythicDivineSpells = false;

    /// <summary>
    /// 是否启用「缺陷」系统。默认开启。
    ///
    /// <para>
    /// 【玩家可见文案在 Main.OnGUI，不要把下面这些实现细节写进界面】
    /// 缺陷系统要改三处**全局共享**的东西：角色共享的 1 级专长进度表
    /// （<c>BasicFeatsProgression</c>）、<c>BlueprintRoot</c> 里对它的引用，
    /// 以及延迟配置的应用时机（<c>LoadPackTOC</c> 上挂了 patch）。
    /// 这比纯新增物品的风险高一个数量级。
    /// </para>
    ///
    /// <para>
    /// 如果怀疑它影响了别的功能（例如某个法术/武器不出伤害），
    /// 把这个开关关掉重启即可**完全排除**：关掉后本 mod 不会碰任何原版共享蓝图，
    /// 也不会挂 <c>LoadPackTOC</c> 的 patch。
    /// </para>
    /// </summary>
    public bool EnableDrawbacks = true;

    public override void Save(UnityModManager.ModEntry modEntry) {
        Save(this, modEntry);
    }
}
