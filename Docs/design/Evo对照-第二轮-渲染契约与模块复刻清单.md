# Evo 对照（第二轮）：渲染契约、UI 系统契约与 EVODemo 模块复刻清单

> 目的：回答三个问题——(1) Evo 的代码与设计对 Gear 的 **ImageEx 效果**还有没有帮助；
> (2) 对 Gear 的 **UI 系统设计**有没有帮助；(3) EVODemo 里那些"很有意思"的模块**能不能复刻**、先复刻哪些。
>
> 基线：Gear 2.0.0 + 当前工作树（2026-11-14）；参照物 `Unity/Gear/Assets/Evo`（Evo UI 2.0.6 / Evo Tween 2.0.1）。
> 方法：读 Evo 运行时源码、22 个 shader/cginc、Demo 预制体清单；读 Gear 的 ImageEx / UISystem / UIInputSystem / Utilities/UI 源码。
> **不运行 Unity、不做视觉验收**；"已完成"以当前工作树代码为准，不以 CHANGELOG 为准。
>
> 一句话结论：**ImageEx 的"效果参数"已经追平，剩下的是"机制"；UI 系统该借的是三个契约，不是组件数量；
> EVODemo 可以复刻，但复刻对象是运行时组件与 shader，不是那 123 个绑定脚本和 180 个预制体。**

---

## 零、结论速览

| 问题 | 结论 |
|---|---|
| Evo 对 ImageEx 还有帮助吗 | **有，但方向变了。** 形状/描边/阴影/渐变/图案/裁剪/命中/圆角百分比/渐变 Zoom/软边/抖动都已落地，滤镜与过渡反而比 Evo 多。继续堆参数收益很低；剩下的是**机制**：多效果单 Graphic 的数据通道、嵌套/任意形状软遮罩、布尔运算、状态色映射。 |
| Evo 对 UI 系统设计有帮助吗 | **有，借三个契约。** Styler（注册表 + token ID + 广播 + 过渡）、Interactive（五态总线 + 效果订阅）、Overlay/导航（协调器 + 焦点/返回仲裁）。`UISystem` / `UIRouter` / `UIModalStack` / `UIInputSystem` / `LanguagesSystem` 的骨架不要动。 |
| EVODemo 能复刻吗 | **能，且没有必须重画的专有贴图**（Demo 目录只有 3 张 PNG、零字体、零材质）。但 123 个 Demo 脚本只是绑定层，技术都在 13 个运行时效果 + 15 类控件/布局 + 22 个 shader 里。建议分 3 个复刻包，不做 15 页全铺。 |

---

## 一、校准：先修正三条旧结论

### 1.1 已经在工作树里落地（不要再排期）

| 能力 | 落点（证据） | 测试 |
|---|---|---|
| 五态交互模型 | `Utilities/UI/Interaction/GearInteractive.cs:35-66`、`GearInteractiveTint.cs` | `Tests/GearInteractiveTests.cs` |
| Tab / Stepper / PageView | `Utilities/UI/Navigation/GearTabGroup.cs:19-121`、`GearStepper.cs:18-160`、`GearPageView.cs` | `Tests/GearTabGroupTests.cs` 等 |
| Popover / Toast 队列 / Tooltip 池 / ContextMenu 子菜单 | `ImageExtensions/Runtime/Overlays/*` | `Tests/GearPopoverTests.cs` |
| 软遮罩 + 滚动渐隐 | `Effects/GearSoftMask.cs:15-148`、`GearScrollFade.cs` | 画廊卡 |
| 文本动效 5 种 | `TextEffects/GearTextEffects.cs:68-89`（打字机/乱序/波浪/抖动/闪光） | `Tests/GearTextEffectsTests.cs` |
| 图表 4 种 + 图例 + 联动 | `Utilities/UI/Charts/*` | `Tests/GearChartTests.cs`、`GearChartListLinkTests.cs` |
| ListView / Progress / Counter | `Utilities/UI/ListView/*`、`Widgets/*` | `Tests/GearListViewTests.cs` |
| 主题 token 扩展到字体/Sprite/渐变 | `Runtime/Theme/ImageExThemeAsset.cs`、`GearThemeBinder.cs:18-66` | EditMode 用例 |
| ImageEx 形状命中 / 圆角百分比 / 渐变 Zoom / 软边 / 抖动 | `ImageEx.Raycast.cs`、`ImageExEnums.cs:55-79`、`ImageEx.DrawingSettings.cs` | `Tests/ImageExShapeHitTests.cs` |
| 图案 / 边缘辉光 / ImageEx 版背景模糊 | `ImageEx.AdvancedEffects.cs`、`RM_Pattern.cginc`、`RM_BorderGlow.cginc`、`RM_Backdrop.cginc` | 画廊卡 |
| 方向性外投影 / 内阴影 Offset / 圆头虚线 / 屏幕空间渐变 | `RM_Shadow.cginc`、`RM_InnerShadow.cginc`、`RM_Outline.cginc`、`RM_Gradient.cginc` | 画廊卡 |

