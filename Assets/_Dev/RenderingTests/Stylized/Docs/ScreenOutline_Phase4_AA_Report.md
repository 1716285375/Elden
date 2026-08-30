# Screen Outline 第四阶段（质量/抗锯齿）— 最终报告

Date: 2026-08-30

---

## 状态

Edge Detection 正确性已通过（RawEdge 几乎全黑 + 细线）。
本阶段目标：消除阶梯锯齿、轮廓过硬、层级不足、对比度问题。

## 修复内容（全部按方案执行）

### Phase 1：Fractional Coverage（禁二值化）

```hlsl
float aa = max(fwidth(delta), 1e-5);
float edge = smoothstep(
    threshold - aa,
    threshold + softness + aa,
    delta);
```
Depth / Normal 均保留 0..1 灰度 coverage（实测 mid 灰度 4.1%，非纯 0/1）。

### Phase 2：Mask LinearClamp 采样
Raw/Dilated mask 纹理 filterMode → Bilinear；shader 全部 `sampler_LinearClamp`。

### Phase 3：抗锯齿 Dilation（距离权重）
```hlsl
float cov = 1.0 - smoothstep(radius - 0.75, radius + 0.75, dist);
dil = max(dil, m * cov);
```
最外层存在 fractional coverage，不再硬方块扩张。

### Phase 4：Silhouette / Crease 独立宽度
- `SilhouetteWidthPixels = 2.25`（外轮廓）
- `CreaseWidthPixels = 0.85`（内部折线更细）
- 分别 dilation（R/G 两通道独立）

### Phase 5：Sobel Silhouette
Depth silhouette 用 **8-tap Sobel**（TL/T/TR/L/R/BL/B/BR）：
```hlsl
gx = (tr+2cr+br) - (tl+2cl+bl);
gy = (bl+2bc+br) - (tl+2tc+tr);
relMag = sqrt(gx²+gy²) / max(dC, 0.001);
```
Normal crease 保留 `1 - dot(normalize(a), normalize(b))`（未回归 length）。

### Phase 6：Composite
`lerp(scene, outlineColor, saturate(mask))`，mask=0 严格等于 scene。

### Phase 7：AutoContrast 按最大对比度
```hlsl
darkContrast = abs(sceneLum - darkLum);
lightContrast = abs(sceneLum - lightLum);
outlineColor = darkContrast > lightContrast ? dark : light;
```
不机械用 0.5 阈值。

### Phase 8：最终 AA
- 相机 `m_Antialiasing = 2 (SMAA)` 已设置（URP 相机 AA，作用于 Outline Composite 之后）
- 顺序：Opaque → Outline → Post → SMAA → FinalBlit ✓

## 验证

| 项 | 结果 |
|---|---|
| 灰度 coverage 存在 | mid 4.1%（非纯 0/1）✅ |
| RawEdge 细线 | 阈值修正后仅轮廓 ✅ |
| 地板透视误判 | 通过 DepthThreshold 0.08 消除（rows 7-8 变黑）✅ |
| Combined 场景 | avg 124.2，暗边 1269（精准）✅ |
| 双宽度 | Silhouette 2.25 / Crease 0.85 ✅ |
| SMAA | 已设置 ✅ |

## 修改文件

| 文件 | 改动 |
|---|---|
| `HiFiScreenOutline.shader` | Sobel silhouette + fwidth AA + 抗锯齿双宽度 dilation + 对比度 AutoContrast |
| `HiFiScreenOutlinePass.cs` | 双宽度参数、Linear mask 采样 |
| `HiFiScreenOutlineRendererFeature.cs` | Silhouette/CreaseWidthPixels 参数 |
| `TEST_HiFi_PC_Renderer.asset` | 新参数 + DepthThreshold 0.08 |
| 测试相机 | m_Antialiasing = SMAA |

## 遗留

- 右下角彩虹噪点（场景/项目预存）
- Hull 保持关闭（Phase 8 待用户确认 Screen Outline 视觉后再恢复）
- DepthThreshold 0.08 偏保守（为压透视渐变）；若远处细小物体漏检可小幅下调至 0.05-0.06
