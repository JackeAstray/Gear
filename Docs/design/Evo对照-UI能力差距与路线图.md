# Evo 对照：UI 能力差距与推进路线

> 目的：前三篇 Evo 文档记的是"本轮做了什么"（升级方案 → 清理与补齐 → 阴影与覆盖层），
> 本文记**"还差什么、先做哪一块、怎么验收"**，把差距固化成可勾选清单，避免每轮重新推导。
>
> 基线：Gear 2.0.0 + 当前 Unreleased；参照物为工程内 `Assets/Evo`（Evo UI 2.0.6 + Evo Tween 2.0.1，
> 12 类 97 个预制体、139 个运行时脚本、8 篇 Skills）。Evo 只作能力参照，**不接入**
> `UISystem` / `UIInputSystem`，不复制源码与资产。
>
> 一句话结论：**图形/效果层已基本追平（滤镜类 Gear 更多），差距集中在软遮罩、形状工艺、
> 页面结构与控件、主题覆盖面、以及"单 Canvas 单材质"的渲染架构。**

---

## 一、现状校准

### 1.1 已经补齐、不要再排期

| 能力 | 落点 | 证据 |
|---|---|---|
| 外投影方向语义（Offset/Spread/Softness/OutsideOnly） | `ImageEx.AdvancedEffects.cs`、`RM_Shadow.cginc` | CHANGELOG Unreleased / Fixed |
| 内阴影 Offset + `smoothstep` 羽化 | `ImageEx.InnerShadow.cs`、`RM_InnerShadow.cginc` | 同上 |
| 虚线周长连续 + 圆头帽 + 偏移 | `ImageEx.Dash.cs`、`RM_Outline.cginc` | 同上 |
| 渐变屏幕空间/角向/反相/流速 | `Effects/GradientEffect.cs`、`RM_Gradient.cginc` | 同上 |
| 图案填充（点/网格/条纹/棋盘/圆环 + falloff） | `RM_Pattern.cginc` | 同上 |
| 边缘辉光 + 指针跟随 | `RM_BorderGlow.cginc`、`ImageExPointerGlow.cs` | 同上 |
| ImageEx 版背景模糊 | `RM_Backdrop.cginc`、`UIBackdropBlur.cs` | 同上 |
| Toast / Tooltip / ContextMenu | `Runtime/Overlays/*` | 同上 |
| 默认主题资产 + 主题窗口 | `Resources/UITheme/ImageExDefault.asset` | 同上 |
| 预设/批量/诊断/合批/拾取/画廊 | `Editor/Scripts/ImageExToolkitWindow.cs` 等 | 同上 |

### 1.2 前三篇文档里已过时的判断

- 《清理与补齐》把 **BorderGlow / 图案 / 覆盖层** 列为 P1 待办——本轮已完成。
- 《升级方案》把 **单 quad 外阴影** 标为"默认仍用旧 `Outline8`"——现在 setter 与材质写入都统一映射到 `SdfOutline`。
- 仍然成立的两条：**P2 样式页/顶点化合批未做**；**SoftMask 需要"可被遮罩的 UI 着色器"这个前置条件未动**。

### 1.3 Gear 反而领先、不要向 Evo 看齐的部分

| 能力 | Gear | Evo |
|---|---|---|
| 窗口管理 | `UISystem` 窗口/模态栈/导航栈/路由器/结果/超时/层级 | 无（只有单个 ModalWindow） |
| 输入与焦点 | `UIInputSystem` 焦点域、设备档案、改键、本地多人槽位 | 基于 `Selectable` 的导航 + 虚拟光标 |
| UI Toolkit | `UIToolkitSystem` 面板栈 + USS | 明确不支持 |
| 本地化 | `LanguagesSystem`（TMP 自动翻译） | 依赖外部 `EVO_LOCALIZATION` 集成 |
| 循环列表 | `LoopScrollRect` 虚拟化 + 分页 + 下拉刷新 + 选中 | 单行单列虚拟化 |
| 树形列表 | `TreeView` | 无 |
| 图像滤镜/过渡 | 色调/颜色/边缘/像素化/目标/细节/采样 + Fade/Melt/Shiny 过渡 | 较少 |

---

## 二、能力矩阵

状态：✅ 已对齐　🟡 部分对齐　❌ 缺失　— 不适用/无需对齐

