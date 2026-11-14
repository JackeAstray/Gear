# EvoDemo 复刻评估：ImageEx 技术取舍与 Gear UI 展厅路线

> 目的：回答三个问题——(1) Evo 的代码和设计对 Gear 的 ImageEx 有没有帮助；(2) 对 Gear 的 UI 系统设计有没有帮助；
> (3) EvoDemo 里的 UI 模块能不能复刻进 Gear，先复刻哪些。
>
> 基线：Gear 2.0.0 + 当前 Unreleased；参照物为工程内 `Unity/Gear/Assets/Evo`（Evo UI 2.0.6 + Evo Tween 2.0.1）。
> Evo UI 运行时约 **6.9 万行 C#**、Demo 约 **1.7 万行**、**97 个预制体**、**15 个 Demo 脚本分组**、**6 个整屏 Showcase**、
> 主场景 **19 MB**。Evo 仅作本地参照，**不提交、不分发、不复制源码 / 着色器 / 预制体 / 字体 / 贴图**。
>
> 结论先行：**能帮，但只帮"零件"和"皮"，不帮"骨架"。**
> Gear 的窗口 / 路由 / 输入 / 本地化骨架明显强于 Evo，不要换；Evo 值得借的是
> **单组件表现力（ProceduralShape / SoftMask / TextEffects）**、**组件状态模型（Interactive + Styler）**、
> **导航与覆盖层的交互细节**，以及 **Demo 的"分类页 + 整屏样例"组织方式**。
> EvoDemo 可以复刻"交互与版式"，不能复制资产与代码；优先复刻 5 个高性价比模块，而不是 15 个页面全铺。

---

## 一、三句话结论

| 问题 | 结论 |
|---|---|
| Evo 对 ImageEx 效果有帮助吗 | **有**。ImageEx 的形状 / 滤镜 / 过渡已经比 Evo 多，差距在 **软边、形状命中、布尔运算、抖动、软遮罩、合批架构**。其中形状命中、圆角百分比、渐变 Zoom 是低成本快赢；软遮罩和 style-page 合批是架构级改造，收益大但要单独排期。 |
| Evo 对 Gear 的 UI 系统设计有帮助吗 | **部分有**。Evo 没有窗口管理 / 路由 / 焦点域 / 本地化，这几块 Gear 更强，不能照搬。可借的是 **Interactive 五态模型**、**Styler 七类 token**、**Tab / Stepper / Page 三件套**、**Toast / Tooltip / ContextMenu / Popover 的细节行为**。 |
| EvoDemo 能复刻到 Gear 吗 | **能，但要"重写"而不是"搬运"**。Demo 的 15 个分类、6 个整屏 Showcase 里，真正值得复刻的是约 5 类效果 + 3 类结构件 + 少量数据组件；VirtualCursor、CurvedCanvas、DockMagnifier、完整图表库等不建议跟。 |

一句话给决策者：**把 Evo 当成"视觉与交互的参考样机"，不要当成"可以直接接进来的 UI 框架"。**

---

## 二、两套 UI 的真实差距

### 2.1 Gear 已经领先、不要向 Evo 看齐的部分

| 能力 | Gear | Evo |
|---|---|---|
| 窗口生命周期 | `UISystem`：四层根节点、打开序号、对象池、加载单飞、超时、结果 | 只有单个 `ModalWindow`，无窗口系统 |
| 模态与返回 | `UIModalStack` + `UIRouter` 导航栈 + ESC/手柄返回 + 栈同步 | ModalWindow 单例，不排队、无返回栈 |
| 输入与焦点 | `UIInputSystem`：焦点域、设备档案、改键、本地多人槽位、滚轮导航 | `Selectable` 导航 + `VirtualCursor`，无改键/多人槽位 |
| 本地化 | `LanguagesSystem`（TMP 自动翻译） | 依赖外部 `EVO_LOCALIZATION` 集成 |
| UI Toolkit | `UIToolkitSystem` 面板栈 + USS | 明确不支持 |
| 列表虚拟化 | `LoopScrollRect`：分页、下拉刷新、选中、横向/纵向 | `VirtualizedList` / `ListView`，能力较窄 |
| 树形列表 | `TreeView` | 无 |
| 图像滤镜 / 过渡 | 色调、颜色、边缘、像素化、目标、细节、采样 + Fade/Melt/Shiny 等过渡 | 以 ProceduralShape 的形状能力为主，滤镜较少 |

