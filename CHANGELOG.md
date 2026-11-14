# Changelog

## [Unreleased]

### Added

- **ImageEx 图案填充**：`Pattern`（点阵/网格/条纹/棋盘/圆环）+ 尺寸/留白/旋转/偏移/流速/径向 falloff，
  按形状像素坐标铺开，非正方形图形不会被拉伸。
- **ImageEx 边缘辉光**：`EnableBorderGlow` / `BorderGlowColor` / 宽度/羽化/环境光/跟随半径，
  配合新组件 `ImageExPointerGlow` 做指标跟随指针的悬停辉光。
- **背景模糊可直接挂在 ImageEx 上**：`UIBackdropBlur` 检测到 `ImageEx` 时不再替换材质，而是把
  `_BackdropTex / _EnableBackdropBlur / _GlassTint` 写进 ImageEx 材质，由新增 `RM_Backdrop.cginc`
  按屏幕 UV 采样；SDF 形状、圆角、描边、图案仍然保留。
- **GearInteractive 五态**：新增 `GearInteractive`（Disabled/Normal/Highlighted/Pressed/Selected）
  与 `GearInteractiveTint` 颜色绑定；非 Button Graphic 也能复用统一 hover/press/select 状态。
- **GearProgressBar / GearCounter**：新增进度条（0..1、平滑、格式化标签）与数字滚动
  （目标值、时长、格式化、立即赋值）；画廊新增「数据 · Progress / Counter」演示卡。
- **GearLineChart / GearBarChart**：新增轻量自绘图表（折线 + 数据点、柱状 + 柱体几何），
  不依赖第三方图表库；画廊新增「数据 · Line / Bar」演示卡。
- **UI 渲染诊断**：新增 `Gear/UI Example/UI 渲染诊断（材质/Canvas）`，输出
  Canvas / Graphic / ImageEx 材质实例与可合批分组报告到 `Docs/design/Gear-UI-渲染诊断.md`。
- **共享材质试点**：新增应用/还原菜单；试点验证 401 → 286 材质实例后，
  因 Shared 模式触发 UI rebuild 日志且 draw call 无法实测，已回滚，保留工具供后续排查。
- **GearRadarChart**：新增 5 轴雷达图（网格/填充/描边/数据点）；画廊新增「数据 · Radar」演示卡。
- **GearChartListLink**：新增图表 → ListView 联动组件；饼图扇区点击按分类列过滤行，
  支持恢复全部/清除筛选；画廊新增「数据 · Pie → List」联动卡。
- **GearPieChart / GearChartLegend**：新增环形/饼图（hover 高亮、内半径、扇区点击事件）与图例组件；
  画廊新增「数据 · Pie / Legend」演示卡。
- **GearListView 数据层**：新增 `GearListView` + `GearListRow`，复用 `LoopScrollRect` 的虚拟化/选中，
  支持多列文本、空态、列宽、CSV 导入、行点击/选中事件；画廊新增「数据 · ListView」演示卡。
- **主题 token 扩展**：`ImageExThemeAsset` 新增字体 / Sprite / 渐变 token；
  新增 `GearThemeBinder`，可把 token 写到 TMP 字体、Image Sprite、ImageEx 渐变。
- **Overlay 阶段 2**：`GearContextMenu` 支持子菜单级联关闭与自定义行；`GearTooltip` 增加
  `Prewarm` 复用池、可配置最大宽与 `ShowCustom` 富内容；`GearPopover` 增加 alpha/scale 开关动画。
- **UI 覆盖层**：`GearToast`（右上角堆叠、unscaled 自动关闭）、`GearTooltip` + `GearTooltipTrigger`、
  `GearContextMenu`（点击外部/Esc 关闭、贴边翻转）；统一走 `GearOverlayLayer` 与主题令牌。
- **默认主题资产**：`Assets/Gear/Resources/UITheme/ImageExDefault.asset`（Overlay* 与 Surface/Accent 令牌）。
- **ImageEx 画廊生成器**：直接清空并重建 `ImageEx 画廊/Scroll View/Viewport/Content`（原 UI示例），100+ 张效果卡片；`Gear/UI 示例` 菜单组已移除。
  需要 Sprite 的滤镜/过渡卡片统一使用 `Assets/Gear/Resources/UI/Sprites/Saber.png`。
- **ImageEx 形状命中**：新增 `RaycastDetectionMode`（Standard / Shape / None）。Shape 模式按 SDF 形状判定，
  胶囊/圆形/星形/四边形的透明区域不再吃点击；None 模式只渲染、不参与命中。Inspector 新增「命中检测」。
- **ImageEx 矩形圆角百分比**：`RectangleImg.RadiusMode` 支持 Pixels / Percentage，100 = 短边的一半（胶囊），
  旧资源默认 Pixels 行为不变；Inspector 的矩形圆角面板可直接切换单位。
- **ImageEx 渐变 Zoom**：`GradientScale` 明确为程序化渐变与渐变纹理共用的 Zoom（0.1–5），
  Inspector 标签改为「渐变 Zoom」，画廊新增 0.5x / 2x / 3x 三张卡。
- **ImageEx 形状软边**：新增 `ShapeSoftness`（0.001–8），shader 压低 SDF 像素尺度做几何边缘羽化；
  Inspector 新增「形状软边」，画廊新增「形状 · 软边」卡。
- **ImageEx 抖动**：新增 `Dither`，按屏幕坐标噪声打散 8bit 渐变色带；画廊新增「抖动 · Dither」卡。
- **ImageEx 圆角同步**：`RectangleImg` 新增 `CornerRadiusSyncMode`（None / MatchParent / MatchTarget / MatchAncestor）
  与 `SyncInset`，嵌套卡片/胶囊按钮可跟随父级或目标圆角；来源改半径时自动脏化子级。
  画廊新增「形状 · 圆角同步」卡。
- **ImageEx 五态状态色**：新增 `ImageExStateColors`，把填充/描边/辉光绑定到 `GearInteractive` 的
  Disabled/Normal/Highlighted/Pressed/Selected，支持可配置过渡时长与 `Bind()` 显式绑定；
  画廊新增「效果 · 五态状态色」卡（可 hover/press 验证）。
- **ImageEx 指针聚光**：新增 `RM_Spotlight.cginc` 与 `ImageExSpotlight` 组件，在图形内部叠一团跟随指针的
  软光（半径/羽化/强度/HDR 颜色，只加色不改 alpha，形状与贴图两条路径都支持）；画廊新增
  「效果 · Spotlight」卡，默认悬停淡入。
