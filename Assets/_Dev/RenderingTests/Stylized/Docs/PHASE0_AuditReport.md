# PHASE 0 — Project Rendering Audit Report

Date: 2026-08-29
Auditor: pi (file-based audit; Unity Editor not launched for this audit)

## 1. Unity Editor version

- `ProjectSettings/ProjectVersion.txt`: **6000.3.11f1** (`3000ef702840`)

## 2. Installed URP package version

- `Packages/manifest.json` → `com.unity.render-pipelines.universal`: **17.3.0**
- PackageCache: `Library/PackageCache/com.unity.render-pipelines.universal@e9f15c489688` (17.3.0)
- `com.unity.render-pipelines.core@84bf82d10e47` present.

## 3. ProjectSettings/GraphicsSettings.asset

- `m_CustomRenderPipeline`: guid `4b83569d67af61e458304325a23e5dfd` → **Assets/_Game/Settings/Rendering/Pipeline/PC_RPAsset.asset** (confirmed via .meta)
- `m_RenderPipelineGlobalSettingsMap[UnityEngine.Rendering.Universal.UniversalRenderPipeline]`: guid `18dc0cd2c080841dea60987a38ce93fa` → **UniversalRenderPipelineGlobalSettings.asset**
- Default rendering path: Forward (m_DefaultRenderingPath: 1)

## 4. ProjectSettings/QualitySettings.asset

- `m_CurrentQuality: 1` → the **"PC"** quality level is active.
- "PC" level `customRenderPipeline` → guid `4b83569d67af61e458304325a23e5dfd` (**PC_RPAsset.asset**) — consistent with GraphicsSettings.
- "Mobile" level → guid `5e6cbd92db86f4b18aec3ed561671858` (Mobile_RPAsset.asset).

## 5. Active Render Pipeline Asset

**Assets/_Game/Settings/Rendering/Pipeline/PC_RPAsset.asset** (k_AssetVersion 13).
Key settings: m_RequireDepthTexture: 1, m_RequireOpaqueTexture: 1, m_SupportsHDR: 1, m_MSAA: 1,
m_MainLightRenderingMode: 1 (PerPixel), m_AdditionalLightsRenderingMode: 1 (PerPixel),
m_ColorGradingMode: 0 (LowDynamicRange), m_ColorGradingLutSize: 32,
m_VolumeProfile → **SampleSceneProfile.asset** (default volume profile of the asset).

## 6. Renderer referenced by the active PC_RPAsset

`m_RendererDataList[0]` guid `f288ae1f4751b564a96ac7587541f7a2` → **Assets/_Game/Settings/Rendering/Renderers/PC_Renderer.asset** (m_DefaultRendererIndex 0).

## 7. Renderer override on game cameras

- No `m_RendererIndex` overrides found in text-serialized scenes (Frontend menu scene). 
- The large level scenes are binary-serialized and were not text-inspectable; camera overrides there (if any) must be confirmed in-editor.
- Default: cameras use the RP asset's default renderer (PC_Renderer).

## 8. Current PC_Renderer renderer features

**PC_Renderer.asset** (m_UseNativeRenderPass: 1, m_RenderingMode: 2 = Forward+):
- `m_RendererFeatures`: exactly ONE feature — **ScreenSpaceAmbientOcclusion** (active: 1)
  - AOMethod: 0 (ScalableAmbientObscurance), Source: 1 (DepthNormals), Intensity: 0.4, Radius: 0.3,
    AfterOpaque: 0, Downsample: 0, SampleCount: -1, BlurQuality: 0, DirectLightingStrength: 0.25

## 9. Existing SSAO

- Present as the single renderer feature on PC_Renderer (see above). Uses the DepthNormals source, so the camera depth/normal textures are already produced when SSAO is enabled.
- SSAO shader asset guid `0849e84e3d62649e8882e9d6f056a017`.

## 10. Existing post-processing configuration

