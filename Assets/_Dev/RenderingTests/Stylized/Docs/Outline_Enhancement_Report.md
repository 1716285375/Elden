# HiFi Outline 增强 — 最终报告

Date: 2026-08-29
Unity 6000.3.11f1 / URP 17.3.0 / Render Graph

---

## 0. 审计结果

- Shader 已位于统一根目录 `Assets/_Game/Art/Shared/Shaders/Stylized/`（含 `Includes/`），
  **按约定保持原位，未搬家**（避免破坏材质引用与 .meta GUID）。
- 项目 Layers：存在 `Player(8)`、`Damageable Character(10)`、`Interactable(11)`、
  `Breakable Object(16)` 等。测试场景物体均在 `Default(0)`，故 TEST 渲染器
  LayerMask 保持 Everything 以便验证；生产迁移时应收缩为
  `Player | Damageable Character | (环境相关层)`，VFX/UI/Sky/Decal/全屏 Quad 不参与。

## 1. 修改文件

| 文件 | 改动 |
| --- | --- |
| `HiFiToon.shader` | Properties 新增 `_OutlineWidthPx("Outline Width (Pixels)", Range(0,12))=3`；HiFiOutline Pass 改用 `Includes/HiFiOutline.hlsl`；Render State 明确 `Cull Front / ZWrite Off / ZTest LEqual / Blend One Zero` |
| `Includes/HiFiCommon.hlsl` | UnityPerMaterial CBUFFER 新增 `half _OutlineWidthPx;`（保留旧 `_OutlineWidth` 兼容已有材质；**未建立第二个 CBUFFER**） |
| `RendererFeatures/HiFiOutlineRendererFeature.cs` | Settings 新增 `m_GlobalWidthScale`（`[Range(0,4)]` 默认 1，带 Tooltip）；保留 m_ProfilerTag / m_RenderEvent / m_LayerMask |
| `Passes/HiFiOutlinePass.cs` | 新增 `_HiFiOutlineGlobalScale` 全局 float 传递（DrawRendererList 前 `SetGlobalFloat`）；渲染列表改用与 URP RenderObjects 一致的 `RenderingUtils.CreateDrawingSettings` + `RendererListParams`（tagValues/stateBlocks/isPassTagName）；`GlobalWidthScale<=0` 时跳过 pass（避免膨胀 0 填充物体表面） |
| `TEST_HiFi_PC_Renderer.asset` | HiFiOutline Settings 增加 `m_GlobalWidthScale: 1` |
| 测试材质（7 个） | 按分级设置 `_OutlineWidthPx` |

## 2. 新增文件

| 文件 | 职责 |
| --- | --- |
| `Includes/HiFiOutline.hlsl` | 独立 Inverted Hull 算法：`OutlineAttributes` / `OutlineVaryings` / `HiFiOutlineVert` / `HiFiOutlineFrag` + `_HiFiOutlineGlobalScale` 声明 |

## 3. 未修改文件

- `Volumes/HiFiStyleVolume.cs` — Outline 参数保持 per-material，不入 Volume（延续原设计注释）
- `Passes/HiFiAOPass.cs` / `RendererFeatures/HiFiAORendererFeature.cs`
- `Passes/HiFiStylizedPostPass.cs` / `RendererFeatures/HiFiStylizedRendererFeature.cs`
- `HiFiBloom.shader` / `HiFiBenDay.shader` / `HiFiComposite.shader` / `HiFiAOLines.shader`
- `Includes/HiFiCommon.hlsl`（仅 CBUFFER 加一字段）/ `Includes/HiFiLighting.hlsl`（未动）

## 4. Outline 算法（像素空间宽度）

```
Object Position ──TransformObjectToHClip──▶ positionCS
Object Normal  ──World──▶ View Normal ──▶ direction = normalVS.xy
lenSq = dot(direction, direction); lenSq = max(lenSq, 1e-6); direction *= rsqrt(lenSq);
pixelToNDC = 2.0 / _ScaledScreenParams.xy;
outlinePx  = _OutlineWidthPx * _HiFiOutlineGlobalScale;
positionCS.xy += direction * pixelToNDC * outlinePx * positionCS.w;
```

- 宽度单位为**屏幕像素**（恒定视觉粗细），非世界单位。
- `rsqrt` + `lenSq≥1e-6` 防 `normalize(0)`（法线正对/背对相机时）。
- `* positionCS.w` 保证透视正确。
- Cull Front（背面壳）、ZWrite Off（不污染深度）、ZTest LEqual、Blend One Zero。

## 5. SRP Batcher

- 所有 HiFiToon 材质共用同一 `UnityPerMaterial` CBUFFER（含 `_OutlineWidthPx`），
  无第二个 CBUFFER → SRP Batcher 布局一致。
- `_HiFiOutlineGlobalScale` 是**全局 uniform**（CBUFFER 外），由 Pass 每帧 `SetGlobalFloat`，
  不参与 SRP Batcher 布局。

## 6. Frame Debugger 结果

Pass 顺序未变，无新增 copy/blit/temp RT：

```
DrawOpaqueObjects
(RP ...) HiFi Outline ── RendererLoop.DrawSRPBatcher   ← 单个 RG Raster Pass
DrawSkybox
...
HiFi AO Lines
HiFi Bloom Extract / Downsample / Upsample
HiFi Composite
BlitFinalToBackBuffer
```

## 7. Console 结果

- 5 个 Shader 全部 `HasErrors: false`（HiFiToon 5 Pass / 27 Properties）。
- C# 编译无错误（assets-refresh Success）。
- 无渲染期错误（诊断日志已移除）。

## 8. 测试材质参数（分级示例）

| 材质 | _OutlineWidthPx | 对应分级 |
| --- | --- | --- |
| M_TEST_Toon_Character | 4 | Player / 角色 |
| M_TEST_Toon_Weapon | 2 | Weapon |
| M_TEST_Toon_Sphere / Cube | 3 | 主要道具 |
| M_TEST_Toon_Floor / Wall | 1.5 | 大型环境 |
| M_TEST_Toon_Emissive | 2 | 小道具 |

Outline Color 统一 `(0.03, 0.03, 0.05)`（非纯黑，材质可单独调整）。

## 实测验证

- **全局倍率**：`m_GlobalWidthScale = 0` → 轮廓消失（pass 跳过）；`1` → 正常；`4` → 显著加粗（暗像素 1089→1366→2662，单调）。
- **像素宽度**：近相机（z=-4）轮廓带宽度中位数 ≈2.5px（设定 3px × scale 1）；远相机（z=-12）轮廓仍存在。
- **近/远**：轮廓长度随物体大小变化，宽度保持像素稳定。
- 静态 MeshRenderer / 非均匀 Scale 场景均正常，无 NaN/黑面/ZWrite 污染。

## 备注

- 调试中发现相机曾回退到默认渲染器（`m_RendererIndex=-1`），已恢复为 1（TEST 渲染器）
  并保存场景——这是本次"轮廓消失"的真正根因。
- 生产迁移建议：将 `PC_Renderer.asset` 的 HiFiOutline LayerMask 从 Everything
  收缩为角色/环境相关 Layer；相对粗细由各材质 `_OutlineWidthPx` 控制。
