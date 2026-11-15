# EVO 对照：UI 效果复刻差距与开发计划

> **基线**：Gear 2.0.0 + 当前 Unreleased（2026-11-15）；Unity 6000.3.10f1；参照物 `Unity/Gear/Assets/Evo`（Evo UI 2.0.6）。
> **方法**：静态读码 + Unity MCP（`http://127.0.0.1:28080`）实测。实测项包括两个场景的组件计数、层级结构、截图留档；
> 未做逐像素视觉比对（本机没有视觉模型）。文中“观感差距”指实现方式、参数面与场景构成的差距，不是主观打分。
> **边界**：Evo 是 Asset Store 商业资产，只读参照。**不复制 Evo 的源码、shader、预制体、贴图、字体、图标**；
> 复刻 = 用 Gear 自己的组件与 shader 重写行为、版式和参数。
> **配套**：本文替代此前的 Evo 对照文档，后续以本文为准。

---

## 一、先给结论

| 问题 | 结论 |
|---|---|
| 为什么“看上去差一些” | **约三成是模块缺失，七成是展厅与组合差距。** Gear 把能力拆成 150 张孤立卡片；Evo 是 14 个分类页 + 6 个整屏样例 + 85 个实时调参面板，同一屏里叠十几个组件、状态和动效。 |
| 用户列出的模块哪些真缺 | **真缺**：Progress Button、Color Picker（Wheel/Palette）、Showcase Panel、Ripple（Evo 版）、Dot Matrix Loader、Combine、Pattern（Sprite/Shape 图块）、Calendar/TimePicker/Timer 的部分能力。**已有但差距**：Backdrop Blur（只能糊场景、不能糊 UI）、Clipping、Shadows、Gradients、Dock Magnifier、Border Glow、Animated Container。**反而领先**：Shapes（12 种 vs 6 种）。 |
| 先做什么 | **M1 展厅与观感地基（6–10 天）**：分类页 + 3 个整屏样例 + 主题/状态/动效 + 已实现但没展出的能力补卡。**M2 缺失模块（12–18 天）**。**M3 形状工艺（5–8 天）**。M4 架构按需。 |

一句话：**不要再按“一个效果加一张卡”的方式补齐**。先把“同一屏里多个组件如何一起工作”做出来，再补真正缺的模块。

---

## 二、实测基线（Unity MCP）

### 2.1 场景规模

用 `execute_code` 在编辑器里统计 `FindObjectsByType<GameObject>` 与所有组件，得到：

| 指标 | Evo UI Demo Example | Gear UI Example | 说明 |
|---|---:|---:|---|
| GameObject 总数 | **12,660** | **1,418** | Evo 约 9 倍 |
| `ProceduralShape` / `ImageEx` | **4,317 / 0** | 0 / **686** | 单体数量差 6 倍 |
| `Button` | **719** | **61** | 可交互密度差 11 倍 |
| `StylerObject` / 主题绑定 | **3,510 / 0** | 0 / **0** | Gear 展厅没有主题绑定 |
| `Interactive` / `GearInteractive` | **92 / 0** | 0 / **1** | Gear 展厅几乎没有状态反馈 |
| `TweenAnimator` | **159** | DOTween（未统计） | Evo 的动效覆盖全场景 |
| `TextMeshProUGUI` | **2,474** | **533** | 文本密度差 4.6 倍 |
| `DemoCustomize`（实时调参面板） | **85** | **0** | Evo 每个分类都能现场调参 |
| `DemoScreen`（整屏样例） | **6** | **0** | Audio Mixer / Card Game / Fishing / Mission / Party / Ship Console |
| `Tooltip` | **39** | **0** | Gear 有组件但展厅没放 |
| `Notification` / `Toast` | **2 / 0** | **0 / 0** | 同上 |
| `ModalWindow` | **1** | **0** | Gear 有 `UIRouter`/`UIModalStack`，展厅没演示 |
| `SoftMask` / `GearSoftMask` | **5 / 0** | 0 / **2** | 嵌套/多层差距 |
| `ProgressButton` | **5** | **0** | 真缺 |
| `ShowcasePanel` | **1** | **0** | 真缺 |
| `Ripple` / `UIRipple` | **12 / 0** | 0 / **0** | Gear 有旧版 `UIRipple`，但展厅没放，也不是 Evo 的 shader 版 |
| `DotMatrixLoader` / `GearDotMatrix` | **2 / 0** | 0 / **1** | Gear 只有 ImageEx 图案，没有 loader 语义 |
| `ColorPicker` + `PaletteColorPicker` | **2 + 1** | `GearColorPicker` ×1 | 只有 SV 方块 |
| `Calendar` / `TimePicker` / `Timer` | **1 / 1 / 5** | **1 / 1 / 1** | 功能面差距，见 3.6 |
| `Carousel` / `Selector` / `FlexibleGridLayout` | **1 / 7 / 1** | 0 / 0 / 0 | 真缺通用组件 |
| `ReorderableList` / `MarqueeSelection` | **2 / 2** | 0 / 0 | 真缺 |
| `RadialSlider` / `ComparisonSlider` | **8 / 1** | 0 / 0 | 真缺 |
| `RectDragger` / `ButtonGroupDimmer` | **4 / 2** | 0 / 0 | 真缺 |

> 计数口径：`FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)`，包含未激活对象；
> 数值是 2026-11-15 当前场景的实测值，会随场景改动变化。Gear 场景截图留在 `.audit_tmp/shots/`；
> Evo 场景在 EditMode 下抓图全黑（疑似依赖运行时初始化），未作为视觉证据。

### 2.2 结构对比

**Evo**（`Main Canvas > Content`，14 页）：

```
Main Canvas
├── Background
├── Header
├── Content (TabManager)
│   ├── Home / Procedural Shape / Buttons / Color / Effects
│   ├── Data Display / Date and Time / Layout and Navigation
│   └── Overlays / Progress and Loading / Selection / Sliders / Text Input / Toggles and Switches
├── Dock (15 项，DockMagnifier + RadioButtonGroup)
├── Search Results (DemoSearch)
└── Intro (TweenAnimator + DemoIntro)
```

- 6 个整屏样例（`DemoScreen`）：Audio Mixer、Card Game、Fishing、Mission Briefing、Party、Ship Console；
- 每个分类页都带 `DemoCustomize` 面板（85 个），滑杆/单选/开关直接改运行时组件；
- 主题、状态、动效、图标、字体是同一套，页面之间视觉一致。

**Gear**（`UI Example`）：

```
Canvas
└── ImageEx 画廊
    └── Scroll View > Viewport > Content (GridLayoutGroup)
        └── 150 张卡（每张 400×340，一张卡演示一个效果/控件）
```

- 149 个唯一卡名，覆盖形状/投影/渐变/裁剪/描边/图案/过渡/滤镜/控件/文本/数据/动效/布局/导航/覆盖层；
- 卡片之间互不组合，只有 1 个 `GearInteractive`、1 个 `ImageExStateColors`，没有主题绑定；
- 已经实现的 `UIBackdropBlur`、`GearToast`、`GearTooltip`、`GearContextMenu`、`GearPopover` 都**没有进场景**
  （只有 1 个 `GearPopoverTrigger`）。

> 这不是“组件少”，而是**展出方式不同**。Evo 用整屏样例教你怎么组合，Gear 用标本卡证明“这个参数存在”。

### 2.3 判定口径

| 标记 | 含义 |
|---|---|
| ✅ | 已对齐：Gear 有等价组件，参数面/行为基本一致 |
| 🟡 | 已有但差距：组件存在，但能力、默认参数或展示方式明显不同 |
| ❌ | 缺失：Gear 没有等价实现，或只有不相关的旧实现 |

---

## 三、逐项复核（你列出的模块）

### 3.1 Procedural Shape