> 这张表的含义：**Gear 的"骨架"是资产，不要为了 Evo 的组件而改骨架。**
> Evo 的组件是长在它自己的 `Styler` / `Evo Tween` / `InputSystemUIInputModule` 假设上的；直接接进来会得到两套主题、两套输入、两套动画。

### 2.2 Evo 明显更强、值得拆开研究的部分

| 能力 | Evo 的做法 | Gear 现状 |
|---|---|---|
| 单 Graphic 多图层 | 一个 `ProceduralShape` 同时画填充、描边、虚线、内/外阴影、图案、裁剪、布尔运算 | `ImageEx` 已覆盖大部分，但**没有布尔运算、形状软边、抖动、形状命中** |
| 合批架构 | **Style Page**：一个 Canvas 一个材质，每个实例的样式写进 RGBAFloat 数据纹理；lite/full 双程序按需切换 | 默认每实例 `Dynamic` 材质；只有编辑器合批工具按指纹分组 |
| 软遮罩 | `SoftMask` ≤4 层、可反相、可隐藏遮罩本体、Sprite/程序化矩形两种来源、TMP 变体 | 只有硬 `Mask` |
| 文本动效 | `TextEffects` 12 种、TMP `ITextPreprocessor`、标签作用域 | 无 |
| 主题 token | 颜色 / 字体 / 渐变 / Sprite / 音频 / 形状样式 / 文本样式 七类 | 只有颜色 / 数值 token |
| 控件库 | 97 个预制体、15 个分类、图表 / 日历 / 取色器 / 选择器 / 进度条 | 以 uGUI 原生控件为主 |
| 覆盖层细节 | Toast 倒计时条 / 音效 / 堆叠协调；Tooltip 延迟 / 池；ContextMenu 子菜单；Popover 外点关闭 | 基础版 Toast / Tooltip / ContextMenu，无 Popover |
| Demo 组织 | Home 卡组 + 分类 Tab + 整屏样例 + 标签 + 搜索 + 主题切换 | 单个 `UI Example` 场景，以 ImageEx 画廊为主 |

---

## 三、Evo 为什么"好看"：五个可拆的技术点

### 3.1 ProceduralShape：一个 Graphic 承担全部形状语义

`ProceduralShape` 是 uGUI `Graphic`，在 shader 里做 SDF，一个组件同时支持：

- 形状：矩形、椭圆、多边形、星形、心形、Chevron；
- 圆角：像素 / 百分比、四角独立、`RadiusSyncMode`（MatchParent / MatchTarget / MatchAncestor）；
- 填充：纯色 / 渐变（Linear / Radial / Angular，Local / ScreenSpace）、渐变 Zoom；
- 描边：实线 / 虚线、圆头帽、渐变描边；
- 阴影：内阴影 / 外阴影，均带 Offset / Size / Softness；
- 裁剪：水平 / 垂直 / 径向 360，用于进度环；
- 图案：形状图案 / Sprite 图案，带 falloff；
- 布尔运算：Union / Subtract / Intersect / Exclude，最多 4 个操作数；
- Sprite：Stretch / Cover / Fit；
- 命中：`RaycastMode` None / Standard / Shape；
- 抖动：Dither，消除 8bit 色带；
- 导出：1x–4x、9-slice、可染色、可替换为 Image。

> 证据：`Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeTypes.cs`（枚举与 API）、
> `ProceduralShape.cs`（1531 行）、`ProceduralShapeMaterial.cs`（1714 行）。
>
> **对 Gear 的启示**：ImageEx 已经覆盖 12 种形状 + 大部分滤镜/过渡，不必换组件。
> 真正值得补的是 **形状命中、圆角百分比/同步、形状软边、抖动、布尔运算**；
> 其中前三个是"C# 侧 + 少量 shader"的快赢，布尔运算要扩顶点数据，放到后面。

### 3.2 Style Page：Evo 单 Canvas 合批的核心

Evo 的合批不是"共享材质"这么简单：

