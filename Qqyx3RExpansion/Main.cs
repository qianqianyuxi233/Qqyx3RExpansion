using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityModManagerNet;
using UnityEngine;
using Kingmaker.Blueprints.JsonSystem;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;

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

        GUILayout.Space(8);
        GUILayout.Label("缺陷（Drawbacks）—— 可选房规");
        ModSettings.EnableDrawbacks = GUILayout.Toggle(
            ModSettings.EnableDrawbacks,
            " 启用缺陷（创建角色时可以选择，每个缺陷换取一个奖励专长）");

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
                ConfigureSafely("小物品（遗骨圣徽 / 夜枝）", QqyxItems.Configure);
                ConfigureSafely("秘火戒指", MysticFireRing.Configure);
                ConfigureSafely("移位脚镯", AnkletOfTranslocation.Configure);
                ConfigureSafely("猛禽面具", RaptorsMask.Configure);
                ConfigureSafely("预知戒指", RingOfAnticipation.Configure);
                // 缺陷系统会改全局共享的 1 级专长进度表，默认关闭，可在设置页开启。
                if (ModSettings.EnableDrawbacks) {
                    ConfigureSafely("缺陷（可选房规）", Drawbacks.Configure);
                }
                ConfigureSafely("新专长（影之刃 / 森林射手 / 突击尖兵）", QqyxFeats.Configure);
                ConfigureSafely("塞壬的恩典", SirinesGrace.Configure);
                ConfigureSafely("龙之力量", DraconicMight.Configure);
                ConfigureSafely("信念", Conviction.Configure);
                ConfigureSafely("群体信念", ConvictionMass.Configure);
                ConfigureSafely("瑞雪之歌", Snowsong.Configure);
                ConfigureSafely("腐囊强化", NecroticEmpowerment.Configure);
                ConfigureSafely("荒野行者（进阶职业）", Wildrunner.Configure);

                // ⚠ 卷宗学者必须放在**最后**：它注册时会把 8 张神术表合并成一张「卷宗表」
                //    （SpellListConfigurator.New 的一次性快照）。若排在前面，
                //    之后注册的法术就进不了卷宗学者的法术书。
                ConfigureSafely("卷宗学者", Archivist.Configure);

                // 武士 是新的基础职业：先建拔刀术专长与拔刀术，再建职业本体。
                ConfigureSafely("拔刀术专长（快速拔刀术 / 拔刀专精，含神话版）", IaijutsuFeats.Configure);
                ConfigureSafely("拔刀术", Iaijutsu.Configure);
                ConfigureSafely("武士（新基础职业）", OaSamurai.Configure);
                // 拔刀师是进阶职业，前提要用到拔刀术与快速拔刀术
                ConfigureSafely("拔刀师（进阶职业）", IaijutsuMaster.Configure);

                // 延迟配置队列里既有缺陷系统（改共享的 1 级进度表），
                // 也有拔刀师注册「快速拔刀术」专长（改共享的普通专长表）。
                // 两者都必须在所有蓝图加载完成后统一应用一次，所以这里无条件调用；
                // 队列为空时它是空操作（幂等）。
                ApplyDelayedBlueprints();

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

    /// <summary>应用延迟配置（幂等：队列空时什么都不做）。</summary>
    internal static void ApplyDelayedBlueprints() {
        try {
            BlueprintCore.Blueprints.CustomConfigurators.RootConfigurator<
                Kingmaker.Blueprints.Root.BlueprintRoot,
                BlueprintCore.Blueprints.Configurators.Root.RootConfigurator>.ConfigureDelayedBlueprints();
            Log?.Log("[蓝图] ConfigureDelayedBlueprints 已调用");
        } catch (System.Exception e) {
            Log?.Log(string.Concat("应用延迟配置失败：", e.ToString()));
            return;
        }

        // 诊断：确认缺陷的两个选择表真的写进了 1 级共享进度表。
        try {
            var prog = BlueprintTool.Get<BlueprintProgression>("5b72dd2ca2cb73b49903806ee8986325");
            var lvl1 = prog.LevelEntries == null ? null : prog.LevelEntries.FirstOrDefault(e => e.Level == 1);
            if (lvl1 == null) {
                Log?.Log("[诊断] BasicFeatsProgression 找不到 Level=1 条目");
                return;
            }
            var guids = lvl1.Features
                .Where(f => f != null)
                .Select(f => f.AssetGuid.ToString())
                .ToList();
            bool has1 = guids.Contains("c3f5daea7fd9415a9bf73af76960c2f8");
            bool has2 = guids.Contains("5d29f1422bbf41f8a81b8de1bc300807");
            Log?.Log("[诊断] 1 级条目共 " + guids.Count + " 个 Feature；缺陷槽 1=" + has1 + " 槽 2=" + has2);
        } catch (System.Exception e) {
            Log?.Log(string.Concat("[诊断] 读取 BasicFeatsProgression 失败：", e.Message));
        }
    }

    /// <summary>
    /// 应用 BlueprintCore 的**延迟配置**。
    ///
    /// <para>
    /// 为什么需要这个：缺陷系统要改「所有角色共享的 1 级进度表」
    /// （<c>Classes/Basic/BasicFeatsProgression</c>）以及 <c>BlueprintRoot</c> 里对它的引用。
    /// 这两处如果在 <c>BlueprintsCache.Init</c> 期间直接改，会被后续的蓝图加载覆盖掉，
    /// 表现为「在游戏里完全没生效」。
    /// </para>
    ///
    /// <para>
    /// 正确做法（照抄 Path of War 的武术实现）：用 <c>Configure(false)</c> 把修改排进
    /// BlueprintCore 的延迟队列，等 <c>StartGameLoader.LoadPackTOC</c> 把蓝图包全部加载完之后，
    /// 再统一应用。
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(StartGameLoader))]
    public static class StartGameLoader_Patch {
        [HarmonyPostfix]
        [HarmonyPatch("LoadPackTOC")]
        public static void LoadPackTOC_Postfix() {
            // 延迟配置队列里既有缺陷系统（改共享的 1 级进度表），
            // 也有拔刀师注册「快速拔刀术」专长（改共享的普通专长表）。
            // 所以这里不能再按缺陷开关直接 return —— 关闭缺陷时那批修改仍然要应用。
            try {
                ApplyDelayedBlueprints();
                Log?.Log("[蓝图] 已应用延迟配置");
            } catch (Exception e) {
                Log?.Log(string.Concat("应用延迟配置失败：", e.ToString()));
            }
        }
    }
}
