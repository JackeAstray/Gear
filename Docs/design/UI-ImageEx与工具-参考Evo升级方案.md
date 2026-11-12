# ImageEx 与 UI 工具：参考 Evo 的升级方案

> 结论先说：**Evo 值得学的是"把每实例参数搬进顶点 + 用材质池/样式页合批"，以及"编辑器里四件套式的工具链"；
> 不值得学的是它的整包引入方式（uGUI-only、66MB、商业资产、强依赖 Evo Tween）。**
> 本文只做**方法级参考**：所有落地代码都是 Gear 自研新脚本（命名空间 `Gear.*`），
> 不复制 Evo 的任何源码/资源，不引用 `Evo.UI` / `Evo.Tween` 程序集。

---

## 1. Evo 优劣拆解

### 1.1 优势（按"能不能搬"排序）

| # | 优势 | 证据 | 可搬性 |
|---|---|---|---|
| A1 | **每实例参数顶点化**：把形状、尺寸、圆角、柔边、裁剪、阴影全部塞进 `uv1/uv2/uv3/tangent`，材质 uniform 只留"样式" | `Evo UI/Scripts/Graphics/ProceduralShape.cs` L594-665（`uv1 = (w, h, packedSoftness, shapeAndFlags)`、`uv2 = (outline, innerShadow, clip, clipConfig)`、`uv3 = 圆角`、`tangent = OuterShadowData`） | ★★★★☆ |
| A2 | **材质池 + 样式页**：同款样式的多个图形共用一份材质，靠"样式页"把多份颜色/渐变写进同一页，从而在 Canvas 内合批 | `ProceduralShapeMaterial.cs` L352 `TryResolveBatchedStyle`、L624 `#region Batched Style Data`、L1180「One material per page keeps batching as it was」 | ★★★★☆ |
| A3 | **轻量/完整双着色器程序**：矩形/圆/多边形+纯色/渐变走 light program，星形/心形/内阴影/虚线/裁剪等才切 full program | `Skills/evo-ui-procedural-shape/SKILL.md` L299 | ★★★☆☆ |
| A4 | **编辑器工具链**：UI Catalog（带预览、搜索、拖拽创建）、Styler Browser、Icon Browser、Sprite Optimizer、Scene View 点选可见图形 | 同上 L199-212 | ★★★★☆（思路可搬） |
| A5 | **Styler 主题令牌**：颜色/字体/形状样式/音效按 ID 取，运行时换预设即换肤，支持过渡 | `Skills/evo-ui-styler-themes/SKILL.md` L20-40 | ★★★☆☆（Gear 需要 UGUI+UITK 双栈版本） |
| A6 | **控件覆盖面**：97 个预制体（下拉/日期/颜色选择/开关/图表/Toast/Tooltip/ContextMenu） | `Evo UI/Prefabs/` 13 类 97 个 | ★★☆☆☆（Gear 走 UITK，重复建设） |

### 1.2 劣势（按"会不会咬人"排序）

| # | 劣势 | 证据 | 对 Gear 的影响 |
|---|---|---|---|
| B1 | **商业资产、不可再分发** | Asset Store 410788；`THIRD-PARTY-NOTICES.md` 已把 Asset Store 资产标为「不可单独再分发」；Gear 是 Apache-2.0 公开 UPM 包 | 只能进宿主游戏工程，进仓库即违规 |
| B2 | **uGUI-only** | 技能 Boundaries：「不提供 UI Toolkit 控件」 | 与 Gear「菜单走 UITK」的既定方向相反 |
| B3 | **强依赖 Evo Tween** | 38 个运行时文件 `using Evo.Tween`；`ModalWindow` 等直接引用 | 与仓库内 DOTween 形成双补间引擎 |
| B4 | **体积** | Evo UI 66MB（Demo 34MB、Prefabs 11MB、Fonts 7.5MB、Sprites 6.6MB）+ Evo Tween 1.5MB | 框架包体不可接受 |
| B5 | **中文缺字** | 只带 Inter（拉丁字形），TMP 字体资产 3 个 | 中文 UI 需自备 SDF 字体回退 |
| B6 | **画布基准写死 2560×1440** | `Scripts/Core/Utilities.cs` L555 | 与 Gear 窗口 1920×1080 冲突，元素会放大 ~33% |
| B7 | **完整程序的移动端代价** | 技能 L299：渐变/裁剪/多边形约 3×（桌面）、约 10×（Mali） | 全屏卡片要单独 Canvas |
| B8 | **自带导航/EventSystem 假设** | `UINavigation : Selectable`、`VirtualCursor` | 与 Gear `UIInputSystem`（焦点域/多设备/方向盘）互不认识 |