- 每个 Canvas 维护一个 `StylePage`，一个 `Material` 服务该页所有 `ProceduralShape`；
- 每个实例分配一个 `styleSlot`，把颜色、渐变、图案、阴影、虚线等写进一张 `RGBAFloat` 数据纹理的对应行；
- 顶点流里带 `styleIndex`，shader 按索引读行；
- 渐变放独立 atlas，图案放独立纹理；
- 默认跑 **lite** 程序，只有当某个形状需要星形/心形/内阴影/布尔/虚线等完整能力时，才把整页切到 **full** 程序；
- 颜色 tween 只改数据纹理，不重建网格、不换材质。

> 证据：`Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeMaterial.cs` 的
> `Batched Style Data` / `StylePage` 段（约 624–1200 行）。
>
> **对 Gear 的启示**：这是 Evo 与 Gear 最大的**架构级**差距。Gear 默认每实例 `Dynamic` 材质，
> 材质不同就打断合批；`Shared` 模式又有"最后写入者决定"的串扰（见已有路线图）。
> 但 style-page 改造要动 **顶点流 + shader + 材质生命周期 + 主题服务**，属于 1–2 周的高风险改造，
> **不应作为第一刀**。先用"按指纹共享 + 不回读"把 Shared 模式修好，等 draw call 真的成为瓶颈再上 style page。

### 3.3 SoftMask：不是 RT，是 shader + 材质修饰器

Evo 的 `SoftMask` 关键设计：

- `MaxMaskDepth = 4`，最多 4 层嵌套；
- 自己是一个 `IMeshModifier`，并给子树每个 `Graphic` 挂隐藏的 `SoftMaskable`（`IMaterialModifier`）；
- 把遮罩的 Canvas→Local 矩阵、Rect、圆角、填充数据、反转标志、Sprite 纹理索引写进 shader 全局数组；
- 支持 `showMaskGraphic`（遮罩本体是否绘制）、`invertMask`；
- 为 `Image` / `RawImage` / TMP 准备不同 shader 变体；
- 用材质缓存 + 层级变更回调，避免每帧扫描。

> 证据：`Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/SoftMask.cs`（1065 行）、`SoftMaskable.cs`。
>
> **对 Gear 的启示**：已有路线图里的"3a Canvas 级全局 uniform / 3b RT"之外，Evo 给出了第三条路——
> **按 Graphic 挂材质修饰器 + shader 内掩码栈**。它支持嵌套和反相，但会打断合批、要维护多套 shader 变体。
> 建议：**先做 3a（每 Canvas 一个矩形/圆角软遮罩，无 RT，覆盖滚动渐隐）**；
> 只有在"嵌套遮罩 / 任意形状遮罩"成为真实需求时，才按 Evo 的语义补 3b。

### 3.4 Interactive + Styler：组件状态与主题的分离

- `InteractionState` 五态：`Disabled / Normal / Highlighted / Pressed / Selected`；
- `Interactive` 把 hover / press / select 合成一个状态机，`BorderGlow`、`Spotlight`、`Ripple` 等效果订阅状态；
- `StylerPreset` 存七类 token：`audioItems / colorItems / fontItems / gradientItems / shapeStyleItems / spriteItems / textStyleItems`；
- 组件按 ID 取 token（如 `"Accent"`），换 Preset 即换肤；单个对象可 `useCustomColor` 局部覆盖；
- 运行时切换支持颜色/渐变过渡。

> 证据：`Scripts/Core/Definitions.cs`（五态）、`Scripts/Styler/StylerPreset.cs`（七类 token）、
> `Scripts/UI Elements/Base/Interactive.cs`。
>
> **对 Gear 的启示**：Gear 有 `ButtonAni` / `ButtonClickEvent` / `DoubleClickButton`，但没有统一的五态模型，
> 主题也只有颜色/数值。**借模型，不借实现**：在 `Utilities/UI` 下加一个轻量 `GearInteractive`（五态 + 事件），
> 把 `ImageExThemeAsset` 扩成"颜色 / 数值 / 字体 / Sprite / 渐变"五类 token（音频 token 先不做）。

### 3.5 TextEffects：TMP 预处理器 + 逐字符网格快照

- `TextEffects : MonoBehaviour, ITextPreprocessor`；
- 12 种效果：Typewriter、Scramble、Palette、AlphaPulse、Shimmer、Wave、Bounce、Shake、Glitch、Pulse、Swing、Wiggle；
- 支持 `EntireText` / `TaggedCharacters` 两种作用域，标签解析器把 `<wave>` 之类的标记映射到字符掩码；
- 每帧只对受影响的字符做 mesh 快照/还原，避免整段重建。

