# Gear UI 渲染诊断

场景：`UI Example`

| 指标 | 数值 |
|---|---|
| Canvas | 2 |
| Graphic | 739 |
| ImageEx | 405 |
| ImageEx Dynamic / Shared | 405 / 0 |
| ImageEx 材质实例 | 405 |
| 可合批组（≥2） | 16 组 / 291 实例 |
| 共享材质后预计材质实例 | 130 |

## Canvas 材质分组（估算）

| Canvas | Graphic | 唯一材质 | 估算批次组 |
|---|---:|---:|---:|
| ImageEx 画廊 | 737 | 405 | 408 |
| Canvas | 2 | 2 | 2 |

## 可合批分组（≥2）

- 13 个实例 · 代表：`RowTemplate(Clone)`
- 2 个实例 · 代表：`Indicator 3`
- 116 个实例 · 代表：`目标 · Luminance`
- 117 个实例 · 代表：`Highlight`
- 4 个实例 · 代表：`Prev`
- 3 个实例 · 代表：`Page 2`
- 13 个实例 · 代表：`Preview`
- 2 个实例 · 代表：`Row 3`
- 2 个实例 · 代表：`Row 2`
- 4 个实例 · 代表：`Tab B`

## 结论

- 本次 ImageEx 材质实例：405 → 预计 130（按可合批组估算，不是实际 draw call）。
- 若「可合批组」很少，优先做材质/样式微调，不要上 Style Page。
- 若同一 Canvas 的唯一材质接近 Graphic 数，说明每实例 Dynamic 材质正在拆批，值得先做共享材质。
- Style Page 是架构级改造；只有 draw call 实测成为瓶颈时才排期。

## 建议下一步

1. 先选可合批组中最大的 1–2 组做共享材质试点。
2. 记录试点前后的 Batches / SetPass Calls。
3. 若收益明显，再批量应用到同设置组；不要直接改 Style Page。
