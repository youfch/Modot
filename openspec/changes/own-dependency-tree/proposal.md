# Proposal

## Why

Modot 的依赖树有三种病，且同源于上游整套项目（`GDSerializer`、`GDLogger`、`MoreLinq`）随 Godot 3 一起冻结在 2022 年：

1. **引擎绑定的包被当成引擎无关。** `GDSerializer 2.0.3` 与 `GDLogger 1.0.1` 的 DLL **都硬引用 `GodotSharp`**，而两者的 nuspec **都不声明**该依赖 —— 从 NuGet 层完全看不出耦合。`GDSerializer` 的 `VectorSerializer` 仍按 Godot 3 字段名读 `Vector2.x`/`Vector3.x`，实测在 Godot 4 上抛 `MissingFieldException`；`GDLogger` 整个建立在 Godot 4 已移除的 `Godot.File` 之上，因此当前只能被替换成一个能力缩水的垫片。
2. **琐碎依赖。** `Carnagion.MoreLinq 1.4.0` 为整套项目只提供 **5 个方法**（`ForEach`/`Indistinct`/`NotNull`/`IndexOf`/`Join`，由它提供的 `System.Linq.EnumerableExtensions` 承载 —— 注意它故意把自己放在 `System.Linq` 命名空间里，看起来像 BCL），而该包 2022 年后不再更新、唯一版本就是 1.4.0，无从"升级"。
3. **包声明与真实需求不符。** `JetBrains.Annotations` 是纯编译期特性包，却被声明成 Modot 发布包的依赖，逼消费方还原一个运行时无用的包；而真正承载补丁与元数据反序列化的序列化器，其类型出现在 Modot 的公共元数据里，必须保证程序集身份稳定。

其中序列化器这一项**无法靠换包或吸收解决**：`Godot.Serialization.*` 的类型全名出现在 Modot 的公共 API 里，而 mod 程序集在运行时按 `AssemblyLoadContext` 加载；把源码吸收进 `Modot.dll` 会让同时引用上游 `GDSerializer` 的 mod 拿到**两份** `SerializeAttribute`，反射按类型标识匹配得到 `null`，导致补丁与元数据**不报错地失效**。因此正确做法是把上游源码 vendor 进 `src/` 一起适配，并**保留程序集名**。

## What Changes

- 新增 `src/GDSerializer/`：上游 MIT 源码（保留 `Copyright (c) 2022 Carnagion`），**保留程序集名 `GDSerializer` 与 `Godot.Serialization.*`/`Godot.Utility.*` 命名空间**；重定目标到 `Godot.NET.Sdk/4.7.2` + `net10.0`；**修复** `VectorSerializer` 改读 Godot 4 的 `X`/`Y`/`Z`，并让 `NodeSerializer` 重新绑定到 Godot 4 的 `Node` API；包名用 `Modot.GDSerializer`。**`System.CodeDom` 保留且不升级** —— 它是 `TypeExtensions.GetDisplayName` 的依赖，而该方法产出的类型名会被写入序列化 XML 的 `Type=` 属性、并作为补丁类型的解析判据（见 design.md - D7）。
- 新增 `src/GDLogger/`：上游 MIT 源码，**保留程序集名 `GDLogger`** 与 `Godot.Log` 的类型全名；把 `Godot.File` 移植到 `Godot.FileAccess`；包名用 `Modot.GDLogger`；**恢复被垫片丢失的文件日志、`Warning`、`EntryWritten` 能力**，并把日志文件句柄改为惰性获取，使类型初始化阶段不做 I/O。
- 删除 `src/Modot/Log.cs`（临时垫片），改由 vendored `GDLogger` 提供 `Godot.Log`；`ModLoader.cs` 与 `Patching/LogPatch.cs` 的 6 个日志调用点保持**零改动**。
- 在仓库内实现那 **5 个**扩展方法（`ForEach`/`Indistinct`/`NotNull`/`IndexOf`/`Join`），**去掉 `Carnagion.MoreLinq` 依赖**。
- `JetBrains.Annotations` 由 `2022.1.0` 升到 `2026.2.0`，并加 `PrivateAssets="all"`，使其不再出现在发布包的依赖声明里。
- `src/Modot` 对两个 vendored 项目改用 `ProjectReference`，由 `dotnet pack` 自动声明依赖；`scripts/pack-local.ps1` 改为按依赖顺序把三个包打到 `D:/GNuget`。
- 序列化的 XML 文本格式与补丁语义**保持不变**，为 Modot 2.x 编写的 mod 数据无需改动。

明确不在范围内：vendor `Carnagion.MoreLinq`（改为在仓库内实现那 5 个扩展方法）；GDLogger 的 `Log.gd` GDScript 伴生文件（Modot 是纯 C#，那是上游面向 GDScript 用户的分发物）；发布到 nuget.org；改变补丁或加载顺序语义；新增序列化能力（例如 Godot 4 泛型 `Godot.Collections.Array<T>`/`Dictionary<K,V>`）。

## Capabilities

### New Capabilities

- `serialization`: Modot 的 XML 序列化契约 —— 支持的类型范围、引擎类型在 Godot 4 上的可序列化性、mod 数据文本格式的稳定性，以及序列化器程序集的身份要求。
- `logging`: Modot 的日志契约 —— 日志落点（Godot 日志与日志文件）、严重度分级、条目事件，以及日志器程序集的身份要求。
- `dependency-manifest`: Modot 发布包的依赖清单契约 —— 只声明真实需要的运行时依赖、不残留未使用的依赖、编译期专用包不外泄、vendored 包以新包名发布但保留上游程序集名。

### Modified Capabilities

（无。`openspec/specs/` 目前为空，本项目尚无任何已归档的能力，因此不存在需要修改的既有规格。）

## Impact

- **新增**：`src/GDSerializer/`、`src/GDLogger/`，以及承载那 5 个扩展方法的文件。
- **删除**：`src/Modot/Log.cs`（能力被 vendored GDLogger 完整覆盖，且同名类型不能并存）。
- **构建**：`src/Modot/Modot.csproj`（两个 `ProjectReference`；`JetBrains.Annotations` 升版并加 `PrivateAssets`）、`Modot.sln`（三个项目）、`scripts/pack-local.ps1`（三个包按依赖顺序；该脚本已 gitignore）。
- **依赖**：移除 `GDLogger`、`GDSerializer`、`Carnagion.MoreLinq` 三个包；改为依赖本仓库产出的 `Modot.GDSerializer` 与 `Modot.GDLogger`；`System.CodeDom` 保留（`GDSerializer` 的必需依赖）。
- **公共 API**：两个 vendored 程序集的**程序集名与类型全名均不变**，因此按上游编译的 mod **无需重编译**；日志能力是**恢复**（文件日志、`Warning`、`EntryWritten` 回来了）而非破坏。
- **分发**：`D:/GNuget` 将同时出现三个包，且必须是依赖先落地。
- **验证能力**：`Vector2`/`Vector3` 往返与日志器惰性加载可在**引擎无关**环境下验证；`Node` 序列化、`FileAccess` 文件写入、`GD.Print` 输出需要引擎，而本机**有** Godot 4.7.2（路径见 `AGENTS.md`），因此这些交给 `add-e2e-test-suite` 交付的 headless e2e 套件验证，不再有"本机不可完成"的项目。在该套件交付前不得声称已验证。