- **ImageEx 材质池（Pooled）**：新增 `MaterialMode.Pooled` 与 `ImageExMaterialPool`：按材质的完整 uniform
  签名（shader 属性逐个取值 + 关键字）复用材质，设置相同的实例自动共享、改设置时换到新材质且不影响旧材质；
  有相机画面 / `GearSoftMask` / 被 Mask 换过材质时自动回退逐实例写入。
  同时把 `GetModifiedMaterial` 的 uniform 写入抽成 `WriteMaterialUniforms(Material)`。
  实测 UI Example（415 个 ImageEx）：batches **418 → 171**、setPass **401 → 147**、drawCalls **418 → 171**。
  默认仍是 Dynamic，需要合批的界面按组切到 Pooled。
- **材质池工具台入口**：新增菜单 `Gear/ImageEx 材质池/选中切 Pooled（同设置共享）` 与
  `选中还原 Dynamic`；自动跳过过渡循环 / 渐变旋转 / 指针辉光这类逐帧改 uniform 的实例。
- **GearAnimatedContainer**：错峰出现容器（每项延迟 = 序号 × Stagger，缩放曲线 + 淡入 + 可选禁用交互），
  用 unscaled 时间推进，`Sample()` 可做编辑预览与测试；画廊新增「动效 · 错峰出现」卡。
- **GearRectSway**：指针倾斜（归一化指针位置驱动俯仰/偏航，SmoothDampAngle 平滑、可单轴开关），
  `SetPointerNormalized()` 支持摇杆/外部驱动；画廊新增「动效 · 指针倾斜」卡。
- **GearDockMagnifier**：Dock 放大（高斯权重 + SmoothDamp，改子项 `sizeDelta` 让布局自动让位，
  Free/Horizontal/Vertical 三种方向）；画廊新增「布局 · Dock 放大」卡。
- **GearSpring**：可序列化阻尼弹簧（固定 4ms 切片积分、最多 8 片，长帧不爆炸），
  提供 `Step/Reset/Settled`；供指针倾斜、Dock、数值动画复用。
- **GearRadialLayout**：径向 `LayoutGroup`（半径/起止角度/顺逆时针/是否均分整圆/朝心旋转）；
  画廊新增「布局 · 径向」卡。
- **GearSpinner**：固定角速度旋转器（可切顺/逆时针、`Step` 可外部驱动）；画廊新增「动效 · 旋转器」卡。
- **GearRectParallax**：指针视差（归一化位置驱动目标平移，可反相，SmoothDamp 平滑）；画廊新增「动效 · 视差」卡。
- **GearDistributionBar**：分布条（按权重分配子项宽度，支持间距/最小宽度/固定总宽）；画廊新增「布局 · 分布条」卡。
- **GearMenuBar**：菜单栏（按标签生成按钮、点击回调序号，配合 `GearContextMenu.Show(anchor, items)` 做贴按钮下拉）；
  同时给 `GearContextMenu` 增加以 `RectTransform` 为锚点的重载（自动上翻/贴边）。
- **GearDotMatrix**：新增 `RM_DotMatrix.cginc` 与 `GearDotMatrix` 组件——按格子 + 时间解析出 8 种点阵图案
  （波浪/同心圆/雷达/螺旋/蛇形/均衡器/棋盘/脉冲），支持圆点/方块/菱形、格子尺寸/半径/颜色/强度/速度；
  只加色不改 alpha，可与 Pattern/Spotlight/SoftMask 叠加。画廊新增「效果 · 点阵」卡。
- **警戒斜纹画廊卡**：验证 `WarningStripe` 用现有 `PatternMode.Stripes`（45°、可滚动）即可覆盖，
  不新增 shader；画廊新增「效果 · 警戒斜纹」卡。
- **GearPixelCurtain**：新增 `RM_PixelCurtain.cginc` 与 `GearPixelCurtain` 组件——按格子 + 逐格随机延迟
  揭示/隐藏（方向可切、格子/随机度/羽化可调），只乘 alpha；支持 `Show/Hide/SetProgress/Step` 与自动往返；
  画廊新增「效果 · 像素幕帘」卡。
- **GearTopographic**：新增 `RM_Topographic.cginc` 与 `GearTopographic` 组件——程序化 value noise + fbm
  生成地形等高线（频率/层级密度/线宽/主线间隔/颜色/强度/滚动/中心衰减），无贴图、只加色；
  画廊新增「效果 · 地形等值线」卡。
- **GearTextEffects 扩到 12 种**：在打字机/乱序/波浪/抖动/闪烁之外新增颜色循环（Palette）、透明度脉冲、
  弹跳（Bounce）、摇摆（Swing，逐字旋转）、左右摆动（Wiggle）、故障（Glitch）、整体脉冲（Pulse）；
  逐字顶点/颜色修改拆到 `GearTextEffects.Extra.cs`，画廊新增「文本 · 弹跳 / 摇摆」卡。
- **ImageEx Sprite Fill**：新增 `SpriteFillMode`（Stretch / Cover / Fit）与 `ImageEx.SpriteFill`；
  Cover 按目标矩形宽高比裁切精灵 outer UV（保持图集偏移居中），Fit 等价于旧 `preserveAspect`。
  网格/阴影/SDF 投影/命中区域共用同一套填充计算；画廊新增「图片 · Cover 裁切」卡。
- **GearInkPanel**：新增 `RM_InkPanel.cginc` 与 `GearInkPanel` 组件——从原点向外扩散带噪声毛边的墨团，
  支持进度/原点/羽化/粗糙度与 `Show/Hide/SetProgress/Step/自动往返`；只乘 alpha；
  画廊新增「效果 · 墨团揭示」卡。Evo 的"烘焙覆盖度到 RT 供 SoftMask 使用"暂未做。
- **GearDropdown**：新增下拉选择组件——点击触发 `GearPopover` 选项列表，选中回写标签并派发序号；
  支持运行时改选项、`SetValueWithoutNotify`、开合与选中即关；画廊新增「控件 · Dropdown」卡。
- **GearSwitch**：新增开关控件——点击切换，把手在两点间缓动、背景色可过渡；`SetIsOn` / 事件 / `Step` 可外部驱动；
  画廊新增「控件 · Switch」卡。
- **GearRangeSlider**：新增双把手区间滑杆——拖动最近把手、自动保持 low ≤ high（可设 `MinGap`），
  同步 fill 宽度与把手位置；画廊新增「控件 · RangeSlider」卡。
- **GearRadioGroup**：新增单选组——一组按钮只允许一个选中，选中项可自动换色，
  支持 `Configure` / `Select` / `allowDeselect` / 事件与键盘焦点；画廊新增「控件 · RadioGroup」卡。
- **GearQuantitySelector**：新增数量选择——减号/数值/加号，支持 `min/max/step`、循环与 `format`，
  到达边界自动禁用按钮；画廊新增「控件 · Quantity」卡。