### 1.3 结论：可借鉴 vs 不可借鉴

- **该学**：A1 顶点化、A2 材质池/样式页、A3 程序分级、A4 编辑器工具链、A5 主题令牌思路。
- **不该学**：整包引入、Evo Canvas/EventSystem 自动创建、Evo 导航、Evo Tween、Styler 的资产格式。

---

## 2. Gear 现状（实测，不是估计）

### 2.1 ImageEx 的三个成本点

| # | 现状 | 位置 | 代价 |
|---|---|---|---|
| C1 | `Dynamic` 模式下每个 ImageEx 一份独立材质 | `ImageEx.Runtime.cs` L61 `DynamicMaterial` | 每个实例一段 draw call；`UI Example.unity` 里有 **29** 个 ImageEx 实例 |
| C2 | `Shared` 模式靠"最后写入者决定" | `ImageEx.Runtime.Rendering.cs` 的 `DiagnoseSharedMaterialContention`（同文件 L143 附近） | 多实例共用一份材质时形状/颜色互相覆盖，只能靠告警提醒 |
| C3 | 每次 `GetModifiedMaterial` 写 **88 处** `mat.Set*`，再加 13 个形状结构体的 `ModifyMaterial` | `ImageEx.Runtime.Rendering.cs` L196-360（`mat.Set` 计数 88） | 动画/悬停/参数变化时每帧上百次 uniform 写入；CPU 侧开销，且每次写脏都会重建材质状态 |

> 注：Gear 其实已经做了 A1 的一半——顶点里已经有 `uv1`（归一化形状坐标）、`uv2`（尺寸）、`tangent.w`（阴影顶点标记），
> 见 `Common/ImageHelper.Internal.cs` L45/L64/L68。**缺的是把颜色/描边/渐变/阴影颜色等"样式参数"也搬出 uniform**，
> 这才是 Evo 能合批的真正原因。

### 2.2 UI 工具的四个断点

| # | 现状 | 位置 | 代价 |
|---|---|---|---|
| D1 | 预设资产（`ImageExPreset`）存在，但**没有浏览器**：只能靠 Project 面板翻 | `Runtime/ImageExPreset.cs`（`ApplyTo`/`ReadFrom`/`SameAs` 齐全） | 效果不可发现，新人不知道有哪些可复用效果 |
| D2 | 设置**不能跨对象同步**：逐个手调 | `Editor/Scripts/ImageExEditor.*` | 多按钮/多卡片统一风格靠人工 |
| D3 | 没有**批处理诊断**：不知道选中对象里有多少独立材质、会不会炸 draw call | 无 | 性能问题只能靠 Frame Debugger 事后发现 |
| D4 | 场景里点选只能点到空容器 | 无 | 调 UI 时频繁点错层 |

---

## 3. 升级方案（P0 / P1 / P2 分级）

### P0 —— 先把"看得见、调得动"补上（本次已实现）

**P0-1 `ImageEx 工具台` 窗口**（`Gear/ImageEx 工具台`）
- **预设库**：扫描全工程 `ImageExPreset`，显示名称 + 效果徽章（渐变/模糊/过渡/边缘/阴影/细节/色调/目标），支持搜索、定位资产、**应用到选中**、**从选中 ImageEx 反建预设**；每项带 64×64 缩略图（`PreviewRenderUtility` 渲染，失败自动回退为占位色块，不影响窗口使用）。
- **批量**：把当前选中第一个 ImageEx 的完整设置（除 Sprite / 材质 / 相机纹理）同步给其余选中对象；统一材质模式；清理"开关关了但参数还留着"的冗余值。
- **诊断**：按材质模式/形状/启用效果统计，给出"材质实例估算"和合批建议；支持「分析选中」与「分析整个场景」；一键全选场景内 ImageEx。
- **合批**：按"材质设置完全一致"分组（SerializedObject 逐属性比对，含 Gradient/AnimationCurve），每组生成并分配一份共享材质；支持整场景扫描与整场景分发（带二次确认）；可还原为 Dynamic、可一键清理生成材质。详见 P2 第 0 步。
- **场景拾取**：`Gear/UI/拾取光标下的 Graphic`（Ctrl+Shift+G），把光标下"视觉最上层"的 Graphic 设为选中，避免点到透明容器/空 RectTransform；只处理 World Space / Screen Space - Camera 的 Canvas（Overlay 不在场景视图绘制）。

