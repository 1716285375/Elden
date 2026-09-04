# Unity Code Analysis Rules

## Goal

AI Agent 在实现、修改或分析 Gameplay 系统后，根据实际代码自动判断是否需要图表，并选择有价值的图表类型，帮助理解：

- 系统结构
- Runtime 调用链
- 状态与决策
- 数据流
- 模块依赖

不要为了绘图而绘图。

## Code Analysis Output

分析 Feature 时至少提取：

| Item         | Content                        |
| ------------ | ------------------------------ |
| Purpose      | 系统解决什么问题               |
| Entry Point  | 主要代码入口                   |
| Core Classes | 核心类及职责                   |
| Runtime Flow | 主要执行链                     |
| State        | 关键状态                       |
| Dependencies | 关键依赖                       |
| Risks        | 耦合、状态、时序、空引用等风险 |

根据以上信息自动决定是否需要图。

## Agent Rule

每次分析代码时依次判断：

```text
What        → 系统做什么
Who         → 哪些类负责
How         → 如何执行
State       → 如何切换
Data        → 如何流动
Dependency  → 谁依赖谁
Risk        → 哪里容易出问题
```

只生成能够帮助回答上述问题的图。

## Tool Chain

分析过程与两个本地图数据库工具配合使用；MCP 工具不可用时（如子代理），用对应 CLI 兜底。

### CodeGraph（结构查询）

结构问题先查图，再读源文件：图谱回答"定义在哪、谁调用、影响面多大"，源码回答"具体怎么实现"。

| 分析产出    | 查询方式（CLI，已验证）            | 说明                     |
| ----------- | ----------------------------------- | ------------------------ |
| Purpose     | `codegraph explore <query>`         | 一次拿到相关符号 + 调用路径（对应 MCP `codegraph_explore`） |
| Entry Point | `codegraph node <name>` / `files`   | 单符号全貌或项目文件结构（对应 MCP `codegraph_node`） |
| Core Classes | `codegraph query -k class <name>`  | 按类型过滤搜符号         |
| Runtime Flow | `codegraph callees <symbol>`       | 该符号调用了谁           |
| Dependencies | `codegraph callers <symbol>`       | 谁调用了该符号           |
| Risks       | `codegraph impact <symbol>`        | 改动该符号的影响面       |
| Risks       | `codegraph affected <files...>`    | 改动文件影响哪些测试     |

MCP 模式下直接调用同名语义工具（如 `codegraph_explore`）；具体工具名以会话内实际注册为准。

### code-review-graph（变更评审）

- 变更前：`detect_changes` → 风险评分的影响文件清单（CLI：`code-review-graph detect-changes`）
- 评审时：`get_review_context` / `get_impact_radius` → 最小评审上下文
- 变更后：`update` 增量同步图（CLI：`code-review-graph update` / `status`）

### 分析顺序

1. 有变更：先 `detect_changes` 确定影响面
2. 结构问题：查 CodeGraph，不用 grep 大海捞针
3. 实现细节：读源文件
4. 按本规则产出七项分析，判断是否绘图

## Diagram Selection

| Code Pattern / Question                 | Diagram          |
| --------------------------------------- | ---------------- |
| 系统由哪些模块组成                      | Architecture     |
| 类、继承、组合关系                      | UML Class        |
| 一次 Gameplay 行为如何执行              | Sequence         |
| 状态及状态切换                          | State Machine    |
| `if / switch / guard` 决策逻辑          | Flowchart        |
| 数值或数据如何加工传递                  | Data Flow        |
| 模块、Manager、程序集依赖               | Dependency Graph |
| Prefab / Hierarchy / 目录父子关系       | Tree             |
| Item / Inventory / Equipment 等实体关系 | ER               |
| Client / Server / Service 部署          | Deployment       |
| Bug 多因素根因分析                      | Fishbone         |

## Souls-like Defaults

| System                 | Preferred Diagrams        |
| ---------------------- | ------------------------- |
| Character              | UML + State Machine       |
| Locomotion             | State Machine + Flowchart |
| Attack / Dodge / Parry | Sequence + Flowchart      |
| Combo / Blocking       | State Machine + Sequence  |
| Damage                 | Data Flow + Sequence      |
| Status Effect          | Data Flow + State Machine |
| Inventory / Equipment  | ER + UML                  |
| Enemy / Boss AI        | State Machine + Sequence  |
| Interaction            | Sequence + Flowchart      |
| Animation              | State Machine + Sequence  |
| Multiplayer            | Sequence + Data Flow      |
| Save System            | Data Flow + ER            |

## Selection Rules

- 简单 Feature：最多 **1 张**
- 中等 Feature：最多 **2 张**
- 复杂系统：最多 **3 张**
- 优先选择能显著减少阅读代码成本的图
- 同类信息不得重复绘制
- 没有明显价值时不生成图

优先顺序：

```text
Structure → Runtime → Logic
```

对应：

```text
Architecture / UML / Dependency
        ↓
Sequence
        ↓
State Machine / Flowchart / Data Flow
```

决定绘图后，按 [DiagramRules.md](DiagramRules.md) 执行绘制。

## Documentation Location

```text
Docs/
├── Architecture/
│   └── ProjectArchitecture.md
└── Systems/
    ├── Combat/
    ├── Character/
    ├── EnemyAI/
    ├── Inventory/
    └── ...
```

Feature 文档：

```text
Docs/Systems/<System>/<Feature>.md
```

## Update Rules

代码发生以下变化时同步检查文档：

- 新 Feature
- Refactor
- 状态逻辑变化
- 调用链变化
- 依赖关系变化

变更后先同步图再核对文档：`codegraph sync`（CodeGraph 自动同步，也可手动）+ `code-review-graph update`。

图必须反映**当前真实代码**。

如果提出重构方案，明确区分：

```text
Current
Proposed
```

不得把建议架构描述为当前实现。