- **GearPickerWheel**：新增竖向滚轮选择——拖动 / 鼠标滚轮 / 键盘手柄上下切换，松手按速度投影吸附最近项，
  支持循环、无 content 时仅逻辑驱动、`SetOptions` / `SetValue` / `Step` / `Confirm`；画廊新增「控件 · PickerWheel」卡。
- **GearTimePicker**：新增时间选择——时/分（+秒、AM/PM）多列滚轮联动，`Use24Hour` 切换 12/24 小时制，
  格式串 `HH/h/mm/ss/tt` 渲染标签，事件给当天秒数；画廊新增「控件 · TimePicker」卡。
- **GearTimer**：新增计时器——秒表 / 倒计时、暂停继续、循环、unscaled、`HH/mm/ss/ff` 格式串与完成事件；
  `Tick` 可手动推进，EditMode 用例覆盖暂停与完成；画廊新增「控件 · Timer」卡。
- **GearOffScreenIndicator**：新增出屏指示器——目标离开可视区时把箭头贴到安全区边缘、指向目标并显示距离，
  边缘投影与朝向是纯函数（`ClampToEdge` / `AngleFor`），画廊新增「控件 · OffScreen」卡。
- **GearInputFieldEnhancer**：新增输入框增强——长度计数（代理对按 1 字）、Enter/多行 Ctrl+Enter 提交、
  IME 组合态拦截（`CompositionProvider` 可注入）、提交清洗/清空；不注册 `ITextPreprocessor`，与语言系统顺序无关；
  画廊新增「控件 · InputField」卡。
- **GearMultiSelect**：新增多选下拉——`GearPopover` 勾选集合 + 确认，掩码（前 31 项）/序号列表、
  `MaxSelection` 上限、`SelectAll` / `ClearSelection`；画廊新增「控件 · MultiSelect」卡。
- **GearCalendar**：新增月视图日历——按周起始日偏移生成日期网格（今天/选中/跨月标记），
  翻月跨年、`SelectDate` / `SelectDay`、可选格子模板与配色；画廊新增「控件 · Calendar」卡。
- **GearSoftMask 软遮罩**：新增 `GearSoftMask` 容器组件，对子树 `ImageEx` 做矩形/圆角/羽化/反相遮罩；
  不读 RT、不改 Image/TMP 材质，嵌套取最近父级。画廊新增「遮罩 · 软边」卡。
- **GearTextEffects 文本动效**：新增 `GearTextEffects`（TMP）：打字机、乱序显现、波浪、抖动、闪烁；
  文字内容变化自动重播，画廊新增「文本 · 打字机 / 波浪」「文本 · Scramble / Shimmer」两张卡。
- **GearTabGroup**：新增 Tab 组组件：按钮切换页面、id/默认页、焦点迁移、`TabChanged` 事件；
  画廊新增「导航 · Tab」演示卡（A/B/C 三页）。
- **GearStepper**：新增步骤流组件：Next/Back、校验门 `CanContinue`、步骤指示器、焦点迁移、
  完成事件；画廊新增「导航 · Stepper」演示卡。
- **GearPageView**：新增页面滑动视图：Prev/Next、拖拽/滑动、页码指示器、可调过渡、自动横/纵向排版、焦点迁移；
  画廊新增「导航 · PageView」演示卡。
- **GearPopover**：新增覆盖层 Popover：锚点定位、屏幕贴边修正、点击外部/按 Esc 关闭、
  内容构建回调与 `GearPopoverTrigger`；画廊新增「覆盖层 · Popover」演示卡。
- **GearScrollFade 滚动渐隐**：新增 `GearScrollFade`，ScrollRect 滚动时刷新 `GearSoftMask`，
  解决内容移动后遮罩停在旧位置；画廊新增「效果 · 滚动渐隐」演示卡。
- **GearToast 队列/时长条**：`MaxVisible` 默认 3，超出排队；每条底部新增 2px 剩余时长条，
  关闭一条后自动补位。
- **UI Example 全量同步**：新增 `Gear/UI Example/全量重建 ImageEx 画廊（会清空手工修改）` 菜单，改完 ImageEx 或画廊目录后可重建并保存场景。
- **UI Example 增量同步**：新增 `Gear/UI Example/仅补充新增 ImageEx 卡片`，只补缺失卡片、不删除/不重建已有卡片；
  全量重建菜单已明确标注会清空手工修改，避免再次误删手工调过的效果。

### Fixed

- **InkPanel / PixelCurtain 在 Inspector 调数值没效果**：两个组件都没有序列化“进度”，且 `OnValidate` 不写回
  ImageEx，进度恒为 1 时墨团/幕帘全显示，调原点、羽化、粗糙度、格子等参数自然看不出变化。
  现分别新增 `Progress`（0–1）字段，`OnValidate` 立即写回；DotMatrix / Topographic / Spotlight / Switch /
  RangeSlider / Dropdown 也补了 Inspector 即时预览（改数值/颜色/选项立即生效）。
- **Dropdown 弹层盖住字段**：弹层 pivot 是左上角，但 X 偏移用了 0（从字段中心展开），上翻时 Y 也没加面板高度；
  现在 X 对齐字段左边缘，空间不足时按“字段顶边 + 弹层高度 + 间距”向上展开。
- **Dropdown 弹层在屏幕底部被裁 / 贴边判断错误**：`GearPopover.Position` 在取 world corners 前没刷新布局，
  尺寸刚写入时按旧 rect 贴边；现在先 `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.Rebuild`，
  并且 `GearDropdown` 在下方空间不足时自动向上展开。
- **Cover 填充在 Inspector 里不可见**：Inspector 增加「填充方式」（Stretch/Cover/Fit）与「保持纵横比」；
  Cover 时禁用并说明"会覆盖保持纵横比"，Fit 等价于 `preserveAspect = true`。
- **控件 · Dropdown 卡片布局**：由整张 320×140 预览当输入框，改为卡片内 240×46 的紧凑字段 + 三角指示，
  弹层从字段下方展开。
- **动效演示卡看不出效果**：`GearAnimatedContainer` 只在 OnEnable 播一次（0.4s），
  `GearRectSway` 8° 且预览被 `RectMask2D` 裁掉，运行后看起来像静态卡。
  现已给 `GearAnimatedContainer` 增加 `Loop`/`LoopInterval`（画廊卡 1.2s 循环、起始缩放 0.7），
  `GearRectSway` 增加 `MaxAngle` 且画廊卡留 18px 边距、角度提到 14°；
  两张卡在 Play Mode 下分别可见循环错峰与 hover 倾斜。
