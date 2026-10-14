# Qqyx3RExpansion（qqyx的3r扩展）

把 D&D 3.5 / Pathfinder 1 的内容搬进《正义之怒》：**8 个法术、2 个职业、3 项专长与超魔、2 个野蛮人变体、2 件魔法物品**。内容与界面全部为中文。

## Requirements

| | |
|---|---|
| **必需前置** | [TabletopTweaks-Core](https://github.com/Vek17/TabletopTweaks-Core) |
| 安装器 | [Unity Mod Manager](https://github.com/newman55/unity-mod-manager) 0.32.4 或更新 |
| 游戏 | Pathfinder: Wrath of the Righteous（Enhanced Edition） |

> ⚠️ **存档依赖**：本 mod 会新增蓝图。一旦在启用状态下保存过存档，该存档就会依赖它，之后必须保持启用，否则可能无法读取。

## Installation

推荐用 [ModFinder](https://github.com/Pathfinder-WOTR-Modding-Community/ModFinder) 安装，它会自动处理好前置。

手动安装：

1. 用 Unity Mod Manager 安装并启用 **TabletopTweaks-Core**
2. 把 `Qqyx3RExpansion` 文件夹放进 `<游戏目录>\Mods\`
3. 启动游戏，确认 UMM 列表里「qqyx的3r扩展」已启用

> 蓝图改动**必须重启游戏**才生效，UMM 的热重载对蓝图无效。

## Featuring

### 法术

- **3r神能**（[Divine Power](https://www.d20srd.org/srd/spells/divinePower.htm)，3.5 SRD）— 牧师 4 环
  - 按 3.5 数值重做：BAB 等于人物等级、力量 +6 增强加值、每施法者等级 1 点临时生命值
  - 法术表：牧师、战争领域、荣耀领域、审判者、萨满、天使道途、御衡者道途
- **赞颂**（Recitation）— 牧师 4 环 / 保护领域 3 环
  - 以自己为中心 60 尺光环，只影响盟友：AC、攻击检定、全部豁免 +3 幸运加值，持续 1 轮/等级
  - 法术表：牧师、保护领域、萨满、审判者、战斗祭司
- **高等提升抗力**（Resistance, Greater）— 4 环
  - 豁免 +3 抗力加值，持续 24 小时（可用「法术延时」变 48 小时）
  - 法术表：吟游诗人、牧师、德鲁伊、萨满、猎人、审判者、战斗祭司、法师（术士共用）
- **进阶提升抗力**（Resistance, Superior）— 6 环
  - 豁免 +6 抗力加值，持续 24 小时
  - 法术表：吟游诗人、牧师、德鲁伊、萨满、法师（术士共用）
- **鬼击术**（Wraithstrike，出自《万法大全》Spell Compendium）— 2 环
  - 迅捷动作，持续 1 轮：期间你的近战攻击变为近战接触攻击
  - 法术表：法师（术士共用）、炼金术士、魔战士、血脉狂怒者
- **黑暗之炎**（Darkfire，出自《万法大全》Spell Compendium）— 牧师 3 环
  - 施法后在**主手生成一把 120 尺远程接触武器**：可以标准动作投一次，也可以整轮攻击连续投掷，掷出后火焰立刻重生成
  - 每次命中造成每施法者等级 1d6 火焰伤害（上限 5d6）；随武器暴击翻倍；吃速射与近程射击；强效 / 极效 / 强化 / 延时 / 法术持久全部生效
  - 法术表：牧师、萨满、审判者、战斗祭司
  - 说明：武器面板伤害显示为 0 是刻意的 —— 真实伤害由法术结算，**看战斗日志**即可
- **辉耀护甲**（Luminous Armor，出自《崇善之书》Book of Exalted Deeds）— 圣洁 2 环
  - 仅善良阵营可施展；提供护甲 AC 加值，并让近战攻击者承受命中惩罚
  - 法术表：牧师、德鲁伊、萨满、审判者、战斗祭司、猎人、圣武士、游侠
- **高等辉耀护甲**（Greater Luminous Armor）— 圣洁 4 环
  - 同上，数值更高

### 职业

- **卷宗学者**（Archivist，出自《恐怖英雄》Heroes of Horror）— 基础职业
  - 神术系的**智力**施法者：采用法师式机制（准备式施法 + 抄录卷轴 + 每级学会 2 个法术），法术表取自牧师表
  - 含「暗知识」能力
- **神圣驱魔师**（Sacred Exorcist，出自《神圣全书》Complete Divine）— 进阶职业
  - 3.5《Complete Divine》内容：每级「现有施法等级 +1」（19 个法术书选项）、第二个引导能量槽位（引导次数翻倍）、抵抗附身、仇敌、反制邪恶、奖励专长
  - 前置：善良阵营 + 知识（宗教）7 级

### 专长与超魔

- **法术持久**（[Persistent Spell](https://www.d20srd.org/srd/feats.htm)，3.5 SRD）— 自定义超魔
  - 升 6 环，把持续时间变成 24 小时；只对「有持续时间文案且非接触」的法术开放
- **神圣超魔**（Divine Metamagic，出自《神圣全书》Complete Divine）— 选择池 + 10 个变体
  - 法术增收 / 强效 / 延时 / 极效 / 顽强 / 瞬发 / 及远 / 甄选 / 加剧 / 持久
  - 用引导能量次数代替升环来应用超魔，仅对神术生效
- **额外驱散**（[Extra Turning](https://www.d20srd.org/srd/feats.htm)，3.5 SRD；本 mod 数值改为 +4）
  - 每日引导能量次数 +4（原版「额外引导」为 +2），可多次选取

### 野蛮人变体

- **猛扑野蛮人** — 1 级时以「猛扑」替换「快速移动」
- **旋风狂暴野蛮人**（[Whirling Frenzy](https://www.d20srd.org/srd/variant/classes/variantCharacterClasses.htm)，3.5 UA 变体）— 以独立的「旋风狂暴」替换狂暴：意志豁免加值改为反射、AC −2 改为 +2 闪避、去掉临时生命值，并附带可勾选的「额外攻击」开关

### 物品

- **遗骨圣徽**（Reliquary Holy Symbol）— 颈部，每日引导能量次数 +3
- **夜枝**（Nightstick）— 权杖，每日引导能量次数 +4

两件物品已在第二、三、五章的珠宝商与魔法物品商人处出售。

### 其他

- UMM 设置页提供两个开关（针对卷宗学者）：允许抄录领域法术（全部 37 张领域表）、允许抄录神话法术（天使 / 御衡者 / 阿兹拉塔 / 恶魔）
- 兼容 TabletopTweaks 的「强化法术」：在本 mod 的法术上同样能正确提高伤害骰上限

## 与规则原文的差异

| 内容 | 原文 | 本 mod |
|---|---|---|
| 鬼击术 | 刺客 2 / 法师术士 2 | 《正义之怒》的刺客职业**没有施法能力**，改给炼金术士 2 环 |
| 赞颂 | 净化（Purification）领域 3 | 游戏没有该领域，取主题最接近的**保护领域 3 环** |
| 黑暗之炎 | 每轮一次近战接触或投掷 | 做成真正的武器，因此**可整轮连投**；另因游戏机制，法术抗力与「水下失效」未实现 |
| 神圣超魔 | 驱散 / 斥喝不死生物 | 用 PF 的**引导能量**代替，消耗次数 = 1 + 该超魔多加的环数 |
| 神圣驱魔师 | 知识（位面） | 游戏没有该技能，用**知识（世界）**代替 |

## Build from source

需要 .NET SDK 9.0 与本机已安装的游戏本体。

```powershell
dotnet build Qqyx3RExpansion.slnx -c Debug
```

构建会自动完成：编译 → 用 ILRepack 把 BlueprintCore 合并进 `Qqyx3RExpansion.dll` → 部署到 `<游戏目录>\Mods\Qqyx3RExpansion\`。

发布版本（额外生成 `bin\Qqyx3RExpansion-<版本>.zip`）：

```powershell
dotnet build Qqyx3RExpansion.slnx -c Release
```

> 构建前请关闭游戏，否则 dll 被占用会导致部署失败。
> 请用 `.slnx` 而不是 `.csproj` 构建 —— 游戏路径由解决方案在构建时自动探测。

## Feedback

遇到问题或有建议，请到 [Issues](https://github.com/qianqianyuxi233/Qqyx3RExpansion/issues) 反馈。

## License

[MIT](LICENSE)
