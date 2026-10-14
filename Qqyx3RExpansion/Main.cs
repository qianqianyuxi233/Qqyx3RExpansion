using HarmonyLib;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;
using Kingmaker.Blueprints.JsonSystem;

namespace Qqyx3RExpansion;

public static class Main {
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger Log;
    /// <summary>mod 安装目录（由 UMM 提供）。自定义资源（如 <c>Icons\*.png</c>）放这里。</summary>
    internal static string ModPath;
    /// <summary>UMM 设置（设置页面里的选项）。</summary>
    internal static Settings ModSettings;
    /// <summary>TabletopTweaks-Core 的 mod 上下文（本 mod 的前置，新超魔等机制要用它的 API）。</summary>
    internal static QqyxModContext Context;

    public static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;
        ModPath = modEntry.Path;
        ModSettings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        Context = new QqyxModContext(modEntry);
        modEntry.OnGUI = OnGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        try {
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        } catch {
            HarmonyInstance.UnpatchAll(HarmonyInstance.Id);
            throw;
        }
        return true;
    }

    public static void OnGUI(UnityModManager.ModEntry modEntry) {
        GUILayout.BeginVertical();

        GUILayout.Label("卷宗学者（Archivist）—— 卷宗表的内容");
        ModSettings.AllowDomainSpells = GUILayout.Toggle(
            ModSettings.AllowDomainSpells,
            " 允许抄录领域法术（把全部 37 张领域法术表并入卷宗表）");
        ModSettings.AllowMythicDivineSpells = GUILayout.Toggle(
            ModSettings.AllowMythicDivineSpells,
            " 允许抄录神话法术（把神术系道途法术并入卷宗表：天使 / 御衡者 / 阿兹拉塔 / 恶魔）");

        GUILayout.Space(4);
        GUILayout.Label("　改动会在下次启动游戏时生效。");

        GUILayout.EndVertical();
    }

    /// <summary>UMM 在保存设置时调用。</summary>
    private static void OnSaveGUI(UnityModManager.ModEntry modEntry) {
        ModSettings.Save(modEntry);
    }

    [HarmonyPatch(typeof(BlueprintsCache))]
    public static class BlueprintsCaches_Patch {
        private static bool Initialized = false;

        // 用 Priority.Last 而不是 First：
        // 「卷宗表」是把各神术法术表合并出来的，必须等**其他 mod** 先把它们的法术
        // 登记进牧师/德鲁伊等表之后再合并，否则会漏掉别人新增的法术。
        // （UnityModManager 的 patch 顺序就是 Harmony priority：数值高的先跑。）
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(nameof(BlueprintsCache.Init)), HarmonyPostfix]
        public static void Init_Postfix() {
            try {
                if (Initialized) {
                    Log.Log("Already initialized blueprints cache.");
                    return;
                }
                Initialized = true;

                Log.Log("Patching blueprints.");

                // 每个内容单独 try-catch：某个内容注册失败时，后面的内容仍然会正常注册。
                // （曾经因为鬼击术的图标写法有误抛异常，导致它之后的内容全部没注册。）
                ConfigureSafely("猛扑野蛮人", PounceBarbarian.Configure);
                ConfigureSafely("旋风狂暴", WhirlingFrenzy.Configure);
                ConfigureSafely("3r神能", DivinePower3R.Configure);
                ConfigureSafely("赞颂", Recitation.Configure);
                ConfigureSafely("高等/进阶提升抗力", ResistanceSpells.Configure);
                ConfigureSafely("鬼击术", Wraithstrike.Configure);
                ConfigureSafely("黑暗之炎", Darkfire.Configure);
                ConfigureSafely("辉耀护甲", LuminousArmor.Configure);
                ConfigureSafely("法术持久（3.5）", PersistentSpell.Configure);
                ConfigureSafely("额外驱散（3r）", ExtraTurning3R.Configure);
                ConfigureSafely("神圣超魔", SacredMetamagic.Configure);
                ConfigureSafely("神圣驱魔师", SacredExorcist.Configure);
                ConfigureSafely("卷宗学者", Archivist.Configure);
                ConfigureSafely("小物品（遗骨圣徽 / 夜枝）", QqyxItems.Configure);

            } catch (Exception e) {
                Log.Log(string.Concat("Failed to initialize.", e));
            }
        }

        /// <summary>单独注册一个内容；失败只写日志，不影响其他内容。</summary>
        private static void ConfigureSafely(string name, Action configure) {
            try {
                configure();
            } catch (Exception e) {
                Log.Log(string.Concat("注册「", name, "」失败：", e.ToString()));
            }
        }
    }
}