> 证据：`Scripts/Effects/TextEffects.cs`（1455 行）。
>
> **对 Gear 的启示**：这是 EvoDemo 里"最有意思"的效果之一，但也是**兼容性风险最高**的：
> Gear 的 `LanguagesSystem` 已经接管 TMP 文本，预处理器顺序、富文本标签、翻译后的字符索引都要重新验证。
> 建议先做**整段文本**的 Typewriter / Wave / Shake 三件套（不做标签解析），验证与翻译系统共存后再扩。

---

## 四、ImageEx：借什么、怎么借

### 4.1 能力取舍表

状态：✅ 已有　🟡 部分　❌ 缺失

| 能力 | Gear 现状 | Evo 机制 | 收益 | 成本 | 建议 |
|---|---|---|---|---|---|
| 形状命中 `RaycastMode` | ❌ 矩形命中 | 用同一套 SDF 求距离，`Standard / Shape / None` | **高**：透明角误点立刻消失 | 低（0.5–1 天，纯 C#） | **P1 立即做** |
| 圆角百分比 | 🟡 像素 + 四角独立 | `RadiusMode.Pixels / Percentage`，100 = 半短边 | 中：胶囊/异形按钮适配尺寸 | 低（C# 换算） | **P1 立即做** |
| 圆角同步 | ❌ | `RadiusSyncMode` MatchParent / Target / Ancestor | 中：嵌套卡片圆角一致 | 低 | P1 顺手做 |
| 渐变 Zoom | ❌ | `FillGradientZoom` | 低：渐变聚焦 | 低（shader 乘子） | P1 顺手做 |
| 形状软边 `Softness` | ❌ | 羽化几何边，与描边/阴影共存 | 中：柔和卡片、光晕边 | 中（shader `smoothstep`） | P2 |
| 抖动 `Dither` | ❌ | Bayer 抖动消色带 | 低：暗色渐变更干净 | 低（shader） | P2 |
| 斜切 `ShapeSkew` | ❌ | SDF 坐标斜切 | 低：倾斜面板 | 低–中 | P2 |
| 布尔运算 | ❌ | Union / Subtract / Intersect / Exclude ≤4 | 中：图标、徽章、组合形状 | 中–高（扩顶点数据） | P3，按需求 |
| 形状导出 Sprite | ❌ | 1x–4x、9-slice、可染色 | 中：美术/性能优化 | 中（编辑器工具） | P3 |
| 多层嵌套 / Twist | ❌ | 多层同形状、逐层旋转/缩放 | 低：EvoDemo Shapes 页的视觉 | 中 | 不做，用子节点拼 |
| 软遮罩 | ❌ | ≤4 层、反相、Sprite/程序化 | **高**：滚动渐隐、柔和面板 | 高（5–8 天起） | **P2 重点** |
| 文本动效 | ❌ | 12 种、标签作用域 | **高**：Demo 观感 | 高（3–5 天起） | **P2 重点** |
| Style Page 合批 | ❌ | 单 Canvas 单材质 + 数据纹理 | **高**：大量 ImageEx 的 draw call | 高（1–2 周，架构级） | P3，先测量再决定 |
| lite/full 双程序 | ❌ | 按需切程序，减少变体开销 | 中 | 中–高 | P3，随合批一起 |

### 4.2 三个"不要"

1. **不要把 ImageEx 换成 ProceduralShape。** Gear 的滤镜/过渡更全，换组件会丢掉现有画廊、预设、主题和合批工具。
2. **不要为了"像 Evo"而引入 Evo Tween。** Gear 已有 DOTween；EvoDemo 的弹簧/序列可以映射到 DOTween，不必多一套动画栈。
3. **不要先做 style-page。** 它是对的，但它是"性能架构"，不是"效果"；先用诊断工具量出 draw call，再决定是否值得。

### 4.3 ImageEx 的"快赢"清单（建议直接排进下一轮）

- `ImageEx.IsRaycastLocationValid` 用 SDF 判定（Standard / Shape / None）；
- `RectangleCornerRadius` 支持百分比模式 + 半径同步；
- `GradientEffect` 加 `Zoom`；
- 画廊各加 1–2 张卡，EditMode 用例覆盖 0/50/100% 换算与命中边界；
- 旧资源默认行为不变（新增字段默认关闭）。