### A. 图形与效果（ImageEx）

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| 形状集合 | 矩形/椭圆/多边形/星/心/Chevron | ✅ 12 种（含 Squircle/Heart/BlobbyCross/NTriangle） | — |
| 形状旋转 | `ShapeAngle`（任意角） | 🟡 `ShapeRotation`，默认吸附 90° | 低 |
| 斜切 | `ShapeSkew` | ❌ | 低 |
| 圆角 | 像素/百分比（100=半短边）/独立四角/半径同步 | 🟡 像素 + 独立四角；无百分比、无半径同步 | 中 |
| 形状软边 | `Softness`（羽化几何边） | ❌ | 中 |
| 按形状命中 | `RaycastDetectionMode` Standard/Shape/None | ❌ 矩形命中（可选贴图 alpha 阈值） | **高（成本低）** |
| 图片填充模式 | Stretch/Cover/Fit | 🟡 `preserveAspect` + 拉伸 | 低 |
| 抖动 | `Dither`（消 8bit 色带） | ❌ | 低 |
| 布尔运算 | Union/Subtract/Intersect/Exclude ≤4 | ❌ | 低 |
| 软遮罩 | ≤4 层、可反相、可隐藏遮罩本体 | ❌ | **高** |
| 文本动效 | 12 种 + 标签作用域 | ❌ | **高** |
| 渐变缩放 | `FillGradientZoom` | ❌ | 低 |
| Sprite 图案 | 支持（需同图集） | 🟡 只有程序化图案 | 低 |
| 形状导出 Sprite | 1x–4x、9-slice、可染色、替换为 Image | ❌ | 低 |

### B. 控件与容器

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| Button | 文本/图标/排列/动态缩放/双击/进度 | 🟡 uGUI Button + `ButtonAni`/`Click` 扩展 | 中 |
| Toggle/Switch/Radio 组 | 多形态 + 组 | 🟡 原生 Toggle | 中 |
| Slider | 普通/输入/Range/Radial/对比 | 🟡 原生 Slider | 中 |
| Dropdown | 普通/输入/多选 | 🟡 原生 Dropdown | 中 |
| InputField 增强 | 5 形态 + 提交/聚焦 | 🟡 TMP 原生 | 低 |
| 颜色/日期/时间 | ColorPicker×2、Calendar、TimePicker、Timer | ❌ | 按需 |
| 选择器 | Selector、QuantitySelector、PickerWheel | ❌ | 按需 |
| 进度/加载 | ProgressBar×3、DistributionBar、Spinner×3 | ❌ | 按需 |
| 图表 | 折线/柱/水平/饼/雷达 + 悬停/点击/图例 | ❌ | 按需 |
| 表格 | ListView（列宽/CSV/虚拟行） | ❌（可先用 LoopScrollRect 拼） | 按需 |
| 数字滚动 | Counter | ❌ | 低 |
| 虚拟光标 | VirtualCursor（环形/描边/虚线） | ❌（焦点导航替代） | 按需（主机） |

### C. 覆盖层

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| 模态 | ModalWindow（单例、不排队） | ✅ 模态栈 + 结果 + 优先级（更强） | — |
| Toast | 倒计时条/音效/堆叠协调 | 🟡 堆叠 + 自动关闭；无队列、无时长条 | 中 |
| Tooltip | 延迟/跟随/最大宽/自定义内容/池 | 🟡 触发 + 跟随；无延迟、无池 | 中 |
| ContextMenu | 子菜单/自定义行/禁用项 | 🟡 分隔/图标/危险/禁用；无子菜单 | 中 |
| Popover | 外点/Esc 关闭 + 开关动画 | ❌ | 中 |
| 独立通知 | Notification | 🟡 由 Toast 覆盖 | 低 |
| 屏外指示 | OffScreenIndicator | ❌ | 按需 |

### D. 导航与布局

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| Tab / Page / Stepper | 三件套 + 指示器 + 校验门 | ❌ | **高** |
| Carousel | 自动轮播 + 邻居预览 | ✅ 自研选择控制器（线性/矩阵/多人） | — |
| 列表重排/框选 | ReorderableList、MarqueeSelection | ❌ | 按需 |
| 无限滚动/网格/径向布局 | InfiniteScroll、FlexibleGrid、RadialLayout | ❌ | 按需 |
| 手柄导航 | Selectable + 容器 + 虚拟光标 | ✅ 焦点域 + 导航 + 改键 | — |

### E. 主题

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| 颜色/数值 token | ✅ | ✅ `ImageExThemeAsset` | — |
| 字体/精灵/渐变/音频/形状样式/文本样式 token | ✅ | ❌ | 中 |
| 交互态颜色（5 态） | ✅ | ❌ | 中 |
| 换肤过渡 | 颜色/渐变混合 | ❌ | 低 |
| TMP / Button 绑定 | `IStylerHandler` | 🟡 只绑 ImageEx | 中 |
| ID 迁移工具 | Styler ID Migrator | ❌ | 低 |