| 子项 | 判定 | Evo 机制 | Gear 现状 | 差距 / 动作 |
|---|---|---|---|---|
| **Backdrop Blur** | 🟡 | 形状内嵌玻璃参数；Demo 用 `DemoShapeBlurSource` 把 UI 拷到相机 Canvas，所以能糊“UI 后面的 UI” | `UIBackdropBlur` 抓场景相机、排除 UI 层，再由 `RM_Backdrop` 采样；**展厅没有这张卡** | 只能糊 3D 场景，糊不了 UI；每实例一台隐藏相机。M1 补卡，M3 做共享抓屏 + UI 拷贝源 |
| **Combine** | ❌ | `ShapeOperation` 最多 4 个操作数，Union/Subtract/Intersect/Exclude + smoothing；操作数引用同一 Canvas 上的另一个 Shape，形状数据进样式页 | ImageEx 没有布尔运算 | M3 先做单操作数（uniform 传递目标 rect/shape），再做多操作数；命中检测要镜像同一份数据 |
| **Clipping** | ✅ | Horizontal / Vertical / Radial360，圆角裁剪边 | `ClipMode` + `ClipSoftness` + Reverse，见 [ImageEx.Clip.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageEx.Clip.cs>) | 参数面基本对齐；补“圆角裁剪边”与组合演示即可 |
| **Pattern** | 🟡 | 图块来源支持 **Shape / Sprite**，带 size/gap/angle/offset/falloff/scroll、独立 ColorMode 与渐变 | 只有程序化 5 种（Dots/Grid/Stripes/Checker/Rings），无 Sprite/Shape 图块、无图案渐变 | 见 3.1 补充；M3 扩 `RM_Pattern.cginc` |
| **Shadows** | 🟡 | 内/外阴影 + 5 层 ColorMode；Demo 用四个 3D 键演示“外阴影当侧面、内阴影当按下” | 内/外阴影、Offset/Spread/Softness/OutsideOnly 都有 | 单体参数接近；差在**多层颜色模式**与**组合演示**。M1 做 3D 键卡片，M3 评估多层 |
| **Gradients** | 🟡 | Linear/Radial/Angular、Local/Screen、Zoom、Reverse；描边/图案/外发光也能复用同一渐变 | 填充渐变 4 类 + Screen + Reverse + Zoom；未见描边/图案渐变入口 | 见 [GradientEffect.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Effects/GradientEffect.cs>)；M3 把渐变接到描边/图案 |
| **Shapes** | ✅ **Gear 领先** | 矩形/椭圆/多边形/星/心/Chevron | 12 种（含 Squircle/Heart/BlobbyCross/NTriangle/Chamfer） | 不需要追；把 12 种放进一屏组合演示即可 |

**Pattern 的具体差距**（Evo `ProceduralShape.cs:86-131`）：

- Evo `PatternSource` 可选 `Shape` 或 `Sprite`，图块本身可以是椭圆/多边形/自定义 Sprite；
- Evo 图案有自己的 `patternColorMode`、`patternStyler`、渐变、渐变角度/缩放/反相；
- Gear `Pattern` 只有 5 个枚举与颜色/尺寸/留白/旋转/偏移/流速/falloff。

**Combiner 的成本提醒**：Evo 的布尔不是“扩顶点”，而是把操作数的仿射矩阵、形状与裁剪写进样式页数据行，
渲染时 shader 采样、命中时 C# 复算。Gear 若先做单操作数，可以把目标 `ImageEx` 的 rect/shape 参数作为一组 uniform 传进去，
但**不要先上 4 操作数**——那会把 ImageEx 的 uniform 数量再翻一倍，且破坏 Pooled 的签名池。

### 3.2 Button

| 子项 | 判定 | Evo | Gear | 差距 / 动作 |
|---|---|---|---|---|
| **Progress Button** | ❌ | `ProgressButton : Interactive`，长按填充、松手回退、完成态、进度事件；Demo 出现 5 次 | 只有 `GearProgressBar`（纯数值填充），不是按钮 | M2 新增 `GearProgressButton`；见 M2 |