---

## 五、UI 系统：借什么、不借什么

| Evo 的做法 | 借不借 | 在 Gear 里怎么落 |
|---|---|---|
| 单 ModalWindow、无窗口系统 | **不借** | Gear 的 `UISystem` + `UIRouter` 已更强 |
| `Selectable` + `VirtualCursor` 导航 | **不借** | Gear 有焦点域 + 设备档案 + 改键；主机需求用焦点导航解决 |
| UniText 文本后端 | **不借** | Gear 用 TMP + `LanguagesSystem` |
| 2560×1440 画布基准 | **不借** | Gear 覆盖层按 1920×1080 |
| `Interactive` 五态 + 效果订阅 | **借模型** | 新增 `GearInteractive`，把 `ButtonAni` / 点击扩展 / 辉光统一到状态机 |
| `Styler` 七类 token | **借模型** | 扩 `ImageExThemeAsset`：颜色 / 数值 / 字体 / Sprite / 渐变；音频先不做 |
| `TabManager` / `Stepper` / `PageManager` | **借交互** | 新增 `GearTabGroup` / `GearStepper` / `GearPageView`，接 `UIInputSystem` 焦点 |
| Toast 倒计时条 / 队列 / 音效 | **借细节** | 扩 `GearToast`：排队、时长条、可选音效 |
| Tooltip 延迟 / 池 / 最大宽 | **借细节** | 扩 `GearTooltip`：延迟、池、最大宽、富内容 |
| ContextMenu 子菜单 / 自定义行 | **借细节** | 扩 `GearContextMenu`：子菜单、自定义行、禁用项 |
| Popover 外点 / Esc / 开关动画 | **借** | 新增 `GearPopover`，挂 `GearOverlayLayer` |
| 图表 / 日历 / 取色器 / 选择器 | **按需** | 只做真实界面需要的最小集，不照单全收 |

### 5.1 关键设计约束：导航组件必须接焦点系统

Evo 的 Tab/Stepper 只关心"显示哪一页"，不关心"焦点去哪"。Gear 如果照抄，会出现：

- Tab 切换后焦点还停在旧页按钮上；
- 手柄/键盘用户看不到新页的第一个可选项；
- 返回键被旧页的 `Cancel` 消费。

所以 `GearTabGroup` / `GearStepper` / `GearPageView` 的验收必须包含：

- 切页后把焦点移到新页的 `firstSelected`（没有则找第一个 `Selectable`）；
- 旧页的 `UIFocusScope` 失活；
- `UIRouter.Back()` 与页内返回不冲突。

---

## 六、EvoDemo 模块清单与复刻分级

### 6.1 Demo 的实际构成

EvoDemo 不是一个"组件列表"，而是 **Home 卡组 + 14 个分类页 + 1 个 Shared 公共件目录 + 6 个整屏样例**：

- **Home**：卡组（可拖拽/甩出）、焦点层、组件标签、主题切换、搜索；
- **14 个分类页**：Buttons、Color、Data Display、Date and Time、Effects、Layout and Navigation、
  Overlays、Procedural Shape、Progress and Loading、Selection、Sliders、Text Input、
  Toggles and Switches；外加 **Shared**（主题选择、搜索、弹簧等跨页公共件）；
- **6 个整屏 Showcase**：Audio Mixer、Card Game、Fishing/Trip Planner、Mission Briefing、Party Game、Ship Console。

> 证据：`Unity/Gear/Assets/Evo/Evo UI/Demo/Scripts/Home/DemoHome.cs`（1155 行，卡组/弹簧/焦点/标签）、
> `Demo/Scripts/*` 15 个分类目录、`Demo/Prefabs/Home/` 6 张卡。

### 6.2 分类页复刻价值