**P0-2 效果组件**（纯 C#，复用 ImageEx 已有 shader 能力，不新增 shader）
- `ImageExDashCrawler`：驱动 `CustomTime`，做虚线描边流动（跑马灯描边）。
- `ImageExGradientSpinner`：驱动 `GradientEffect.Rotation`，做线性渐变旋转（呼吸/流光底）。
- `ImageExTransitionLooper`：驱动 `TransitionRate`，做加载/进度式的过渡扫过（支持两端停留的往返）。

> 这三个动效只改 ImageEx 的公开属性，不接管渲染，也不引入任何补间库；时间基准可选 scaled/unscaled，
> 暂停菜单、`timeScale=0` 场景下仍可动。

**P0-3 主题令牌**（等价于 Evo Styler 的最小实现，纯 C#、不碰 shader）
- `ImageExThemeAsset`：按 ID 存颜色 / 数值（`Accent`、`Background`、`Radius` 之类）。
- `ImageExThemeService`：当前主题 + 切换事件 + 默认主题（`Resources/UITheme/ImageExDefault`）。
- `ImageExThemeBinder`：挂在 ImageEx 上，把 tokenId 写到 `Tint / 描边色 / 边缘色 / 内阴影色 / 阴影色 / 过渡色 / 4 个宽度`。
- `Gear/ImageEx 主题` 窗口：浏览主题、应用到场景、设为默认、列出解析不到的 token。
- 换肤只需切换 Theme 资产，不需要改预制体；未解析的 token 静默跳过并在窗口里提示。

### P0-4 示例卡片（已移除）

演示卡片与 Evo 的完成度差距较大，容易造成误导，已**删除生成入口**；
保留菜单 `Gear/UI 示例/删除 ImageEx 示例卡片`，用于一键清掉之前生成的卡片对象（走 Undo）。

### P1 —— ImageEx 效果补齐（需要动 shader，建议单独迭代）

| 顺序 | 效果 | 做法 | 状态 / 预估 |
|---|---|---|---|
| 1 | 内阴影（Inner Shadow） | 新增 `RM_InnerShadow.cginc`，在 SDF 距离内取 `smoothstep` 叠色；C# 侧加 4 个属性 + Inspector + Preset 字段 | ✅ 本次已实现 |
| 2 | 单 quad 外阴影（替代 `Outline8` 的 8 个 quad） | 新增 `ShadowMode.SdfOutline`：网格只生成 1 个外扩 quad（uv1 外插）+ 主图，片元按 `sampleSdf(sdf - grow)` 做等距膨胀 | ✅ 本次已实现（附加模式，默认仍用旧 `Outline8`） |
| 3 | 圆角裁剪进度（clip/progress） | 新增 `RM_Clip.cginc` + `_ClipMode/_ClipAmount/_ClipReverse/_ClipSoftness`，横向 / 纵向 / 径向（扇形），形状走 SDF 坐标、贴图走 UV；阴影通道同步裁剪 | ✅ 本次已实现 |
| 4 | 背景模糊（Backdrop Blur） | 新增 `UIBackdropBlur`：复制源相机渲染到 RT（排除 UI 层）→ 降采样 + 多次 4-tap 模糊 → 面板材质按屏幕 UV 采样；支持每 N 帧抓屏、不可见时暂停；玻璃参数 Tint / TintAmount / Saturation / Brightness / Noise | ✅ 已实现（需在编辑器内按实际观感调参） |
| 5 | 软遮罩（Soft Mask） | 用 ImageEx 的 SDF 当 mask，配合 `Mask`/Stencil 或直接传给子物体材质 | 1-2 人日 |
| 6 | 虚线像素级参数（长度/间隔/柔边/动画） | `RM_DashPattern`：矩形按像素、圆形按周长均匀排布；`DashLength=0` 回退旧 UV 逻辑；随 `CustomTime` 平移 | ✅ 已实现（圆头/周长闭合仍待专项） |