Evo 的真实行为（[ProgressButton.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Button/ProgressButton.cs#L10-L46>)）：

- `holdDuration` 决定长按时长，`resetDuration` 决定松手回退；
- 三套 `CanvasGroup`：Normal / InProgress / Complete，可滑入/缩放切换；
- `onProgressStart` / `onProgressUpdate(float)` / `onComplete` / `onCancel`；
- 支持 `stayOnComplete` 与 `completeStateDuration`；
- 鼠标按住、手柄 Submit 都能走。

Gear 现在没有任何 `ProgressButton` 命中（全仓 grep 为 0），这是**真缺**，也是“按住确认/蓄力/长按删除”这类交互的空白。

### 3.3 Color

| 子项 | 判定 | Evo | Gear | 差距 |
|---|---|---|---|---|
| Square（SV 方块） | 🟡 | `ColorPickerType.Square`，生成贴图 + selector + Hue/S/V/Opacity 滑条 + Hex | `GearColorPicker` 有 SV 方块、Hue 滑条、Hex、预览 | 没有 Opacity、没有 S/V/B 独立滑条、没有生成式贴图尺寸选项 |
| Wheel（色轮） | ❌ | `ColorPickerType.Radial`，运行时生成色轮贴图（64–1024），按角度取 Hue、按半径取 S | 无 | M2 扩 `GearColorPicker` 的 `PickerType`，或单独 `GearColorWheel` |
| Palette（调色盘） | ❌ | `PaletteColorPicker : MaskableGraphic`，多层环形色板、悬停外推、辉光、选中边框，可键盘/手柄 | 无 | M2 新增 `GearColorPalette`；这是“设计系统”里最有辨识度的组件之一 |

证据：Evo [ColorPicker.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Color Picker/ColorPicker.cs#L10-L48>)、
[PaletteColorPicker.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Color Picker/PaletteColorPicker.cs#L13-L36>)；
Gear [GearColorPicker.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Widgets/GearColorPicker.cs#L20-L68>)。

### 3.4 Effects

| 子项 | 判定 | Evo | Gear | 差距 / 动作 |
|---|---|---|---|---|
| **Ripple** | 🟡→❌ | `Ripple : InteractiveEffect`，shader 内最多 8 个并发波，按最远角算覆盖，不建 GameObject | `UIRipple` 是旧实现：`Mask + Image` + 生成波纹 GameObject，池 16，不能挂 ImageEx | M2 做 ImageEx 版 `GearRipple`；旧 `UIRipple` 保留兼容 |
| **Dot Matrix Loader** | 🟡 | `DotMatrixLoader : Image`，8 图案 / 3 点形 / 网格尺寸 / 未激活点 alpha 与缩放 / 边缘柔化 / 相移 / 拖尾 / 负速度 | `GearDotMatrix` 是 ImageEx 的叠加效果：图案/格子/半径/颜色/强度/速度 | 缺“loader”语义与 inactive/trail/edge/phase；M2 扩字段或新组件 |
| **Dock Magnifier** | ✅ | 高斯权重 + SmoothDamp，改邻居 `sizeDelta` | `GearDockMagnifier` 已实现 | 单体对齐；展厅只 1 个，Evo 3 个且和 Dock 导航组合 |
| **Border Glow** | ✅ | 边缘光 + 指针跟随 | ImageEx 的 `EnableBorderGlow` + `ImageExPointerGlow` | 单体对齐；缺“按钮组里多个光效同时存在”的组合演示 |
| **Animated Container** | ✅ | 错峰出现、曲线缩放、淡入 | `GearAnimatedContainer` 已实现 | 单体对齐；缺“整页/整屏入场”的组合演示 |

**Ripple 的差距要讲清楚**：Evo 的 ripple 不是“生成一个 Image 再缩放”，而是把 8 个波的
`center + startTime + strength` 写进共享图集的一行，shader 按距离/时间算环宽与覆盖；
所以它能同时叠在任意形状上、不增加 GameObject、不打断合批
（[Ripple.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/Ripple.cs#L14-L20>)、
[InteractiveEffectRenderer.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/InteractiveEffectRenderer.cs#L13-L19>)）。
Gear 的 `UIRipple` 必须要求同物体 `Mask` + `Image`（[UIRipple.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Ripple/UIRipple.cs#L16-L67>)），
在 ImageEx 卡片/圆角/异形上不能用，视觉上限也低。

**DotMatrix 的差距**：Gear 有 8 图案与点形，但参数是“格子像素/半径/强度/速度”；
Evo 的 loader 还有 `gridSize`、`inactiveAlpha`、`inactiveScale`、`edgeSoftness`、`phaseOffset`、`trailLength`，
这些决定了“点阵呼吸/拖尾/暗点可见”的质感（[DotMatrixLoader.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/DotMatrixLoader.cs#L8-L50>)）。
Gear 对应字段见 [ImageEx.DotMatrix.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageEx.DotMatrix.cs#L30-L117>)。

### 3.5 Data Display

| 子项 | 判定 | Evo | Gear | 差距 / 动作 |
|---|---|---|---|---|
| **Showcase Panel** | ❌ | `ShowcasePanel`：自动轮播、按钮列表、文本显示、背景 Shape/Shadow、滑入 + 文本淡入；Demo 出现 1 次 | 无 | M2 新增 `GearShowcasePanel`；见 M2 |

Evo 的结构（[ShowcasePanel.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Showcase Panel/ShowcasePanel.cs#L12-L40>)）：
`items` 列表（标题/描述/按钮/背景）→ 生成按钮与文本 → 定时切换 → 背景 Shape 换色/换图 → 文本滑入。
它本质是“带自动轮播的功能展示窗”，Gear 现在只有 `GearPageView`（页面滑动），没有“一个面板内多 item + 自动轮播 + 背景联动”的版本。

### 3.6 Date and Time

| 子项 | 判定 | Evo | Gear | 差距 |
|---|---|---|---|---|
| **Calendar** | 🟡 | 日/月/年三视图，缩放 + 滑动切换；月/年下拉；本地化月名；年范围；今天/选中/跨月 | 单月视图 + 上/下月、上/下年；今天/选中/跨月 | **没有日→月→年缩放、没有月/年网格、没有本地化月名、没有年范围** |
| **TimePicker** | 🟡 | 时/分/AM-PM 三滚轮；`MinuteInterval`（1/5/10/15/30）；行高；AM/PM 标签；tick 音效；Styler | 时/分/秒/AM-PM 四滚轮；12/24 制；格式串；`GearPickerWheel` | **没有分钟步进、没有 tick 音效、没有行高/AM-PM 标签配置** |
| **Timer** | 🟡 | 倒计时/正计时/系统时钟；`timeMultiplier`；进度条平滑；逐位滚动数字；等宽数字；反向填充；格式化 | 倒计时/秒表；循环/暂停/unscaled/格式/Progress/Tick | **没有系统时钟、没有滚动数字、没有进度平滑、没有反向填充/倍速** |

证据：Evo [Calendar.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Date and Time/Calendar.cs#L13-L59>)、
[TimePicker.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Date and Time/TimePicker.cs#L13-L52>)、
[Timer.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Date and Time/Timer.cs#L18-L58>)；
Gear [GearCalendar.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Widgets/GearCalendar.cs#L46-L116>)、
[GearTimePicker.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Widgets/GearTimePicker.cs#L17-L102>)、
[GearTimer.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Widgets/GearTimer.cs#L27-L79>)。

### 3.7 其他值得复刻的模块

| 模块 | 判定 | 说明 | 优先级 |
|---|---|---|---|
| Carousel | ❌ | Evo 自动轮播 + 邻居露出 + 指示器收缩；Gear 只有 `GearPageView`（无 AutoSlide） | 中高 |
| FlexibleGridLayout | ❌ | AutoFit / FixedRows / FixedColumns / 纵横比 / Masonry | 中高 |
| ReorderableList | ❌ | 占位、间隙动画、跨列表转移 | 中（背包/编辑器） |
| MarqueeSelection | ❌ | 框选、分组拖拽、转移规则 | 中（背包/编辑器） |
| Selector | ❌ | 单条目切换 + 指示器 + 循环 | 中 |
| RadialSlider / ComparisonSlider | ❌ | 角度拖拽 / 分割线裁剪 | 中低 |
| RectDragger | ❌ | 轴约束 + 边界 + 回弹 | 低 |
| ButtonGroupDimmer | ❌ | 组内悬停时其余变暗 | 低 |
| VirtualCursor | ⛔ | 与 Gear 焦点域/输入系统重叠，除非主机 UI 成为主线 | 不做 |
| CurvedCanvas | ⛔ | 每帧改网格，只适合特定美术风格 | 不做 |

> 这些不是用户清单里的重点，但它们是“整屏样例”里最常用的零件；M2 选做，M3 的整屏样例会用到。

---

## 四、为什么观感差一截

### 4.1 展厅结构：标本卡 vs 应用样机

Gear 的 150 张卡是**单效果标本**：一张卡一个形状/一个滤镜/一个控件，卡片之间不组合。
Evo 的分类页是**应用样机**：同一屏里有背景、卡片、按钮、文本、图标、状态、动效、弹层。
用户看到的不是“某个滤镜不够好”，而是“这些组件放在一起不像一个产品”。

### 4.2 组合密度

- Evo：4,317 个 Shape、719 个 Button、2,474 个 TMP；
- Gear：686 个 ImageEx、61 个 Button、533 个 TMP。

单卡片的参数可能一样，但 Evo 用 6–10 个组件围出一个场景，Gear 只有一个预览图。
**密度直接决定“丰富感”**，而密度不需要新 shader。

### 4.3 状态与反馈

- Evo：92 个 `Interactive`（hover/press/selected 状态机），状态驱动颜色、缩放、辉光、音效；
- Gear：1 个 `GearInteractive`、1 个 `ImageExStateColors`。

Gear 已有五态模型与状态色（[GearInteractive.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Interaction/GearInteractive.cs#L35-L66>)、
[ImageExStateColors.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Interaction/ImageExStateColors.cs#L18>)），
但展厅没有用起来。**把状态接到每张卡/每个按钮，观感提升比再加 5 个滤镜更明显。**

### 4.4 主题一致性

- Evo：3,510 个 `StylerObject`，颜色/字体/渐变/Sprite/形状样式全部走 token，页面之间一致；
- Gear：展厅 0 个主题绑定，颜色是每张卡手调的；`ImageExThemeAsset` 有 5 类 token、`GearThemeBinder` 可用，但没用。

这解释了为什么 Gear 的卡片“每张都还行，放在一起不成套”。
M1 应先接主题，再谈新组件（主题服务本身也缺过渡/注册表，属于下一阶段议题）。

### 4.5 动效

Evo 有 159 个 `TweenAnimator`，页面切换、卡片入场、指示器收缩、文本滑入都是统一的曲线。
Gear 有 `GearAnimatedContainer` / `GearRectSway` / `GearDockMagnifier` 等，但展厅只各放 1 个，且大多是静态卡。
**同一套缓动/时长/错峰，比每个卡各写一套动画更重要。**

### 4.6 资产与默认参数

Evo 的观感有一部分来自图标集、字体、默认 Shape 预设（圆角/阴影/渐变调过）。这些**不能复制**，
Gear 需要自备（或使用开源图标/字体）。此外 Evo 的 Shape 默认参数更“厚”（外阴影 + 内阴影 + 描边 + 渐变叠在同一形状），
Gear 的卡片多是单层效果。M1/M3 的组合演示要显式叠层，而不是每层一张卡。

### 4.7 渲染机制（影响叠加与性能，不是单卡外观）

Evo 单 Canvas 单材质 + 样式数据纹理（StylePage）、效果数据图集（InteractiveEffectRenderer）、4 层 SoftMask；
Gear 是每实例材质 + 签名池，效果各自写 uniform。单卡可能看不出差别，但一旦“一个卡片同时叠 ripple + glow + spotlight + mask”，
Gear 会拆材质、拆批，且指针逐帧改 uniform（见 6.3）。
这条是架构级差距，放到 M4，不要先做。

### 4.8 结论：30% / 70%

- **30%** 是用户列出的真缺模块：ProgressButton、Color Wheel/Palette、ShowcasePanel、Ripple、DotMatrixLoader、Combine、Pattern 图块、Date & Time 能力；
- **70%** 是展厅：分类页、整屏样例、主题、状态、动效、实时调参、图标/字体。

所以 M1 的收益最高，且不碰 shader；M2 才是补模块；M3 才是形状工艺；M4 是架构。

### 4.9 UI 系统：借三个契约，不借骨架

Evo 没有 Gear 这样的窗口系统、路由栈、焦点域、本地化，这些是 Gear 的资产。Evo 值得借的是三个“契约”：

| 契约 | Evo 的做法 | Gear 现状 | 动作 |
|---|---|---|---|
| **主题** | `IStylerHandler` 注册表 + `ApplyPreset(preset, persistent, transitionDuration)` 广播 + `ColorTransition` 同一 target/channel 只保留一个过渡（[Styler.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Styler/Styler.cs#L715-L758>)、[ColorTransition.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Core/ColorTransition.cs#L9-L17>)） | `ImageExThemeService` 只有 `ThemeChanged`，各自订阅，无过渡/注册表/音频 token（[ImageExThemeService.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme/ImageExThemeService.cs#L31-L44>)） | M2.10：`IGearThemeTarget` + `GearThemeRegistry.Apply(theme, seconds)` |
| **交互** | 一个 `Interactive` 状态机，Ripple/Spotlight/BorderGlow/音效/光标都是订阅者 | `GearInteractive` 有五态与 `StateChanged`，但只有 `GearInteractiveTint` / `ImageExStateColors` 订阅；Spotlight/Glow/Ripple 各写各的 | M2.11：`IGearInteractionEffect` 总线，优先级/去重集中 |
| **覆盖层** | Toast/Tooltip/Popover/ContextMenu 有统一 owner、音效、焦点迁移 | 队列/池/子菜单/外点关闭都有，但 Popover 与 ContextMenu 各自读 Esc，没有 owner 栈/音效 | M2.12：`GearOverlayCoordinator`（owner 栈 + Esc 仲裁 + SFX） |

**不借**：单 `ModalWindow`、`VirtualCursor` 自管选中、UniText、Evo Tween、2560×1440 基准、StylePage 全量。
Gear 的 `UISystem` / `UIRouter` / `UIModalStack` / `UIInputSystem` / `LanguagesSystem` 不动。

---

## 五、开发计划

### M0｜基线与验收工具（1 天）

| 任务 | 产出 | 验收 |
|---|---|---|
| 固化 MCP 复核流程 | 本文 6.1 的脚本/命令 | 能在任意分支上生成组件计数与截图 |
| 截图目录隔离 | `.audit_tmp/shots/`（已在 `.gitignore`） | `git status` 不出现截图 |
| 场景基线 | 记录 M1 前的 batches/setPass/verts（Unity Profiler 或渲染诊断） | 后续每阶段对比 |

> 注意：`Assets/Evo` 目前有 2,699 个文件被 git 跟踪，且仓库是公开的。见第七节。

### M1｜展厅与观感地基（6–10 天，最高优先）

目标：**不新增 shader 的前提下，让现有 686 个 ImageEx 看起来像一个设计系统。**

| # | 任务 | 落点 | 验收 |
|---|---|---|---|
| 1.1 | 展厅目录：`ShowcaseCatalog`（分类/卡片/标签/搜索文本） | `Assets/Gear/UI/Showcase/ShowcaseCatalog.cs` | 149 张卡全部有 id/分类/描述；搜索能命中中文/英文 |
| 1.2 | 分类页 + 详情窗口：`GearTabGroup` + `LoopScrollRect` + `GearSearchIndex` + `UIRouter` | `ShowcaseWindow`（MainUI）、`ShowcaseDetailWindow`（NormalUI） | 8 个分类页；点卡进详情，返回回到原位 |
| 1.3 | 3 个整屏样例（用现有组件组合） | Audio Mixer（滑杆/Switch/进度/Toast）、Party Lobby（头像/选择/倒计时/Modal）、Inventory（Grid/Tooltip/ContextMenu/Drag） | 每屏 ≥20 个交互元素；手柄可走通 |
| 1.4 | 主题接入：默认 Dark/Light 两套 token；展厅顶部主题切换 | `ImageExThemeAsset` ×2、`GearThemeRegistry`（若 M2.10 未做则先用 `ImageExThemeService.SetTheme`，M1 可先做 1–2 天的注册表最小版） | 切主题不重建场景；所有卡片颜色/字体/渐变随主题变 |
| 1.5 | 状态接入：每张卡/每个按钮挂 `GearInteractive` + `ImageExStateColors` | 展厅预制体/构建脚本 | hover/press/selected 三态可见；与主题不冲突 |
| 1.6 | 动效接入：页面入场用 `GearAnimatedContainer`，卡片 hover 用 `GearRectSway`/`BorderGlow` | 展厅控制器 | 同一套时长/曲线；可全局关 |
| 1.7 | 补“已实现但没展出”的卡 | `UIBackdropBlur`、`GearToast`、`GearTooltip`、`GearContextMenu`、`GearPopover`、BorderGlow 组合、SoftMask 嵌套（先单层两张） | 每张卡可交互；Console 无红 |
| 1.8 | 实时调参面板（Evo `DemoCustomize` 的等价物） | `ShowcaseCustomizer`：滑杆/开关 → 明确适配器，不改 SerializedObject | 关键 20 张卡可在 Play 下调参并重置 |

**M1 不做**：新 shader、新控件、Style Page、布尔运算。

**M1 验收**：
- 场景根对象 < 200；打开分类页 < 100 ms（编辑器粗略计时）；
- 150 张卡内容全部保留（可迁到 Catalog/详情预制体）；
- 主题切换、手柄导航、搜索、返回路径可用；
- 截图 8 页 + 3 屏，人工比对“是否像一个产品”。

> **2026-11-15 已落地（M1.1 / M1.2）**
>
> - `Assets/Gear/UI/Showcase/ShowcaseCatalog.cs`：页/条目/预制体引用 + 空页裁剪 + 标题/说明/标签匹配；
> - `ShowcaseBuilder`：从 `UI Example` 导出 **150 张卡**到 `UI/Showcase/Cards/<page>/`，生成 **8 页 / 150 条**的 Catalog；
> - `ShowcaseUIPlane`：8 个分类 Tab、全局搜索、4×2 分页网格（8 张/页）、详情入口；
> - `ShowcaseDetailUIPlane`：放大卡片 + 标题/说明 + 返回键关闭；
> - `ShowcaseLauncher` + `UI Showcase Example` 场景：等 UISystem 就绪后自动开窗；
> - Unity MCP 实测：150 个预制体 **0 缺失组件**；Play 下 23 页加载 **150 张卡**、Console 无 error；
> - EditMode：`ShowcaseCatalogTests` 2 条全过。
>
> **2026-11-15 追加（M1.3 3/3）**
>
> - `AudioMixerSampleUIPlane`：4 通道（音量/静音/电平/分贝）+ 主输出 + 预设 + Apply/Reset，
>   用 `Slider` / `GearSwitch` / `GearProgressBar` / `GearRadioGroup` 组合；
> - `PartyLobbySampleUIPlane`：4 个玩家位 + 准备开关 + 模式/回合 + 5 秒开始倒计时
>   （`GearSwitch` / `GearRadioGroup` / `GearQuantitySelector` / `GearTimer` / `GearAnimatedContainer`）；
> - `InventorySampleUIPlane`：6×4 物品格 + 选中详情 + 悬浮 Tooltip + 右键 ContextMenu + 按稀有度排序；
> - 展厅 Header 新增三个样例入口；样例窗口走 `UIRouter.PushAsync`，ESC/手柄 B 返回；
> - MCP 实测：三个样例打开、倒计时、右键菜单、排序、返回均通过，Console 0 error。
> - 附带修复滚轮：`GearPickerWheel` 运行期复制 3/5 周期行并环绕重定位（首尾不再空白），
>   新增选中条与行淡出；`GearTimePicker` 时/分/秒/AM-PM 默认循环，12 小时制 11↔12 自动翻 AM/PM。
>
> **2026-11-15 追加（M1.4）**
>
> - `IGearThemeTarget` + `GearThemeRegistry`：注册/注销/广播 + `Apply(theme, seconds)`，
>   旧 `ImageExThemeService.ThemeChanged` 继续兼容；
> - `GearThemeColorTarget`：Graphic → token，切换时 unscaled 过渡，不引入补间库；
> - 新增 `Resources/UITheme/ImageExLight.asset`（亮色 token），`ImageExDefault` 继续作为暗色；
> - 展厅 Header 增加 `Light` / `Dark` 切换；Background/Header/Footer/Tabs/Text/Search/按钮接入 theme target，
>   三个整屏样例在切换后打开也会读取当前 token；
> - MCP 实测：暗色切亮色后背景 `0.043→0.950`、Header `0.078→1.000`、Tab 选中蓝/未选中浅灰；
>   亮色下打开 Audio Mixer 的 Panel 为白、标题为深色；Console 0 error。
>
> **2026-11-15 追加（窗口 Prefab 化）**
>
> - 五个窗口全部改为 prefab 层级：Showcase / Detail / Audio / Party / Inventory；
> - 控制器 `OnInit` 只做 `BindBakedLayout` / `TryBindBaked`，正常打开不再生成 UI；
> - `ShowcaseBuilder` 删除窗口生成菜单，全部重建不再覆盖窗口 prefab；
> - `ShowcasePrefabTests` 校验 Header / Tabs×8 / Grid / Footer / Preview / 三个样例 Content；
> - MCP 实测：主窗口 6 个子节点、无重复构建；详情预览卡正常；Audio 绑定 5 路电平；
>   Party 5 秒倒计时到 GO；Inventory 24 格 + 排序；全量 421 条测试通过。
>
> **2026-11-15 追加（M1.5 起步：五态接入）**
>
> - 8 个 Tab 直接在 prefab 挂 `GearInteractive` + `GearInteractiveTint`，Button transition = None；
> - Header 的 Theme/三个样例/Close、Footer 的 Prev/Next，以及详情 Close/Back、
>   Audio Apply/Reset、Party Start、Inventory Sort/Use/Equip/Discard 同样接入；
> - `RefreshTabColors` 写选中/未选中态，`RefreshButtonTints` 在主题切换后刷新按钮底色；
> - MCP 实测：Tab Normal `0.118/0.129/0.169` → Hover `0.224/0.234/0.268` →
>   Pressed `0.096/0.106/0.138`；Theme 按钮暗色 Normal `0.118`、亮色 Normal `0.900`；
>   Audio Apply 白色 Normal → Hover `0.860/0.890/0.950` → Pressed `0.820`。
> - 数据项也改为 prefab：`ShowcaseGridCell.prefab`（CardHost + Open）与 `InventoryItemCell.prefab`
>   （Icon/Name/Count/Rarity + Button）；格子背景/按钮五态在模板里，数据由控制器写入。
>   Inventory 物品格 MCP 实测 Normal `0.078/0.086/0.114` → Hover `0.171/0.178/0.202` →
>   Pressed `0.067/0.073/0.097`，Console 0 error。
> - `ShowcaseGridCell` 增加透明 `Hover` overlay（ImageEx 描边/辉光）+ 根 `GearInteractive`；
>   `ImageExStateColors` 支持父节点查找，Hover/Pressed/Selected 描边 0.55/0.80/1.00，过渡 0.12s；
>   卡片本体材质不改写。
>
> **2026-11-15 追加（M1.6 动效起步）**
>
> - Grid prefab 挂 `GearAnimatedContainer`：FromScale 0.92、ItemDuration 0.26、Stagger 0.03；
>   `RefreshGrid` 布局完成后 `Play()`，换页重新错峰入场；
> - `ShowcaseGridCell` 根挂 `GearRectSway`（MaxAngle 3°），指针移动时卡片轻微倾斜；
> - `GearAnimatedContainer.Cache` 跳过 inactive 子项，避免换页时把待销毁旧格子计入总时长；
> - MCP 实测：换页 `IsPlaying=true`、TotalDuration 0.47（8 格），Sway 前倾 2.1° 后 Reset 回 0；
>   Console 0 error。
> - 新增 `GearMotionSettings` 全局开关与 Header `Motion On/Off`；关闭后入场直接完成、Sway 不倾斜；
>   重新开启会重播 Grid 入场。MCP 实测 `Motion Off` 后 `IsPlaying=false`、格子 scale 1、Sway 0°。
> - 示例场景增加 `ShowcasePreview (Edit Only)` prefab 实例（8 张预览卡）；Play 时
>   `ShowcaseScenePreview` 在 Awake 移除，场景里只有 Launcher，正式窗口仍从 Resources 加载。
> - 修复详情/样例按钮白底白字：Close/Back/Apply/Reset/Start/Sort/Use/Equip/Discard 改为
>   SurfaceRaised / Accent / Danger 底色；新增 prefab 白底白字扫描用例，全量 427 条通过。
>
> **2026-11-15 追加（M1.7 起步：覆盖层补卡）**
>
> - 新增 prefab：`150-覆盖层---Toast`、`151-覆盖层---Tooltip`、`152-覆盖层---ContextMenu`，
>   复用 Popover 卡片结构，统一由 `ShowcaseExtrasDemo` 绑按钮，不在运行时铺 UI；
> - 新增 `153-组合---BorderGlow`，Preview 直接开 `ImageEx.EnableBorderGlow`（Accent，0.8）；
> - 新增 `157-组合---BackdropBlur`（Showcase prefab 内自带 `BackdropCamera`）与
>   `156-遮罩---双面板SoftMask`（两张单层软遮罩并排，验证最近父级遮罩语义）；
> - 目录通过 `ShowcaseCatalog.EditorAddEntry` 追加到 overlays/shape/effects 页，总条目 156；
> - `UIBackdropBlur` 抓屏 RT 改为 16 位深度，消除 URP depth-buffer 警告；
> - MCP 实测：Toast 弹出、ContextMenu 展开、Tooltip 悬停显示、BackdropBlur 无警告、
>   双面板 SoftMask masks=2；全量 430 条通过。
>
> M1.7 补卡完成。尚未做：卡片内部控件全量五态、M1.8 实时调参。
>
> **2026-11-15 追加（M1.8 实时调参）**
>
> - 新增 `ShowcaseCustomizer.prefab`：4 个滑杆 + Reset，挂在详情 `Panel/Customizer`；
> - 适配器直接读写 `ImageEx.OutlineWidth / BorderGlowIntensity / InnerShadowWidth / GradientScale`，
>   不通过 SerializedObject；详情 `FX` 按钮切换显隐，Reset 恢复 Bind 时初值；
> - MCP 实测：打开详情 `Target=True`、`FX` 后 `sliders=4`，Outline 1→8，Reset 回 1；Console 0 error；
> - 全量 431 条通过。
>
> **2026-11-15 追加（M1.5 卡片内部控件五态）**
>
> - 156 张卡中的 64 个标准 `Button` 全部挂 `GearInteractive` + `GearInteractiveTint`，
>   `Button.transition=None`；保留原底色，Hover 提亮/近白色转浅灰，Pressed 压暗，Disabled 灰；
> - 新增卡片 Button 扫描用例；全量 432 条通过；
> - `ShowcaseBuilder` 重建 Catalog 时保留 `/extras-` 条目；但重建卡预制体仍会覆盖卡片 prefab，
>   所以卡片 prefab 现在应按“手工维护”对待。
>
> **2026-11-15 追加（M1.4 卡片主题补齐）**
>
> - 156 张卡根 ImageEx / CardTitle / CardDescription 全部挂 `GearThemeColorTarget`：
>   Surface / Text / TextSecondary；效果 Preview 保持原色；
> - 新增卡片主题扫描用例，全量 433 条通过；Dark/Light 下卡面与文字同步换色。
>
> **2026-11-15 追加（M2.1 GearProgressButton）**
>
> - 新增 `GearProgressButton`：HoldDuration / ResetDuration / ProgressCurve / StayOnComplete /
>   CompleteStateDuration；Start/Update/Complete/Cancel 事件；三态 CanvasGroup；
> - 鼠标 down/up 与 Submit 长按；松手在 ResetDuration 内回退；完成可停留；
> - 新增 `GearProgressButtonTests` 4 条 + `控件 · ProgressButton` 展卡（prefab，带 Toast）；
> - 全量 438 条通过；Unity MCP 不可用期间用 batchmode 验证。
>
> **2026-11-15 追加（M2.2 GearColorWheel）**
>
> - 新增 `GearColorWheel`：角度 Hue / 半径 Saturation / 亮度滑条，64–1024 程序化贴图，
>   `EvaluateWheelColor` 纯函数、handle/preview/Hex、SetColor/SetHSV/ColorChanged；
> - 新增 `GearColorWheelTests` 4 条 + `控件 · ColorWheel` 展卡（prefab）；
> - 全量 443 条通过；Palette 多层环/色板尚未做。
>
> **2026-11-15 追加（M2.2 ColorPalette）**
>
> - `GearColorSwatch`：色块颜色/选中描边/按钮点击；`GearColorPalette`：环状布局数据、
>   悬停 1.18×、选中 1.1× + 2px 白描边、最近色选择、Button 导航；
> - 新增 `控件 · ColorPalette` 展卡（12 色块 + 中心预览）；3 条调色板用例；
> - MCP 实测：swatches=12、初始红、Select(3) 边框 2；全量 447 条通过。
>
> **2026-11-15 追加（M2.3 GearShowcasePanel）**
>
> - `GearShowcasePanel`：items、PageView 滑动、标题/说明/背景色、淡入、自动轮播（循环）、按钮跳页、Tick；
> - 新增 `GearShowcasePanelTests` 3 条与 `控件 · ShowcasePanel` 展卡（3 页，2s 自动轮播）；
> - MCP 实测：pages=3、自动从 Ember 切到 Aurora；全量 451 条通过。
>
> **2026-11-15 追加（M2.4 GearRipple）**
>
> - `RM_Ripple.cginc`：8 波 `_RippleData`、速度/宽度/强度/颜色，UV 径向扰动 + 已裁剪 alpha 内加环色；
> - `GearRipple`：环形缓冲最多 8 波、点击 Emit、Clear、材质 SetVectorArray；
> - 新增 `GearRippleTests` 3 条与 `效果 · Ripple` 展卡；MCP 实测 count=1、Console 0 error；
> - 全量 455 条通过。
>
> **2026-11-15 追加（M2.5 DotMatrix 扩展）**
>
> - ImageEx 新增 `DotInactiveAlpha / DotInactiveScale / DotEdgeSoftness / DotPhaseOffset / DotTrailLength`；
> - shader 支持非活跃点亮度/尺寸、圆/方/菱形软边、相位错峰、拖尾；
> - `GearDotMatrix` 暴露同名属性；现有 `效果 · 点阵` 卡直接验证（cell=14、inactive=0.22）；
> - 全量 455 条通过。
>
> **2026-11-15 追加（M2.6 TimePicker 对齐）**
>
> - `MinuteInterval`：分钟选项 / 吸附 / wheel index 映射；`AmLabel/PmLabel` 自定义标签；
> - `RowHeight` 统一四个滚轮行高；`TickAudio/TickClip` 滚轮变化播放音效；
> - 12h `tt/t` 使用自定义 AM/PM 标签；新增 2 条用例，全量 457 条通过。
> - Calendar 三视图、Timer 系统时钟/TimeMultiplier/逐位滚动仍未做。
>
> **2026-11-15 追加（M2.6 Calendar 三视图）**
>
> - `GearCalendar`：Day/Month/Year 视图、ZoomIn/ZoomOut、SelectMonth/SelectYear、
>   当前 Culture 月名、12 年范围、Header 点击缩小；
> - 月/年视图复用同一 grid：月格点回日视图，年格点回月视图；
> - 新增 3 条用例；MCP 实测 Header 点击 → Month，首月格点击 → Day/month=1；
> - 全量 460 条通过；滑动切换/月年下拉未做。
>
> **2026-11-15 追加（M2.6 Timer 增强）**
>
> - `TimeMultiplier` 缩放 Tick；`UseSystemClock` 用 DateTime 基准；
> - `SmoothProgress` / `ProgressSmoothing` 平滑 `DisplayProgress`；
> - `ReverseFill` / `FillAmount` 支持倒置填充；新增 3 条用例，全量 463 条通过。
> - 逐位数字滚动仍待做。
>
> **2026-11-15 追加（M2.7 Carousel）**
>
> - `GearPageView.PagePadding`：页面偏移，露出邻页；
> - `GearCarousel`：AutoSlide、循环、IndicatorShrink、NeighborOpacity/Scale、MaxVisibleNeighbors；
> - 新增 3 条 Carousel 用例 + `控件 · Carousel` 展卡；MCP 实测 index 1→0 循环、
>   邻页 alpha=.38/scale=.9；全量 468 条通过。
> - FlexibleGridLayout 尚未做。
>
> **2026-11-15 追加（M2.7 FlexibleGridLayout）**
>
> - `GearFlexibleGridLayout`：FixedColumns/Responsive、MinCellSize、AspectRatio、Spacing、Masonry；
> - 子项可用 `LayoutElement.preferredHeight` 控制瀑布流高度；
> - 新增 3 条布局用例 + `控件 · FlexibleGrid` 展卡；MCP 实测 8 子项、3 列宽 70、x=78；
> - 全量 472 条通过。
>
> **2026-11-15 追加（M2.8 Selector / RectDragger / ButtonGroupDimmer）**
>
> - `GearSelector`：选项单选、指示器滑动、颜色切换；
> - `GearRectDragger`：Both/Horizontal/Vertical/None、Bounds 夹取、SpringBack；
> - `GearButtonGroupDimmer`：悬停项保留，其余成员按 DimAlpha 变暗；
> - 新增 5 条 EditMode 用例 + `控件 · Selector / RectDragger / ButtonGroupDimmer` 三张展卡；
> - MCP 实测 Selector 3 项、选中 2、指示器 x=72；全量 480 条通过。
>
> **2026-11-15 追加（M2.9 Marquee / ComparisonSlider）**
>
> - `GearMarquee`：超出 viewport 才滚动，循环、暂停、重置、Tick；
> - `GearComparisonSlider`：value 0..1 分割 before/after，handle 拖拽/事件；
> - 新增 4 条用例 + `控件 · Marquee / ComparisonSlider` 展卡；全量 486 条通过。
> - Reorderable（占位/间隙动画/跨列表）仍未做。
>
> **2026-11-15 追加（M2.9 Reorderable）**
>
> - `GearReorderableList`：MoveItem、实时 sibling 换位、CanvasGroup 拖拽透明、可选手动排布；
> - `GearReorderableItem`：把 BeginDrag/Drag/EndDrag 转发给 owner；
> - 新增 2 条用例 + `控件 · Reorderable` 展卡；MCP 实测 4 项、MoveItem(0,3)=True；
> - 全量 489 条通过；跨列表/框选分组拖拽未做。
>
> **2026-11-15 追加（M2.10 ThemeRegistry 过渡）**
>
> - `GearThemeRegistry.TargetCount` 诊断；`GearThemeColorTarget` 以 `Graphic` 为通道，
>   同一通道新过渡自动取消旧过渡；
> - `IsAnimating` / `CancelTransition`；可选 `TransitionAudio/TransitionClip`；
> - 新增 1 条用例，全量 490 条通过；主题切换沿用现有 Header 开关。
>
> **2026-11-15 追加（M2.11 InteractionEffectBus）**
>
> - `IGearInteractionEffect`：Priority / Channel / OnInteraction；
> - `GearInteractionEffectBus`：订阅 GearInteractive.StateChanged，优先级降序 + Channel 去重；
> - Ripple/Spotlight/Glow 三个独立适配器；ImageEx 两组件新增 `SetHover`；
> - 新增 3 条用例 + `控件 · InteractionEffects` 展卡；MCP 实测 effects=3、dispatch=1、ripple=1；
> - 全量 494 条通过。
>
> **2026-11-15 追加（M2.12 OverlayCoordinator）**
>
> - `GearOverlayCoordinator`：owner 栈、Esc 栈顶仲裁、同 owner 替换、CloseTop/CloseAll、可选 SFX；
> - `GearPopover` / `GearContextMenu` 接入协调器：非栈顶不响应 Esc，销毁时 Release；
> - 新增 3 条用例；全量 497 条通过；覆盖层展卡沿用现有 Popover/ContextMenu/Toast/Tooltip。
>
> M2 全部完成；进入 M3。
>
> **2026-11-15 追加（M3.1 ShapeOperand）**
>
> - `ImageEx.ShapeOperand`：target / OperationType（Union/Subtract）/ Smoothing；
> - `RM_Operand.cginc`：按目标 Rect 归一化 center/size，圆或圆角矩形 SDF 与主 alpha 组合；
> - 新增 2 条用例 + `效果 · ShapeOperand` 展卡；全量 500 条通过；
> - 当前只覆盖圆/圆角矩形操作数，其他 13 种形状的半径参数后续扩展。
>
> **2026-11-15 追加（M3.2 PatternSource）**
>
> - `PatternSourceType`：Procedural / Sprite / Shape；
> - Sprite 按格子采样 `_PatternTex`；Shape 支持圆/矩形图块；
> - `PatternColorType`：Solid / Gradient，Gradient 采样 `_PatternGradient`；
> - 新增 2 条用例 + `效果 · PatternSource` 展卡；全量 503 条通过。
>
> **2026-11-15 追加（M3.3 组合样例）**
>
> - `样例 · GooeyMenu`：3 圆形按钮 + 内外阴影 + GearInteractive；
> - `样例 · ActivityRings`：3 个 Radial Clip 同心环；
> - `样例 · 3DKeys`：外阴影 + 内阴影 + 描边；
> - `样例 · Wallpaper`：Pattern Dots + Falloff + Scroll；
> - 新增 4 条 prefab 用例；MCP 实测 Gooey circles=7、interactive=True；全量 507 条通过。
>
> **2026-11-15 追加（M3.4 共享 Backdrop）**
>
> - `UIBackdropSharedCapture`：按 source camera + downsample + cullingMask + 分辨率共享
>   一台隐藏相机和 capture RT，引用计数归零销毁；
> - `UIBackdropBlur.UseSharedCapture` 默认开启；各实例仍独立 ping-pong 模糊；
> - 新增 2 条共享用例 + Backdrop 卡共享开关用例；全量 510 条通过；
> - 可选 `UIBackdropSource` UI 拷贝源未做。
>
> **2026-11-15 追加（M3.5 可选形状导出）**
>
> - `GearShapeSpriteExporter`：选中 ImageEx，按 DrawShape/圆角/尺寸烘焙 1x–4x Sprite；
> - 输出白色可染色 alpha 贴图，支持 9-slice border；菜单入口 + 编程 API；
> - 新增 1 条导出用例，全量 511 条通过。
> - `UIBackdropSource` UI 拷贝源仍未做（可选）。
>
> M3 完成；进入 M4。
>
> **2026-11-15 追加（M4.1 瞬态效果材质通道）**
>
> - `ImageEx.AddTransientEffect/RemoveTransientEffect` 引用计数 + `HasTransientEffects`；
> - ImageExSpotlight / ImageExPointerGlow / GearRipple 自动注册，OnDisable 注销；
> - Pooled 模式检测到瞬态效果时跳过 `AcquirePooledMaterial`，走独立材质，
>   避免 SpotlightPosition / BorderGlowPosition / Ripple 数据进入材质签名；
> - 新增 1 条用例，全量 512 条通过。
>
> **2026-11-15 追加（M4.2 双层 SoftMask）**
>
> - `GearSoftMask.Shape`：RoundedRect / Circle；
> - 最多两层父级遮罩：最近 → `_GearMask*`，外层 → `_GearMask2*`；
> - shader 同时对两层求 alpha，支持嵌套/多层裁剪；
> - 新增 2 条用例，全量 514 条通过；超过两层暂不支持。
>
> **2026-11-15 追加（M4.3/M4.4 预算与 Lite/Full）**
>
> - `GearStylePageBudget`：统计 Canvas 下 Graphic 数、唯一材质数、唯一纹理数；
> - `GearImageExQuality`：全局 `GEAR_UI_LITE` keyword；Lite 跳过 Pattern/Ripple/Glow/Spotlight/DotMatrix；
> - 新增 2 条用例，全量 516 条通过；
> - M4.3 单 Canvas 单材质仍需 profiler batch/setPass 证据，暂不盲做；
> - M4.4 lite/full 已完成 shader 层开关，双程序打包仍按需。
>
> **2026-11-15 追加（M4.3 预算采样）**
>
> - `GearRenderBudgetProbe`：采样 Batches / SetPass Calls，并统计唯一材质；
> - 展厅 Play：`samples=60, materials=62`；当前编辑器 `ProfilerRecorder.Valid=False`，
>   无法取得可信 batch/setPass；
> - 结论：没有证据表明 batch/setPass 已成为瓶颈，M4.3 单材质优化暂缓；
> - 新增 1 条探针用例，全量 517 条通过。
>
> **2026-11-16 追加（展厅可用性修复）**
>
> - 浅色主题：补齐顶栏按钮、详情 Panel/Title/Description/Close/Back/Customize/Customizer 主题绑定；
> - 覆盖层卡片触发按钮从白底白字改为 Accent 底 + 白字；
> - 详情关闭按钮字形改为 `X`，移除与主题目标冲突的 Tint；
> - `GearPopoverTrigger` 增加 Button.onClick 绑定，键盘/手柄 Submit 可打开，鼠标不重复触发；
> - 副标题移除旧参考字样；全量 517 条通过。
>
> M4 的 4.1/4.2/4.4 已实现；4.3 以测量结论延后。

### M2｜缺失模块（12–18 天）

按依赖顺序，先做“整屏样例要用”的，再做“锦上添花”的。

| # | 模块 | 关键 API / 行为 | 落点 | 估算 |
|---|---|---|---|---|
| 2.1 | **GearProgressButton** | `HoldDuration`、`ResetDuration`、`ProgressCurve`、`StayOnComplete`、`CompleteStateDuration`、`OnProgressStart/Update/Complete/Cancel`；三态 CanvasGroup；鼠标/手柄 Submit 长按 | `Utilities/UI/Widgets/GearProgressButton.cs` | 1–2 天 |
| 2.2 | **GearColorWheel / Palette** | `PickerType { Square, Wheel, Palette }`；色轮/方块生成贴图（64–1024）；Palette 多层环、悬停外推、选中边框、键盘/手柄 | 扩 `GearColorPicker.cs` + 新 `GearColorPalette.cs` | 2–3 天 |
| 2.3 | **GearShowcasePanel** | items 列表、自动轮播、按钮列表、文本显示、背景 Shape/Shadow、滑入 + 淡入 | `Utilities/UI/Widgets/GearShowcasePanel.cs` | 1–2 天 |
| 2.4 | **GearRipple** | ImageEx shader 内 8 波：`_RippleData[8]`（center.xy/start/strength）、`_RippleCount`、`_RippleSpeed/Width/Softness/Color`；点击写 ring buffer；SDF 形状裁剪 | `ImageEx.Ripple.cs` + `RM_Ripple.cginc` + `GearRipple.cs` | 2–3 天 |
| 2.5 | **GearDotMatrixLoader** | 优先在 `ImageEx.DotMatrix` 上补 `GridSize`、`InactiveAlpha`、`InactiveScale`、`EdgeSoftness`、`PhaseOffset`、`TrailLength`、负速度；若业务需要独立 `Image` 组件，再包一层 `GearDotMatrixLoader` | `ImageEx.DotMatrix.cs` + `RM_DotMatrix.cginc` | 1–2 天 |
| 2.6 | **Date & Time 对齐** | Calendar：Day/Month/Year 三视图、缩放/滑动、月/年下拉、本地化月名、年范围；TimePicker：`MinuteInterval`、AM/PM 标签、行高、tick 音效；Timer：系统时钟、`TimeMultiplier`、进度平滑、逐位滚动、反向填充 | `GearCalendar.cs`、`GearTimePicker.cs`、`GearTimer.cs` | 3–5 天 |
| 2.7 | **Carousel / FlexibleGrid** | Carousel：AutoSlide、IndicatorShrink、ShowNeighbors、NeighborOpacity、MaxVisibleNeighbors；FlexibleGrid：FitType、Masonry、AspectRatio、MinCellSize | `GearCarousel.cs`、`GearFlexibleGridLayout.cs` | 2–3 天 |
| 2.8 | **Selector / RectDragger / ButtonGroupDimmer** | 单项切换 + 指示器；轴约束 + 边界 + 回弹；组内悬停变暗 | 对应 Widgets/Layout | 2 天 |
| 2.9 | **Reorderable / Marquee / 双滑杆** | 占位 + 间隙动画 + 跨列表；框选 + 分组拖拽；Radial/Comparison | 对应 Widgets/Layout | 4–6 天（可后置） |
| 2.10 | **GearThemeRegistry + 过渡** | `IGearThemeTarget` 注册/注销/广播；`Apply(theme, seconds)`；同一 target/channel 只保留一个活动过渡；音频 token 可选 | `Theme/GearThemeRegistry.cs` | 1–2 天 |
| 2.11 | **IGearInteractionEffect 总线** | Spotlight/Glow/Ripple 订阅 `GearInteractive.StateChanged`；优先级/去重集中；`ButtonAni` 用 adapter 桥接，不合并枚举 | `Interaction/` | 1 天 |
| 2.12 | **GearOverlayCoordinator** | owner 栈、Esc 消费顺序、Toast/Tooltip/Popover 单 owner、SFX token；导航组件只暴露 `FocusTarget` | `Overlays/GearOverlayCoordinator.cs` | 1–2 天 |

**关键实现约束**：

- 新控件必须接 `UIInputSystem` 焦点（暴露 `FocusTarget` / `RegisterFirstSelected`），验收含手柄/键盘路径；
- 新控件必须接 `LanguagesSystem`（可本地化文本走 TMP，不硬编码）；
- 新控件不直接抢 `EventSystem` 选中，不复制 Evo 的 `Interactive` 基类；
- 新 shader 复用 ImageEx 的材质/主题；不要新建第二套 UI 材质体系；
- 每个新组件配 EditMode 用例（纯逻辑：进度、颜色转换、日历日期、Timer 格式化）。

**M2 验收**：
- 每个模块有 1 张展卡 + 1 个整屏样例用法；
- `GearProgressButton` 按住 2s 完成、松手回退、只触发一次完成；
- `GearRipple` 8 波并发、圆角/异形裁剪正确、点击不建 GameObject；
- `GearDotMatrixLoader` 暗点/拖尾/负速度可见；
- Calendar 日→月→年切换、TimePicker 分钟步进、Timer 系统时钟正确跨天；
- 手柄/键盘全路径可走。

### M3｜形状工艺（5–8 天）

| # | 任务 | 说明 | 估算 |
|---|---|---|---|
| 3.1 | **单操作数 Combine** | `ImageEx.ShapeOperand { target, OperationType, Smoothing }`；target 的 rect/shape 参数作为一组 uniform；先用 Union/Subtract | 3–5 天 |
| 3.2 | **Pattern 图块扩展** | `PatternSource { Procedural, Sprite, Shape }`；图块采样、图案渐变、图案 ColorMode；扩 `RM_Pattern.cginc` | 2–3 天 |
| 3.3 | **组合形状样例** | Gooey 菜单（Union + Subtract）、Activity Rings（Radial Clip + Rounded Edges）、3D Keys（Outer/Inner Shadow）、Wallpaper（Pattern + Falloff + Scroll） | 2–3 天 |
| 3.4 | **Backdrop 共享抓屏 + UI 拷贝源** | 多面板共享一台抓屏相机；可选 `UIBackdropSource` 把指定 UI 图形拷到相机 Canvas，实现“糊 UI 后面的 UI” | 2–3 天 |
| 3.5 | （可选）形状导出 Sprite | 1x–4x、9-slice、可染色；编辑器烘焙 | 2 天 |

**M3 验收**：四个组合样例在 Play 下可交互；Combine 的命中区域与渲染一致；Pattern 的 Sprite/Shape 图块不拉伸。

### M4｜架构（按需，先量后做）

| # | 任务 | 触发条件 | 估算 |
|---|---|---|---|
| 4.1 | 效果数据通道（Evo `InteractiveEffectRenderer` 等价物） | 一个按钮要同时叠 ripple + glow + spotlight，或指针效果在 Pooled 下逐帧产生材质 | 3–5 天 |
| 4.2 | SoftMask 多层/嵌套/任意形状 | 需要嵌套圆角或非矩形遮罩 | 3–5 天 |
| 4.3 | StylePage 单 Canvas 单材质 | 实测 draw call / setPass 成为瓶颈（先看 Pooled 后的 171 batches 是否还高） | 1–2 周 |
| 4.4 | lite/full 双程序 | 随 StylePage；或 ImageEx 片元成本成为 GPU 瓶颈 | 1 周 |

**M4 的前置**：先修 Pooled + 指针效果的语义（见 6.3）。否则效果图集/样式页只会把“材质身份”问题藏得更深。

---

## 六、验收

### 6.1 MCP 复核脚本（可重复）

Unity MCP 端点：`http://127.0.0.1:28080/mcp`（streamable HTTP，需 `Accept: application/json, text/event-stream`）。

**组件计数**（在任意场景执行）：

```csharp
var counts = new System.Collections.Generic.Dictionary<string,int>();
var gos = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
foreach (var go in gos)
    foreach (var c in go.GetComponents<UnityEngine.Component>())
        if (c != null) counts[c.GetType().Name] = counts.TryGetValue(c.GetType().Name, out var v) ? v + 1 : 1;
var sb = new System.Text.StringBuilder();
foreach (var kv in counts) sb.AppendLine(kv.Key + "=" + kv.Value);
return sb.ToString();
```

**场景切换与截图**：

```text
manage_scene  { "action": "load", "path": "Assets/Gear/Scenes/Example/UI Example.unity" }
manage_camera { "action": "screenshot", "screenshot_file_name": "gear.png",
                "output_folder": "Assets/Screenshots", "include_image": false }
```

截图后把 PNG 移到 `.audit_tmp/shots/`，再从 Unity 删除 `Assets/Screenshots`，避免污染工程。

**控制台**：`read_console { "action": "get", "types": ["error","warning"], "count": 20 }`。

### 6.2 视觉验收

- 每个分类页、每个整屏样例各留一张 1920×1080 截图；
- 同一组件在 Dark/Light 两套主题下各一张；
- 重点检查：卡片叠层（阴影 + 描边 + 渐变 + 图案）、状态三态、模态/弹层遮挡关系、圆角/裁剪边缘；
- 没有视觉模型时，由人看截图；不要只凭编译通过。

### 6.3 已知缺陷（M1/M4 必须先处理）

1. **Pooled + 指针效果会逐帧产生新材质**：`ImageExSpotlight` / `ImageExPointerGlow` 写 `ImageEx` 属性并 `SetMaterialDirty()`；
   材质池签名逐 shader 属性取值（[ImageExMaterialPool.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageExMaterialPool.cs#L75-L131>)），
   `_SpotlightPosition` / `_SpotlightHover` 变化 → 新签名 → 新 probe 材质；到 4096 上限后回退逐实例。
   编辑器切 Pooled 的跳过名单只含 `ImageExPointerGlow`，不含 `ImageExSpotlight`
   （[ImageExPooledTool.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Editor/Scripts/ImageExPooledTool.cs#L59-L64>)）。
2. **每次 `GetModifiedMaterial` 都做父级软遮罩查找**：`GetComponentInParent<GearSoftMask>(true)`
   （[ImageEx.Runtime.Rendering.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/ImageEx.Runtime.Rendering.cs#L172-L180>)）。
3. **`UIBackdropBlur` 每实例一台隐藏相机 + 3 张 RT**，多玻璃面板时成本线性增长
   （[UIBackdropBlur.cs](<../../Unity/Gear/Assets/Gear/Utilities/UI/Backdrop/UIBackdropBlur.cs#L273-L277>)）。

处理建议：

- 瞬态 uniform（指针/悬停/波纹）不参与材质签名；写当前 `m_Material` 或强制 Dynamic；或走 4.1 的数据通道；
- 软遮罩查找结果缓存到 `ImageEx`，`SetMaterialDirty` 前失效；
- 背景抓屏做引用计数共享；需要“糊 UI 后面的 UI”时再加拷贝源。

### 6.4 自动验收

```bash
dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear
dotnet build Unity/Gear/Gear.Utilities.csproj
pwsh Tools/unity-verify.ps1 -IncludePlayMode
dotnet run --project Tools/DocsCheck -- .
```

- EditMode 新增用例计入门槛；新组件的纯逻辑必须有测试；
- 展厅改动要跑 PlayMode：点卡、返回、切主题、手柄导航；
- 文档改动跑 DocsCheck。

### 6.5 性能预算

| 指标 | 目标 | 方式 |
|---|---|---|
| 展厅场景 setPass / batches | 不高于 M0 基线 +10% | 渲染诊断或 Profiler |
| 指针效果 | 悬停/移动不产生新材质 | 材质池条目数不增长；相邻不同设置不串扰 |
| 页面打开 | < 100 ms（编辑器粗略） | 秒表/Profiler |
| 文本/图标 | 不引入每帧 GC | Profiler GC Alloc |

---

## 七、风险与合规

| 风险 | 影响 | 处理 |
|---|---|---|
| **Evo 资产在公开仓库** | Asset Store EULA 通常不允许再分发源码/贴图/预制体 | 当前 `git ls-files "Unity/Gear/Assets/Evo/**"` = **2,699**；建议 `git rm -r --cached` + `.gitignore`，历史清理单独评估 |
| 直接抄 Evo 代码 | 许可 + 维护风险 | 只借机制与交互，代码/shader/预制体全部自己写 |
| Combine 成本低估 | 需要数据通道，可能拖成两周 | 先单操作数；多操作数与 4.1 合并排期 |
| SoftMask 嵌套 RT | 显存/移动端重建 | 先只覆盖 ImageEx，单 Canvas 试点 |
| 展厅范围蔓延 | 6 个整屏 + 15 分类 = 数月 | M1 只做 3 屏；其余按业务需求 |
| 新控件绕过焦点系统 | 手柄/键盘点不到 | 所有新控件暴露 `FocusTarget`，验收含手柄路径 |
| 主题过渡顺序 | 切主题闪色/叠加 | 同一 target/channel 只保留一个活动过渡（参考 Evo `ColorTransition` 的约束，不复制实现） |

---

## 八、证据索引

**Gear（当前工作树）**

- ImageEx 运行时：[Runtime 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime>)
- 图案/聚光/点阵/幕帘/地形/墨团 shader：[Common shader 目录](<../../Unity/Gear/Assets/Gear/Resources/Shader/Common>)、
  [ImageEx.shader](<../../Unity/Gear/Assets/Gear/Resources/Shader/UI/ImageEx/ImageEx.shader>)
- 展厅：[UI Example.unity](<../../Unity/Gear/Assets/Gear/Scenes/Example/UI Example.unity>)
- 覆盖层：[Overlays 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Overlays>)
- 控件：[Widgets 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/Widgets>)
- 动效：[Motion 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/Motion>)
- 交互：[Interaction 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/Interaction>)
- 主题：[Theme 目录](<../../Unity/Gear/Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Theme>)

**Evo（只读参照）**

- 形状：[ProceduralShape.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShape.cs>)、
  [Operations](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeOperations.cs>)、
  [Material/StylePage](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeMaterial.cs>)
- 效果：[Effects 目录](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects>)，重点
  [InteractiveEffectRenderer.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/InteractiveEffectRenderer.cs>)、
  [Ripple.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/Ripple.cs>)、
  [DotMatrixLoader.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Effects/DotMatrixLoader.cs>)
- 控件：[Button/ProgressButton.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Button/ProgressButton.cs>)、
  [Color Picker](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Color Picker>)、
  [Showcase Panel](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Showcase Panel>)、
  [Date and Time](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/UI Elements/Date and Time>)
- 布局：[FlexibleGridLayout.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Layout/FlexibleGridLayout.cs>)、
  [ReorderableList.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Layout/ReorderableList.cs>)、
  [MarqueeSelection.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Layout/MarqueeSelection.cs>)
- 主题：[Styler.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Styler/Styler.cs>)、
  [ColorTransition.cs](<../../Unity/Gear/Assets/Evo/Evo UI/Scripts/Core/ColorTransition.cs>)
- Demo 绑定层：[Demo/Scripts](<../../Unity/Gear/Assets/Evo/Evo UI/Demo/Scripts>)（只作行为参考，不搬代码）

**相关文档**

- [架构总览](../01-架构总览.md)
- [03-新增一个窗口](../03-新增一个窗口.md)
- [便捷性与可移植性评估](便捷性与可移植性评估.md)
- [工具箱](../guides/工具箱.md)
