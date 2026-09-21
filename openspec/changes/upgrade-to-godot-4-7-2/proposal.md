# Proposal

## Why

Modot 2.0.2 固定在 Godot 3.x（`Godot.NET.Sdk/3.3.0`、`netstandard2.1`），既无法在 Godot 4 引擎中加载，也无法被 Godot 4 应用引用；上游仓库仍停留在 Godot 3，本 fork 必须自行完成迁移才能继续维护。

两个已核实的阻塞事实：

- `Godot.Directory` 与 `Godot.File` 在 Godot 4.7.2 中已被移除（由 `DirAccess`/`FileAccess` 取代），而 `Utility/Extensions/DirectoryExtensions.cs` 整个建立在 `Godot.Directory` 之上。
- `GDLogger` 在 nuget.org 上只有 1.0.0 与 1.0.1 两个版本，没有 Godot 4 版本，其 `Log` 依赖已被移除的 `Godot.File`。

## What Changes

- **BREAKING** 重定构建目标：`Godot.NET.Sdk/3.3.0` → `4.7.2`，`netstandard2.1` → `net10.0`。
- **BREAKING** `PackageVersion` 由 2.0.2 提升到 3.0.0；消费方必须为 `net10.0`，`net8.0`/`net9.0` 项目无法引用（`NU1202`）。
- **BREAKING** 以 Godot 3 `GodotSharp` 编译的 mod 程序集不再受支持，mod 作者需针对 Godot 4 重新编译。
- **BREAKING** `DirectoryExtensions` 的接收者类型由 `Godot.Directory` 变为 `Godot.DirAccess`（前者已不存在，无法避免）。
- 移除 `GDLogger` 依赖，改为仓库内 `internal static Godot.Log`，只保留 Modot 实际使用的两个成员，使 6 个既有调用点零改动。
- 源码从仓库根的 `Modding/`、`Utility/` 迁入 `src/Modot/`，并新增根级 `Modot.sln`，形成"按项目分目录"的布局。
- 把程序集加载由 `Assembly.LoadFile` 改为 `AssemblyLoadContext`，避免类型标识不一致导致 `[ModStartup]` 静默失效。
- 重写 `.gitignore`：移除 `*.sln` 规则（新增的解决方案需要入库），并按"凡是构建产物、编辑器/IDE 生成物、本机工具目录一律忽略"的策略，补全 Godot、.NET、Visual Studio、VS Code、Rider 与操作系统的忽略规则（完整清单与取舍见 design.md - D7）。

明确不在范围内：任何新功能；`Mod.xml`、`Data/**/*.xml`、`Patches/**/*.xml` 的格式；加载顺序与补丁语义。`Godot.Error`、`ProjectSettings.LoadResourcePack` 与 `RootNamespace` 在 Godot 4 中未变，保持不动。

## Capabilities

### New Capabilities

- `runtime-compatibility`: Modot 支持的 Godot 引擎与 .NET 运行时版本、对既有 mod 数据的向后兼容承诺，以及加载失败必须产出的诊断。

### Modified Capabilities

（无。本项目尚无任何已建立的 spec，因此没有需要变更的既有能力。）

## Impact

- **构建**：`src/Modot/Modot.csproj`（SDK 固定版本与目标框架）、新增 `Modot.sln`、重写 `.gitignore`（忽略范围见 design.md - D7）。
- **代码**：`src/Modot/Utility/Extensions/DirectoryExtensions.cs`（`Godot.Directory` → `Godot.DirAccess`）、`src/Modot/Modding/Mod.cs`（程序集加载）、新增 `src/Modot/Log.cs`；`Modding/ModLoader.cs` 与 `Modding/Patching/LogPatch.cs` 的 6 个日志调用点保持零改动。
- **依赖**：移除 `GDLogger 1.0.1`；保留 `GDSerializer 2.0.3` 与 `JetBrains.Annotations 2022.1.0`。注意 `GDSerializer` **并非引擎无关**（其 DLL 硬引用 `GodotSharp`，`VectorSerializer` 仍用 Godot 3 字段名，在 Godot 4 上该路径会失败），但 Modot 自身的可序列化面不触及该路径，详见 design.md - Context 与 D10。
- **验证能力**：本机可用 Godot 4.7.2 (.NET)（路径见 `AGENTS.md`），因此 `DirAccess`、`ProjectSettings.LoadResourcePack`、`GD.Print`/`GD.PushError` 这些**运行时**行为是**可以真正验证**的，而不是只能列为遗留事项：验证载体是 `add-e2e-test-suite` 交付的 headless e2e 套件。在该套件交付前，不得声称这些行为已验证。