- **增量同步写错 Content**：`FindContent` 只认根节点名 `UI示例`，根改名 `ImageEx 画廊` 后走 fallback，
  选中了「滚动渐隐」演示卡内部的 `ScrollFade/Viewport/Content`，把 17 张卡加进了演示卡。
  现已兼容两个根名，并优先取子节点最多的 Content；误加对象已清理。
- **Popover 触发器丢失脚本**：`GearPopoverTrigger` 与 `GearPopover` 放在同一文件时被 Unity 序列化成场景内
  MonoScript，Inspector 显示 Missing；已拆成独立 `GearPopoverTrigger.cs` 并重建「覆盖层 · Popover」卡。
- **GearTextEffects 打字机停住**：重写 Scramble 时漏掉了 Update 里的打字机推进分支，`Play()` 把
  `maxVisibleCharacters` 设为 0 后不再恢复；已补回推进/完成逻辑。PlayMode 实测打字机逐字显示、
  波浪顶点位移、Shimmer 颜色变化均生效。
- **画廊增量同步重复卡**：`Transform.Find` 会把标题里的 `/` 当成层级路径；改为遍历子对象名精确判重，
  并清理了场景里重复的「文本 · 打字机 / 波浪」卡。
- **架构守卫超大文件**：`ImageEx.Runtime.Rendering.cs` 的 `InitValuesFromSharedMaterial` 拆到
  `ImageEx.Runtime.SharedMaterial.cs`；`ImageExPreset.cs` 的 `ReadFrom` / `SameAs` 拆到
  `ImageExPreset.ReadWrite.cs`；`ImageExToolkitWindow.cs` 拆到
  `ImageExToolkitWindow.Tools.cs`（诊断）与 `ImageExToolkitWindow.Batching.cs`（统计/合批），
  并从 `file-size-baseline.txt` 删除对应豁免。EditMode 全量 230 项全绿。
- **ImageEx 外投影方向失效**：旧实现把 `shadowOffsetLocal` 只当等距扩张距离，不同方向的投影渲染结果相同。
  新增 `ShadowSpread / ShadowSoftness / ShadowOutsideOnly`，网格按 Offset 四边不对称外扩，
  shader 在偏移点求 SDF，语义对齐 Evo 的 Outer Shadow。
- **`Outline8` 阴影消失**：网格走了 `SdfOutline` 单 quad，但材质仍写 `_ShadowMode = 8`，shader 只识别 9。
  现在 `Shadow` setter 与材质写入都统一映射到 `SdfOutline`。
- **ImageEx 阴影被精灵 alpha 二次裁剪**：形状投影现在不再乘 `baseSample.a`，半透明贴图不会出现阴影缺块。
- **内阴影无方向、曲线偏线性**：新增 `InnerShadowOffset`，改为 Evo 的 `smoothstep(-aa, aa, sdf + size)`；
  `Softness` 0-1 映射到约 0-12px 羽化。
- **虚线过角断裂、alpha 反向**：矩形/圆形改为沿周长连续参数化，新增 `DashRounded` 圆头帽与 `DashOffset`；
  修正“虚线处用填充 alpha、空隙用描边 alpha”的问题，空心图形虚线不再整圈消失。
- **渐变能力补齐**：`GradientEffect` 新增 `ScreenSpace / Reverse / Speed` 与 `Angular` 类型；
  屏幕空间采样处理 D3D/GL 的 Y 翻转，自动流动直接读 `_Time.y`，不再需要 C# 每帧写 uniform。
- **ImageEx 预设/主题覆盖**：新参数进入 `ImageExPreset` 的 Apply/ReadFrom/SameAs，
  `ImageExThemeBinder` 新增投影/图案/辉光目标。

### Docs

- 新增 [Evo对照-ImageEx阴影与UI覆盖层](Docs/design/Evo对齐-ImageEx阴影与UI覆盖层.md)，记录对照结论、修改文件与验证步骤。
- 新增 [Evo对照-UI能力差距与路线图](Docs/design/Evo对照-UI能力差距与路线图.md)：把三轮对照的
  "已完成 / 还差 / 不照搬"固化成能力矩阵、分阶段路线（结构件 → 形状快赢 → 软遮罩 → 控件）、
  验收命令与可勾选清单。
- 新增 [Evo对照-EvoDemo复刻与ImageEx技术取舍](Docs/design/Evo对照-EvoDemo复刻与ImageEx技术取舍.md)：
  记录 EvoDemo 的真实构成（15 个脚本分组 / 6 整屏 / 约 6.9 万行运行时 + 1.7 万行 Demo）、
  ImageEx 的"立即做 / 中期 / 不做"取舍、UI 系统借模型不借骨架的边界，以及 Gear UI 展厅的分级路线。
- 新增 [Evo对照-第二轮-渲染契约与模块复刻清单](Docs/design/Evo对照-第二轮-渲染契约与模块复刻清单.md)：
  以当前工作树校准"已落地/已过期"结论，修正布尔运算与 uv3 通道的成本判断，给出 UI 系统三个可借契约、
  EVODemo 逐模块复刻成本与三个复刻包；并确认 `Assets/Evo` 已被提交进公开仓库（2699 个文件），
  同步修正三篇旧文档里"保持 untracked"的错误描述。

## [2.0.0] - 2026-11-05

这一版只做**一致性**：公开成员命名、接口命名、命名空间归属、文件名与类型名对齐。
没有语义变化，但**编译期不兼容** —— 接入方需要按 [迁移指南](Docs/guides/2.0-迁移指南.md) 改 `using` 与成员名。
1.3.1 引入的加密帧线格式变更（`NetworkProtocol.Version = 3`）仍适用于 2.0.0。

### Changed（破坏性）

- **公开成员 PascalCase**：`isInited` → `IsInited`（14 个系统）；`HttpResponse` 的
  `url/isSuccessful/isHttpError/isNetworkError/statusCode/bytes/text/error/texture/responseHeaders`
  → `Url/IsSuccessful/IsHttpError/IsNetworkError/StatusCode/Bytes/Text/Error/Texture/ResponseHeaders`；
  `Timer` 的 `duration/elapsed/isCountingDown/isLoop/loopCount/maxLoop/state/timeScale`
  → `Duration/Elapsed/IsCountingDown/IsLoop/LoopCount/MaxLoop/State/TimeScale`。
  这些都是普通属性而非 Unity 序列化字段，继续沿用 camelCase 会误导调用方。
- **接口改名**：`UIImgComponent` → `IUIImageComponent`（文件同步改名）。
  接口内部成员仍是 camelCase：它们是 Inspector 既有字段名，改名会连带改序列化键、让已有预制体失联。
