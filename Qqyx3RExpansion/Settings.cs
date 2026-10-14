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

    public override void Save(UnityModManager.ModEntry modEntry) {
        Save(this, modEntry);
    }
}
