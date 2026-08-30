# Ben-Day Dot Pattern — 行为说明与优化文档

Date: 2026-08-29
适用于 `Game/Stylized/HiFiBenDay`、`Game/Stylized/HiFiComposite`、`HiFiStyleVolume`。

---

## 1. 当前公式确认

最终合成阶段（`HiFiComposite.shader` Pass 0）确认使用如下**乘法**逻辑，
DotPattern 只会缩放 Bloom，绝不会直接叠加到 SceneColor 上：

```
DotPattern    = HiFiBenDayDots(uv, density, radius, rotation, softness)   // 0..1 屏幕空间圆点遮罩
BloomGate     = smoothstep(DotBloomMin, DotBloomMax, Luminance(bloom))    // 0..1 亮度门控
StylizedBloom = BloomTexture * DotPattern * BloomGate * BloomIntensity
FinalColor    = SceneColor + StylizedBloom
```

- `DotPattern` 是一个 0/1 之间的**乘法遮罩**（半调网点），不是加法项。
- `FinalColor = SceneColor + StylizedBloom` 里的 `+` 是把「网点化后的 bloom」加回场景，
  与「把网点直接加到场景颜色」是两回事：网点只裁剪了 bloom，没有改变场景本身。

---

## 2. 参数语义（已在 shader 注释中明确）

| 参数 | 控制内容 | 数学表达 |
| --- | --- | --- |
| `DotDensity` | 屏幕 UV 的**网格划分密度**（全屏有多少个单元） | `cell = frac(uv * density)`，单元尺寸 = `1/density` |
| `DotRadius` | 每个网格单元内**圆点的覆盖率**（相对半个单元） | `dist = length(cell - 0.5) * 2`，`mask = 1 - smoothstep(...)` |
| `DotRotation` | 仅**旋转图案坐标系**（网格旋转后取 frac） | `uv' = R(θ)·uv`，再 `frac(uv' * density)` |
| `DotSoftness` | 圆点边缘的 smoothstep 过渡宽度 | `inner = radius - softness/2`，`outer = radius + softness/2` |
| `DotBloomMin/Max` | Bloom 亮度门控区间（低于 Min 完全不显示网点） | `gate = smoothstep(min, max, Luminance(bloom))` |

---

## 3. 现象解释（数学 + 视觉语言）

### 3.1 为什么 Density 增大时，光斑面积明显缩小？

- 圆点半径是**相对单元**的（`radius` 是半个单元的比例），而单元尺寸是 `1/density`。
  所以圆点的**绝对屏幕面积** ≈ `π · (radius · 1/density)²`。
- Density 翻倍 → 单元减半 → 每个网点绝对面积降到 **1/4**，同时网点数量翻倍。
  单个发光体的光斑由它覆盖到的「网点孔洞」组成：孔洞变小、间距变密，
  视觉上光斑明显收缩（覆盖率不变但颗粒变小，能量被摊薄）。
- 视觉语言：同样的半径比例，把网格从 32×32 换成 128×128，圆点从「硬币」变成「针孔」。

### 3.2 为什么 Radius 减小时，光斑迅速缩小甚至几乎消失？

- 圆点面积与半径成**平方关系**：`A ≈ π r²`（相对单元）。
  Radius 从 0.35 → 0.17（减半），面积降到约 **1/4**；
  Radius 从 0.35 → 0.10，面积降到约 **1/12**，几乎不可见。
- 同时旧实现用 `smoothstep(radius, radius+0.03, dist)` 的硬边，半径小时网点小于 1–2 像素，
  光栅化后只有零星像素命中 → 视觉上「光斑消失」。
- 优化：新增 `DotSoftness` 用平滑过渡取代硬边，小半径时仍有柔和的渐变网点，
  避免「快速消失」的突变感。

### 3.3 为什么 Rotation 只改变方向，不改变强度？

- 网格是**均匀周期结构**：旋转坐标系（`uv' = R(θ)uv`）后再 `frac`，
  圆点的分布密度、单点面积在统计上完全不变（旋转是保面积的等距变换）。