| 分类 | 代表模块 | Gear 现状 | 复刻价值 | 建议 |
|---|---|---|---|---|
| Effects | SoftMask、TextEffects、BorderGlow、Spotlight、Ripple、PixelCurtain、InkPanel、TopographicSkin、WarningStripe、DockMagnifier | 有 BorderGlow、Ripple、BackdropBlur；无 SoftMask/TextEffects/Spotlight/PixelCurtain | **最高**：Demo 的"惊艳感"主要来自这里 | 先做 SoftMask + TextEffects + Spotlight；PixelCurtain 次之 |
| Layout and Navigation | TabManager、Stepper、PageManager、Carousel、ReorderableList、Marquee、RadialLayout、VirtualCursor、MenuBar | 有 Carousel；无 Tab/Stepper/Page/Reorderable | **高**：任何设置/向导/多页界面都需要 | 先做 Tab + Stepper + Page；其余按需 |
| Overlays | ModalWindow、Toast、Tooltip、ContextMenu、Popover、OffScreenIndicator | 有 Modal/Toast/Tooltip/ContextMenu 基础版 | **高**：细节差距最容易感知 | 先补 Toast 队列/时长条、Tooltip 延迟/池、ContextMenu 子菜单、Popover |
| Data Display | Line/Pie/Radar/Vertical/Horizontal Chart、ListView、Counter | 只有 LoopScrollRect / TreeView | 中：数据页/结算页才需要 | 按真实界面做最小集（折线/柱/环 + Counter） |
| Progress and Loading | ProgressBar、DistributionBar、Spinner、Timer | 无专用组件 | 中：加载/倒计时常用 | 做 ProgressBar + Timer 两个即可 |
| Selection | Selector、QuantitySelector、PickerWheel、Dropdown | 原生 Dropdown | 中：选型/数量/滚轮 | 按需，先做 QuantitySelector |
| Sliders | Slider、RangeSlider、RadialSlider、ComparisonSlider | 原生 Slider | 中：设置页常用 | RangeSlider 优先 |
| Toggles and Switches | Toggle、Switch、RadioButtonGroup | 原生 Toggle | 中：设置页常用 | Switch + RadioGroup 优先 |
| Text Input | InputField、InputForm、FriendList | TMP 原生 | 低–中 | 按需 |
| Buttons | Button、ProgressButton、FilterChips、SettingRow | uGUI Button + 点击扩展 | 中：版式价值大于逻辑 | 借"行/芯片/进度按钮"的版式 |
| Color | ColorPicker、Palette、Halo | 无 | 低 | 按需 |
| Date and Time | Calendar、TimePicker、Timer、Bid/Quiz | 无 | 低 | 按需 |
| Procedural Shape | 形状、圆角、渐变、描边、阴影、图案、布尔、导出、模糊 | ImageEx 大部分已有 | **中–高**：可直接对照补缺 | 借"形状命中/软边/布尔/导出"；不借组件 |
| Shared | ThemeSelector、Search、Spring、Customize | 主题有基础版；无搜索/弹簧 | 中：Demo 的粘合剂 | 只借 ThemeSelector 的"运行时换肤"思路 |
| Home | 卡组、焦点层、标签、背景 | 无 | 低–中：元 UI，不是业务界面 | **不建议先做**；先把分类页做出来 |

### 6.3 复刻分级

| 层级 | 内容 | 估算 | 产出 | 判断 |
|---|---|---|---|---|
| **T0 骨架** | 展厅场景 + 分类导航 + 卡片列表 + 详情窗口 + 返回 | 2–3 天 | 一个可浏览的 Gear UI 展厅 | 必做，但不要先做 Home 卡组 |
| **T1 效果** | SoftMask 滚动渐隐、TextEffects 打字机/波浪/抖动、Spotlight、PixelCurtain | 5–8 天 | Effects 页 4–5 张卡 | 最高性价比 |
| **T2 结构** | GearTabGroup、GearStepper、GearPageView、Toast/Tooltip/ContextMenu/Popover 升级 | 5–8 天 | 设置/向导/弹层样例 | 第二优先 |
| **T3 数据** | 折线/柱/环 + Counter + ProgressBar + RangeSlider/Switch | 2–4 周 | 数据页/设置页样例 | 有真实界面再做 |
| **T4 不建议** | VirtualCursor、CurvedCanvas、DockMagnifier、InkPanel、TopographicSkin、完整 97 预制体、Evo Tween | — | — | 与 Gear 架构冲突或 ROI 低 |

### 6.4 建议先复刻的 5 个模块