- **命名空间对齐**：
  - `Gear.Framework.Runtime.Base` → `Gear.Base`（程序集名与命名空间终于一致，`Gear.Base.asmdef`
    的 `rootNamespace` 同步修正）；
  - `CoreEnum`（`LoaderMode`/`PanelType`/`PanelSize`）`Gear.Framework.Runtime` → `Gear.Common`，
    与其所在的 `Common/` 程序集一致；
  - `Utilities/UI/TreeView`：`Gear` → `Gear.Utilities.UI.TreeView`；
  - `Utilities/UI/ButtonExtensions/{Click,Animated}`：`Gear.Utilities.UI.ButtonClick` /
    `…ButtonAnimated` → `Gear.Utilities.UI`（与同目录其余按钮扩展统一）。
- **`Config.Enable_LOG` → `Config.EnableLog`**。
- **文件名与类型名对齐**：`KCPClientChannel.cs`→`KcpClientChannel.cs`、`KCPServerChannel.cs`→
  `KcpServerChannel.cs`、`VertexStream.cs`→`VertexDataStream.cs`、`UIWindowResult.cs`→`WindowResult.cs`、
  `CommonExtensions.cs`→`Extensions.cs`、`Type.cs`→`ImageExEnums.cs`（类型名不变，仅文件）。

### Removed

- **根目录 `Images/`（445 MB 文档示例 GIF）**：示例图已无人维护，移除后 clone 不再被这批文件拖累；
  `Docs/features.md` 的 18 处 `<img>` 一并清掉，各示例只保留文字说明。
  历史提交里的 LFS 对象仍在，要从历史剔除需 `git filter-repo --path Images --invert-paths`。

### Fixed

- 删除 10 处跨层死 `using Gear.Framework.Runtime;`（`Gear.Utilities` 并不引用该程序集，
  此前只是因为 `Gear.Framework.Runtime.Base` 的隐式命名空间链才“恰好”编译通过）。
- `Tools/upm-publish.ps1 -Export` 一直崩溃：`$export = Start-Process ...` 与脚本参数 `[switch]$Export`
  同名（PowerShell 变量不区分大小写），Process 被塞进 SwitchParameter 直接抛
  `ArgumentTransformationMetadataException`。改名 `$exportProcess` 后导出链路跑通。
- `link.xml` 与随包模板 `Documentation/CustomSystem.cs.txt` 里的旧命名空间 —— 由扩写后的
  架构守卫旧名规则当场发现。

## [1.3.1] - 2026-11-04

这一版把加密链路从"能加密"推到"能抵抗重放"，并给质量门槛补上用例数与覆盖率棘轮。

### Changed（破坏性：线格式变更）

- **加密帧 v3（防重放）**：`EncryptedCodec` 帧头新增 `[LE32 序号]` 并纳入 HMAC 覆盖范围，
  接收端只接受严格大于已见最大序号的帧（CAS 更新，并发解码也不会双收）。
  修复前帧内没有序号，抓包得到的合法加密帧可以在同一会话里原样重发 —— 重复触发一次
  "购买 / 扣费 / 提交"是完全可行的。序号是每连接状态：握手完成时新建 codec，新会话从 1 重新开始。
  **与 1.3.0 及更早不互通**（旧帧没有序号，无法区分"重放的旧帧"与"新帧"）。
- **握手 v3（协议版本化）**：`NetworkHandshake` 负载改为 `[1B 协议版本][16B 随机数][32B 持有证明]`，
  版本字节参与持有证明的 HMAC；新增 `NetworkProtocol`（`Version` / `MinSupportedVersion` / `IsSupported`）
  作为版本单一事实源。不兼容时在**握手阶段**断开并输出明确原因，而不是"看起来连上了、业务帧全部静默失败"。
  旧的 48 字节（无版本）负载整体拒绝，不做猜测式兼容。
- **`Modules.All` 只读**：`Modules.All` 与 `GameEngine.Modules` 由 `IList<ICustomSystem>` 改为
  `IReadOnlyList<ICustomSystem>`（`GameEngine` 侧返回 `ReadOnlyCollection` 视图）。此前外部拿到列表后
  直接 `Add/Remove` 能绕过 `AddModuleAsync` / `RemoveModule`，导致初始化顺序与事件不触发。

### Added

- **CI 用例数与覆盖率门槛**：新增 `Tools/CoverageGate`（零 NuGet 依赖）。始终执行"用例数不低于
  `Tools/CoverageGate/baseline.json`、失败数为 0"的棘轮；存在覆盖率数据时再卡行覆盖率。
  覆盖率包不进 manifest（离线打开工程会卡在包解析），改由 CI 在仓库变量 `GEAR_ENABLE_COVERAGE=true`
  时临时注入。用法见 [Docs/guides/测试与覆盖率.md](Docs/guides/测试与覆盖率.md)。
- **回归测试 9 条**：加密帧重放 / 乱序 / 序号篡改 / 新会话序号复位 / 旧握手负载拒绝 / 版本范围，
  以及窗口目录的 `Reset` 重扫、程序集晚加载失效、并发预热（`Tests/NetworkHandshakeTests.cs`、
  `Tests/UIWindowCatalogTests.cs`）。

### Fixed

- **`UIWindowCatalog` 失效竞态**：`AssemblyLoad` 触发的失效可能发生在 `EnsureBuilt` 扫描途中，
  扫描末尾的 `built = true` 会把它吞掉 —— 该程序集里的窗口永久登记不上。改为版本号 CAS：
  扫描开始记下版本，结束时版本变了就不置 `built`；扫描加锁，并发预热不再重复清表。
- **`UIRouter.Recompute` 重入**：`openWindowBuffer` 是复用列表，`RefreshModalState` 是 public，
  在 `OnClose`/`OnOpen` 回调里再调一次会让内层 `Clear()` 清空外层正在遍历的列表（漏阻断/越界）。
  加重入守卫，并在 `finally` 里清空，避免静态列表长期持有已关闭窗口。
- **`UISystem.CloseAllWindows` 重入**：同样的问题换了个位置 —— `CloseWindow` 触发用户 `OnClose`
  回调，回调里再调 `CloseAllWindows` 会清空外层正在倒序遍历的 `closeBuffer`（越界/漏关）。加重入守卫。
- **`SoundSystem` 快照列表**：`GetSpawnedList` 原先全局共用一只列表，`RecycleAll`/`StopAll` 在遍历中
  触发回调再入时，内层 `Clear()` 会把外层正在遍历的内容清空。改为双缓冲轮换取用。
- **`EncryptedCodec` 日志节流**：坏帧（重放/探测）可以持续发送，逐帧 `Log.Warning` 会刷屏并掩盖真正的故障；
  前 5 次全量记录，之后每 100 次一条。
