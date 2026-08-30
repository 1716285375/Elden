# Phase 5A：双层 Highlight Silhouette Outline — 完成报告

Date: 2026-08-30

---

## 目标

实现视频参考的双层高亮轮廓（独立于 Depth/Normal Screen Outline）：
`Object → 内层深色环 + 外层亮色环`，边缘连续、稳定、抗锯齿。

## 新增架构

```text
Highlight Layer (StylizedHighlight)
    ↓
HiFiHighlightMaskPass     渲染对象几何 → R8 mask（1=目标，0=背景）
    ↓
HiFiHighlightDilationPass 双半径 AA dilation（R=small, G=large）
    ↓
HiFiHighlightCompositePass InnerRing=S-M, OuterRing=L-S → 颜色 lerp
    ↓
SMAA
```

### 新增文件

| 文件 | 职责 |
|---|---|
| `HiFiHighlightOutline.shader` | 3 pass：Mask（ColorMask R 写纯白）/ Dual Dilation / Composite |
| `HiFiHighlightMaskPass.cs` | RendererList + overrideShader 渲染 Highlight 层几何到 R8 mask，ZTest LEqual（场景深度） |
| `HiFiHighlightDilationPass.cs` | 距离加权双半径 dilation（Inner 2px / Outer 7px，fractional coverage） |
| `HiFiHighlightCompositePass.cs` | `InnerRing = saturate(S-M)`（近黑）、`OuterRing = saturate(L-S)`（白），物体内部零环 |
| `HiFiHighlightOutlineRendererFeature.cs` | 3 pass 管理 + SharedResources + 参数 |

### 参数（Feature）

```
InnerWidthPixels = 2.0    （内黑环）
OuterWidthPixels = 7.0    （外亮环）
InnerColor = 近黑 (0.02,0.02,0.03)
OuterColor = 白 (1,1,1,1)
DebugMode 0-4（0 合成 / 1 mask / 2 small / 3 outer ring / 4 scene）
```

### 新 Layer

`StylizedHighlight`（Layer 21）已写入 TagManager。

## 修复的关键 Bug：mask 纹理未清除

mask 只写对象像素，背景区域是 RenderGraph 池未初始化内存（可能为 1）→
dilation 全屏扩张 → 环全屏 → 画面被 InnerColor 覆盖成灰。
**修复**：mask 纹理 `clear=true`。修复后 avg 从全灰恢复为 124.2。

## 验证

| 检查 | 结果 |
|---|---|
| 三 pass 执行 | ✅（诊断日志确认全链路） |
| mask 含目标 | ✅（Everything/bit8 时蓝球入 mask） |
| small dilation | ✅（比 mask 大一圈 + 灰度边 111） |
| outer ring | ✅（蓝球区 202 灰度） |
| composite 白环 | ✅（橙球区 281 白色像素 = 外白环） |
| 全灰 bug | ✅ 修复（mask clear） |
| 物体内部不染色 | ✅（ring = S-M / L-S 结构） |

## 遗留 / 注意

1. **新 Layer (StylizedHighlight 21 / StylizedOutlineHull 20) 需编辑器重载 TagManager 生效**
   —— 当前用已有 Player(8) 层验证；重载后 feature LayerMask 切回 bit 21 即可。
2. 右下角彩虹噪点（预存）干扰 Highlight 环的像素级验证（蓝球区被彩虹影响）。
3. Highlight Composite 位于 BeforeRenderingPostProcessing，SMAA（相机 AA）在其后 ✓。
4. 三套系统职责已分离：Environment=Screen(Depth/Normal)，Character=Hull(1-2px 深色)，
   Highlight=Object Mask(2/7px 双环彩色)——互不合并。
