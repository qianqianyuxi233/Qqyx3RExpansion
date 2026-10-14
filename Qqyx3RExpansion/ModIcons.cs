using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Qqyx3RExpansion;

/// <summary>
/// 自定义图标加载器。
///
/// 游戏里法术/能力/buff 的图标就是 <see cref="Sprite"/>（<c>BlueprintUnitFact.m_Icon</c>），
/// 平时它们都存在游戏的 AssetBundle 里、靠 guid 索引。本类让 mod 可以**直接用自己的 PNG**，
/// 不需要 Unity Editor 打包 AssetBundle：
///
///   1. 把 PNG 放到 <c>&lt;mod目录&gt;\Icons\</c> 下（例如 Icons\recitation.png）
///   2. 代码里 <c>.SetIcon(ModIcons.Load("recitation.png"))</c>
///
/// 说明：
///   - 纹理在运行时用 <see cref="Texture2D.LoadImage"/> 解码，再用 <see cref="Sprite.Create"/> 生成 Sprite；
///   - 生成的 Sprite 会被缓存（静态字典持有引用），否则会被 GC 回收导致图标变空白；
///   - 加载失败返回 null，调用方可以兜底，例如：
///     <c>.SetIcon(ModIcons.Load("a.png") ?? AbilityRefs.Prayer.Reference.Get().Icon)</c>
///
/// 另一种"官方"路线是用 Unity 打包 AssetBundle：BlueprintCore 的 AssetTool 会自动加载
/// 与程序集同名的 <c>&lt;程序集名小写&gt;_assets</c>（本 mod 即 <c>qqyx3rexpansion_assets</c>），
/// 之后 bundle 里的资源名就能直接当 assetId 用。那条路需要一个 Unity 2020.3.48f1 环境。
/// </summary>
internal static class ModIcons {

    private const string IconFolderName = "Icons";

    /// <summary>缓存已生成的 Sprite，防止被 GC 回收。</summary>
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    /// <summary>图标目录的完整路径。</summary>
    internal static string IconFolder {
        get {
            return Path.Combine(Main.ModPath ?? ".", IconFolderName);
        }
    }

    /// <summary>
    /// 从 <c>&lt;mod目录&gt;\Icons\&lt;fileName&gt;</c> 加载图标。失败时返回 null 并写日志。
    /// </summary>
    internal static Sprite Load(string fileName) {
        if (string.IsNullOrEmpty(fileName)) return null;
        if (Cache.TryGetValue(fileName, out var cached)) return cached;

        try {
            var path = Path.Combine(IconFolder, fileName);
            if (!File.Exists(path)) {
                Main.Log?.Log($"[图标] 找不到文件：{path}");
                return null;
            }

            // 先建 2x2 占位，LoadImage 会按 PNG 实际尺寸重建
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) {
                Main.Log?.Log($"[图标] 解码失败（不是有效的 PNG/JPG？）：{path}");
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            // pivot 取中心，pixelsPerUnit 用 100（UI 显示只关心 rect，PPU 无影响）
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = fileName;

            Cache[fileName] = sprite;
            Main.Log?.Log($"[图标] 已加载 {fileName}（{texture.width}×{texture.height}）");
            return sprite;
        } catch (Exception e) {
            Main.Log?.Log($"[图标] 加载 {fileName} 失败：{e}");
            return null;
        }
    }
}