> 这张表的作用：**旧文档里的 P1/P2 大部分已经做完了**，再按旧清单排期会重复劳动。

### 1.2 三处需要修正的判断

**① 布尔运算不是"扩顶点"，而是 style page 的附属能力。**

`Scripts/Graphics/ProceduralShapeOperations.cs:11-28` 最多 4 个操作数，每个操作数引用**同一 Canvas 上的另一个
ProceduralShape**；它的变换被压成两行仿射（`:166-176`）、写进共享样式页的数据行，渲染时 shader 采样，
命中检测时 C# 用同一份数据复算（`:228-255`）。所以：

- 抄一个 `Union/Subtract` API 没有意义——它要么依赖样式页（数据纹理），要么退化成"单操作数 + uniform"；
- 旧文档把它标成"扩顶点流、中–高成本"，**成本估错了方向**；真正的难点是数据通道与命中镜像。

**② 顶点流并不缺通道，uv3 是空的。**

`Common/ImageHelper.Internal.cs:69,92` 传给 `VertexHelper.AddVert` 的 uv3 是 `Vector2.zero`；
shader 的 `appdata_t` 只读 `TEXCOORD0/1/2 + TANGENT`（`Resources/Shader/UI/ImageEx/ImageEx.shader:305-315`）。
如果将来做 style-page 合批，`styleIndex` **可以直接塞 uv3**（再打开 Canvas 的 TexCoord3 通道），不必改网格布局。
瓶颈在材质生命周期、数据纹理上传与主题联动，不在顶点。

**③ "复刻 EVODemo" 的复刻对象不是 Demo 脚本。**

`Demo/Scripts/**` 有 123 个文件、约 1.7 万行，绝大多数是"滑杆/单选 → 字段 + Replay"的绑定层
（例外是 `DemoHome.cs`，1156 行）；真正的技术在 `Scripts/Effects`、`Scripts/Graphics`、
`Scripts/{Layout,UI Elements,Navigation}` 与 `Shaders`。这也解释了为什么 Evo 的"好看"搬不过来：
**外观在 83 个 Demo 预制体与 Sprite 里，代码只是 30–100 行胶水。**

### 1.3 必须先处理的事实：Evo 已经进了公开仓库

- `git ls-files "Unity/Gear/Assets/Evo/**"` → **2699 个文件被跟踪**；引入提交 `211553c2`（"临时上传"）
  已是 `origin/main` 的祖先；远程是 `github.com/JackeAstray/Gear`（公开仓库，Apache-2.0）。
- 但既有文档仍写"`Assets/Evo` 保持 untracked"（三处），**与事实不符**，本轮已同步修正。
- Evo 是 Asset Store 商业资产（`Assets/Evo/Evo UI/Skills/*/SKILL.md` 内含 asset-store 链接），
  Asset Store 标准 EULA 通常不允许把源码/贴图/预制体再分发（以具体许可条款为准）。
  **把它留在公开仓库里，风险高于任何"复刻"收益。**

建议动作（与本文档解耦，但应尽快）：

1. `git rm -r --cached "Unity/Gear/Assets/Evo"`，并在 `Unity/Gear/.gitignore` 加 `Assets/Evo/`（本地文件保留，供参照）；
2. 历史已推送，若要彻底消除需 `git filter-repo` 重写历史并强推——影响 LFS、tag 与所有 fork，单独评估；
3. UPM 分支已确认**不含** Evo（`git ls-tree -r upm | grep -ci evo` = 0），发布包目前是干净的；
4. 清理完成前，新文档/提交不要再用 `Assets/Evo` 路径当"项目内资产"引用，只作本地参照。

---

## 二、ImageEx：从"加效果"转向"补机制"

### 2.1 参数面已经对等甚至领先

| 维度 | Evo ProceduralShape | Gear ImageEx |
|---|---|---|
| 形状 | 矩形/椭圆/多边形/星/心/Chevron | 12 种（含 Squircle/Heart/BlobbyCross/NTriangle） |
| 描边 | 实线/虚线/圆头/渐变 | 实线/虚线/圆头/渐变/周长参数化 |
| 阴影 | 内/外，Offset/Size/Softness | 内/外，Offset/Spread/Softness/OutsideOnly |
| 渐变 | Linear/Radial/Angular、Local/Screen、Zoom | 同左 + Corner + Reverse + Speed |
| 图案 | 形状/Sprite 图案 + falloff | 程序化图案（点/网格/条纹/棋盘/圆环）+ falloff + 流速 |
| 裁剪 | 水平/垂直/径向 | 同左（含 65% 扇形卡） |
| 命中 | Standard/Shape/None | 同左（SDF 形状命中） |
| 软边/抖动 | Softness / Dither | 同左 |
| 滤镜/过渡 | 少 | **Gear 更多**（色调/颜色/边缘/像素化/目标/细节/采样 + Fade/Melt/Shiny） |

> 结论：**"Evo 的效果比 Gear 多"这个前提已经不成立。** 现在比的是机制。

### 2.2 剩余能力分三档