1. **SoftMask + 滚动渐隐**（Effects）：最像"Evo 的质感"，也最贴 Gear 的列表/弹窗打磨需求。
2. **TextEffects：Typewriter / Wave / Shake**（Effects）：Demo 里最抓眼球的动效，先做整段文本版。
3. **GearTabGroup + GearStepper**（Layout）：设置/向导/选型的高频结构件，必须接焦点系统。
4. **Overlay 细节**（Overlays）：Toast 队列/时长条、Tooltip 延迟/池、ContextMenu 子菜单、Popover。
5. **ImageEx 快赢**（Procedural Shape）：形状命中、圆角百分比、渐变 Zoom——不是"复刻 Demo"，但直接提升所有界面的手感。

---

## 七、Gear UI 展厅：落点方案

### 7.1 不做"一个大场景"，做"窗口 + 预制体"

EvoDemo 是一个 19 MB 的单场景，里面几百个对象、全部预摆。这在 Gear 里会：

- 拖慢场景打开与编译；
- 让 `UISystem` 的对象池/加载单飞失去意义；
- 让主题切换、分辨率适配难以统一。

建议结构：

```
Assets/Gear/Scenes/Example/Gear UI Showcase.unity     ← 只放一个启动窗口 + UIRoot
Assets/Gear/UI/Showcase/
├── ShowcaseUIController.cs        ← MainUI 层，分类导航 + 卡片列表
├── ShowcaseCatalog.cs             ← 分类/卡片数据（ScriptableObject 或静态表）
└── Cards/                         ← 每张卡一个 Prefab（ImageEx + 说明）
Assets/Gear/Resources/Prefabs/UIs/Showcase/
├── ShowcaseEffects.prefab         ← NormalUI 详情窗口（Effects 分类）
├── ShowcaseNavigation.prefab      ← NormalUI 详情窗口（Tab/Stepper）
└── ShowcaseOverlays.prefab        ← NormalUI 详情窗口（Toast/Tooltip/...）
```

### 7.2 交互流

```
启动 → ShowcaseUIController（MainUI）
      → 左侧/顶部 GearTabGroup 选分类
      → 中间 LoopScrollRect 卡片列表（每张卡是一个 ImageEx + 标题）
      → 点卡片 → UIRouter.PushAsync<ShowcaseDetailWindow, ShowcaseArgs>(cardId)
      → 详情窗口内演示该模块
      → ESC / 返回按钮 → UIRouter.Back()
      → 主题切换 → ImageExThemeService 换 Preset（不重建窗口）
      → Toast / Tooltip / ContextMenu → GearOverlayLayer
```

### 7.3 复用现有资产

- **ImageEx 画廊生成器**：`ImageExGalleryBuilder` / `ImageExGalleryCatalog` 已经能生成 100+ 张效果卡，
  展厅的 "Procedural Shape" 分类直接复用它，不要重写。
- **主题**：`Resources/UITheme/ImageExDefault.asset` + `ImageExThemeBinder`。
- **覆盖层**：`GearOverlayLayer` / `GearToast` / `GearTooltip` / `GearContextMenu`。
- **列表**：`LoopScrollRect`；卡片网格可先用 `GridLayoutGroup` + `LoopScrollRect`。
- **动画**：DOTween（已有），不要引入 Evo Tween。

### 7.4 明确不做

- 不复制 Evo 的预制体、贴图、字体、Sprites、Skills 文档；
- 不把 `Assets/Evo` 提交进仓库或打进 UPM 包；
- 不做 Evo 的 2560×1440 画布基准；
- 不为了展厅改 `UISystem` / `UIInputSystem` 的公开 API。

---

## 八、路线与验收

### 8.1 建议排期

| 阶段 | 内容 | 估算 | 前置 |
|---|---|---|---|
| P0 | 本文落库、更新索引、确认复刻范围 | 0.5 天 | — |
| P1 | ImageEx 快赢：形状命中、圆角百分比/同步、渐变 Zoom、画廊卡片、EditMode 用例 | 2–3 天 | P0 |
| P2 | 展厅骨架：场景 + `GearTabGroup` + 卡片列表 + 详情窗口 + 返回 | 3–5 天 | P1 |
| P3 | Effects 页：SoftMask 滚动渐隐 + TextEffects 三件套 + Spotlight | 5–8 天 | P2 |
| P4 | 结构/覆盖层：`GearStepper`、`GearPageView`、Toast/Tooltip/ContextMenu/Popover 升级 | 5–8 天 | P2 |
| P5 | 数据/控件最小集 | 2–4 周 | 有真实界面需求 |

> 估算按"一个人、熟悉 Gear 代码"计；不含美术出图与人工视觉验收。

