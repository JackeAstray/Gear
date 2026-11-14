# Gear UI 下一阶段开发规划（Evo 对照后）

> 目标：把 EvoDemo 中真正有价值、且适合 Gear 骨架的部分，按“先地基、再细节、最后数据”的顺序落地。
> 约束：Evo 仅作行为/设计参考，不复制源码、Shader、预制体、字体、贴图；不复刻 Evo 的窗口/输入/Tween 骨架。
>
> 当前基线（2026-11）：EditMode 273/273；UI Example `Content` 117 个对象；
> 旧卡 92 张（其中 3 张有手工参数差异，已保留）；累计新增 23 张卡；`DocsCheck` 0 缺失。

---

## 一、总体判断

- **Evo 帮“零件和皮”，不帮“骨架”。**
- Gear 继续保留：`UISystem` / `UIRouter` / `UIModalStack`、`UIInputSystem` 焦点域与改键、
  `LanguagesSystem`、`LoopScrollRect` / `TreeView`、`UIToolkitSystem`。
- 只借 Evo 的：五态交互模型、主题 token 覆盖面、导航/覆盖层细节、Demo 的展厅组织方式。
- 不照搬：ProceduralShape、Evo Tween、Evo Styler 实现、VirtualCursor、完整 97 个预制体。

---

## 二、阶段划分

### 阶段 1：设计系统地基（本迭代执行）

| 任务 | 交付物 | 验收 |
|---|---|---|
| 1A 五态交互 | `GearInteractive` + `GearInteractiveTint` | 任意 Graphic/Selectable 可挂；五态事件；EditMode 用例 |
| 1B 进度组件 | `GearProgressBar` | 值/平滑/标签；EditMode 用例 |
| 1C 数字滚动 | `GearCounter` | 目标值/时长/格式化；EditMode 用例 |
| 1D 展厅同步 | UI Example 增量卡「数据 · Progress / Counter」 | 旧卡 0 差异；场景保存 |
| 1E 文档 | CHANGELOG + 路线图 + 本文档勾选 | DocsCheck 通过 |

**为什么先做这三个：** 都不依赖 Evo 的底层架构，能直接长在现有 uGUI/ImageEx/TMP 上；
后续 Tooltip、ContextMenu、ListView、图表都能复用“状态 + 数值 + 格式化”这套地基。

### 阶段 2：覆盖层打磨

| 任务 | 交付物 | 说明 |
|---|---|---|
| 2A Tooltip | 延迟、池、最大宽、富内容 | 现有 `GearTooltip` 只有触发 + 跟随 |
| 2B ContextMenu | 子菜单、自定义行、禁用项 | 现有基础菜单无子菜单 |
| 2C Popover | 开关 alpha/scale 动画 | 现有外点/Esc/贴边已做 |
| 2D Toast | 可选音效、模板、堆叠协调 | 队列/时长条已做 |

### 阶段 3：主题与数据展示

| 任务 | 交付物 | 前置条件 |
|---|---|---|
| 3A 主题 token | 颜色/数值 + 字体/Sprite/渐变 | 先定 token 命名与迁移规则 |
| 3B ListView | 基于 `LoopScrollRect` 的行池/列布局 | 不照搬 Evo ListView |
| 3C 简单图表 | 折线/柱/饼最小集 | 有真实界面需求再做 |
| 3D Style Page | 单 Canvas 单材质合批 | **先测量 draw call，暂不排期** |

---

## 三、本迭代验收标准

1. 新增组件 `< 600 行`，架构守卫通过。
2. `GearInteractive` 五态：Disabled / Normal / Highlighted / Pressed / Selected，事件去重。
3. `GearProgressBar` 支持 `0..1`、平滑时间、`SetValueImmediate`、格式化标签。
4. `GearCounter` 支持目标值、时长、格式化、立即赋值。
5. UI Example 只增量追加，旧 92 张卡 Preview 参数 0 差异。
6. 全量 EditMode、`DocsCheck` 通过。
7. CHANGELOG / 路线图同步，不留下“文档说做了、代码没做”的项。

---

## 五、执行记录