- RP asset default volume profile: `SampleSceneProfile.asset` → components: **Bloom** (intensity 0.25, threshold 1, highQualityFiltering on), **MotionBlur**, **Tonemapping**, **Vignette** (intensity 0.2).
- `DefaultVolumeProfile.asset` → full stack: Bloom, ChannelMixer, ChromaticAberration, ColorAdjustments, ColorCurves, ColorLookup, DepthOfField, FilmGrain, LensDistortion, LiftGammaGain, MotionBlur, PaniniProjection, ProbeVolumesOptions, ScreenSpaceLensFlare, ShadowsMidtonesHighlights, SplitToning, Tonemapping, Vignette, WhiteBalance + project custom components (OasisFogVolumeComponent, OutlineVolumeComponent) + URP test artifacts (CopyPasteTestComponent*, TestVolume, TestAnimationCurveVolumeComponent, VolumeComponentSupported*).
- Color grading mode: LowDynamicRange, LUT size 32.
- LevelDesign volume: `EP99-100 Ashen Crypt Volume.asset`.

## 11. Existing Global Volumes

- Scenes contain `Volume` components with `sharedProfile` references (Frontend menu scene references SampleSceneProfile via guid `10fc4df2da32a41aaa32d77bc913491c`).
- `Assets/_Game/Settings/LevelDesign/EP99-100 Ashen Crypt Volume.asset` (level volume profile).
- Dev volume: `Assets/_Dev/VFXAuthoring/SwordSlash/Settings/Rendering/Volume/VP_Global.asset` (Bloom only).

## 12. Render Graph compatibility mode

- **Disabled.** `UniversalRenderPipelineGlobalSettings.asset` → `RenderGraphSettings.m_EnableRenderCompatibilityMode: 0`.
- URP 17.3 removed the legacy `Execute` path from the production flow; Render Graph is the only execution path.
- Deprecated legacy field `m_EnableRenderGraph: 0` exists in the asset (migration-only; the authoritative `RenderGraphSettings` block states compatibility mode is OFF).
- New custom passes MUST implement `RecordRenderGraph(RenderGraph, ContextContainer)`.

## 13. _Dev assets duplicating production asset names

`Assets/_Dev/VFXAuthoring/SwordSlash/Settings/Rendering/Pipeline/` contains:
- **PC_RPAsset.asset** — same display name as production, but a separate asset (k_AssetVersion 13) referencing renderer guid `c0fabf18e3d918d47ab26b931d1da16f` (NOT the production renderer).
- **PC_Renderer.asset** — same display name as production, `m_RendererFeatures: []` (no features).
- **VP_Global.asset** — dev volume profile (Bloom only).

These are dev-only assets and are NOT referenced by GraphicsSettings/QualitySettings. Production pipeline assets remain unambiguous.

## Environment notes / tooling

- No Unity Editor process is running on this machine and no editor binary is installed under Program Files (only a CLI stub at `C:\Users\Jie\AppData\Local\Unity\bin\unity.exe`, which is not the Editor). 
- A Unity-MCP HTTP gateway was reachable at localhost:27786, but it proxies to an Editor that is not running.
- Consequence: shader/C# compilation, asset import, and visual/Render Graph validation cannot be executed from this session. All code/assets are authored against the installed URP 17.3.0 source (read directly from Library/PackageCache) and must be imported/validated in the Editor. Each phase report below explicitly flags what needs in-editor verification.

## Explicit distinction

| Area | Production (`Assets/_Game/Settings/Rendering/`) | Dev/test (`Assets/_Dev/RenderingTests/Stylized/` + existing `_Dev/VFXAuthoring/...`) |
| --- | --- | --- |
| Pipeline | Pipeline/PC_RPAsset.asset (active), Mobile_RPAsset.asset | TEST_HiFi_PC_RPAsset.asset (duplicate, isolated) |
| Renderer | Renderers/PC_Renderer.asset (SSAO feature), Mobile_Renderer.asset | TEST_HiFi_PC_Renderer.asset (duplicate + HiFi features) |
| Volumes | Volumes/DefaultVolumeProfile.asset, SampleSceneProfile.asset | VP_HiFiStyle.asset (new stylized volume) |

Production assets are NOT modified by this task until an explicit later phase, and only after in-editor validation.
