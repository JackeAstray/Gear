# Evo 对齐：ImageEx 阴影修正与 UI 覆盖层

> 本轮不是再写一份“参考方案”，而是把上一版 P0/P1 里**实现与预期不一致**的地方按 Evo 的真实语义修正，
> 并补齐 3 个 UI 覆盖层与 1 个默认主题。所有代码仍为 Gear 自研，不引用 `Evo.UI` / `Evo.Tween` 程序集。

---

## 一、对照 Evo 后发现的实际偏差

### 1. 外投影：Offset 只被当成“扩张距离”

- 旧实现（`ImageEx.Runtime.Rendering.cs`）：`_ShadowGrow = |shadowOffsetLocal|`，网格按该值**对称**外扩；
  片元里 `sampleSdf(sdf - grow)` 也只做等距膨胀。
- 结果：`shadowOffsetLocal = (0, -10)` 和 `(10, 0)` 渲染完全一样；没有“向下投影”的方向感。
- Evo 语义（`ProceduralShape.shader` L921-944）：`shdDist = distance(p - OuterShadowOffset) - OuterShadowSize`，
  `mask = 1 - smoothstep(-aa, aa, shdDist)`，`Softness` 决定羽化。
- 另外旧网格把 `Outline8` 映射成单 quad，但材质仍写 `_ShadowMode = 8`，shader 只有 `9` 分支 → 旧资源阴影会消失。

**修正**：新增 `ShadowSpread / ShadowSoftness / ShadowOutsideOnly`；网格按 `offset + spread + softness` **四边不对称外扩**；
shader 在 `shapeData.xy - _ShadowOffset` 处重新求 SDF，再按 `sdf - spread` 做羽化；`Outline8` 在材质写入时统一映射到 `SdfOutline`。

### 2. 内阴影：没有 Offset，衰减曲线也不是 Evo 的

- 旧实现：`depth / width` 线性条带，再用 `pow` 改曲线；没有方向。
- Evo：`inDist = distance(p - InnerShadowOffset)`，`mask = smoothstep(-aa, aa, inDist + InnerShadowSize)`，
  `Softness` 直接作为 AA 宽度。
- **修正**：新增 `InnerShadowOffset`；shader 改为在偏移点求 SDF 的 `smoothstep`；
  `Softness` 保持 0-1 序列化范围，映射到约 0-12px 羽化，避免旧资源突然变成硬边。

### 3. 虚线：四边各自用 x/y，过角必断；alpha 混合方向反了

- 旧实现矩形分别用 `shapeData.x` 与 `shapeData.y` 做虚线相位 → 四个角相位不连续，看起来“每边各画各的”。
- 旧 alpha：虚线处用**填充 alpha**、空隙用**描边 alpha**，空心图形的虚线描边会整圈消失。
- Evo：沿轮廓周长连续参数化，虚线是带圆头帽的胶囊 SDF。
- **修正**：新增矩形/圆形周长参数化 + 胶囊 SDF；`DashRounded` 打开圆头帽，`DashOffset` 手动对齐相位；
  修正为“虚线处 = 描边色 + 描边 alpha，空隙 = 填充色 + 填充 alpha”。

### 4. 渐变：只有局部空间、只能乘色、没有自动流动

- Evo 的 Fill Gradient 支持 `Local / ScreenSpace`、`Linear / Radial / Angular`、`Reverse`、`Zoom`、自动动画。
- 旧实现只有 Local + Linear/Corner/Radial，旋转靠外部组件每帧写 uniform。
- **修正**：新增 `GradientEffect.ScreenSpace / Reverse / Speed`，`GradientType.Angular`；
  shader 用 `SV_POSITION` 构造屏幕坐标（带 D3D/GL 的 Y 翻转处理），`Speed` 直接读 `_Time.y`，不再需要 C# 每帧写。

### 5. 缺失的“Evo 可见能力”