- **`SaveSystem` 密钥派生**：`DeriveKey` 每次存档/读档都跑一遍 1000 轮 PBKDF2，而输入全是编译期常量。
  改为 `Lazy` 缓存主密钥与 MAC 密钥（移动端每次存读少几毫秒）。
- **`GetPlayerGroupKey` 每帧分配**：原先每次调用都重拼 `manufacturer#serial`，而它会在"枚举设备找同组"
  的循环里被逐个调用。结果并入 `DeviceCache`，随设备缓存算一次。
- **`GameObjectPoolRegistry` 在编辑器下抛异常**：`DontDestroyOnLoad` 只能在播放模式调用，
  EditMode 测试走到 `EnsureRoot` 直接抛 `InvalidOperationException`；改为仅在播放模式挂常驻，
  `ClearAll` 在非播放模式用 `DestroyImmediate`（编辑器没有帧循环，`Destroy` 延迟到下一帧会让断言飘）。
- **编译错误（HEAD 上就红）**：`RawTcpClientChannel.EnqueueEvent` 里 `dropped` 与 `out var dropped`
  同名（CS0136）；`ObjectPoolPrewarmTests` 缺 `using Gear.Framework.Modules.Pool`（CS0103）。两个都让
  整个工程编译不过 —— 也就是说这两处之前任何一次"测试通过"都不成立。

- **下载批次的取消/超时**：`FileDownloader` 的看门狗从"总墙钟"改为"连续无进度"（默认 30s），且在
  `executors` + `executorsOld` 上取消时跳过已完成的执行器 —— 原实现会在超时或用户取消时把已经下好的
  文件和 `.done` 标记一起删掉，形成"下到一半 → 清空 → 重下"。
- **断点续传**：`.etag` sidecar 改为在发第一个分块前写入。原实现只在下载完成时写，中断后
  `localEtag=null ≠ 远端 ETag` 会被判成"远端已替换"而删掉分块；标准 CDN 都带 ETag，续传实际不生效。
- **整文件下载超时**：非分块 GET 不再沿用 6s 的分块超时，按"至少 256KB/s + 60s"估算。
- **下载回调语义**：`Cancel(uri)` 不再在计数归零时触发 `OnDownloadsSuccess`；`DownloadMgr.DownloadFiles`
  检查 `Download()` 返回值，超时/取消/失败会回调 `error`；`skipIfExists` 与写入侧共用
  `HTTPHelper.GetFilenameFromUriNaively`，带 query 的 URL 不再重复下载。
- **HttpMgr.Clear()**：先快照再 Abort。原实现在 `foreach` 中 `Abort` 会从字典移除自身，
  下一次 `MoveNext` 抛 `InvalidOperationException`，其余在途请求既不 Abort 也不 Dispose。
- **Addressables 发布流水线**：构建前显式切 `Publish` Profile（`EnsureSetup` 默认切回 DevLocal，
  原实现会把 `http://localhost:8080` 烘进上传到 CDN 的 catalog）；上传失败时批处理退出码非 0；
  "增量对比后无需上传"按成功处理，未配置 OSS 与上传失败区分开。
- **UI 模态**：`UISystem.CloseWindow` 直接 `UIRouter.NotifyClosed`。原实现只靠 `UIRouter.PushAsync`
  懒安装关闭订阅，直接用 `UISystem.OpenWindow` 打开的模态关闭后，下层的 `interactable=false` 永不还原。
  `HideAllWindows` / `ShowAllWindows` 后也会重算阻断。
- **Bootstrap 重入**：`StartAsync` 单飞，启动期间的重入等待同一次启动；原实现会把"正在初始化"
  当失败并 `CleanupFailedStartup()`，销毁第一个合法启动的 Driver/引擎。
- **SceneSystem.Clear()**：旧加载在 `finally` 里不再无条件释放 `isLoadingAtomic` / 回写
  `targetSceneName`；增加 generation 守卫，避免 teardown 后与下一轮加载互相踩状态。
- **网络**：`ServerConnection` / `RawTcpServerConnection` 初始化 `LastReceiveTime`
  （开启 `idleTimeoutSeconds` 后新连接不再在下一帧被误踢）；`SwapToEncrypted` 后补发
  `DrainReliableResendQueue`（加密握手 + `persistOnDisconnect` 时积压消息不再永久丢失）。
- **对象池**：`GameObjectPoolRegistry.GetPool` 先创建 inactive GameObject、赋值 prewarm/maxSize
  再激活，修复参数被 Awake 抢先建池忽略的问题；`prewarmCount` 现在会真正预热实例。
- **启动期模块注册**：`AddModuleAsync` 在 `BeforeInit` / `Initializing` 期间不再重复登记更新列表
  （`InitModulesAsync` 会统一登记），避免该模块每帧 `Update` 两次。
- **PauseSystem**：恢复侧移出 `pauseAudioOnPause` 条件；暂停期间关闭该开关后恢复不再让
  `AudioListener.pause` 永久停在 true。
- **编辑器工具**：`CreateUIPlane` 的「第一步」先保存当前场景，覆盖场景/预制体前确认；
  `SmallFunctions` 切场景前保存；示例场景生成器覆盖前确认。
- **UI 模态层级**：阻断只作用于"比最上层模态更早打开"的窗口；打开在模态之上的提示/确认框不再被置灰。
- **UI 返回结果**：`CloseWithResult` 优先关闭最上层模态，不再误关下层导航窗口。
- **UI 玩家槽位**：`CarouselSelectionController.PlayerSlot` 改走 `SetPlayerSlot`，运行时改槽位会重新分桶，不再收不到输入。
- **网络限流**：限流移到 `HandleFrame` 入口，PING/PONG/握手/畸形 ACK 不再绕过 `maxMessagesPerSecond`。
- **RawTcp 反压**：接收队列超限不再丢中间字节，改为清空积压并断开连接（由重连恢复），避免长度前缀错位；
  丢弃 Connected 事件时显式关闭被接受的 socket。
- **加密握手超时**：客户端/服务端新增 `handshakeTimeout`（默认 10s），半握手连接不再永久占用连接槽。
- **Addressables 上传去重**：同名文件比较 MD5，内容不同时显式告警而不是静默丢弃；
  多个 catalog 时取最新写入的一份并告警，避免把旧 catalog 的 md5 烘进 `version.json`。
- **Excel 导表**：多工作表时显式告警只导第一张；JSON 输出保留子目录相对路径，同名文件不再互相覆盖。
- **UPM 导出**：交互式覆盖已有包目录前确认（批处理保持自动覆盖）。
- **UI HUD 焦点域**：`requestUIControl=false` 的窗口首次创建 `UIFocusScope` 时不再瞬时进入/退出 UI 模式。
- **UI 类型化 API**：`OpenWindow<T>` / `CloseWindow<T>` / `IsOpen<T>` 统一走 `[UIWindow]` 的 Id，
  不再在"类名 ≠ Id"时开出两个窗口。
