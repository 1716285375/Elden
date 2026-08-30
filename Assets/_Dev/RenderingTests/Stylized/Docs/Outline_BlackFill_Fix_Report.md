# HiFi Outline 黑化修复 — 最终报告

Date: 2026-08-29

---

## 根因（已确认）

用户诊断正确：**Inverted Hull 把整个物体涂黑，而不是只画轮廓**。

直接原因（通过代码审计 + 实验确认）：

```text
旧的顶点膨胀：
positionCS.xy += direction * pixelToNDC * outlinePx * positionCS.w;
```

只在 **clip 空间 xy**（屏幕平面）膨胀，**深度 z 完全不变** →
膨胀后的外壳与原始表面**深度完全相同** → `ZTest LEqual` 处处通过 →
Cull Front 的背面壳覆盖整个可见表面 → **整个物体变成 OutlineColor**。

Cube 不变黑是**正确的行为**（它走 Screen-space，不撑模型），恰好证明 Layer 分流生效。

## 修复内容

### 1. Hull 深度修正（`Includes/HiFiOutline.hlsl`）

```hlsl
// 屏幕像素膨胀（保持恒定像素宽度）
positionCS.xy += direction * pixelToNDC * outlinePx * positionCS.w;

// 深度修正：沿视图法线 z 分量外推，
// 使外壳位于表面外侧（内部被原表面遮挡，仅轮廓边缘露出）
positionCS.z += (normalVS.z * rsqrt(lenSq)) * pixelToNDC.y * outlinePx * positionCS.w * 0.5;
```

- 背面/正面法线 → 深度被推开 → 物体内部不可见（不再填充）
- 轮廓边缘法线（侧向，z≈0）→ 深度不变 → 边缘正常露出

验证：角色中心 RGB 从 `(8,8,10)`（全黑）恢复为 `(143,164,179)`（原色），
角色区域轮廓 dark = 81 像素（边缘存在，内部干净）。

### 2. Stencil 职责拆分（用户 Phase 2 要求）

`HiFiOutlinePass` 不再承担 stencil 标记。新增独立 pass：

| Pass | 职责 | 状态 |
|---|---|---|
| `HiFiOutlineStencilMaskPass`（新） | 原始几何 + Cull Back + **ColorMask 0** + Stencil Ref 1 Replace | 只写 stencil，**绝不写颜色** |
| `HiFiOutlinePass` | 纯 Hull：Cull Front / ZWrite Off / ZTest LEqual / Blend One Zero | 只画外轮廓 |
| `HiFiScreenOutlinePass` | Depth+Normal 边缘，Stencil NotEqual 跳过角色 | 环境描边 |

Shader 新增 `LightMode = "HiFiOutlineStencilMask"` pass（HiFiToon 现 6 pass）。
Feature 按顺序 enqueue：StencilMask → Hull。

### 3. Screen Outline 颜色模式（用户 Phase 5 要求）

`HiFiScreenOutlineRendererFeature` 新增：

```csharp
public enum OutlineColorMode { Fixed = 0, AutoContrast = 1, InvertedColor = 2 }
```

| 模式 | 逻辑 |
|---|---|
| Fixed | 直接使用 `_OutlineColor` |
| AutoContrast | `luma = dot(scene, (0.2126,0.7152,0.0722))`；亮 → `_DarkOutlineColor`，暗 → `_LightOutlineColor` |
| InvertedColor | `1.0 - scene`（互补色） |

参数：`m_OutlineColor / m_DarkOutlineColor / m_LightOutlineColor / m_AutoContrastThreshold`。

验证：AutoContrast 模式下暗背景产生浅边（3894 亮边像素）、亮背景产生深边（309 暗边像素）——自适应生效。

## 实测验收

| 检查 | 结果 |
|---|---|
| 角色主体颜色与关闭 Outline 一致 | ✅ (143,164,179) 原色 |
| 打开 Outline 只增加边缘 | ✅ 中心 0 暗像素，边缘有轮廓 |
| Cube 行为原则一致（原色+边） | ✅ Screen Outline 路径 |
| Fixed Black 黑边 | ✅ |
| AutoContrast 亮背景暗边/暗背景亮边 | ✅ |
| Hull Width=0 → Screen 仍工作 | ✅（Hull pass 跳过，Screen 独立） |
| Screen=0 → Hull 只出轮廓不填充 | ✅（深度修正后） |
| Frame Debugger 三个独立 Pass | ✅ StencilMask / Hull / Screen 独立 |
| Shader 编译 | ✅ 全部 HasErrors=false |

## 修改文件

| 文件 | 改动 |
|---|---|
| `Includes/HiFiOutline.hlsl` | 深度修正 + StencilMask vert/frag |
| `HiFiToon.shader` | 新增 HiFiOutlineStencilMask pass |
| `HiFiOutlineStencilMaskPass.cs` | **新增**：ColorMask 0 只写 stencil |
| `HiFiOutlinePass.cs` | 移除 stencil 标记职责 |
| `HiFiOutlineRendererFeature.cs` | 管理两个 pass（Mask 先、Hull 后） |
| `HiFiScreenOutlineRendererFeature.cs` | 颜色模式枚举 + 参数 |
| `HiFiScreenOutlinePass.cs` | 传递颜色模式参数 |
| `HiFiScreenOutline.shader` | Fixed/AutoContrast/InvertedColor 实现 |
| `TEST_HiFi_PC_Renderer.asset` | Screen Outline 参数 |

## 已知残留（非本次范围）

- 右下角彩虹噪点：生产 PC_Renderer 同样存在，属场景/项目预存问题。
- `StylizedOutlineHull` Layer 20 已写入 TagManager，编辑器重载 ProjectSettings 后可用（当前 Hull 用 Player 层验证）。
- `LV01SceneArchitectureMigration.cs`（用户未跟踪脚本）的 ProBuilder 编译错误未改动。
