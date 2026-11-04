# 仓库体积与 LFS

> 现状快照：2026-11-03（**已完成历史重写 + LFS 迁移**）。`.git/objects` 约 143 MB，
> `.git/lfs` 约 642 MB（23 个对象），`.git` 合计约 786 MB；clone 仍要拉这 642 MB。
> 要进一步缩小 clone，需要把 `Images/`（工作区 83 MB，GIF 多为 LFS 指针）与字体外置，或从历史剔除。

## 1. 体积构成（实测）

| 位置 | 体积 | 说明 |
|---|---|---|
| `.git/objects` | ≈143 MB | 重写后的 Git 历史（GIF/unitypackage 已变 LFS 指针） |
| `.git/lfs` | 642 MB | 23 个 LFS 对象（GIF / unitypackage 等） |
| `Images/` | **已移除** | 2026-11 删除：文档示例 GIF 已不再维护，工作区少 445 MB。历史提交里仍留有对象数据 |
| `Unity/Gear/Assets/Gear/Fonts/` | 130 MB | 思源 / HarmonyOS Sans / 第三方中文字体 |
| 工作区 `Assets/Gear/` | 194 MB | 含上述字体 |
| 本地 `Unity/Gear/`（含 Library/Build） | 15 GB | 已被 `.gitignore` 忽略，可随时删除重建 |

`.gitattributes` 已为 `*.gif` / `*.otf` / `*.ttf` / `*.psd` / `*.fbx` / `*.mp4` / `*.unitypackage`
配置 LFS。2026-10-31 的历史重写（`git lfs migrate import`）之后，`git lfs ls-files` 列出 23 个文件；
**协作者必须重新 clone**，旧的本地仓库不会有这些 LFS 指针。

## 2. 非破坏性止损（已默认采用）

这些操作不改历史、不需要协作者重新 clone：

1. **不再新增裸大文件**：新增 GIF/字体/视频先确认 `.gitattributes` 的 LFS 规则生效
   （`git check-attr filter -- Images/x.gif` 应输出 `filter: lfs`）。
2. **运行时包默认排除字体与示例**：UPM 的 Runtime profile 已排除 `Fonts/`、`Scenes/`、
   `3rd/Joystick Pack` 等，接入方不需要拉 130 MB 字体。
3. **文档图外置（可选）**：把 `Images/` 迁到 GitHub Release 附件/图床，README 用外链；
   能少拉约 445 MB 的 GIF，但历史仍不变。
4. **定期检查**：`git lfs migrate info --everything` 只读，能列出各扩展名占用，不改仓库。

## 3. 历史重写（已于 2026-10-31 执行；协作者必须重新 clone）

目标：把历史里的大文件迁进 LFS，或从历史中剔除。两种方案：

### 方案 A：迁进 LFS（保留文件，改指针）

```bash
# 先备份/打 tag，通知所有协作者
git lfs migrate info --everything
git lfs migrate import --include="*.gif,*.png,*.psd,*.otf,*.ttf,*.fbx,*.mp4,*.unitypackage" --everything
git push --force-with-lease --all
git push --force-with-lease --tags
```

代价：**所有提交哈希改变**，协作者必须重新 clone（不能简单 pull）。
GitHub 上需要临时允许 force push；LFS 配额会随历史增长。

### 方案 B：从历史剔除（不保留旧版本）

```bash
# 示例：剔除 Images/ 与 Release/（会改写历史）
git filter-repo --path Images --path Release --invert-paths
git push --force --all
```

代价同上，且旧提交里这些文件**永久消失**。只有在确认文档图可以外置后才做。

### 推荐顺序

1. 先做第 2 节的止损，冻结增长；
2. 把文档图迁到外置存储，确认 README 不再依赖 `Images/`；
3. 再执行方案 B（比 A 更能缩小仓库）；
4. 若必须保留历史图，选方案 A，并在发布说明里写清"需重新 clone"。

## 4. 验收

```bash
git count-objects -vH          # size-pack 应显著下降
git lfs ls-files | wc -l       # 迁移后应接近大文件数量
du -sh .git Images Unity/Gear/Assets/Gear/Fonts
```

> 注意：`git gc` 只能压缩，不会删除仍被历史引用的对象；不重写历史时 `.git` 不会明显变小。