- **UI 强类型参数**：派发前按当前窗口重新判断是否实现 `IWindowArgsReceiver<T>`，
  修复缓存命中后对不支持的窗口 `Invoke` 抛 `TargetException`、每次打开刷 error。
- **UI ESC 死局**：UI 模式下只有 `UIRouter.CanGoBack` 时才把 ESC 让给 UI，HUD/Static 窗口不再卡在 UI 模式。
- **UIToolkit Clear 竞态**：新增面板生命周期代次；`Clear()` 期间恢复的 `OpenPanelAsync` 不再把资源写回已清空的缓存，
  失败面板会走 `OnClose` 解除绑定。
- **CameraToImageEx**：RT 尺寸重建后沿用首次绑定的 `previousTargetTexture`，不再把相机原有 RT 丢成 null。
- **网络**：`Stop()` 主动断开时补发 `OnClientDisconnected`；单连接编码/发送异常不再中断广播或冒泡到 Tick；
  监听器持续异常时 Accept 线程退避 50ms（不再忙等）；重连重置 `pingSentTime`；
  RPC correlation / 可靠 seq 回绕时跳过仍占用中的 ID。
- **编辑器**：`OpenPath` 不再把平台名格式化成 "0"；工具箱页脚乱码修正；
  删除永远不可能命中的 Newtonsoft 冲突规则；Excel 临时副本改到系统临时目录（不再在 Assets 下留孤儿 `.meta`）。
- **UIWindowCatalog**：不再在枚举前就置 `built=true`；程序集数量变化（包安装后晚加载的程序集）会触发重扫，
  枚举失败也不会让窗口目录永久为空。
- **LoopScrollRect 分页**：平滑滚动期间以 `pendingPage` 为基准累加，连续点"下一页/上一页"不再停在同页；
  拖拽/新跳转会作废旧目标页。
- **Addressables 配置**：`Remote.LoadPath` 真正变化时才置 `changed` 并 `SaveAssets`，
  修改不会再只停留在内存里。
- **SaveSystem**：`SubsystemRegistration` 时清空自动保存的静态注册与 driver 引用；
  关闭 Domain Reload 后上一会话的 provider 不会再继续写旧数据；没有注册自动保存时也不再创建常驻 Driver。
- **NetworkMgr**：`UniversalNetworkBehaviour.Update` 也会消费延迟移除队列，
  只用 NetworkBehaviour、不启动 GameEngine 的工程不会让被移除通道滞留在列表里。
- **引擎重启**：`LaunchAsync` 在 `Paused` 状态先 `ClearModuleData()` 再初始化，不再对存活模块二次 `Init`；
  `GameEngine.Create` 丢弃 `Failed` 引擎前先 `Dispose`，避免旧静态 Subject 订阅泄漏到新引擎。
- **UIToolkit**：`Clear()` 归还 `GLOBAL_THEME_PATH` 的 `ResourcesSystem` 引用，主题样式表不再每次重启多一份引用。
- **UIInputSystem**：`RegisterUIEvents` 先释放上一轮订阅，重复 `Init` 不再重复回调 `OnUIOpened/OnUIClosed`。
- **网络握手机制不一致**：服务端收到未握手业务帧时改为记录明确错误并断开，不再静默丢弃
  （原先表现为"连接正常但消息全部消失"）。
- **KCP**：服务端 `Disconnect` 先用 `GetClientEndPoint` 确认连接存在，未知 id 返回 false，不再恒真。

### Changed

- 自托管 workflow 的 `UNITY_EDITOR` 改为优先取仓库 Variable，未设置时回退默认路径。
- `unity-verify.yml` 的 UPM 导出步骤改用 `Start-Process -Wait` 取真实退出码（原 `&` 对 GUI 子系统
  不等待且 `$LASTEXITCODE` 为 `$null`，该步骤必失败）；`upm-publish.ps1 -Export` 同样处理。
- `unity-ci.yml` 的 `.Instance` 计数段临时关闭 `pipefail`：计数归零时 `grep` 返回 1 会让
  `set -euo pipefail` 在赋值处中止整个 step（棘轮打满反而红）。
- `upm-smoke-test.ps1` 拒绝递归删除系统临时目录之外的 `-WorkDir`。
- 自托管 PR workflow 增加作者身份门禁：只允许 `OWNER` / `MEMBER` / `COLLABORATOR`，
  外部协作者与 Dependabot 默认不再落到自托管 runner（runner 隔离仍是主要防线）。

### Added

- `Tests/ObjectPoolPrewarmTests.cs`：共享池 prewarm/maxSize 回归。
- `DownloadIntegrityTests.NaiveFilename_Matches_What_Is_Written_To_Disk`：下载文件名口径回归。

### Docs

- 修正 `S-验收清单` 的过度声明：去掉不存在的"覆盖率阈值 / 大文件门禁 / 守卫单一实现"，
  体积数字改为 2026-11-03 实测。
- `便捷性与可移植性评估` 的总表标注已落地项；`01-架构总览` 删除已不存在的 `ModuleRuntime`；
  `Docs/README` 改为"本地链接由 CI 检查"；LFS 指南更新为"历史重写已执行 / 23 个 LFS 文件"。

## [1.3.0] - 2026-10-31

这一版集中在"把静默错误变成可观测的失败"，以及修正发布身份。

### Fixed

- **UPM 包身份**：导出器固定 `com.jackeastray.gear@1.3.0`，`package.json` 校验 name/version；
  `upm` 分支曾遗留 `com.reunion.movement@1.3.0` 旧包，发布脚本现在会拒绝推送错误包名。
- **网络重连分帧**：`NetworkClient.CreateChannel` 在重建连接前重置 `NetworkStreamAssembler`，
  避免旧连接的半帧与新流首字节拼帧（`LengthPrefixed` / `RawTcp` 下会静默错帧）。
- **通道被替换**：`NetworkClient.Tick` 检测 `channel.IsConnect`，同名通道被 `NetworkMgr` 替换后
  会收敛状态并触发重连，不再停在 Connected。
- **可靠重发**：`DrainReliableResendQueue` 改为 Peek → 发送成功 → Dequeue，重连补发失败不再丢消息。
- **RPC 失败语义**：新增保留消息 ID `0xFFF7` 与 `NetworkRpcException`；未鉴权/未注册/处理器异常
  回错误帧，客户端不再把"空响应"当成功。
