# Unity Diagram Drawing Rules

图表统一通过 `diagram-design` 插件绘制（Skill：`diagram-design:diagram-design`）。

## Drawing Rules

- 输出：单个自包含 HTML 文件（内联 SVG + CSS），默认静态；不套用 Mermaid 渲染器布局
- 数据来源：图必须基于 CodeGraph 查询到的真实节点与边绘制，不凭记忆画；结构类图可先用 CodeGraph 的架构文档工具（如 `generate_architecture_doc`）生成初稿，再按本文约束裁剪
- 绘图前：确认图表类型与密度预算；画幅与风格按出版规范固定（见下节），用户可重定向时先说明再绘制
- 复杂度与规范：遵守 skill 的复杂度预算（节点/连接上限）、4px 网格、连接线规范、可访问性检查
- 首次绘图：如 skill 提示样式仍为默认值（paper `#f5f5f5` / ink `#2d3142` / accent `#eb6c36`），先询问用户是否定制风格

## Diagram Constraints

| Diagram       | Limit           |
| ------------- | --------------- |
| Architecture  | 5–12 个核心节点 |
| UML           | ≤ 12 个核心类   |
| Sequence      | ≤ 8 个参与者    |
| State Machine | ≤ 12 个状态     |
| Flowchart     | 仅关键判断      |

禁止逐行把代码翻译成图。

## Type Mapping

| Code Analysis 名称 | diagram-design 类型 |
| ------------------ | ------------------- |
| Architecture       | Architecture        |
| UML Class          | Architecture        |
| Sequence           | Sequence            |
| State Machine      | State machine       |
| Flowchart          | Flowchart           |
| Data Flow          | Data flow           |
| Dependency Graph   | Architecture        |
| Tree               | Tree                |
| ER                 | ER / data model     |
| Deployment         | Architecture        |
| Fishbone           | Tree                |

## Output Location

```text
Docs/Diagrams/<System>/<Feature>-<type>.html   源文件（唯一真源）
Docs/Diagrams/<System>/<Feature>-<type>.png    出版稿（按需导出，scale=3）
```

Mermaid 源（`.mmd`）用 `diagram-design:import-mermaid` 导入重绘；导出 PNG/SVG 用 `diagram-design:export-diagram`（仅在用户要求导出时）。

## Book Publishing Standards

出版场景下，全书所有图必须统一规格。以下参数一经选定，不得逐图更改。

### 统一画幅

- 全书所有图使用同一个 size preset：默认 `print-a4-landscape`（viewBox `1120 × 792`）；若书籍为 16:9 电子版则全书统一改为 `doc-wide`（`1280 × 720`）
- 禁止混用画幅；半幅插图也使用全幅画布排版留白，不单独切尺寸
- 保留 skill 的 40px 安全边距，防止印刷裁切伤图

### 统一分辨率

- 出版稿统一 PNG `scale=3`：`print-a4-landscape` → 3360 × 2376（约 300 DPI 印刷级）；`doc-wide` → 3840 × 2160
- HTML 是唯一源文件；PNG/SVG 只能由 `diagram-design:export-diagram` 从 HTML 导出，禁止手工生成或改图

### 统一风格

- 全书共用同一 style guide：首次绘图时定制一次并保存为 profile，后续所有图复用
- 只使用 minimal light / full editorial 模板；出版稿不使用 dark variant

### 统一字体与字号

- 西文固定三件套：Instrument Serif（标题）/ Geist（节点名）/ Geist Mono（技术标注）
- 字号走 print ramp：标题 32 / 节点名 12 / 标注 9 / 箭头标签 8
- 中文通过 font-family fallback 扩展统一中文字体（如 `'Noto Sans SC'`），不整体替换皮肤；示例：`font-family="'Geist', 'Noto Sans SC', sans-serif"`
- 中文文字不小于 10px，节点名优先 12px；CJK 字形比西文宽约 10%，节点盒子宽度预留余量

### 图号与图题

- 每张图配图号与图题，按书籍章节编号：`图 X-Y`（X 为章，Y 为章内序号），置于图下方居中
- 图题命名：`图 X-Y <系统>-<图类型>`，如 `图 3-2 战斗系统-时序图`
- HTML 页面的 eyebrow 与标题内容必须与图题一致

### 灰度兼容

- 出版可能灰度/双色印刷：配色必须保证转灰度后节点与连线仍可区分（靠明度差与线型，不靠色相）
- 导出出版稿前执行灰度检查（将图转灰度查看）；不达标则调整 fill 明度或改用虚线/线宽区分

### 出版五查（导出验收）

1. 画幅一致：viewBox 与全书统一预设相同
2. 分辨率一致：PNG 均为 scale=3
3. 字体一致：西文三件套 + 统一中文 fallback，字号走 print ramp
4. 灰度可读：转灰度后信息不丢失
5. 图号图题齐全：`图 X-Y` 编号连续，图题与内容一致
