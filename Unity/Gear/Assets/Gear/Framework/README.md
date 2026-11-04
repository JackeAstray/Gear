# Framework —— 引擎与运行时系统

**放**：三块互相约束的东西。

| 子目录 | 程序集 | 职责 |
|---|---|---|
| `Base/` | `Gear.Base` | 只有接口与状态（`ICustomSystem`、`ModuleOrder`、模块生命周期枚举），依赖最少 |
| `Runtime/` | `Gear.Framework.Runtime` | 引擎本体：`Bootstrap`、`GameEngine`、`System/*`（UI / 音频 / 场景 / 输入…） |
| `Modules/` | `Gear.Framework.Modules` | 业务向运行时系统：网络、HTTP、下载、计时器、对象池、存档 |

**不放**：编辑器工具（→ `Editor/`）、纯工具类（→ `Utilities/`）、示例业务（→ `UI/` 或 `Game/`）。

**依赖方向（由 `ArchitectureGuard` 强制）**：`Gear.Base` → `Gear.Common` → `Gear.Utilities` → `Gear.Framework.Runtime` / `Gear.Framework.Modules`。
`Gear.Framework.Runtime` **不得**引用 `Gear.Framework.Modules` —— 引擎不能反过来依赖具体系统，
需要时通过 `ICustomSystem` / 事件解耦。新增文件后跑一次 `dotnet run --project Tools/SyntaxCheck -- Unity/Gear/Assets/Gear`
和架构守卫，别等 CI 报错。

**典型文件**：`Runtime/GameEngine.cs`、`Runtime/System/GameEngine.Modules.cs`、`Modules/Network/Core/NetworkClient.cs`

**判断标准**：这段代码是"引擎的一部分"还是"某个具体功能"？前者进 `Runtime/`，后者进 `Modules/`。