> 前 3 个是纯 shader + 属性扩展，收益/风险比最好；4、5 属于"新能力"，建议先做原型再排期。

> **P1-5 软遮罩的前置条件**：需要一套"可被遮罩"的 UI 着色器——在 `ImageEx.shader` 与 `UI/Default` 各加一个
> `_SdfMaskTex / _SdfMaskTransform` 变体，由 `ImageExSdfMask` 组件把自身 SDF 渲染成一张 8-bit 遮罩并广播给子物体；
> 子物体材质需支持采样该遮罩（新增 `RM_SdfMask.cginc`）。因为要同时覆盖 ImageEx 与原生 Image/Text，
> 且边缘质量必须实机看，建议单独排期，先做"只支持 ImageEx + 单层级"的试验版再推广。

> **预设覆盖范围（当前）**：渐变效果 / 渐变纹理 / 过渡 / 边缘 / 阴影 / 模糊 / 色调 / 颜色滤镜 / 目标 / 细节 / 采样 / 内阴影 / 进度裁剪。
> 形状（DrawShape + 13 个形状结构体）与描边宽度暂不在预设内，需要时再按同样方式补。

### P2 —— ImageEx 性能（架构级，建议单独立项）

目标：**同款样式的多个 ImageEx 合批**，同时不出现 Shared 模式的串扰。

**第 0 步（已落地）：编辑器侧「按设置分组共用材质」**
不改 shader、不改渲染路径，先把"设置完全一致"的实例合并到同一份材质：
- 判定用 SerializedObject 逐属性比对（等价于序列化结果），只排除 Sprite / 材质 / Raycast 事件 / Script；
- 组内实例写入共享材质的 uniform 完全相同，因此不存在串扰；
- 生成的材质是工程资产，名带 `ImageExShared_` 前缀，可一键清理。
- 收益上限：只对"外观完全一致的重复元素"有效（列表项、同款按钮、卡片），通常已覆盖 UI 的大头。

**第 1-3 步（未做，需要 shader 重构）：**

1. **样式页（Style Page）**：建一个 `ImageExStylePage`（材质 + 一张 1D 样式纹理/常量数组），把"填充色、描边色、阴影色、渐变端点、宽度"等写入页内槽位，每个图形只带一个 `styleIndex` 顶点属性；
2. **顶点补位**：把现在走 uniform 但"每实例不同"的数值（`StrokeWidth`、`OutlineWidth`、`FalloffDistance`、`ShapeRotation`、裁剪量等）补进 `uv3`/`normal` 的空闲通道（`uv1`/`uv2`/`tangent.w` 已被占用，见 2.1 注）；
3. **材质池**：`MaterialKey { shaderProgram, clipState, textureId, stylePage }` 作为字典键，同键复用同一份材质；程序分级沿用 A3（light/full 两套变体）。

验收指标：
- `UI Example` 场景：29 个 ImageEx 的 draw call 段数从"按实例数"降到"按样式页数"；
- 动画（悬停/过渡）期间不再每帧重建材质，只改页内槽位；
- Frame Debugger 中相邻同款图形能合批；
- 移动端全屏卡片开渐变时不劣化（必要时给渐变卡片单独 Canvas）。

> P2 是"值不值得做"的分水岭：如果项目 UI 规模一直是 3 个窗口 + 几十个 ImageEx，P0/P1 足够；
> 如果一个界面有上百个卡片/按钮，P2 才划算。

---

## 4. 本次落地清单（全部为 Gear 自研新脚本）