状态：✅ 已有　🟡 部分　❌ 缺失

| 能力 | Evo 机制 | Gear | 成本 | 结论 |
|---|---|---|---|---|
| 圆角同步 `MatchParent/Target/Ancestor` | `RadiusSyncMode` | ❌ | 低（纯 C#） | **A 档，先做** |
| Sprite 填充 `Stretch/Cover/Fit` | `SpriteFillMode` | 🟡 `preserveAspect` | 低（UV 计算） | **A 档** |
| 任意角旋转 | `ShapeAngle` | 🟡 有 `ShapeRotation` + `ConstrainRotation`，默认约束 90° | 低 | **A 档**：不是缺功能，是 Inspector 没讲清楚 |
| Spotlight 径向跟随光 | 效果层 + 指针数据 | ❌（有 BorderGlow/PointerGlow） | 低–中（shader 加一项） | **A 档** |
| Pattern Sprite（同图集图案） | 支持 | ❌（只有程序化） | 中（多一张图集采样） | B 档，按需求 |
| 多效果单 Graphic 数据通道 | `InteractiveEffectRenderer` 图集行 | ❌（效果各自写材质） | 中（见 2.3） | **B 档重点** |
| 文本动效 12 种 + 标签作用域 | `TextEffects` 预处理器 | 🟡 5 种、无标签 | 中 | **B 档重点** |
| 软遮罩嵌套/任意形状 | 4 层 + Sprite/程序化 | 🟡 单层矩形/圆角（最近父级） | 中–高 | B 档，先做 RT 试点 |
| 布尔运算 | ≤4 操作数，依赖 style page | ❌ | 高（全量）/中（单操作数） | B 档：只做单操作数 |
| 形状导出 Sprite | 1x–4x、9-slice | ❌ | 中（编辑器烘焙） | 按美术需求 |
| Style Page 合批 | 单 Canvas 单材质 + 数据纹理 | ❌ | 高（1–2 周） | **C 档，先测量** |
| lite/full 双程序 | 关键字切换 | ❌（单程序运行时分支） | 高 | C 档，随合批 |
| 多层同形状 / Twist | 5 层 + 逐层变换 | ❌ | 中 | **不做**，用子节点拼 |
| CurvedCanvas / InkPanel 级 RT | 专用 shader | ❌ | 高 | 不做 |

### 2.3 最被低估的一条：多效果单 Graphic

Evo 的 `InteractiveEffectRenderer.cs:13` 是 `BaseMeshEffect + IMaterialModifier`，它把每个 Graphic 的
ripple（最多 8 个并发）/ pointer / border 参数打包成**共享 RGBAFloat 图集里的一行**（`:16-19`），
行可回收（`:660-700`）。好处是：光晕、涟漪、指针高光**不用各自改材质**，一个 Graphic 叠多个指针效果仍能合批。

Gear 的现状是反的：BorderGlow 是 ImageEx 的字段、`ImageExPointerGlow` 独立写 uniform、
`UIRipple` 是另一套（Mask + Image 缩放），三种效果三种驱动。建议二选一：

- **轻方案**：把指针类效果统一成"ImageEx 的一个 uniform 块"（`_FxPointer` / `_FxRipple0..3`），
  组件只写这一块，不再各写各的材质字段；
- **重方案**：照 Evo 的图集行做 `GearEffectAtlas`，为将来"一个按钮叠 ripple + glow + spotlight"留扩展位。

轻方案能在 3–5 天内拿到 80% 的收益，建议先做。

### 2.4 合批：比 style page 更划算的一步

现状（`Docs/design/Gear-UI-渲染诊断.md`）：405 个 ImageEx / 405 个材质实例；按设置指纹分组可降到约 130。
Shared 试点把 401 降到 286 后回滚，原因是 rebuild 日志。根因不是"共享材质"本身，而是
`ImageEx.Runtime.Rendering.cs:145-161` 在 `GetModifiedMaterial` 里**每次重建都写约 100 个 uniform**，
共享材质必然被多实例互相覆盖（`:120-137` 的串扰告警就是证据）。

建议路线（不动 shader，风险比 style page 小一个数量级）：

1. `ImageExMaterialPool`：按 `(shader, 设置签名)` 取材质实例；同一签名的 ImageEx 共享一个材质；
2. **池化材质不写 uniform**：`GetModifiedMaterial` 对共享材质直接返回，不逐字段 `Set*`；
3. **写时复制（COW）**：任何属性 setter 先判断"当前材质是否被共享"，是则克隆成私有材质再写；
4. 去掉 `Shared` 模式的 `InitValuesFromSharedMaterial` 回读（`ImageEx.Runtime.SharedMaterial.cs`），
   回读只会把组件字段改成材质默认值。

若之后 draw call 仍是瓶颈，再考虑 style page：uv3 可放 `styleIndex`（见 1.2），
但还需要 `Canvas.willRenderCanvases` 上传、`RGBAFloat` 能力检查、light/full 关键字，
可参考 `ProceduralShapeMaterial.cs:624-935`。**在那之前，不要动 ImageEx 的渲染核心。**

