<a id="readme-top"></a>

<div align="center">
  <img src="Assets/_Game/UI/Frontend/HiFi/MenuLogo.png" alt="Zephyring logo" width="420">

  <h1>Zephyring · 疾风回响</h1>

  <p>一款使用 Unity 6 制作的第三人称风格化动作角色扮演游戏。</p>

  [![Unity](https://img.shields.io/badge/Unity-6000.3.11f1-000000?style=for-the-badge&logo=unity)](https://unity.com/)
  [![C Sharp](https://img.shields.io/badge/C%23-gameplay-512BD4?style=for-the-badge&logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
  [![URP](https://img.shields.io/badge/URP-17.3-222C37?style=for-the-badge&logo=unity)](https://docs.unity3d.com/Manual/urp/urp-introduction.html)
  [![Netcode](https://img.shields.io/badge/Netcode-2.13-0055FF?style=for-the-badge&logo=unity)](https://docs-multiplayer.unity3d.com/netcode/current/about/)

  [功能演示](#功能演示) · [快速开始](#快速开始) · [操作方式](#操作方式) · [项目结构](#项目结构)
</div>

<details>
  <summary>目录</summary>
  <ol>
    <li><a href="#关于项目">关于项目</a></li>
    <li><a href="#功能演示">功能演示</a></li>
    <li><a href="#核心功能">核心功能</a></li>
    <li><a href="#技术栈">技术栈</a></li>
    <li><a href="#快速开始">快速开始</a></li>
    <li><a href="#操作方式">操作方式</a></li>
    <li><a href="#项目结构">项目结构</a></li>
  </ol>
</details>

## 关于项目

Zephyring（疾风回响）是一个以近战博弈、角色成长和世界探索为核心的第三人称动作 RPG 项目。游戏包含完整的标题界面、角色创建、固定槽位存档、玩家战斗与移动、敌人 AI、装备和物品系统，以及按区域加载的开放场景。

项目以组件化角色架构组织玩法逻辑，并使用 Netcode for GameObjects 同步角色状态和动作。画面基于 URP，结合风格化材质、描边、后处理和分区场景构建明亮的幻想世界。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 功能演示

### 新游戏与角色创建

从标题画面进入 New Game，选择职业、外观与角色名称，随后加载游戏世界。

<p align="center">
  <img src="Docs/Media/new-game.gif" alt="新游戏与角色创建流程" width="720">
</p>

<table>
  <tr>
    <th width="50%">战斗动作</th>
    <th width="50%">移动动作</th>
  </tr>
  <tr>
    <td align="center">轻攻击连段、重攻击、跑攻与跳跃攻击</td>
    <td align="center">行走、奔跑、冲刺、翻滚、后撤步与跳跃</td>
  </tr>
  <tr>
    <td><img src="Docs/Media/combat.gif" alt="玩家战斗动作展示" width="100%"></td>
    <td><img src="Docs/Media/movement.gif" alt="玩家移动动作展示" width="100%"></td>
  </tr>
</table>

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 核心功能

- 角色创建：职业、性别、发型、发色与名称定制。
- 动作战斗：轻重攻击连段、蓄力、双持、格挡、翻滚攻击、跳跃攻击、弓箭与法术。
- 角色移动：自由移动、冲刺、跳跃、翻滚、潜行、锁定、攀梯和环境交互。
- RPG 系统：属性、体力与专注值、装备、背包、快捷物品、商店、武器升级和存档。
- 世界玩法：敌人及首领 AI、对话、赐福点、死亡回收、区域流式加载与多人状态同步。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 技术栈

- Unity `6000.3.11f1`
- Universal Render Pipeline `17.3.0`
- Unity Input System `1.19.0`
- Netcode for GameObjects `2.13.1` 与 Unity Transport
- AI Navigation、Cinemachine、Animation Rigging、Timeline 与 Unity Recorder

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 快速开始

### 环境要求

- Unity Hub
- Unity Editor `6000.3.11f1`
- Git（建议同时安装 Git LFS）

### 本地运行

1. 克隆项目：

   ```powershell
   git clone https://github.com/1716285375/Elden.git
   cd Elden
   ```

2. 在 Unity Hub 中添加项目目录，并使用 `6000.3.11f1` 打开。

3. 等待 Package Manager 完成依赖解析和资源导入。

4. 打开 `Assets/_Game/Scenes/Frontend/SCN_MainMenu.unity`，点击 Play。

> 首次导入会比后续启动耗时更久。若本地文件路径依赖不可用，请先在 `Packages/manifest.json` 中替换为你机器上的有效包路径。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 操作方式

| 功能 | 键盘与鼠标 | 手柄 |
| --- | --- | --- |
| 移动 / 镜头 | `WASD` / 鼠标 | 左摇杆 / 右摇杆 |
| 翻滚 / 冲刺 | `Space` / `Left Shift` | `B / Circle` |
| 跳跃 | `F` | `A / Cross` |
| 轻攻击 / 重攻击 | 鼠标左键 / 鼠标右键 | `RB / R1` / `RT / R2` |
| 格挡 / 战技 | `Left Ctrl` / `C` | `LB / L1` / `LT / L2` |
| 交互 / 锁定 | `R` / `Tab` | `Y / Triangle` / 右摇杆按下 |
| 使用 / 切换快捷物品 | `X` / `↓` | `X / Square` / 十字键下 |
| 角色菜单 | `Esc` | `Start / Options` |

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 项目结构

```text
Assets/_Game/
├── Data/          # 物品、角色动作、AI 与世界配置
├── Editor/        # 内容配置、验证和开发工具
├── Prefabs/       # 角色、UI、世界对象与特效预制体
├── Resources/     # 运行时加载的数据与资源
├── Scenes/        # 主菜单、主世界和分区场景
├── Scripts/       # 核心玩法代码
├── Settings/      # 输入、渲染等项目配置
├── Tests/         # Edit Mode 与 Play Mode 测试
└── UI/            # 标题界面和 HUD 美术资源
```

运行时代码集中在 `Assets/_Game/Scripts`，按角色、战斗、物品、存档、UI、世界和渲染等稳定领域划分；详细约定见 `Docs/Architecture/ProjectArchitecture.md`。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>

## 致谢

- README 结构参考 [Best-README-Template](https://github.com/othneildrew/Best-README-Template)。
- 项目使用 Unity 官方的 URP、Input System、Netcode、AI Navigation、Cinemachine 与 Recorder 等软件包。

<p align="right">(<a href="#readme-top">返回顶部</a>)</p>
