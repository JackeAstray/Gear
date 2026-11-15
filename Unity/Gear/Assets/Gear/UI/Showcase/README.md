# Showcase —— UI 展厅

**放**：展厅目录、展厅/详情窗口、卡片预制体与编辑器生成器。

**不放**：
- 可复用的 UI 组件 → `Utilities/UI/`（ImageEx、LoopScrollRect 等）
- 窗口框架 → `Framework/Runtime/System/UISystem/`
- `UI Example` 场景里的原始卡片 → 生成器只读它们，导出到 `Cards/`

## 资源链路

```
UI Example 场景（150 张卡）
        │  ShowcaseBuilder 扫描 CardTitle / CardDescription
        ▼
UI/Showcase/Cards/<page>/<NNN>-<标题>.prefab      ← 每张卡独立预制体
        │
        ▼
Resources/UI/Showcase/ShowcaseCatalog.asset      ← 页 + 条目 + 预制体引用
Resources/UITheme/ImageExDefault.asset           ← 暗色主题 token
Resources/UITheme/ImageExLight.asset             ← 亮色主题 token
        │
        ▼
Resources/Prefabs/UIs/ShowcaseUIPlane.prefab     ← 分类页 / 搜索 / 分页网格
Resources/Prefabs/UIs/ShowcaseDetailUIPlane.prefab
Resources/Prefabs/UIs/AudioMixerSampleUIPlane.prefab
Resources/Prefabs/UIs/PartyLobbySampleUIPlane.prefab
Resources/Prefabs/UIs/InventorySampleUIPlane.prefab
        │
        ▼
Scenes/Example/UI Showcase Example.unity         ← ShowcaseLauncher 打开展厅
```

## 生成

窗口/样例已改为**直接维护的 prefab**，生成器不会再覆盖它们。

先打开 `Assets/Gear/Scenes/Example/UI Example.unity`，再执行菜单：

1. `Gear/UI 展厅/1. 重建卡预制体与目录` —— 从 `UI Example` 导出 150 张卡 + 生成 Catalog；
2. `Gear/UI 展厅/2. 定位主窗口 prefab（直接维护）` —— 选中 `ShowcaseUIPlane.prefab`，不再生成；
3. `Gear/UI 展厅/3. 生成示例场景` —— 生成 `UI Showcase Example`。

只改分类/跳过空页时，可调用 `ShowcaseBuilder.RebuildCatalogOnly()` 复用已有卡片预制体。

窗口与数据项 prefab 路径：

```
Resources/Prefabs/UIs/ShowcaseUIPlane.prefab
Resources/Prefabs/UIs/ShowcaseDetailUIPlane.prefab
Resources/Prefabs/UIs/AudioMixerSampleUIPlane.prefab
Resources/Prefabs/UIs/PartyLobbySampleUIPlane.prefab
Resources/Prefabs/UIs/InventorySampleUIPlane.prefab
Resources/UI/Showcase/ShowcaseGridCell.prefab
Resources/UI/Showcase/InventoryItemCell.prefab
```

修改布局直接打开 prefab 编辑；Catalog/卡片仍可重建，二者互不覆盖。**不要手改 `Cards/`**，下次重建会覆盖。

## 运行

打开 `Assets/Gear/Scenes/Example/UI Showcase Example.unity` 按 Play。
场景名包含 `Example`，Bootstrap 只初始化引擎、不跳转场景；`ShowcaseLauncher` 等 UISystem 就绪后打开窗口。

- **Edit 模式**：场景里有一份 `ShowcasePreview (Edit Only)` prefab 实例，用于查看/调整布局；
- **Play 模式**：`ShowcaseScenePreview` 在 Awake 立即移除预览，正式窗口仍由
  `ShowcaseLauncher` 从 `Resources/Prefabs/UIs/ShowcaseUIPlane` 经 UISystem 加载；
- 因此 Scene 里只有一个 Launcher 是正常的，Edit 下看到的是预览，不是运行时窗口。

- 8 个分类页：形状·图像 / 材质·滤镜 / 效果·遮罩 / 控件 / 文本 / 数据 / 动效·布局·导航 / 覆盖层；
- 每页 4×2=8 张卡，`上一页/下一页` 翻页；
- 搜索框对全部页按标题/说明/标签做包含匹配；
- 卡片右上角 `查看` 放大到详情窗口，详情里卡片仍可交互；
- 顶部 `样例 · 混音台` / `样例 · 派对` / `样例 · 背包` 打开三个整屏样例：
  - Audio Mixer：4 通道音量/静音/电平 + 主输出 + 预设 + Apply/Reset；
  - Party Lobby：4 个玩家位/准备开关 + 模式/回合设置 + 5 秒开始倒计时；
  - Inventory：6×4 物品格 + 选中详情 + Tooltip + 右键 ContextMenu + 按稀有度排序；