| 能力 | 实现 |
|---|---|
| 图案填充（点阵/网格/条纹/棋盘/圆环 + falloff/流动） | `RM_Pattern.cginc`、`ImageEx.Pattern*` |
| 边缘辉光（环境光 + 指针跟随） | `RM_BorderGlow.cginc`、`ImageEx.BorderGlow*`、`ImageExPointerGlow` |
| 背景模糊直接作用在 ImageEx 上（保留形状/圆角/描边） | `RM_Backdrop.cginc` + `UIBackdropBlur` 的 ImageEx 分支 |
| 主题令牌默认资产 | `Resources/UITheme/ImageExDefault.asset` |
| Toast / Tooltip / ContextMenu | `Runtime/Overlays/*` |

---

## 二、新增/修改文件

### ImageEx 运行时

| 文件 | 作用 |
|---|---|
| `Runtime/ImageEx.AdvancedEffects.cs` | 外投影 Spread/Softness/OutsideOnly、图案、边缘辉光字段与属性 |
| `Runtime/ImageEx.InnerShadow.cs` | 新增 `InnerShadowOffset` |
| `Runtime/ImageEx.Dash.cs` | 新增 `DashRounded` / `DashOffset` |
| `Runtime/ImageExPointerGlow.cs` | 指针位置写入 + 悬停淡入（`IPointerMoveHandler`） |
| `Runtime/Effects/GradientEffect.cs` | `ScreenSpace / Reverse / Speed`、`Angular` |
| `Runtime/ImageEx.Runtime.Rendering.cs` | 新 uniform 写入 / Shared 回读 / Outline8 映射 |
| `Runtime/ImageExPreset.cs` | 新字段进入预设的 Apply/Read/SameAs |
| `Runtime/ImageEx.Properties.Transition.cs` | `Shadow` setter 统一映射 `Outline8 → SdfOutline` |
| `Runtime/Theme/ImageExThemeBinder.cs` | 新主题目标（投影/图案/辉光） |

### 着色器

| 文件 | 作用 |
|---|---|
| `Common/RM_Shadow.cginc` | 方向性 SDF 投影 + OutsideOnly + 不再乘精灵 alpha |
| `Common/RM_InnerShadow.cginc` | Offset + Evo 语义的 smoothstep |
| `Common/RM_Gradient.cginc` | 屏幕空间 / Angular / Reverse / Speed |
| `Common/RM_Outline.cginc` | 周长连续虚线 + 圆头帽 + alpha 修正 |
| `Common/RM_Pattern.cginc` | 新增图案模块 |
| `Common/RM_BorderGlow.cginc` | 新增边缘辉光模块 |
| `Common/RM_Backdrop.cginc` | ImageEx 背景模糊采样 |
| `UI/ImageEx/ImageEx.shader` | 新属性、include 与调用点 |

### UI 覆盖层

| 文件 | 作用 |
|---|---|
| `Runtime/Overlays/GearOverlayLayer.cs` | 运行时 Overlay Canvas（优先挂 `TipsUIRoot`）、主题取色、圆角面板/文本工厂 |
| `Runtime/Overlays/GearToast.cs` | 右上角堆叠 Toast，unscaled time 自动关闭 |
| `Runtime/Overlays/GearTooltip.cs` | 悬停触发器 + 指针跟随提示 |
| `Runtime/Overlays/GearContextMenu.cs` | 上下文菜单，点击外部/Esc 关闭，贴边翻转 |

### 编辑器

| 文件 | 作用 |
|---|---|
| `Editor/Scripts/ImageExGalleryBuilder.cs` | 清空并重建 `ImageEx 画廊/Scroll View/Viewport/Content`（原 UI示例）；`Gear/UI 示例` 菜单已移除，由一次性入口调用 |
| `Editor/Scripts/ImageExGalleryCatalog.cs` | 100+ 张卡片的完整目录；需要 Sprite 的滤镜/过渡卡片统一使用 `Resources/UI/Sprites/Saber.png` |

---

## 三、使用方式

### 外投影（Evo 语义）