### 2.5 比"更多滤镜"更有用的：状态色

Evo 的 ProceduralShape 支持 per-state 颜色（`ProceduralShape.cs:1362-1459`），StylerObject 也有
五态颜色/字体映射（`StylerObject.cs:40-59`）。Gear 的 ImageEx 目前是"一个颜色 + 一堆效果"，
没有 hover / press / selected 语义；`GearInteractive` 有状态却没有消费者（全仓只有
`GearInteractiveTint` 引用它）。

建议新增 `ImageExStateColors`（五态：Normal/Highlighted/Pressed/Selected/Disabled 的颜色、描边色、
辉光强度），订阅 `GearInteractive.StateChanged`。这会让所有卡片/按钮立刻"像设计系统"，
比再加 5 个滤镜更值，而且完全不碰 shader——只改 uniform。

---

## 三、UI 系统设计：借三个契约

### 3.0 先讲清楚 Gear 的骨架

| 层 | Gear 的做法（证据） |
|---|---|
| 窗口 | `UISystem` 四个层根 Main/Normal/HeadInfo/Tips + 对象池 + 加载单飞 + 超时 + 结果（`UISystem.cs:83-87,189-199`） |
| 导航 | `UIRouter` + `UINavigationStack` + `UIModalStack`，窗口以 `UIController` 生命周期管理 |
| 输入 | `UIInputSystem` 焦点域、设备档案、改键、本地多人槽位；`UIFocusScope` / `PushFocus` / `PopFocus` |
| 文本 | `LanguagesSystem` 接管 TMP 翻译；`UIToolkitSystem` 面板栈 + USS |
| 组件 | `Utilities/UI`：ImageEx、LoopScrollRect、TreeView、图表、覆盖层、导航三件套 |

> 这张骨架就是 Gear 的资产：Evo 没有窗口系统、没有路由/模态栈、没有焦点域、没有本地化，
> 所以**任何"把 Evo 接进来"的方案都会得到两套输入、两套主题、两套动画**。下文只讨论"借契约"。

### 3.1 契约一：Styler = 注册表 + token ID + 广播 + 过渡

Evo 的做法（`Styler/Styler.cs:715-758`、`StylerObject.cs:28-59`）：

- `IStylerHandler` 自带 `Preset` 与 `UpdateStyler()`，组件在启用时注册；
- `Styler.ApplyPreset(preset, persistent, transitionDuration)` 遍历注册表广播，**带过渡时长**；
- 组件只持有 token ID（`colorID/fontID/textStyleID/gradientID/spriteID`），换 Preset 即换肤；
- 每个对象可 `useCustomColor` 局部覆盖；五态颜色/字体是同一套映射。

Gear 的现状（`ImageExThemeService.cs:13-47`、`GearThemeBinder.cs:18-66`）是**拉模式**：
有 `Current`/`ThemeChanged`，但 Binder 要手动 `Apply(theme)`，没有注册表、没有过渡、没有状态映射。

建议（不复制 Evo 的资产格式）：

```csharp
public interface IGearThemeTarget
{
    void ApplyTheme(GearThemeAsset theme, float transitionSeconds);
}

public static class GearThemeRegistry
{
    public static void Register(IGearThemeTarget target);
    public static void Unregister(IGearThemeTarget target);
    public static void Apply(GearThemeAsset theme, float transitionSeconds = 0f);
}
```

- token 类型先保持现有 5 类（颜色/数值/字体/Sprite/渐变），音频/形状样式/文本样式按需再加；
- 过渡用 DOTween（unscaled），同一 target/channel 只保留一个活动过渡
  （Evo 的 `ColorTransition.cs:9-17` 就是这么约束的，避免过渡叠加）；
- `GearThemeBinder` 变成 `IGearThemeTarget` 的实现之一，旧 API 保留。

### 3.2 契约二：Interactive = 状态总线 + 效果订阅

Evo：`InteractionState` 五态 + `OnStateChanged`，BorderGlow/Spotlight/Ripple 都是订阅者。

Gear：`GearInteractive.cs:35-66` 已经有状态机与 `StateChanged`，但**全仓只有 `GearInteractiveTint` 订阅**
（`grep GearInteractive` 仅命中 3 个文件）。BorderGlow、Ripple、PointerGlow、ButtonAni 仍各自为政，
其中 `ButtonAni` 还有自己的 `ButtonAniState` 枚举，与 `GearInteractionState` 语义重复。

建议：

- 定义 `IGearInteractionEffect { void OnStateChanged(GearInteractionState state); }`；
- `ImageExPointerGlow` / `Ripple` / `BorderGlow` 接进来；`ButtonAni` 用 adapter 桥接（不合并枚举，
  避免破坏 2.x 公开 API）；
- 状态 → 效果的**优先级与去重**放在 `GearInteractive` 里，效果组件不再自己判 hover。

### 3.3 契约三：Overlay / 导航 = 协调器 + 焦点/返回仲裁