- **握手主密钥缺失**：服务端在未配置主密钥时主动断开连接，不再留下"TCP 已连、握手永不完成"的半连接。
- **下载完整性**：成功落盘写 `<file>.done`（内容为字节数）；`DownloadMgr.skipIfExists` 只认
  "标记存在且大小一致"的文件，中断产生的截断文件会重新下载/续传。
  旧缓存没有标记时默认重下；确认完整时可临时开启 `DownloadMgr.treatLegacyFileAsComplete`
  一次性补写标记（见字段注释）。
- **HttpMgr 清理**：实现 `ISystemDisposable.Clear()`，引擎重启/模块移除时 Abort 在途与排队请求。
- **UI 状态机**：`Open→Open`（重复打开刷新）与 `Closed→Closed`（池化 `OnClose→OnDespawned`）
  改为合法转换，不再刷假告警。
- **ESC 与输入框**：文本输入聚焦时 `DispatchCancel` 直接消费，不再冒泡到 `UIRouter.Back()` 误关窗口。
- **返回键顺序**：`UIRouter.Back()` 优先关闭最上层模态，其次导航栈顶；`CanGoBack` 计入模态栈。
- **玩家槽位持久化**：跨会话只持久化稳定设备键；同型号无 serial 手柄的 `#dupN` 槽位只在本次会话内生效，
  并迁移/丢弃旧版本落盘的不稳定键。
- **死代码**：`UISystem.OnSet` 合并到"已打开窗口再次打开"的真实路径，`OnSetSubject` 恢复可触发；
  `[UIWindow(Static = true)]` 真正生效（关闭保留实例）。
- **UIInputSystem 访问收敛**：对 UISystem 的 5 处 `Instance` 改为 `Modules.TryGet`，
  `.Instance` 守卫基线从 117 降到 111。
- **`.Instance` 收敛**：11 个 Runtime 模块新增 `GetOrInstance()`（引擎优先、单例兜底），
  散落调用点统一迁移；守卫基线从 **111 降到 22**（仅注册点与 Modules 程序集独立使用保留）。
- **Static 重开**：关闭后的 Static 窗口在重开前先走 `OnSpawned` 回到 Ready，避免
  `Closed→Open` 非法转换告警，并让焦点域重新置顶。
- **UI Toolkit**：缺失 PanelSettings 时的兜底 `CreateInstance` 对象在 `Clear()` 中销毁，不再泄漏。

### Changed

- 巨型文件按 partial 拆分：`NetworkClient`（RPC）、`NetworkServer`（RPC）、`UWRExecutor`（完成标记）、
  `UIInputSystem`（玩家槽位）、`CarouselSelectionController`（导航）各自独立成文件，主文件回落到守卫基线以内。
  `LoopScrollRect` 1336 → 472 主文件 + 447 layout + 204 pull + 259 scroll，`>1000` 文件清零。
- 新增 `Tools/SyntaxCheck`（Roslyn 语法门禁，不依赖 Unity），CI 在 PR 上无条件执行。
- 新增 `Tools/upm-smoke-test.ps1`：空工程 `file:` 安装 + 编译烟测，自托管 main 验证已接入。
- `NetworkClientConfig.enableRpcErrorFrames` / `NetworkServerConfig.enableRpcErrorFrames`：
  混合版本部署时服务端可关错误帧，回退"空响应"以兼容旧客户端。
- `DownloadMgr.treatLegacyFileAsComplete`：旧缓存无 `.done` 时可选一次性补标记，避免弱网用户被重下。
- 平台：WebGL 本机构建通过（44.3 MB / 0 错误，12m55s）；浏览器真机验证仍待做。
- 新增 `UI/Gear.UI.asmdef` 与 `Editor/Gear.Editor.Framework.asmdef`：UI 与编辑器工具不再落
  `Assembly-CSharp` / `Assembly-CSharp-Editor`，架构守卫能看到这两层依赖。
- CI 新增 `unity-verify-pr.yml`：同仓库分支的 PR 在自托管 runner 上跑编译 + EditMode + 守卫；
  fork PR 明确跳过（安全边界），仍由 hosted 的静态门禁覆盖。
- 发布：`upm` 已推送为 `com.jackeastray.gear@1.3.0`；历史已重写并把 GIF/`.unitypackage`
  迁入 LFS（23 个对象）。旧包备份标签 `upm-backup-20261031`；协作者需要重新 clone。

### Added

- `NetworkRpcFrames.EncodeError / TryDecodeError`、`NetworkRpcException` 与对应测试。
- `UWRExecutor.HasCompletedDownload` / `MarkCompleted` 与 `DownloadIntegrityTests`。
- `UIWindowCatalogTests`、`NetworkCodecTests` 的分帧重置 / 保留 ID / RPC 错误帧用例。
- `UIModalStack`（纯 C# 模态栈）与 `UIModalStackTests`；`NetworkClient.IsChannelLost` 与
  `NetworkClientLivenessTests`。
- `Tests/PlayMode/UIRouterPlayModeTests`：Back 模态顺序、Static 重开、重复打开、ESC 文本聚焦；PlayMode 6 条。
- `Tests/PlayMode/NetworkLoopbackPlayModeTests`：真实 TCP 回环的 RPC 成功往返、未注册处理器错误、
  服务端重启后的自动重连；PlayMode 总计 9 条。
- 根目录 `THIRD-PARTY-NOTICES.md`，并随 UPM 包导出为 `Third Party Notices.md`。
- `Docs/design/S-验收清单.md`：S 的验收标准、已通过命令与剩余阻塞项。

### Docs

- 修正守卫数量（6 条 + link.xml）、测试数量、asmdef 数量、`.git` 体积等过期数字。
- 修正 `Docs/03` 的池化条件（AND 而非 OR）、`panelSize` 未实现的承诺、ESC 行为说明。
- UPM 指南新增"发布前检查"与包身份核对步骤。

## [1.2.0] - 2026-10-25

### Added

- `ModulePresets.Full / Minimal / Without<T> / Of`：模块清单从「背依赖表」变成选一行。
- `ModuleCatalog.ExpandDependencies`：启动时按 `[ModuleOrder(..., DependsOn)]` 自动补回缺失的被依赖模块。

### Changed

- Addressables 默认关闭：仓库不再带 `Assets/AddressableAssetsData`，只有显式启用时才初始化。
- 菜单顶层按段位固化：工具箱 → 打包 → 构建 → 内容 → Addressables → 开发。

## [1.1.0] - 2026-10-22

### Added

- 标准 UPM 包结构（package.json / README / CHANGELOG / LICENSE / Documentation~）。
- 运行时 / 完整 / OpenUPM 依赖三种导出 profile。

### Changed

- 编辑器工具通过 `GearPaths` 解析框架根，不再硬编码 `Assets/Gear`。
- 没有入口声明时 Bootstrap 不再隐式回退 `StartGame`；可用 `BootstrapOptions.Entry` 显式指定。