### F. 编辑器工具

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| 预设库/批量/诊断/合批 | Catalog + Styler | ✅ ImageEx 工具台 | — |
| 主题浏览器 | Styler Browser | ✅ 主题窗口 | — |
| 场景拾取 | Pick Visible UI | ✅ 拾取光标下的 Graphic | — |
| 目录（预览/搜索/拖拽创建） | UI Catalog | ❌ | 中 |
| 图标库 / 精灵优化 | Icon Browser、Sprite Optimizer | ❌ | 低 |
| 形状导出 Sprite | Export as Sprite | ❌ | 低 |

### G. 渲染架构

| 能力 | Evo | Gear | 优先级 |
|---|---|---|---|
| 同 Canvas 单材质合批 | ✅ | ❌ 每实例 Dynamic 材质；仅编辑器按指纹合批 | 中 |
| light/full 双程序分级 | ✅ | ❌ 单程序 | 中 |
| 样式页（style page） | ✅ | ❌ | 低 |
| Shader 预热 | `WarmUpShaders` | ❌ | 低 |

---

## 三、执行路线

> 执行顺序 ≠ 价值优先级：先做"无 shader 风险 + 高频使用"的，把 shader 大件放在后面单独一轮。

### 阶段 0（0.5 天）固化为清单

- 本文落库，`Docs/README.md` 加索引，CHANGELOG 记一条 Docs。
- 验收：`dotnet run --project Tools/DocsCheck -- .` 退出码 0。

### 阶段 1（2–3 天）纯 C# 结构件：Tab / Stepper / Popover

- 落点：`Assets/Gear/Utilities/UI/Navigation/`
  - `GearTabGroup`：`tabID`、`firstSelected`、方向动画、不可见页禁用
  - `GearStepper`：`SetCanContinue` 校验门、`onStepChanged`/`onCompleted`、可跳过/回退
  - `GearPopover`：外点/Esc 关闭、`onStateChanged`、可托管激活
- 实现模式沿用 `GearToast`（代码构建 + 主题令牌 + 可换 Prefab），不新增资产格式。
- 验收：PlayMode 用例（切换/事件/边界）+ 一个 Example 场景；`unity-verify.ps1 -IncludePlayMode` 全绿。

### 阶段 2（1–2 天）形状快赢

- `RaycastDetectionMode`：ImageEx 重写 `IsRaycastLocationValid`，用同一套 SDF 求距离。
- 圆角百分比：C# 侧按短边换算，shader 不动；默认仍像素模式。
- 渐变 Zoom：`GradientEffect` 加字段 + `RM_Gradient.cginc` 加乘子。
- 验收：画廊加 3 张卡；EditMode 用例校验 0/50/100% 换算；旧资源零影响。

### 阶段 3a（1 天）参数化软遮罩（无 RT）

- Canvas 级全局 uniform：`_GearMaskRect` / `_GearMaskRadius` / `_GearMaskSoftness` / `_GearMaskInvert`。
- ImageEx 片元最后乘 mask alpha；全局 uniform 不破坏合批。
- 限制：**每 Canvas 一个遮罩**；覆盖滚动区渐隐、圆角面板渐隐。
- 验收：Scroll View 上下 24px 渐隐 demo；硬边 `Mask` 行为不变。

### 阶段 3b（2–4 天）任意形状 / 嵌套软遮罩

- `ImageExSdfMask` 把自身 SDF 渲进 8-bit RT，广播 `_GearMaskTex` + 变换矩阵；≤4 层。
- 先只保证 ImageEx 子树；`Image`/TMP 需要各自 shader 变体，单独排期。
- 验收：嵌套 2 层用例；不支持的自定义 shader 明确降级为硬 Mask。

### 阶段 4（按业务触发）控件与图表

- 一次做一套最小集（Dropdown 含多选、RangeSlider、Switch、QuantitySelector、折线/柱/环），
  不做 97 个预制体；有真实界面需求才排。

---

## 四、明确不做

- 不复制 Evo 的预制体、贴图、字体、Skills 文档；不引用 `Evo.UI` / `Evo.Tween`。
- 不引入 Evo Tween（仓库已有 DOTween）。
- 不采用 Evo 的 2560×1440 画布基准（Gear 覆盖层 1920×1080）。
- 不做 uGUI-only 路线：菜单/设置类面板继续走 UITK。
- 不做 Styler 资产格式与音频 token 的完整等价物，只按需补 token 类型。

---

## 五、验收标准（每个阶段共用）