Evo 的覆盖层细节值得借：Toast 队列/时长条/音效、Tooltip 延迟/池、Popover 外点/Esc、
Navigation 组件自带焦点迁移。Gear 已经有队列/时长条/池/子菜单/Popover 动画，缺三件事：

1. **音效**：Toast/按钮状态切换没有音效 token（Evo 有 AudioItem token）；
2. **唯一 owner**：同一时刻可能开出多个 Popover/Tooltip，没有"新开旧的关"的协调；
3. **返回仲裁**：`UIRouter.Back()` 与页内返回、Popover 的 Esc、ContextMenu 的外点关闭，
   没有约定"谁先消费 Esc"。

建议 `GearOverlayCoordinator`：维护 owner 栈、统一 Esc 消费顺序、Toast 音效、优先级；
导航组件统一暴露 `FocusTarget`，由 `UIInputSystem` 驱动（Gear 已有 `PushFocus`/`PopFocus`/
`UIFocusScope`/`RegisterFirstSelected`，**不要学 Evo 让组件自己抢 EventSystem 选中**）。

### 3.4 明确不借

| Evo 的做法 | 为什么不借 |
|---|---|
| 单 `ModalWindow`、无窗口系统 | Gear 的 `UISystem` + `UIRouter` + `UIModalStack` 是更强骨架 |
| `Selectable` + `VirtualCursor` 自管选中 | Gear 有焦点域/设备档案/改键；主机需求单独评估 |
| UniText 文本后端 | Gear 用 TMP + `LanguagesSystem` |
| 2560×1440 画布基准 | Gear 覆盖层按 1920×1080 |
| Evo Tween | 仓库已有 DOTween；Evo Tween 运行时还不在包内，Demo 本来就不可直接搬 |
| 5 层 ColorMode（fill/outline/inner/outer/pattern） | 只需五态状态色，不需要 5 条独立渲染层 |
| Style Page 全量 / lite-full | 先做 2.4 的材质池；没有 draw call 数据前不排 |

---

## 四、EVODemo：能复刻什么、怎么复刻

### 4.1 真实构成

| 项 | 数量 | 说明 |
|---|---|---|
| 运行时脚本 | 139 | `Evo UI/Scripts/**` |
| Demo 脚本 | 123（约 1.7 万行） | 多为 30–296 行的绑定层；`DemoHome.cs` 1156 行是例外 |
| 预制体 | 180（97 运行时 + 83 Demo） | Demo 卡片的"外观"主要在这里 |
| shader / cginc | 22 | 效果全是程序化/材质驱动 |
| 专有贴图 | 3 张 PNG | Edge Fade、Evo UI Logo、Michsky Logo；**零字体、零材质** |
| 整屏 Showcase | 6 | Audio Mixer / Card Game / Fishing / Mission / Party / Ship Console |
| 分类 | 15 | 见 4.2–4.4 |

两条关键事实：

- **Evo Tween 运行时不在包内**，Demo 与核心都用 `TweenHandle`/`TweenSequence`/`TweenAnimator`
  （例如 `DemoMarquee.cs:160`、`DemoModalWindow.cs:28-31`），只有 `Scripts/Integrations/Evo Tween/` 适配层。
  → Demo 代码无法直接搬；Gear 用 DOTween 替换，成本是"换语法"不是"造轮子"。
- **Styler 耦合很低**：只有 VirtualCursor / Notification / TimePicker / Toggle 等少数组件用 `stylerID`；
  大部分"Evo 风"来自预制体与 Sprite，不是 token。→ **行为可重写，版式必须重做。**

### 4.2 Effects 类（13 个）

| 模块 | 真实技术（证据） | Gear 对应 | 成本 | 结论 |
|---|---|---|---|---|
| AnimatedContainer | 一条 0..total 的线性 tween，子项按 `i*delay` 错峰；曲线缩放、之后淡入；`CanvasGroup` 自动补 | 无 | 低 | **做**（列表/卡片出现动画） |
| RectSway / RectParallax / Magnetic | 指针投影/阻尼/弹簧；`DemoMagnetic` 只是设置 RectSway 字段 | 无 | 低 | **做**（手感包） |
| DockMagnifier | 高斯权重 + `SmoothDamp` + 改 RectTransform 宽高（邻居跟随，不是 scale） | 无 | 低 | **做** |
| BorderGlow | 边缘光 + 指针跟随，写入效果图集 | `ImageEx.BorderGlow` + `ImageExPointerGlow` | — | **已有，验证差距即可** |
| Spotlight | 径向软光按 Graphic 覆盖裁剪，跟随指针 | 无 | 低–中 | **做**（shader 加一项） |
| Ripple | shader 内 8 个并发波，按最远角做覆盖率 | `UIRipple`（Mask+Image 缩放，单波） | 中 | 有需要再做 |
| DotMatrix | 8 种解析图案（Wave/Rings/Radar/…）+ 3 种点形，网格 ≤64×64 | 无 | 中 | 做（单 shader 包） |
| WarningStripe | 5 个 float 的斜纹滚动 | `ImageEx.Pattern` 可能直接覆盖 | 低 | 先验证，别新写 |
| TopographicSkin | 噪声等值线 + 主线间隔 + 椭圆 falloff | 无 | 中 | 做（单 shader 包） |
| PixelCurtain | `IMeshModifier` 把 0..1 写进 uv1；格子数按 `rect/cellSize` 取整；逐格 hash 揭示 | 无 | 中 | 做（转场） |
| InkPanel | SDF 墨团 + GPU 噪声 + 为 SoftMask 烘焙 ARGB32 RT | 无 | 高 | **最后做** |
| SoftMask | ≤4 层，遮罩数据以纹理+矩阵广播，关键词分支，TMP 变体 | `GearSoftMask`（单层矩形/圆角） | 高 | 见 2.2 B 档 |
| TextEffects | 12 种效果 + `ITextPreprocessor` 标签解析 + 逐字符网格快照 | `GearTextEffects` 5 种、无标签 | 中 | **B 档重点** |