- 任何 θ 下，单位面积内的网点覆盖比例（≈ `π r² / 4`，r 为相对半个单元）不变，
  因此总透过的 bloom 能量不变 —— 只有图案的**朝向**在变。
- 视觉语言：把一张印满点的纸转 45°，点还是那些点，只是斜了。

### 3.4 为什么光斑接近消失时仍残留少量微小亮点？

1. **空间量化残留**：Density 高 / Radius 小时网点只有几个像素甚至亚像素。
   半调的本质是「用稀疏的孔透光」——在每个网点中心，`DotPattern = 1`，
   而 bloom 纹理是双线性采样的低分辨率模糊，网点中心正好落在发光核心上时，
   `bloom · 1 · gate · intensity` 仍然很亮 → 屏幕上留下几个孤立的小亮点。
2. **平滑边残余**：`1 - smoothstep(inner, outer, dist)` 在 `dist < inner` 处恒为 1，
   无论半径多小，圆点圆心处的遮罩永远是 1，只要 bloom 够亮就会透出光点。
3. **门控不生效于最亮处**：残留点出现在 bloom 峰值（最亮区域），
   它们高于 `DotBloomMax`，亮度门控反而完全放行。

**如何减弱残留点（可选微调）**：
- 适度增大 `DotRadius`（避免亚像素网点）或降低 `DotDensity`（≤ 64 比较安全）。
- 提高 `DotBloomMax`（把峰值区间也部分压住）或降低 `DotBloomIntensity`。
- 若完全不需要峰值处的点状辉光，可提高 `DotBloomMin` 让暗部网点消失（峰值处仍会有）。

---

## 4. 本次改动清单

| 文件 | 改动 |
| --- | --- |
| `Includes/HiFiCommon.hlsl` | `HiFiBenDayDots` 增加 `softness` 参数；smoothstep 过渡替代硬边；补充参数语义注释 |
| `HiFiBenDay.shader` | 增加 `_HiFiDotSoftness`；更新注释 |
| `HiFiComposite.shader` | 增加 Bloom 亮度门控 `DotBloomMin/Max`；新增 Pass 4 (BloomGate) 与 Pass 5 (StylizedBloom)；顶部注明公式 |
| `HiFiStyleVolume.cs` | 新增 `DotSoftness`、`DotBloomMin`、`DotBloomMax`（含 Tooltip） |
| `VP_HiFiStyle.asset` | 序列化新增三个参数（DotSoftness=0.06, DotBloomMin=0.4, DotBloomMax=2） |
| `HiFiStylizedRendererFeature.cs` | DebugMode 增加 `BenDayPattern=4`、`BloomGate=5`、`StylizedBloom=6`（0–3 保持稳定） |
| `HiFiStylizedPostPass.cs` | 绑定新参数；`DotDensity` 运行时 clamp 到 128（超限仅警告一次，防亚像素采样噪声） |

## 5. Debug 模式速查（`TEST_HiFi_PC_Renderer` → HiFiStylizedPost → m_DebugMode）

| 值 | 视图 | 用途 |
| --- | --- | --- |
| 0 | Composite | 最终合成 |
| 1 | BloomOnly | 原始 bloom 链输出（未网点化） |
| 2 / 4 | DotsOnly / BenDayPattern | 纯网点图案（含软边） |
| 3 | SceneOnly | 关闭效果预览 |
| 5 | BloomGate | 亮度门控可视化（黑=关闭，白=全开） |
| 6 | StylizedBloom | 最终 Ben-Day 辉光（bloom·dots·gate·intensity，无场景） |

## 6. 验证结果（编辑器内实测）

- 5 个 shader 全部 `HasErrors: false`；HiFiComposite 现为 6 个 pass。
- Volume 新参数正确反序列化（DotSoftness=0.06 / DotBloomMin=0.4 / DotBloomMax=2）。
- Debug 视图实测：BenDayPattern 呈现软边网点；BloomGate 呈现黑/白门控区域；
  StylizedBloom 呈现稀疏半调辉光；Composite 正常合成。
- 无相关 Console 错误。