### 8.2 每阶段共用验收

1. `dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear` 退出码 0
2. `dotnet build Unity/Gear/Gear.Utilities.csproj` 0 error
3. `pwsh Tools/unity-verify.ps1 -IncludePlayMode` 全绿（新增用例计入门槛）
4. 展厅/画廊有新入口；旧资源默认关闭时行为不变
5. CHANGELOG 一条 Added/Fixed + 对应设计文档更新
6. 文档改动跑 `dotnet run --project Tools/DocsCheck -- .`

> 视觉结论（软遮罩边缘、文本动效、辉光）没有 MCP 时仍需人工截图确认，不能只凭编译通过。

---

## 九、许可与风险

| 风险 | 影响 | 处理 |
|---|---|---|
| **Evo 是 Asset Store 商业资产** | 复制源码/着色器/预制体/字体/贴图会违反许可，Gear 是 Apache-2.0 公开仓库 | 只读作参照；行为与版式可 clean-room 重写；`Assets/Evo` **已误提交到公开仓库**（2699 个文件），需 `git rm --cached` 并评估历史清理，见[第二轮文档](Evo对照-第二轮-渲染契约与模块复刻清单.md) 1.3 |
| Evo Tween 依赖 | 抄组件会连带抄动画栈 | 不引入；用 DOTween 重写 |
| 两套输入/主题并存 | 焦点冲突、换肤不一致 | Evo 不接入 `UISystem` / `UIInputSystem` |
| SoftMask shader 变体扩散 | ImageEx + UI Default + TMP 三套维护成本 | 先只覆盖 ImageEx；其余按真实界面加 |
| Style Page 改造 | 顶点流/材质/主题服务联动，回归面大 | 先修 Shared 模式串扰；量出 draw call 再决定 |
| 文本动效与翻译冲突 | 预处理器顺序/标签/字符索引错位 | 先做整段文本版；标签版等 `LanguagesSystem` 兼容验证 |
| 范围蔓延 | 15 个分类全铺 = 数月 | 按 6.4 的 5 个模块先做；其余按需 |

---

## 十、决策规则

| 最近要做的界面 | 先做 |
|---|---|
| 设置 / 选型 / 向导 | `GearTabGroup` + `GearStepper` |
| 列表 / 弹窗 / 滚动区打磨 | SoftMask（3a 参数化遮罩） |
| 透明角误点、胶囊/异形按钮 | 形状命中 + 圆角百分比 |
| 数据展示 / 战绩 / 结算 | 折线/柱/环 + Counter |
| 需要"展厅/作品集"效果 | Effects 页：SoftMask + TextEffects + Spotlight |
| 主机/手柄为主 | 焦点导航 + `GearStepper`，不上 VirtualCursor |

---

## 附：证据索引

### Evo

- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShape*.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeMaterial.cs`（StylePage / lite-full）
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/SoftMask.cs` / `SoftMaskable.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/TextEffects.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Styler/StylerPreset.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Base/Interactive.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Scripts/Layout/{TabManager,Stepper,PageManager}.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Demo/Scripts/Home/DemoHome.cs`
- `Unity/Gear/Assets/Evo/Evo UI/Skills/*/SKILL.md`（8 篇能力说明）

### Gear

- `Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/**`
- `Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Overlays/**`
- `Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme/**`
- `Unity/Gear/Assets/Gear/Framework/Runtime/System/UISystem/**`
- `Unity/Gear/Assets/Gear/Framework/Runtime/System/UIInputSystem/**`
- `Unity/Gear/Assets/Gear/Framework/Runtime/System/UIToolkitSystem/**`
- `Unity/Gear/Assets/Gear/Resources/Shader/Common/RM_*.cginc`
- `Unity/Gear/Assets/Gear/Scenes/Example/UI Example.unity`

### 相关文档

- [Evo 对照：UI 能力差距与路线图](Evo对照-UI能力差距与路线图.md)
- [Evo 对照：ImageEx 清理与 UI 能力补齐](Evo对照-ImageEx清理与UI能力补齐.md)
- [Evo 对齐：ImageEx 阴影与 UI 覆盖层](Evo对齐-ImageEx阴影与UI覆盖层.md)
- [UI-ImageEx 与工具：参考 Evo 升级方案](UI-ImageEx与工具-参考Evo升级方案.md)