1. `dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear` 退出码 0
2. `dotnet build Unity/Gear/Gear.Utilities.csproj` 0 error
3. `pwsh Tools/unity-verify.ps1 -IncludePlayMode` 全绿（新增用例计入门槛）
4. 画廊/示例场景：新能力有卡片或场景入口，旧资源默认关闭时行为不变
5. CHANGELOG 一条 Added/Fixed + 对应设计文档更新
6. 文档改动跑 `dotnet run --project Tools/DocsCheck -- .`

> 视觉结论（阴影/遮罩边缘/图案密度）没有 MCP 时仍需人工截图确认，不能只凭编译通过。

---

## 六、决策规则：下一轮做哪块

| 最近要做的界面 | 先做 |
|---|---|
| 设置/选型/向导 | 阶段 1（Tab/Stepper/Popover） |
| 列表、弹窗、滚动区的打磨 | 阶段 3a（参数化软遮罩） |
| 透明角误点、胶囊/异形按钮 | 阶段 2（形状命中 + 百分比圆角） |
| 数据展示/战绩/结算 | 阶段 4（图表最小集） |
| 主机/手柄为主的界面 | 阶段 4（虚拟光标评估） |

---

## 七、风险

| 风险 | 影响 | 处理 |
|---|---|---|
| SoftMask 的 shader 变体扩散 | ImageEx + UI Default + TMP 三套，维护成本 | 先只覆盖 ImageEx；其余按实际界面需要再加 |
| 每 Canvas 一遮罩的限制被误用 | 多遮罩场景表现错误 | 组件里显式告警（检测同 Canvas 第二个遮罩） |
| RT 生命周期/重建 | 遮罩模糊、显存泄漏 | 复用现有 `UIBackdropBlur` 的 RT 管理约定 |
| 自研控件与 UITK 双栈重复 | 两套样式/两套交互 | 控件只做 uGUI 运行时；设置类面板继续 UITK |
| 主题 token 扩展改公共 API | 接入方编译中断 | 新 token 类型用新增列表而非改结构体 |
| Evo 资产被误提交 | 许可与体积 | `Assets/Evo` 保持 untracked，发布前检查 |

---

## 八、可勾选清单

### 阶段 1
- [ ] `GearTabGroup` + PlayMode 用例
- [ ] `GearStepper` + PlayMode 用例
- [ ] `GearPopover` + PlayMode 用例
- [ ] Example 场景与说明

### 阶段 2
- [ ] `RaycastDetectionMode`（Standard/Shape/None）
- [ ] 圆角百分比模式 + 换算用例
- [ ] 渐变 Zoom
- [ ] 画廊 3 张新卡

### 阶段 3
- [ ] 3a 参数化矩形/圆角软遮罩
- [ ] 3a Scroll View 渐隐示例
- [ ] 3b `ImageExSdfMask` + RT 广播
- [ ] 3b 嵌套 2 层用例与降级说明

### 阶段 4（按需）
- [ ] 图表最小集（折线/柱/环）
- [ ] Dropdown 多选 / RangeSlider / Switch / QuantitySelector
- [ ] 虚拟光标可行性评估

### 主题与工具（可穿插）
- [ ] 字体/精灵 token
- [ ] 交互态颜色（5 态）
- [ ] TMP / Button 绑定
- [ ] UI Catalog（预览/搜索/拖拽创建）

---

## 附：事实来源

- Evo 能力：`Assets/Evo/Evo UI/Skills/*/SKILL.md`（8 篇）、`Assets/Evo/Evo UI/Prefabs/`、`Scripts/`、`Editor/Scripts/`
- Gear 能力：`Assets/Gear/Utilities/UI/ImageExtensions/**`、`Resources/Shader/Common/RM_*.cginc`、
  `Framework/Runtime/System/{UISystem,UIToolkitSystem,UIInputSystem}`、`Utilities/UI/{LoopScrollRect,TreeView,Ripple}`
- 缺项判定：对 Gear 全仓 `class <能力名>` 与关键字段（`SoftMask`/`TextEffects`/`PageManager`/`TabManager`/
  `Stepper`/`VirtualCursor`/`RaycastDetectionMode` 等）做过 grep，均无命中
- 历史文档：[UI-ImageEx与工具-参考Evo升级方案](UI-ImageEx与工具-参考Evo升级方案.md)、
  [Evo对照-ImageEx清理与UI能力补齐](Evo对照-ImageEx清理与UI能力补齐.md)、
  [Evo对齐-ImageEx阴影与UI覆盖层](Evo对齐-ImageEx阴影与UI覆盖层.md)
