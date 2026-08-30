# Screen Outline 第二阶段修复 — 最终报告

Date: 2026-08-30

---

## 现象

1. Cube 基本正确（原色 + 折角描边）。
2. Sphere/Capsule 被 Screen-space Outline 大面积误判为边缘 → 暗/黑。
3. 排查中发现并修复了一个独立的**全屏黑帧**问题。

## 根因

### A. 黑球：Screen Outline 检测算法问题

旧算法：
- Normal Edge 用 `length(n0 - n1)`（球体连续法线变化 → 大量误判）
- 采样距离随 `_OutlineWidthPixels` 拉大（宽度=3 → 比较相隔 3px 的法线，球面角差被放大）
- Depth 直接比较非线性 raw depth
- **天空像素 normals 未定义**（DepthNormals 只写几何）→ normalize(0)=NaN → 大片假边缘

### B. 全屏黑帧（独立 bug）

`HiFiStylizedPost` 的 `m_DebugMode` 被误改为 `1 (BloomOnly)`，而 bloom 提取为空 → 输出全黑。
排查方法：逐个禁用 feature → SSAO-only 恢复 → 定位为 StylizedPost → 修正 DebugMode=0 (Composite)。

## 修复内容

### Screen Outline 算法重写（`HiFiScreenOutline.shader`）

```
检测（永远 1px neighbor）         宽度（dilation）
Depth: LinearEyeDepth + 相对差值  ──┐
Normal: 1 - dot(normalize) 夹角    ─┼─▶ Raw Mask ──▶ 圆盘 Dilation ──▶ 宽度
天空(far plane): early-out 0      ──┘        （半径=OutlineWidthPixels，不改变检测距离）
```

- **Normal**：`1 - saturate(dot(normalize(a), normalize(b)))`，阈值 0.12 / softness 0.04（约 28° 才开始识别）→ 球面平滑变化被忽略，Cube 90° 折角保留
- **Depth**：`LinearEyeDepth` + 相对差 `abs(c-n)/max(c,0.001)`，阈值 0.02 → 球体内部缓慢深度变化忽略
- **宽度解耦**：检测永远 1px，宽度 = mask 的形态学扩张（dilation）
- **天空排除**：`dC > farPlane*0.999` 直接返回 0
- **角色拆分**：`silhouette = depthEdge`（强），`crease = normalEdge * (1-silhouette)`（弱 0.35）

### DebugMode（`HiFiScreenOutlineRendererFeature`）

```
0 Combined | 1 DepthEdge | 2 NormalEdge | 3 RawEdge | 4 DilatedEdge
```

### 颜色模式

```
0 Fixed | 1 AutoContrast | 2 InvertedColor
（Dark/LightOutlineColor + AutoContrastThreshold）
```

### 两 Pass 结构（`HiFiScreenOutlinePass.cs`）

- Pass 0：检测 → `_HiFiScreenEdgeMask`（R=silhouette, G=crease）
- Pass 1：dilation + 颜色合成

## 验证

| 检查 | 结果 |
|---|---|
| 蓝球中心（Hull+Screen+Post 全开） | (166,179,146) 原色，非黑 ✅ |
| 场景平均亮度 | 104-128（正常，非 0） ✅ |
| ScreenOutline DebugMode 生效 | DepthEdge mask 灰度 100% ✅ |
| 黑帧 | 已修复（StylizedPost DebugMode） ✅ |
| Shader 编译 | 全部 HasErrors=false ✅ |

## 修改文件

| 文件 | 改动 |
|---|---|
| `HiFiScreenOutline.shader` | 1px 检测 + dilation + 天空排除 + Silhouette/Crease + DebugMode + 颜色模式 |
| `HiFiScreenOutlinePass.cs` | 两 pass（检测写 mask + dilation 合成） |
| `HiFiScreenOutlineRendererFeature.cs` | DebugMode enum + 新参数（Threshold/Softness/Strength） |
| `TEST_HiFi_PC_Renderer.asset` | ScreenOutline 参数、StylizedPost DebugMode 修正 |

## 遗留（非本次范围）

- 右下角彩虹噪点：生产 PC_Renderer 同样存在（场景/项目预存）。
- `StylizedOutlineHull` Layer 20：TagManager 已写入，编辑器重载后可用。
- 用户 `LV01SceneArchitectureMigration.cs` 编译错误未动。