> 注意：Demo 里的 `DemoSoftMask`/`DemoSpotlight` 等只是把滑杆绑到运行时组件，**技术不在 Demo 脚本里**。

### 4.3 布局与导航（10 个）

| 模块 | 技术点 | Gear 现状 | 成本 | 结论 |
|---|---|---|---|---|
| Carousel | 从预设生成项+指示器、自动轮播、指示器收缩 | `GearPageView` 有拖拽/指示器/焦点迁移，缺自动轮播与指示器收缩 | 低–中 | 做（`GearCarousel`） |
| FlexibleGrid | 真正的 `LayoutGroup`：AutoFit/Columns/Masonry/纵横比 | 无 | 中 | 做（响应式网格） |
| Marquee | 框选 + 分组拖拽 + 转移规则 + 约束 | 无 | 高 | 按需（编辑器/背包） |
| MenuBar | 生成按钮 + 每项一个 ContextMenu 实例 | `GearContextMenu` 只有静态 `Show` | 低 | 做（实例化菜单栏） |
| PageManager | 阈值 + 弹性阻尼 + 页间距 + 指示器 | `GearPageView` 已有拖拽/指示器 | 低 | 扩展，不新建 |
| RadialLayout | 子项中心排布在拟合圆上 | 无 | 低 | 做 |
| ReorderableList | 占位 + 间隙动画 + 跨列表转移 + 拖拽缩放 | `LoopScrollRect` 无重排 | 高 | 按需 |
| Stepper / TabManager | 内容 CanvasGroup + 指示器动画 + 方向性进出 | `GearStepper`/`GearTabGroup` 基础版 | 低 | 扩展动画，不新建 |
| VirtualCursor | 合成 `PointerEventData` 走射线、自由/跟随选中 | `UIInputSystem` 只有焦点导航 | 高 | 主机主线时单独评估 |
| ScrollRect snap / drag helpers | 聚焦吸附带 / 拖拽转发 | 无 | 低–中 | 按需 |

### 4.4 覆盖层、选择、滑杆、开关、输入、颜色、日期、进度、数据、Shared

| 分类 | 模块 | 技术点 | Gear 现状 | 成本 | 结论 |
|---|---|---|---|---|---|
| Overlays | ModalWindow | CanvasGroup + 开关 tween + 恢复选中 | `UIRouter`/`UIModalStack` 更强 | 低 | 只做 `GearConfirmDialog` |
| | OffScreenIndicator | 视口坐标钳制 + 边缘箭头 + 距离 | 无 | 中 | 做（纯数学） |
| | Popover 翻转/贴边 | pivot 放置 + 翻边 | `GearPopover` 有外点/Esc/贴边 | 低 | 补翻转 |
| Selection | Dropdown / MultiSelect | 覆层面板 + 生成项 + 过滤 + 多选 | 原生 Dropdown | 中–高 | **功能空洞最大，优先** |
| | Selector / QuantitySelector | 单项切换 + 指示器 / min-max-step | 无 | 低 | 做 |
| Sliders | RangeSlider | 双把手 + 互推 | 原生 Slider | 中 | 做 |
| | RadialSlider / ComparisonSlider | 角度拖拽 / 分割线裁剪 | 无（ImageEx 有 Clip） | 中 | 按需 |
| Toggles | Switch / RadioGroup | 把手滑动 / 组注册 + 动画指示 | 原生 Toggle/ToggleGroup | 低 | 做 |
| Text Input | InputFieldEnhancer | 占位移动/提交/清除按钮 | TMP 原生 | 低–中 | 做 |
| Color | ColorPicker / Palette | 色轮+方块 / 自定义 mesh 调色板 | 无 | 中 | 按需 |
| Date | Calendar / TimePicker | 生成日历网格 / 滚轮（惯性+吸附+循环） | 无 | 高 | 月视图先做；滚轮按需 |
| | Timer | 倒计时 + 进度裁剪 + 格式/循环/暂停 | `GearProgressBar` 只有值 | 中 | 做 |
| Progress | DistributionBar / Spinner | 分段宽度 / 旋转 | 无 | 低 | 做 |
| Data | Line/Pie/Radar | Mesh 绘制 + 轴 + hover hit-area | Gear 已有 4 种 | 低 | 补轴标签/hover |
| | Counter 数字滚动 | 里程表式逐位滚动 | `GearCounter` 值/格式已覆盖 90% | 低/高 | 不做逐位 |
| | ListView | 虚拟行 + 列 + CSV | `GearListView` 已对齐 | 低 | 补表头对齐 |
| Shared | ThemeSelector | `ApplyPreset(preset, duration)` | 缺广播+过渡 | 低 | 随 3.1 做 |
| | SearchIndex | 无分配分词 + 字段加权 + 命中位掩码 | 无 | 中 | 做（命令面板） |
| | Spring | 固定 4ms 切片的阻尼弹簧 | 无 | 低 | 做（工具件） |
| Home | DemoHome 卡组 | 弹簧 + 甩出 + 强调色传播 | 无 | 高 | **不做** |

