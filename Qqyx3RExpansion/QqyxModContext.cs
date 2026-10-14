using TabletopTweaks.Core.Localization;
using TabletopTweaks.Core.ModLogic;
using UnityModManagerNet;

namespace Qqyx3RExpansion;

/// <summary>
/// 本 mod 的 TabletopTweaks-Core 上下文。
///
/// TTT-Core 的 <c>MetamagicExtention.RegisterMetamagic</c> 需要一个 <see cref="ModContextBase"/>：
/// 它要用 <c>ModEntry</c>（百科条目 key 的后缀）、<c>Logger</c> 和 <c>ModLocalizationPack</c>
/// （<c>Helpers.CreateString</c> 会把超魔名字注册进这个本地化包）。
///
/// 写法与 TomeOfTheFirebird 的 <c>ModContextTomeOfTheFirebird</c> 一致，区别只有：
/// 本项目用 BlueprintCore 建蓝图，所以不走 TTT 的 Blueprints / Localization 文件系统。
///
/// ⚠ 千万不要调用 <c>LoadLocalization()</c>：它内部用的是
/// <c>Assembly.GetExecutingAssembly()</c>（在 TTT-Core 里执行 → TTT-Core 自己的程序集），
/// 我们传进去的类路径在它里面找不到内嵌资源，<c>GetManifestResourceStream</c> 会返回 null，
/// 紧接着 <c>new StreamReader(null)</c> 抛 ArgumentNullException。
/// 这里直接换成带 Context 的空本地化包即可。
/// </summary>
internal class QqyxModContext : ModContextBase {

    public QqyxModContext(UnityModManager.ModEntry modEntry) : base(modEntry) {
        ModLocalizationPack = new MultiLocalizationPack(this);
    }

    public override void LoadAllSettings() {
        // 不使用 TTT 的 Blueprints / Settings 文件系统
    }

    public override void SaveAllSettings() {
        // 不落盘（TTT 默认实现会往 mod 目录写 Blueprints.json）
    }

    public override void AfterBlueprintCachePatches() {
        // 同上：TTT 默认实现会 SaveSettings，这里不写任何文件
    }
}
