# Base —— 只有接口与状态

**放**：跨程序集共享的**契约**。`ICustomSystem`（模块生命周期）、`ModuleOrder` / `ModuleOrderAttribute`（初始化顺序）、
模块状态枚举、以及不依赖任何实现的纯数据定义。

**不放**：任何具体实现、任何 `UnityEngine` 之外的框架引用、任何 `Gear.Framework.Modules` / `Gear.Framework.Runtime` 的类型。
本程序集是分层最底层，被上面所有程序集引用；一旦这里引用了上层，整个依赖图就成环。

**典型文件**：`ICustomSystem.cs`、`ModuleOrderAttribute.cs`

**判断标准**：删掉它，上层能不能编译？必须能 —— 这里只放"上层实现、下层依赖"的抽象。