### 4.5 三个复刻包（建议顺序）

**Pack A｜手感包（纯 C#，约 4–6 天）**

`GearAnimatedContainer`、`GearRectSway`/`GearRectParallax`、`GearDockMagnifier`、`GearMagnetic`、
`GearMenuBar`、`GearRadialLayout`、`GearSpring`、`GearDistributionBar`、`GearSpinner`。
产出：Effects/布局各 2–3 张演示卡。全部不碰 shader，风险最低，观感提升最直接。

**Pack B｜单 shader 包（约 7–10 天）**

`GearWarningStripe`（先验证能否用 Pattern 参数覆盖，能则零代码）、`GearDotMatrix`、`GearTopographicSkin`、
`GearSpotlight`、`GearPixelCurtain`。每个都是一个片元函数 + 少量 uniform，复用 ImageEx 的材质与主题；
`InkPanel` 最后做（要 RT 烘焙）。

**Pack C｜控件包（约 8–12 天）**

`GearDropdown` + MultiSelect（功能空洞最大）、`GearSwitch`/`GearRadioGroup`、`GearRangeSlider`、
`GearQuantitySelector`、`GearInputFieldEnhancer`、`GearTimer`、`GearOffScreenIndicator`、
`GearPickerWheel` + `GearTimePicker`（简化）、`GearCalendar`（先月视图）、`GearColorPicker`（简化）、`GearSearchIndex`。
每个都要接 `UIInputSystem` 焦点与 `LanguagesSystem`，验收必须含手柄/键盘路径。

### 4.6 不建议复刻

| 内容 | 原因 |
|---|---|
| VirtualCursor | 与 Gear 焦点域重叠；除非主机 UI 成为主线，单独评估 |
| CurvedCanvas | 需要每帧改网格，收益是"曲面屏"这类特定美术风格 |
| Home 卡组 / 6 整屏 Showcase | 元 UI，成本高、复用低；需要"作品集"时再做 |
| 180 个预制体 / Sprite / 字体 | 资产不可搬，且 Evo 观感主要来自它们 |
| 完整 Calendar / ColorPicker / 3D 图表 | 只有真实业务需求才做最小集 |
| Evo Tween / Evo Styler 实现 | 换 DOTween / 现有主题服务；只借契约 |
| Style Page 全量 | 先做 2.4 的材质池，量出 draw call 再决定 |

### 4.7 展厅：catalog + 详情窗口，不要一个大场景

EvoDemo 是一个 19 MB 的单场景（工作树已删除），几百个对象预摆。这在 Gear 里会拖慢场景打开、
让 `UISystem` 的对象池/加载单飞失去意义。建议：

```
Assets/Gear/UI/Showcase/
├── ShowcaseCatalog.cs        ← ScriptableObject：分类/卡片/标签/搜索文本
├── ShowcaseUIController.cs   ← MainUI：GearTabGroup 分类 + LoopScrollRect 卡片 + 搜索
└── Cards/                    ← 每张卡一个 Prefab（ImageEx + 说明）
Assets/Gear/Resources/Prefabs/UIs/Showcase/
├── ShowcaseDetail.prefab     ← NormalUI 详情窗口
└── ShowcaseThemeBar.prefab   ← 主题切换（GearThemeRegistry）
```

- 复用 `ImageExGalleryBuilder` 的卡片生成逻辑与 `UI Example` 场景，不重写画廊；
- 点卡片 → `UIRouter.PushAsync<ShowcaseDetailWindow, ShowcaseArgs>(cardId)`，返回 → `UIRouter.Back()`；
- 主题切换 → `GearThemeRegistry.Apply(theme, 0.25f)`，窗口不重建。

---

## 五、建议的下一批（三个 Sprint）

