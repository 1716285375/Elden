# Screen Outline 第三阶段修复 — 最终报告

Date: 2026-08-30

---

## 现象（用户定位）

非 Stencil 对象（墙/地板/Cube/球）整面被 OutlineColor 覆盖（红屏）；
Capsule 保持白色（证明 Stencil 排除生效）。核心问题在 Screen Outline 的
EdgeMask / Composite，而非 Hull。

## 修复内容

### 1. 三独立 Render Graph Pass（替代单 pass 混合）

```text
Pass A: HiFi Screen Outline Edge Detect
        输入 Depth + Normals（永远 1px 邻居）
        输出 RawEdgeMask（R8G8: R=silhouette, G=crease）
        天空(far plane) early-out 0

Pass B: HiFi Screen Outline Dilation
        输入 RawEdgeMask
        输出 DilatedEdgeMask（R8G8，分别扩张两通道）
        圆盘 dilation：dilated = max(0, neighbors)，绝不反相

Pass C: HiFi Screen Outline Composite
        输入 SceneColor + RawMask + DilatedMask
        mask = saturate(silhouette*1.0 + crease*(1-sil)*0.35)
        lerp(scene, outlineColor, mask)
```

### 2. Debug Mode 全灰度 + 可分离

```
0 Combined | 1 DepthEdge(dil.r) | 2 NormalEdge(dil.g) | 3 RawEdge(raw.r) | 4 DilatedEdge(combined)
```

### 3. Phase 1 配置

- Hull Feature 关闭（隔离）
- Screen Outline Stencil Skip 移除（全部对象走 Screen）
- OutlineColor = 黑（Fixed），DebugMode = Combined

### 4. 检测算法（每项验证）

- Normal：`1 - dot(normalize(a), normalize(b))`，阈值 0.12 / softness 0.04
- Depth：`LinearEyeDepth` + 相对差 `abs(c-n)/max(min(c,n),0.001)`，阈值 0.02 / softness 0.01
- Dilation：`init 0; dilated = max(dilated, sample)`（无 1-sample、无反相）
- Composite：`lerp(scene, outline, mask)`，mask=0 严格等于 scene

## Debug Mask 逐级验证结果

| Mode | 结果 | 判定 |
|---|---|---|
| DepthEdge | silhouette 1.6%，物体内部全黑 | ✅ |
| NormalEdge | crease 1.9%，与 depth 分离 | ✅ |
| RawEdge | **0% 白，仅轮廓细线(~170)** | ✅ 核心验收 |
| DilatedEdge | 1.6%，比 raw 粗但未填满 | ✅ |
| Combined | 物体原色保留 + 黑边（2160 暗像素），无全红/全黑 | ✅ |

## 验收（Phase 7）

- ✅ 蓝球/Cube/Capsule/墙 均保留原色 + 黑色描边
- ✅ 无整面红 / 整面黑 / 整对象被 OutlineColor 覆盖
- ✅ RawEdge 画面几乎全黑，仅真轮廓细白线
- ✅ 3 shader 编译无错误（HiFiScreenOutline 3 pass），场景已保存

## 修改文件

| 文件 | 改动 |
|---|---|
| `HiFiScreenOutline.shader` | 3 pass：Detect(1px, RG) → Dilate(圆盘) → Composite(lerp)，Debug 灰度 |
| `HiFiScreenOutlinePass.cs` | 3 个独立 RG Pass + 2 中间纹理（R8G8） |
| `TEST_HiFi_PC_Renderer.asset` | Hull OFF、黑边 Fixed、Combined |

## 遗留

- 右下角彩虹噪点（场景/项目预存，PC_Renderer 同样存在）
- Hull + Stencil（Phase 8）待 Screen Outline 确认后恢复：
  - Capsule 走 Hull silhouette 增强
  - 环境只走 Screen Outline