| 日期 | 阶段 | 结果 |
|---|---|---|
| 2026-11-14 | 1A 五态交互 | ✅ `GearInteractive` + `GearInteractiveTint`；3 个 EditMode 用例 |
| 2026-11-14 | 1B/1C 数据组件 | ✅ `GearProgressBar` / `GearCounter`；2 个 EditMode 用例 |
| 2026-11-14 | 1D 展厅同步 | ✅ 新增「数据 · Progress / Counter」卡；旧 92 卡保留 |
| 2026-11-14 | 1E 文档 | ✅ CHANGELOG / 路线图 / 文档索引同步 |
| 2026-11-14 | 2A Tooltip | ✅ 延迟 + `Prewarm` 复用池 + 可配置最大宽 + `ShowCustom` 富内容 |
| 2026-11-14 | 2B ContextMenu | ✅ 子菜单 + 级联关闭 + 自定义行 |
| 2026-11-14 | 2C Popover | ✅ alpha/scale 开关动画；PlayMode 冒烟通过 |
| 2026-11-14 | 3A 主题 token | ✅ 字体/Sprite/渐变 token + `GearThemeBinder`；2 个 EditMode 用例 |
| 2026-11-14 | 3B ListView | ✅ 数据层 + 列宽 + CSV + `LoopScrollRect` 演示卡 |
| 2026-11-14 | 3C 图表（折线/柱状） | ✅ 自绘 `GearLineChart` / `GearBarChart` + 演示卡 |
| 2026-11-14 | 3C 图表（饼/图例） | ✅ `GearPieChart` hover + 点击 + `GearChartLegend` + 演示卡；PlayMode 冒烟通过 |
| 2026-11-14 | 3C 图表联动 | ✅ `GearChartListLink` + 「数据 · Pie → List」联动卡；A=2/all=6 验证 |
| 2026-11-14 | 3C 雷达图 | ✅ `GearRadarChart` + 「数据 · Radar」卡（5 轴） |
| 2026-11-14 | 4A 渲染诊断 | ✅ 诊断工具 + 报告：405 ImageEx / 405 材质实例 / 可合批 16 组 291 实例，预计降至 130 |
| 2026-11-14 | 4B 共享材质试点 | 🟡 试点验证 401→286 后回滚；Shared 触发 rebuild 日志，draw call 未实测 |
| 2026-11-14 | S1 圆角同步 | ✅ `RectangleImg.CornerRadiusSyncMode`（None/MatchParent/MatchTarget/MatchAncestor）+ `SyncInset`；抽屉与 7 个用例 |
| 2026-11-14 | S1 五态状态色 | ✅ `ImageExStateColors`（填充/描边/辉光 + 过渡 + `Bind()`）；5 个用例 |
| 2026-11-14 | S1 指针聚光 | ✅ `RM_Spotlight.cginc` + `ImageExSpotlight`；5 个用例（含材质 uniform 回读） |
| 2026-11-14 | S1 画廊/验收 | ✅ 新增 3 张卡（117→120）；全量 EditMode 290/290 |
| 2026-11-14 | S1 材质池 | ✅ `MaterialMode.Pooled` + `ImageExMaterialPool`（签名复用/自动回退）；实测 Batches 418→171 |
| 2026-11-14 | Pack A 手感 | ✅ `GearAnimatedContainer` / `GearRectSway` / `GearDockMagnifier`；3 张卡（120→123） |
| 2026-11-14 | Pack A 小件 | ✅ `GearSpring` / `GearRadialLayout` / `GearSpinner`；2 张卡（123→125） |
| 2026-11-14 | Pack A 收尾 | ✅ `GearRectParallax` / `GearDistributionBar` / `GearMenuBar`；2 张卡（125→127） |
| 2026-11-14 | Pack B 起步 | ✅ `GearDotMatrix` + 警戒斜纹（Pattern 覆盖）；2 张卡（127→129） |
| 2026-11-14 | Pack B 幕帘 | ✅ `GearPixelCurtain`；1 张卡（129→130） |
| 2026-11-14 | Pack B 等值线 | ✅ `GearTopographic`；1 张卡（130→131） |
| 2026-11-14 | 文本动效补全 | ✅ `GearTextEffects` 扩到 12 种；1 张卡（131→132） |
| 2026-11-14 | Sprite Fill | ✅ `SpriteFillMode`（Stretch/Cover/Fit）；1 张卡（132→133） |
| 2026-11-14 | Pack B 收尾 | ✅ `GearInkPanel` 墨团揭示；1 张卡（133→134） |
| 2026-11-14 | 材质池工具 | ✅ 选中切 Pooled / 还原 Dynamic 菜单；跳过逐帧改 uniform 的实例 |
| 2026-11-14 | Pack C 起步 | ✅ `GearDropdown`（Popover 选项列表）；1 张卡（134→135） |
| 2026-11-14 | Pack C 设置控件 | ✅ `GearSwitch` / `GearRangeSlider`；2 张卡（135→137） |
| 2026-11-14 | Pack C 选择控件 | ✅ `GearRadioGroup` / `GearQuantitySelector`；2 张卡（137→139） |
| 2026-11-14 | Pack C 滚轮 | ✅ `GearPickerWheel` / `GearTimePicker`；2 张卡（139→141）；EditMode 349/349 |
| 2026-11-14 | Pack C 计时器 | ✅ `GearTimer`；1 张卡（141→142）；EditMode 356/356 |
| 2026-11-14 | Pack C 出屏指示 | ✅ `GearOffScreenIndicator`；1 张卡（142→143）；EditMode 361/361 |
| 2026-11-14 | Pack C 输入框 | ✅ `GearInputFieldEnhancer`；1 张卡（143→144）；EditMode 370/370 |
| 2026-11-14 | Pack C 多选 | ✅ `GearMultiSelect`；1 张卡（144→145）；EditMode 377/377 |
| 2026-11-14 | Pack C 日历 | ✅ `GearCalendar`；1 张卡（145→146）；EditMode 384/384 |
| 2026-11-14 | Pack C 取色 | ✅ `GearColorPicker`；1 张卡（146→147）；EditMode 391/391 |
| 2026-11-14 | Pack C 搜索 | ✅ `GearSearchIndex`；1 张卡（147→148）；EditMode 398/398 |
| 2026-11-14 | Pack C 收尾 | ✅ C1–C8 全部完成 |
| 2026-11-14 | P1 文本标签作用域 | ✅ `GearTextTagScope`；1 张卡（148→149）；EditMode 405/405 |
| 下一轮 | 3C 收尾 | ⏳ 雷达图/点击钻取/真实数据联动 |

## 六、明确不做

- 不复制 Evo 的 ProceduralShape / SoftMask / TextEffects 源码；
- 不引入 Evo Tween / Evo Styler / Evo 输入模块；
- 不为了“像 Evo”重写 ImageEx；
- 不在没有 draw call 数据时做 Style Page；
- 不把 15 个 EvoDemo 分类页全搬进 UI Example。