| Sprint | 内容 | 估算 | 前置 |
|---|---|---|---|
| S1 渲染收口 | `ImageExMaterialPool`（指纹池 + COW + 不回读）、`ImageExStateColors`、RadiusSync、Sprite Fill、Spotlight | 1 周 | 先跑现有诊断拿基线 |
| S2 手感包 | Pack A 的 4 个纯 C# 组件（AnimatedContainer / Sway / DockMagnifier / MenuBar）+ 4 张卡 | 1 周 | S1 的卡片规范 |
| S3 效果补齐 | `GearTextEffects` 扩到 12 种 + `<tag>` 作用域；`GearSoftMask` 嵌套（RT 试点） | 1–1.5 周 | 与 `LanguagesSystem` 的预处理顺序验证 |
| 按需 | Pack C 控件（Dropdown 优先）；Pack B 效果；材质池之后再看 style page | — | 有真实界面需求 |

> 顺序理由：S1 是"所有界面都受益"的地基；S2 是"观感立刻提升且零 shader 风险"；
> S3 才是"Evo 最抓眼球但兼容性最高"的部分。**不要先做 style page / 布尔运算。**

---

## 六、验收

1. `dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear` 退出码 0
2. `dotnet build Unity/Gear/Gear.Utilities.csproj` 0 error
3. `pwsh Tools/unity-verify.ps1 -IncludePlayMode` 全绿（新增用例计入门槛）
4. 画廊/示例场景：新能力有卡片，旧资源默认关闭时行为不变
5. CHANGELOG 一条 Added/Fixed + 对应设计文档更新
6. 文档改动跑 `dotnet run --project Tools/DocsCheck -- .`

> 视觉结论（软遮罩边缘、Spotlight 衰减、材质池是否串扰）没有 MCP 时仍需人工截图确认，
> 不能只凭编译通过；材质池必须用"两个设置不同的 ImageEx 相邻"用例验证不串扰。

---

## 七、风险

| 风险 | 影响 | 处理 |
|---|---|---|
| **Evo 资产已在公开仓库** | 许可与合规 | 见 1.3；先 `git rm --cached` + `.gitignore`，历史清理单独评估 |
| 材质池 COW 写错 | 相邻实例串色/串特效 | 用"不同设置相邻"用例 + 断言材质引用不同 |
| 文本动效与翻译冲突 | 预处理器顺序/字符索引错位 | 先整段文本，再标签版；用翻译后的文本做用例 |
| SoftMask 嵌套 RT | 显存/重建/移动端成本 | 复用 `UIBackdropBlur` 的 RT 约定；先只覆盖 ImageEx |
| 新控件绕过焦点系统 | 手柄/键盘用户点不到新页 | 组件必须暴露 `FocusTarget`，验收含手柄路径 |
| 范围蔓延 | 15 类全铺 = 数月 | 按 4.5 三个包；Home/整屏/完整日历明确不做 |

---

## 八、证据索引

**Evo**

- `Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeOperations.cs`（布尔运算、命中镜像）
- `Assets/Evo/Evo UI/Scripts/Graphics/ProceduralShapeMaterial.cs:624-935`（StylePage / light-full / WarmUp）
- `Assets/Evo/Evo UI/Scripts/Effects/InteractiveEffectRenderer.cs:13-19,660-700`（效果图集）
- `Assets/Evo/Evo UI/Scripts/Effects/{SoftMask,SoftMaskable,TextEffects,PixelCurtain,InkPanel,DotMatrixLoader,TopographicSkin,WarningStripe,DockMagnifier}.cs`
- `Assets/Evo/Evo UI/Scripts/Styler/{Styler.cs,StylerObject.cs,StylerPreset.cs}`
- `Assets/Evo/Evo UI/Scripts/Core/ColorTransition.cs`
- `Assets/Evo/Evo UI/Scripts/{Layout,Navigation,UI Elements}/**`
- `Assets/Evo/Evo UI/Demo/Scripts/**`（绑定层，仅作验收样例）

**Gear**

- `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/**`（ImageEx 与效果）
- `Assets/Gear/Utilities/UI/ImageExtensions/Runtime/Overlays/**`
- `Assets/Gear/Utilities/UI/{Interaction,Navigation,Charts,ListView,Widgets,TextEffects}/**`
- `Assets/Gear/Resources/Shader/UI/ImageEx/ImageEx.shader`、`Resources/Shader/Common/RM_*.cginc`
- `Assets/Gear/Framework/Runtime/System/{UISystem,UIInputSystem,UIToolkitSystem}/**`
- `Assets/Gear/Tests/**`（UI 用例）

**相关文档**

- [Evo 对照：UI 能力差距与路线图](Evo对照-UI能力差距与路线图.md)
- [Evo 对照：EvoDemo 复刻与 ImageEx 技术取舍](Evo对照-EvoDemo复刻与ImageEx技术取舍.md)
- [Evo 对照：ImageEx 清理与 UI 能力补齐](Evo对照-ImageEx清理与UI能力补齐.md)
- [Evo 对齐：ImageEx 阴影与 UI 覆盖层](Evo对齐-ImageEx阴影与UI覆盖层.md)
- [Gear UI 下一阶段开发规划](Gear-UI-下一阶段开发规划.md)
- [Gear UI 渲染诊断](Gear-UI-渲染诊断.md)
