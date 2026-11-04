# Scenes —— 启动链与示例场景

**放**：构建里真正会用到的那几个场景，按启动顺序编号靠前。

| 场景 | 用途 |
|---|---|
| `StartGame.unity` | 启动场景：挂 `Bootstrap`，决定初始化顺序（见 `Docs/01-架构总览.md`） |
| `LoadingScene.unity` | 加载/过渡场景（Loading 流程与进度条） |
| `Temp.unity` | 临时调试场景，**不要**往里放正式内容 |
| `Example/` | 示例场景：UI Example、Camera Billboard Example 等，仅用于演示与截图 |

**不放**：业务正式关卡、Prefab、材质（→ `Resources/` 或各系统的资源目录）。

**注意**：改场景列表要同步 `ProjectSettings/EditorBuildSettings.asset`；
`StartGame` 必须在索引 0，否则 `Bootstrap` 不会先于业务代码执行。
