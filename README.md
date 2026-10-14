# Qqyx3RExpansion（qqyx的3r扩展）

把 D&D 3.5 的部分内容搬进《正义之怒》：内容选择全看作者个人喜好，并且绝大部分内容放在pf环境可能会导致一定程度的强度崩坏。因为部分兼容性，无法实现等问题，部分内容经过作者的魔改，可能与3r原版不同。
模组仅支持中文。

包含这个文档在内的几乎所有模组内容均为ai制作。


## Requirements

| | |
|---|---|
| **必需前置** | [TabletopTweaks-Core](https://github.com/Vek17/TabletopTweaks-Core) |
| 安装器 | [Unity Mod Manager](https://github.com/newman55/unity-mod-manager) 0.32.4 或更新 |
| 游戏 | Pathfinder: Wrath of the Righteous（Enhanced Edition） |

> ⚠️ **存档依赖**：本 mod 会新增蓝图。一旦在启用状态下保存过存档，该存档就会依赖它，之后必须保持启用，否则可能无法读取。

## Installation

1. 用 Unity Mod Manager 安装并启用 **TabletopTweaks-Core**
2. 用 Unity Mod Manager 安装并启用 `Qqyx3RExpansion`
3. 启动游戏，确认 UMM 列表里「qqyx的3r扩展」已启用

> 蓝图改动**必须重启游戏**才生效，UMM 的热重载对蓝图无效。

## Featuring

### 法术

- **3r神能**（Divine Power）— 牧师 4 环
  - 你的基本攻击加值变为等于你的人物等级（这也许可使你进行多次攻击），你的力量获得+6的增强加值，并获得每施法者等级1点暂时生命值，持续 1 轮/等级。
- **赞颂**（Recitation）— 牧师 4 环 / 保护领域 3 环
  - 以自己为中心 60 尺光环，只影响盟友：AC、攻击检定、全部豁免 +3 幸运加值，持续 1 轮/等级
- **高等提升抗力**（Resistance, Greater）— 4 环
  - 豁免 +3 抗力加值，持续 24 小时
- **进阶提升抗力**（Resistance, Superior）— 6 环
  - 豁免 +6 抗力加值，持续 24 小时
- **鬼击术**（Wraithstrike）— 2 环
  - 迅捷动作，持续 1 轮：期间你的近战攻击变为近战接触攻击
- **黑暗之炎**（Darkfire）— 牧师 3 环
  - 施法后在**主手生成一把 120 尺远程接触武器**：可以标准动作投一次，也可以整轮攻击连续投掷，掷出后火焰立刻重新生成。每次命中造成每施法者等级 1d6 火焰伤害（上限 5d6）；
- **辉耀护甲**（Luminous Armor）— 牧师 2 环
  - 仅善良阵营可施展；它能提供等同于胸甲的防护能力（AC能得到+5盔甲加值），没有最大敏捷调整值、防具检定减值，也没有奥术失败率。护甲的光亮会让敌人在以近战攻击受术者时受到-4减值。
- **高等辉耀护甲**（Greater Luminous Armor）— 牧师 4 环
  - 除了能提供等同于全身铠的防护（AC得到+8盔甲加值）以外，此法术与「辉耀护甲」相同。


### 职业

- **卷宗学者**（Archivist）— 基础职业
    可以使用任何来源神术的神术施法职业。
- **神圣驱魔师**（Sacred Exorcist）— 进阶职业
    获得第二个驱散槽位，几乎是专门为了神圣超魔所作。

### 专长与超魔

- **法术持久**（[Persistent Spell]）— 超魔
  - 升 6 环，把法术的持续时间变成 24 小时；
- **神圣超魔**（Divine Metamagic）— 超魔
  - 用引导能量次数代替升环来应用超魔，仅对神术生效
- **额外驱散**（Extra Turning）
  - 每日引导能量次数 +4（原版「额外引导」为 +2），可多次选取。

### 野蛮人变体

- **猛扑野蛮人** — 1 级时以「猛扑」替换「快速移动」
- **旋风狂暴野蛮人**— 以独立的「旋风狂暴」替换狂暴：意志豁免加值改为反射、AC −2 改为 +2 闪避、去掉临时生命值，并且获得额外攻击。

### 物品

- **遗骨圣徽**（Reliquary Holy Symbol）— 颈部，每日引导能量次数 +3
- **夜枝**（Nightstick）— 权杖，每日引导能量次数 +4

两件物品已在第二、三、五章的珠宝商与魔法物品商人处出售。

### 其他

- UMM 设置页提供两个开关（针对卷宗学者）：允许抄录领域法术、允许抄录神话法术（天使 / 御衡者 / 阿兹拉塔 / 恶魔）



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
