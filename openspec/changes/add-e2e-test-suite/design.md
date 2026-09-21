# Design

## Context

动机见 proposal.md - Why。以下为已实测的环境事实：

- Godot 4.7.2 (.NET / mono) 位于 `D:\APP\Godot_v4.7.2-stable_mono_win64\`，目录内含 `Godot_v4.7.2-stable_mono_win64.exe`（GUI，181MB）与 `Godot_v4.7.2-stable_mono_win64_console.exe`（控制台，198KB）。`--version` 返回 `4.7.2.stable.mono.official.ed1daf0bf`。
- headless 机制已实测可用，最小验证如下：
  ```
  & "<引擎目录>\..._console.exe" --headless --path <项目目录> --script res://smoke.gd
  → 输出 "SMOKE_OK"，退出码 0
  ```
  `project.godot` + `extends SceneTree` + `_initialize()` 里 `print` 后 `quit(0)` 即可，`--path` 指定项目、`--headless` 不建窗口、stdout 可被 PowerShell 捕获、退出码可用。
- 现有代码状态：`Mod.LoadAssemblies` 会把 mod 目录 `Assemblies/` 下**所有** `*.dll` 递归加载进 `AssemblyLoadContext.GetLoadContext(typeof(Mod).Assembly)`；`Mod.Metadata.Load` 读 `Mod.xml`；`ModLoader.LoadMods` 是批量加载入口；`ModLoader.LoadMod` 是单加载入口。失败路径（重复 ID、缺依赖、循环依赖、互相不兼容）全部在 `ModLoader` 内部经 `Log.Error` 上报。
- 上游 `README.md` 记载：Godot 下用 NuGet 包需要 `CopyLocalLockFileAssemblies=true`（Godot bug #42271），否则引擎找不到依赖程序集。

## Goals / Non-Goals

**Goals:**

- 把"能验证"变成"已建成且常驻"：两层测试入库、可重复运行、失败即红。
- 用真实 mod 程序集覆盖**类型标识**这条静默失效路径。
- 让后续两个变更的运行时遗留任务有明确载体。

**Non-Goals:**

- 不覆盖 `.pck` 资源包（需先构建 pck，成本与工具链另算）。
- 不接入 CI，不把 e2e 塞进 `dotnet test`。
- 不改动 `src/` 的任何产品代码；不修改发布包内容。
- 不追求覆盖率数字；只覆盖"已知会坏且值得守"的路径。

## Decisions

### D1 三层验证，边界写死

| 层 | 位置 | 需要引擎 | 负责 |
|---|---|---|---|
| 编译级 | `dotnet build` | 否 | 类型与 API 兼容性 |
| 引擎无关 | `tests/Modot.Tests/` | **否** | 元数据解析、标量/集合序列化、`Vector2`/`Vector3` 往返、日志器加载不做 I/O |
| 引擎运行时 | `tests/e2e/` | **是** | mod 加载与排序、补丁效果、`[ModStartup]` 执行、`DirAccess`、`.pck`、日志输出、`Node` 序列化 |

边界写进 `tests/README.md` 并由 spec 的 Coverage boundary Requirement 固定。之所以专门立这条 Requirement：本仓库此前正是因为"把无法验证的说成已验证、把未安装的引擎说成已安装"出过错，边界必须可查而不是靠记忆。

### D2 引擎运行时层用真实 Godot 项目 + headless，而不是纯 .NET 反射

曾考虑直接用 `dotnet` 加载 `Modot.dll` 并调用 `ModLoader`，省掉引擎。**不可行**：`Mod` 的构造会走 `LoadResources`（`ProjectSettings.LoadResourcePack`）、`Log.Error` 走 `GD.PushError`、`DirectoryExtensions` 全部基于 `DirAccess` —— 这些都是原生调用，在纯 .NET 进程里会崩。所以宿主必须是真实 Godot 项目。

宿主形态：`Godot.NET.Sdk/4.7.2` + `net10.0` 的 Godot **C#** 项目，通过 `OS.GetCmdlineUserArgs()` 接收 `--` 之后的 mod 目录列表，调用 `ModLoader.LoadMods`，逐条打印 `PASS:`/`FAIL:`，最后 `GetTree().Quit(failCount == 0 ? 0 : 1)`。

runner 用 `_console.exe` 而不是主 exe：Windows 上主 exe 是 GUI 子系统、不挂 stdout，抓不到断言输出。

### D3 fixture 分两类：1 个编译型 mod + 多个纯 XML fixture

- **编译型（1 个）**：`tests/e2e/Mods/AlphaMod/`，用 `Godot.NET.Sdk/4.7.2` + `net10.0` 构建（这与真实 mod 作者的构建方式一致，是 spec 的 Mod fixture realism 要求）；项目目录**本身就是 mod 目录** —— `Mod.xml`、`Data/`、`Patches/` 入库，`Assemblies/` 由构建产出并 gitignore。
- **纯 XML（多个）**：`tests/e2e/fixtures/<名字>/Mod.xml`（可带 `Data/`、`Patches/`），**不参与编译**。加载顺序与失败矩阵只需要"多个 mod 的组合"，不需要任何程序集：重复 ID、缺依赖、循环依赖、互相不兼容、补丁目标、属性设置/移除、条件补丁 —— 全部靠改 XML 就能覆盖。

这一分工是本设计的核心判断：**"创建 Mod"的项目只需要一个，而"被加载的 Mod"需要多个**。若把失败路径也塞进编译型项目，会为了覆盖它们而编译一堆空程序集，纯属浪费。

### D4 mod 目录的 `Assemblies/` **只放 mod 自己的程序集**

`Mod.LoadAssemblies` 把 mod 目录 `Assemblies/` 下的**所有** `*.dll` 递归 `LoadFromAssemblyPath` 到 Modot 所在的同一个 `AssemblyLoadContext`。若把 `dotnet build` 的整个输出目录拷进去，里面还带着 `Modot.dll`、`GDSerializer.dll`、`GDLogger.dll`、`GodotSharp.dll` → 同名程序集在同一上下文二次加载冲突。

因此 runner 的组装步骤必须**只拷贝 mod 自己的输出程序集**（`AlphaMod.dll`），而不是整个 `bin/`。这条同时把 `own-dependency-tree` 里记录的"mod 不要附带 `GDSerializer.dll`"警告变成**实测验证**，并连带回答一个开放问题：Modot 是否值得对"已加载的同名程序集"更宽容（本变更不改 `src/`，若测试暴露出真实需求，另起变更）。

### D5 引擎位置注入，不硬编码

runner 从环境变量 `MODOT_GODOT` 取引擎可执行文件路径，未设置时回落到本机默认位置。**版本控制的脚本里不出现硬编码绝对路径**（这与 `AGENTS.md` 的约定一致）。宿主另行打印引擎版本，使"跑的是哪个引擎"在输出里可见。

替代方案：把路径写进脚本 —— 放弃，换机器即失效，且违反仓库既有约定。

### D6 生成物一律 gitignore

需要忽略：`tests/e2e/Mods/*/Assemblies/`、`tests/e2e/Mods/*/obj/`、`tests/e2e/Mods/*/bin/`、`tests/e2e/Mods/*/.godot/`、`tests/e2e/Host/obj/`、`tests/e2e/Host/bin/`、`tests/e2e/Host/.godot/`。既有规则已覆盖 `obj/`、`bin/`、`.godot/`，因此只需补 `Assemblies/` 一条。

### D7 mod 与宿主如何引用 Modot

两者都用 `ProjectReference` 指向 `src/Modot/Modot.csproj`（开发期形态）。为保证引擎能找到依赖程序集，两者都保留 `CopyLocalLockFileAssemblies=true`（上游 README 记载的 Godot bug #42271）。宿主额外需要在运行目录里拥有 Modot 及其依赖，这正是 `CopyLocalLockFileAssemblies` 的作用。

## Risks / Trade-offs

- [e2e 强依赖本机引擎版本] → 引擎路径注入 + 宿主打印版本；版本不符时输出可见，而不是静默用错引擎。
- [Godot 的 C# 项目首次启动可能尝试自行构建解决方案] → runner 先用 `dotnet build` 预构建，再以 `--headless` 启动，避免依赖引擎的构建行为。
- [headless 下 mod 程序集能否加载尚未验证] → 这正是本变更要验证的内容；任务把"首次跑通"单列，若失败则作为阻塞上报而不是绕过。
- [`Mod.LoadAssemblies` 对同名程序集不宽容] → D4 用组装约束规避；若实践中仍是真实痛点，另起变更改 `src/`，本变更不夹带产品改动。
- [纯 XML fixture 与真实 mod 的差距] → 编译型 fixture 保证"有代码的真实形态"被覆盖，XML fixture 只负责组合与失败矩阵，两者分工写进 `tests/README.md`。
- [测试与产品代码不同步演进] → 两层都进 `Modot.sln`，构建即暴露破坏；`src/` 的破坏性变更会直接让宿主或 mod 编译失败。

## Migration Plan

一次性引入，无灰度。回滚：删除 `tests/` 下的新增内容并撤销 `Modot.sln` 与 `.gitignore` 的相应条目；`src/` 未被触碰，因此回滚不影响产品。

## Open Questions

（无。会影响规格、方案与任务拆分的未知项均已实测或由用户决定。）
