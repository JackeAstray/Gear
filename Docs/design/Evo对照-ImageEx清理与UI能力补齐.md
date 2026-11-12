# Evo 对照：ImageEx 清理与 UI 能力补齐

> 目的：以本项目内已导入的 `Assets/Evo`（Evo UI 2.0.6 + Evo Tween 2.0.1）为参照，
> 明确 Gear 的 **ImageEx 要删掉哪些落后设计**、**UI 模块要补哪些能力**，并按可验证的里程碑推进。
>
> 现状：Evo 在本工程已编译通过（Editor.log：`error CS` 0、`Shader error` 0）。
> 两套 UI 栈只做"能力参照"，不把 Evo 接进 Gear 的 `UISystem` / `UIInputSystem`。

---

## 一、ImageEx 的落后设计（清理清单）

| # | 位置 | 为什么落后 | 处理 |
|---|---|---|---|
| 1 | `ShadowMode.Outline8`（8 个偏移四边形） | 与 `SdfOutline` 重复；旧实现每次 `AddShadowQuad` 还会**重复追加一份主图**，实际 17 个四边形 | ✅ 已废弃：运行时统一映射到 `SdfOutline`（单 quad）；枚举值保留仅为旧资源序列化兼容，Inspector 标注"已废弃" |
| 2 | `AddQuad(appendShadow)` 总是追加主图 | 调用方只要阴影时却多画一份主图 → 多倍 overdraw、半透明叠色 | ✅ 已修：新增 `addMainQuad` 参数，`AddShadowQuad` 只写阴影 |
| 3 | `MaterialMode.Shared` + `InitValuesFromSharedMaterial`（~100 次 Get* 回读） | 共享材质靠"最后写入者决定"，有串扰；回读还会把组件字段改成材质默认值 | ⏳ 待办：改为"按设置指纹共享 + 不回读"，或由合批工具独占该模式（当前工具已按指纹分组，串扰已被约束） |
| 4 | `OnValidate` 里整段重新赋值 ~80 个属性 | 序列化字段已经更新，`GetModifiedMaterial` 每次脏化都会写 uniform；这段是历史遗留的"手动回写"，编辑器改一个值会触发大量 setter | ⏳ 待办：删到只保留结构体 `OnValidate` + `SetMaterialDirty()`，逐项验证后移除 |
| 5 | `ComputeSettingsSignature` + `s_sharedMatLastMask` 串扰诊断 | 只为提醒 Shared 模式的错误用法；与 #3 绑定 | ⏳ 随 #3 一起删除 |
| 6 | `ImageExReplica` / `CameraToImageEx`、`ImageExTweener` | 分别依赖另一份 ImageEx、DOTween，职责与"组件本身"耦合 | ⏳ 评估：保留但移出核心目录（`Integrations/`），核心只留 ImageEx + 预设 + 主题 |
| 7 | `DrawShape == None` 的 Sobel 纹理描边 | 与 SDF 描边是两套语义，维护成本高 | ⏳ 保留（纹理模式仍需要），文档标注"非形状路径" |

---

## 二、Evo 演示能力 → Gear 差距（按页面）

| Evo Demo 页 | 能力 | Gear 现状 | 计划 |
|---|---|---|---|
| **Shapes** | 形状 + 7 层嵌套 + `Twist` 旋转栈 | 单形状 | M2：`ImageExLayerStack`（N 层同形状、逐层旋转/缩放/透明），网格层数可配 |
| **Gradients** | 屏幕空间渐变、可动画、同 Canvas 共享 | 本地渐变 + 旋转组件 | M2：`_GradientSpace`（Local/Screen）+ `_GradientSpeed`（shader 内 `_Time`） |
| **Outline** | 圆头虚线、沿周长闭合、动画偏移 | 像素级虚线（已实现），方头/分边 | M2：perimeter SDF 专项（直线段 + 圆弧分解求弧长），实现圆头 cap 与无接缝 |
| **Pattern** | 点阵/星形图案 + falloff（Start/End/原点） | 仅细节纹理 | M2：`_PatternMode`（Dot/Star/Grid）+ falloff 参数 |
| **Backdrop Blur** | Blur/Tint/Saturation/Brightness/Noise | ✅ 已实现 | 验收调参 |
| **Shadows（Evo UI 组件页）** | 内/外阴影带 Offset/Size/Softness | 内阴影（无 Offset）、SdfOutline（等距） | M2：内阴影补 Offset；外阴影补"方向 + 扩散"语义 |

---

## 三、UI 模块差距（Evo UI → Gear）

| 分类 | Evo 提供 | Gear 现状 | 结论 |
|---|---|---|---|
| 主题 / Styler | 预设 + token + 运行时换肤 | ✅ 主题令牌（本次） | 对齐完成 |
| Overlays | Modal / Toast / Tooltip / ContextMenu / Popover | 只有 `UIModalStack` | **P0 补齐**：Toast / Tooltip / ContextMenu（纯 UGUI，接 `UISystem` 的分层根） |
| 导航 | PageManager / TabManager / Stepper / Carousel | 有 Carousel（自研），无 Page/Tab/Stepper | P1：先做 Stepper/Tab（选型面板常用） |
| 数据展示 | ListView / Counter / 4 种 Chart | `LoopScrollRect`（够用），无 Chart | P2：Counter + 简单折线/环形（图表需求未验证前不铺开） |
| 控件 | Dropdown / InputField / Slider / Switch / 日期 / 颜色选择 | uGUI 原生控件为主 | P2：按业务需要做，不照单全收 |
| 效果 | SoftMask / BackdropBlur / BorderGlow / TextEffects | BackdropBlur ✅；其余缺 | P1：SoftMask（SDF 遮罩）、BorderGlow（边缘发光） |
| 编辑器工具 | Catalog / Styler Browser / Icon Browser / Sprite Optimizer | ✅ 工具台（预设/合批/诊断/主题/拾取/缩略图） | 基本对齐 |

---

## 四、里程碑

### M1 基线（本次）
- 清理 Outline8 与重复主图（已完成）；
- 冻结"对比基线"：同一场景里并排 Gear / Evo 卡片，参数写入 JSON，便于逐轮比对（待做）。

### M2 单组件能力（ImageEx）
1. 渐变屏幕空间 + 动画；
2. 内阴影 Offset、外阴影方向/扩散；
3. 图案（点阵 + falloff）；
4. perimeter 圆头虚线；
5. 层叠 + Twist。

### M3 UI 模块
1. Toast / Tooltip / ContextMenu（Overlays）；
2. SoftMask / BorderGlow；
3. Stepper / Tab；
4. 图表按需。

---

## 五、风险与约束

- **许可**：`Assets/Evo` 是 Asset Store 商业资产，当前为 git untracked；不要提交到公开仓库、不要打进 UPM 包。
- **两套栈并存**：Evo 的 Canvas/EventSystem 假设（2560×1440、`InputSystemUIInputModule`）与 Gear 的 `UISystem`/`UIInputSystem`（1920×1080、自研焦点域）不同，Evo 只作视觉参照。
- **删除公共 API 的兼容性**：`Outline8` 保留枚举值但行为映射到 `SdfOutline`；后续删除 `MaterialMode.Shared` 前需先迁移合批工具。
- **验证**：着色器改动依赖 Unity 导入编译；无 MCP 时视觉结论需人工截图确认。