```csharp
image.DrawShape = DrawShape.Rectangle;
image.Rectangle = new RectangleImg { CornerRadius = new Vector4(16, 16, 16, 16) };
image.AppendShadow = true;
image.Shadow = ImageEx.ShadowMode.SdfOutline;
image.ShadowOffsetLocal = new Vector2(0f, -12f); // 向下
image.ShadowSpread = 2f;                        // 比形状大 2px
image.ShadowSoftness = 24f;                     // 24px 羽化
image.ShadowOutsideOnly = true;                 // 半透明形状内部不叠色
```

### 图案

```csharp
image.Pattern = ImageEx.PatternMode.Dots;
image.PatternSize = new Vector2(18, 18);
image.PatternGap = new Vector2(6, 6);
image.PatternColor = accent;
image.PatternFalloff = new Vector2(0.15f, 0.9f); // 从中心到边缘淡出
image.PatternSpeed = new Vector2(8, 0);          // 横向流动
```

### 圆头虚线

```csharp
image.OutlineWidth = 3f;
image.EnableDashedOutline = 1;
image.DashLength = 18f;
image.DashGap = 12f;
image.DashRounded = true;
```

### 边缘辉光

```csharp
image.EnableBorderGlow = true;
image.BorderGlowColor = accent;
image.BorderGlowWidth = 2f;
image.BorderGlowSoftness = 12f;
image.BorderGlowAmbient = 0.35f;
image.BorderGlowFollow = 1f; // 1 = 完全跟随指针
image.gameObject.AddComponent<ImageExPointerGlow>();
```

### 背景模糊（ImageEx 版）

```csharp
// 挂在 ImageEx 上即可，形状/圆角/描边保留，只把填充换成抓屏模糊结果
var blur = image.gameObject.AddComponent<UIBackdropBlur>();
// 组件会把 _BackdropTex / _GlassTint / _EnableBackdropBlur 写到 ImageEx 材质
```

### UI 覆盖层

```csharp
GearToast.Show("已保存", "进度", GearToastKind.Success);
GearTooltip.Attach(button.gameObject, "按住 Shift 多选", "提示");
GearContextMenu.Show(Mouse.current.position.ReadValue(), new[]
{
    new GearContextMenuItem("重命名", Rename),
    GearContextMenuItem.Separator(),
    new GearContextMenuItem("删除", Delete, danger: true),
});
```

主题：`Resources/UITheme/ImageExDefault.asset` 已内置 `Overlay*` 令牌；覆盖层优先读主题，
换主题时 Toast/Tooltip/ContextMenu 会一起换色。

---

## 四、验证

1. **C# 编译**：`dotnet build Gear.Utilities.UI.ImageExtensions.Editor.csproj` → 0 error（本轮实测）。
2. **Unity 编译**：打开工程后 Console 无 `error CS`、无 `Shader error`。
3. **画廊**：打开 `UI Example` 场景，由一次性入口重建 → 100+ 张卡片写入 `ImageEx 画廊/Scroll View/Viewport/Content`（原 UI示例）：
   - 渐变卡应有 45° 紫→青且缓慢流动；
   - 图案卡应为点阵并由中心向外淡出；
   - 虚线卡矩形四角不应出现相位断裂，端点应圆头；
   - 内阴影卡应向右下偏移，越靠边缘越深；
   - 辉光卡鼠标在预览上移动时，亮边跟随指针；
   - 裁剪卡为 65% 扇形。
4. **回归**：未开启新开关的旧资源不受影响（`_PatternMode=0`、`_EnableBorderGlow=0`、
   `_ShadowSpread=0` 时行为与旧版一致；`Outline8` 现在会按 `SdfOutline` 正常显示）。

### 已知限制

- 阴影/图案/辉光都走同一个片元程序的运行时分支，移动端全屏大图开这些效果仍有成本；
  建议大背景卡片单独放子 Canvas（与 Evo 的 light/full 程序分级思路一致，但 Gear 目前尚未拆程序）。
- 圆头虚线目前只覆盖矩形/圆形；星形/心形等仍回退旧的 UV 虚线。
- 内阴影只作用于形状路径（`DrawShape != None`），纯贴图没有 SDF。