- 顶部 `Light` / `Dark` 切换主题，走 `GearThemeRegistry.Apply(theme, 0.25f)`；
  展厅 chrome 与之后打开的整屏样例跟随主题，已打开窗口不重建；
- 8 个分类 Tab、Header/Footer 按钮、详情与样例操作按钮均直接在 prefab 挂
  `GearInteractive` + `GearInteractiveTint`：Normal / Hover / Pressed / Selected / Disabled；
- 新增 M1.7 覆盖层展卡：Toast / Tooltip / ContextMenu / BorderGlow / BackdropBlur / 双面板 SoftMask（prefab 直出）；
- 新增 `控件 · ProgressButton` 展卡：长按 1.2s 完成、松手回退、完成 Toast；
- 新增 `控件 · ColorWheel` 展卡：程序化 HSV 色轮，角度 Hue、半径 Saturation；
- 新增 `控件 · ColorPalette` 展卡：12 色块环，悬停外推/选中描边/键盘导航；
- 新增 `控件 · ShowcasePanel` 展卡：3 页自动轮播，标题/说明/背景色随页切换；
- 新增 `效果 · Ripple` 展卡：点击 Preview 发射 8 波 shader 涟漪；
- DotMatrix 扩展 inactive/edge/phase/trail，现有 `效果 · 点阵` 卡直接生效；
- TimePicker 支持分钟步进/自定义 AM-PM/行高/tick 音效；
- Calendar 支持 Day/Month/Year 三视图，点 Header 缩小、点月/年格回到下一层；
- Timer 支持 TimeMultiplier、系统时钟、平滑进度与反向填充；
- 新增 `控件 · Carousel` 展卡：3 页自动轮播，邻页露出并降低透明度/缩放；
- 新增 `控件 · FlexibleGrid` 展卡：响应式 3 列 + Masonry 高度；
- 新增 `控件 · Selector / RectDragger / ButtonGroupDimmer` 三张交互展卡；
- 新增 `控件 · Marquee / ComparisonSlider` 两张展卡；
- 新增 `控件 · Reorderable` 展卡：拖动行实时换位；
- 主题切换走 `GearThemeRegistry`，同一 Graphic 过渡自动去重；
- 新增 `控件 · InteractionEffects`：一个按钮挂 Ripple/Spotlight/Glow，总线按优先级/通道调度；
- Overlay 统一走 `GearOverlayCoordinator`：Esc 只关闭栈顶，同 owner 不重复；
- 新增 `效果 · ShapeOperand`：主矩形 Subtract 圆形操作数；
- 新增 `效果 · PatternSource`：Shape 图块 + Gradient 颜色模式；
- 新增 `样例 · GooeyMenu / ActivityRings / 3DKeys / Wallpaper` 四张组合样例；
- Backdrop 默认共享同源隐藏相机与 capture RT；
- 菜单 `Tools/Gear/Export ImageEx Shape Sprite` 可导出 1x–4x 可染色形状 Sprite；
- Spotlight/Glow/Ripple 自动注册瞬态效果，Pooled 模式强制独立材质；
- SoftMask 支持最近 + 外层两层，RoundedRect/Circle 可嵌套；
- `GearImageExQuality` 可全局切 Lite，跳过叠加效果；`GearStylePageBudget` 统计唯一材质/纹理；
- `GearRenderBudgetProbe` 可采样 Batches/SetPass；展厅实测唯一材质 62；
- 156 张卡的卡面/标题/说明已接主题 token：Surface / Text / TextSecondary；效果 Preview 保持原色；
- 156 张卡内的 64 个 Button 已全部接 `GearInteractive` + Tint 五态；
- 详情页 `FX` 打开 `ShowcaseCustomizer`：4 个滑杆实时改 ImageEx 描边/辉光/内阴影/渐变，Reset 恢复初值；
- 展厅格子用 `ShowcaseGridCell.prefab` 的透明 Hover overlay 显示卡片 hover/press 描边，不改卡片本体材质；
- Header 的 `Motion On/Off` 走 `GearMotionSettings`；关闭后 Grid 入场直接完成、卡片不倾斜；
- Grid 挂 `GearAnimatedContainer` 做换页错峰入场；格子挂 `GearRectSway` 做指针倾斜；
- ESC/手柄 B 由 `UIRouter` 关闭详情/样例。

## 已知限制

- 卡片是静态导出，`UI Example` 里后续手工改动不会自动同步；需要重建；
- 每页 8 张、非虚拟化网格；150 张全量同屏会重建 4500+ ImageEx，故用分页；
- 五态、动效、实时调参（设计文档 M1.5、M1.6、M1.8）尚未接入；
- `Cards/` 约 9 MB，属于生成物；如仓库体积敏感，后续可改为 Addressables 或构建期生成。