| 文件 | 作用 | 依赖 |
|---|---|---|
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageExMotionBehaviour.cs` | 动效组件基类（时间基准/启停/目标解析） | 仅 ImageEx |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageExDashCrawler.cs` | 虚线描边流动 | 仅 ImageEx |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageExGradientSpinner.cs` | 线性渐变旋转 | 仅 ImageEx |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageExTransitionLooper.cs` | 过渡进度循环（加载/流光） | 仅 ImageEx |
| `Assets/Gear/Utilities/UI/ImageExtensions/Editor/Scripts/ImageExToolkitWindow.cs` | 预设库 / 批量 / 诊断 / 合批 四分区窗口 | Gear.Utilities + Gear.Editor.Shared |
| `Assets/Gear/Utilities/UI/ImageExtensions/Editor/Scripts/ImageExBatchingTools.cs` | 按设置分组、分配共享材质、还原、清理生成材质 | Gear.Utilities |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageEx.InnerShadow.cs` | 内阴影字段/属性（4 个 uniform） | Gear.Utilities |
| `Assets/Gear/Resources/Shader/Common/RM_InnerShadow.cginc` | 内阴影片元实现（SDF 深度叠色） | 2D_SDF.cginc |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Common/ImageHelper.cs` | 新增 `GenerateSdfShadowSprite`（单 quad 外阴影网格） | 现有 ImageHelper |
| `Assets/Gear/Resources/Shader/Common/RM_Shadow.cginc` | 新增 `_ShadowMode == 9` 的等距膨胀分支 | RM_SDFShapes |
| `Assets/Gear/Resources/Shader/Common/RM_Clip.cginc` | 进度裁剪（横向 / 纵向 / 径向） | 无 |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageEx.Clip.cs` | 裁剪字段/属性（4 个 uniform） | Gear.Utilities |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme/ImageExThemeAsset.cs` | 主题令牌资产（颜色/数值） | Gear.Utilities |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme/ImageExThemeService.cs` | 当前主题 / 切换事件 / 默认主题 | Gear.Utilities |
| `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme/ImageExThemeBinder.cs` | tokenId → ImageEx 属性绑定 | ImageEx |
| `Assets/Gear/Utilities/UI/ImageExtensions/Editor/Scripts/ImageExThemeWindow.cs` | 主题浏览 / 应用 / 设默认 / 未解析检查 | Gear.Utilities |
| `Assets/Gear/Utilities/UI/Backdrop/UIBackdropBlur.cs` | 背景模糊：抓屏相机 + 多级 4-tap 模糊 + 屏幕 UV 面板 | Gear.Utilities |
| `Assets/Gear/Resources/Shader/UI/BackdropBlur/BackdropBlurPanel.shader` | 面板按屏幕 UV 采样模糊结果 | — |
| `Assets/Gear/Resources/Shader/UI/BackdropBlur/BackdropBlur.shader` | 4-tap 模糊 Pass（Hidden） | — |

> 新增脚本已用 Roslyn（Unity 6000.3 自带 `DotNetSdkRoslyn/csc.dll`）对 `UnityEngine` / `UnityEditor` 引用做过一次类型检查，
> 运行时 4 个文件与编辑器窗口均无编译错误；`.meta` 由 Unity 首次导入自动生成。

**边界（重要）**
- 不复制 Evo 的源码、预制体、贴图、字体、Skill 文档；
- 不引用 `Evo.UI` / `Evo.Tween`，不新增任何 `Evo.*` 命名空间；
- 新组件的类名、菜单、字段命名全部走 Gear 现有风格（`Gear.EditorTools.*` / `UI/Gear/...`）；
- 不修改 ImageEx 的现有渲染路径（P0 只读公开属性；shader 级改动留到 P1）。

---

## 5. 验证方法

1. **编译**：Unity 6000.3.10f1 打开工程，Console 无红错；`Gear.Utilities.UI.ImageExtensions.Editor` 程序集能编译。
2. **工具台**：`Gear/ImageEx 工具台` 能打开；预设列表能扫到 `ImageExPreset`，每项右侧有缩略图（预览失败会显示深色占位块，Console 有 warning）；应用预设后 Inspector 值变化、可 Ctrl+Z 撤销。
3. **合批**：选中若干外观一致的 ImageEx → 合批页「分析选中」应出现 ≥2 的组 → 「生成并分配共享材质」→ Frame Debugger 里这些实例应合并为同一段；「还原为 Dynamic」可回退。
   - 注意：分组后同一组是一个「样式单元」，单独改某个实例的特效参数会影响整组；要单独调参先还原 Dynamic。
3. **动效**：`Button Example` / `UI Example` 场景挂 `ImageExDashCrawler`（先开 `EnableDashedOutline=1`）和 `ImageExGradientSpinner`（先开 `GradientEffect.Enabled=1` + 线性），Play 后可见流动/旋转；关闭组件立即停止。
4. **内阴影**：选中一个形状 ImageEx（`DrawShape != None`）→ Inspector 勾「内阴影」→ 调宽度/柔和度/颜色，边缘内侧应出现压暗/染色；关闭后回到原样，形状外的像素不受影响。
   - 纯贴图（`DrawShape = None`）下不显示内阴影属于预期：贴图模式没有形状距离场。
5. **单 quad 外阴影**：`appendShadow = true` + 阴影模式选 `SdfOutline`（Inspector 下拉里新增的项）→ 阴影应是一圈等距外扩、圆角自然；对比旧 `Outline8` 的 8 向偏移阴影，边缘更顺滑。
   - 网格从「8 次阴影四边形（每次还附带一份主图复制）」降到「1 个外扩 quad + 1 个主图」，顶点与 overdraw 明显下降；用 Frame Debugger 对比顶点数即可。
   - 默认仍是旧模式，不影响既有预制体。
6. **进度裁剪**：Inspector「进度裁剪」选 `Horizontal / Vertical / Radial`，拖「比例」0→1，图形应像进度条/扇形冷却一样显现；「反向」从另一端开始；「柔和度」控制边缘像素过渡。
   - 形状路径走 SDF 坐标（圆角/描边/内阴影都保持），纯贴图路径走 UV；形状阴影也会跟随裁剪。
7. **主题令牌**：`Gear/ImageEx 主题` 窗口能扫到 `ImageExThemeAsset`；给 ImageEx 挂 `ImageExThemeBinder` 绑定 `Accent` 之类 token → 窗口「应用到场景」后组件颜色/宽度按主题变化；「设为默认」会写入 `Assets/Gear/Resources/UITheme/ImageExDefault.asset`，下次启动自动加载。
   - 换一套主题资产即可整体换肤；未解析 token 会在窗口里提示，不会报错。
8. **背景模糊**：在 Image / RawImage 上挂 `UIBackdropBlur`（不要挂 ImageEx）→ Play 后该面板应透出背后场景的模糊画面；调 `Downsample`（默认 2）与 `Iterations` 平衡质量与开销。
   - 性能开关：`Capture Interval`（每 N 帧抓屏一次，默认 1）、`Skip When Invisible`（alpha=0 时暂停抓屏）；移动端建议 `Downsample ≥ 2`、`Iterations ≤ 2`、`Capture Interval ≥ 2`。
   - 若出现黑屏/花屏，先确认 `Graphics.Blit` 后的 active RT 已还原（组件内已用 try/finally 处理）。
9. **场景拾取**：在 Scene 视图把光标移到某个 UI 图形上 → `Gear/UI/拾取光标下的 Graphic`（Ctrl+Shift+G）→ 应选中该 Graphic 而非外层容器；排序按 Canvas.sortingOrder + 层级深度。
10. **虚线像素级参数**：ImageEx 勾 `EnableDashedOutline` → Inspector「虚线（像素级）」里把长度设为 >0（如 24）、间隔 14、柔边 0.2 → 矩形虚线段长度一致、边缘变软；给物体挂 `ImageExDashCrawler` 后虚线会整体平移。
   - 长度保持 0 时仍是旧的 UV 比例虚线，旧资源观感不变。
11. **回归**：现有 ImageEx 渲染不变（P0 只改公开属性；P1-1 只在 `_EnableInnerShadow=1` 时进入新分支，P1-2 只在 `ShadowMode == SdfOutline` 时生效，P1-3 只在 `_ClipMode != 0` 时生效，P1-4 是独立组件，不挂就不生效）；`LoopScrollRect Example`、`UI Example` 无异常。
12. **性能（P2 前置观测）**：Frame Debugger 记录 `UI Example` 的 draw call 段数，作为 P2 的基线。

---

## 6. 后续可选（按收益排序）

1. P1-5 软遮罩（Soft Mask）：用 ImageEx 的 SDF 当 mask（方案见 P1 节后的前置条件说明）；
2. 预设缩略图支持"应用到选中"的即时预览（当前只显示静态缩略图）。
