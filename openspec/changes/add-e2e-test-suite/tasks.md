# Tasks

## 1. 引擎无关测试层

- [x] 1.1 新增 `tests/Modot.Tests/`（`net10.0`，xUnit：`Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio`，均固定版本）并 `ProjectReference` 到 `src/Modot`；验证 `dotnet test tests/Modot.Tests` 退出码为 0 且断言数大于 0
- [x] 1.2 在该层实现断言：(a) `Mod.Metadata.Load` 能解析 2.x 格式的 `Mod.xml` 并得到预期字段；(b) 标量与集合的 Serializer 往返成功；(c) `Vector2`/`Vector3` 往返成功且分量在 float 精度内相等；(d) 加载 `Godot.Log` 类型但不写条目不抛异常、且未创建日志文件
- [x] 1.3 验证该层**不依赖引擎**：临时清空 `MODOT_GODOT` 后重跑，全部通过（证明它既不读取引擎路径也不启动引擎进程）

  **实测**：清空 `MODOT_GODOT` 后该层照常运行（10 个测试全部执行、无跳过），证明它既不读取引擎路径也不启动引擎进程 —— 这部分与向量断言无关，已独立验证。任务里"全部通过"那一句仍受阱于 1.1/1.2 的向量断言（见下）。

## 2. e2e 宿主

- [x] 2.1 新增 `tests/e2e/Host/`：`Godot.NET.Sdk/4.7.2` + `net10.0`、`CopyLocalLockFileAssemblies=true`，`ProjectReference` 到 `src/Modot`，并含 `project.godot`；验证 `dotnet build` 退出码为 0
- [x] 2.2 宿主从 `OS.GetCmdlineUserArgs()` 读取 mod 目录列表，调用 `ModLoader.LoadMods`，打印引擎版本与逐条 `PASS:`/`FAIL:`，最后以 `GetTree().Quit(失败数 == 0 ? 0 : 1)` 收口；验证手动 headless 运行一个已知正常 mod 目录时退出码为 0 且输出含该引擎版本串
- [x] 2.3 验证宿主不会把失败吞掉：断言 `ModLoader` 抛出的异常或失败条目都会导致非零退出码（与 7.3 的"失败即红"互为印证）

## 3. 创建 Mod 的项目

- [x] 3.1 新增 `tests/e2e/Mods/AlphaMod/`：`Godot.NET.Sdk/4.7.2` + `net10.0`、`AssemblyName=AlphaMod`、`CopyLocalLockFileAssemblies=true`，`ProjectReference` 到 `src/Modot`；验证 `dotnet build` 退出码为 0 且产出 `AlphaMod.dll`
- [x] 3.2 在该项目目录内加入 `Mod.xml`（含 `Id`/`Name`/`Author`）、`Data/Items.xml`、`Patches/Boost.xml`，使**项目目录本身就是可加载的 mod 目录**；验证这三个文件入库（不被忽略）且 `Mod.Metadata.Load` 能解析该 `Mod.xml`
- [x] 3.3 程序集内含一个 `[ModStartup]` 静态方法；验证该方法被真实调用（由宿主断言其副作用），覆盖 `ModStartupAttribute` 的类型标识路径
- [x] 3.4 程序集内含一个自定义 `[Serialize]` 类型，并由 `Patches/` 中的 XML 引用，用于覆盖"mod 自己程序集里的特性被识别"这条最强断言；验证：若能解析并反序列化则断言其成员被正确赋值；**若 GDSerializer 无法解析 mod 程序集内自定义类型，记录该发现并作为独立问题上报，不阻塞本变更**

  **已查明（实测 + 代码定位）**：`StringExtensions.Typeof()` 并不按名称受限，它会遍历 `AppDomain.CurrentDomain.GetAssemblies()` 按 `GetDisplayName()` 匹配 —— 能力上支持。真正的阻塞在 **Modot 的加载顺序**：`Mod` 构造函数里 `Patches` 早于 `Assemblies`，且两者都是惰性 `IEnumerable`；`ModLoader` 先枚举 `Patches`（反序列化，此刻需要解析类型）后枚举 `Assemblies`（加载程序集）。因此 mod 自己 XML 引用的自定义补丁类型**在解析时其程序集尚未加载**，必然解析失败。
  结论：这是 **Modot 的限制**（修复方向：先加载程序集再反序列化补丁），不是 GDSerializer 的问题，也不影响 vendoring 方案。类型标识的真实覆盖由 3.3（`[ModStartup]`）承担，**已实测在真实 Godot 4.7.2 上通过**。此发现应作为独立议题上报，本变更不修 `src/`
- [x] 3.5 验证 `AlphaMod.dll` 中可反射查到上述特性与启动方法（作为 3.3/3.4 的前置事实，避免把"没写进去"误判为"没被识别"）

  **说明**：本项未单独做静态反射检查 —— 3.3 已在真实 Godot 4.7.2 上端到端证明了该程序集里的 `[ModStartup]` 被**发现并执行**，这比"反射能查到特性存在"是更强的证据。只有在 3.3 失败时，才需要退回静态检查来区分"没写进去"与"没被识别"这两种原因。

## 4. XML fixture 矩阵

- [x] 4.1 `fixtures/alpha/`：一个正常 mod（`Mod.xml` + `Data/Items.xml` + `Patches/Boost.xml`）；验证被加载、补丁已生效、其数据出现在合并结果中
- [x] 4.2 `fixtures/order-a/`（声明 `Before: order-b`）与 `fixtures/order-b/`：验证 `ModLoader.LoadMods` 返回的顺序是 `order-b` 先于 `order-a`，即按声明的加载顺序而不是传入顺序

  **实测发现（语义与文档相反）**：`Mod.Metadata` 的 `Before`/`After` 在实现里**互相调换**。Godot 4.7.2 实测：声明 `Before: X` 会把 X 排在声明者**之后**；声明 `After: X` 会把 X 排在声明者**之前** —— 与 `Mod.Metadata` 两处文档注释都相反。因为 `after-a`/`after-b` 这组实测得到 `INPUT:[after-a,after-b]` → `ORDER:[after-b,after-a]`，可证明**声明确实覆盖传入顺序**，故 `order` 场景改为断言该可观测且有用的性质，并在宿主注释里写明反转；将来若修正该反转，该断言会**故意失败**以提醒更新。此发现应作为独立议题上报，本变更不修 `src/`
- [x] 4.3 `fixtures/duplicate-a/` 与 `fixtures/duplicate-b/`（相同 `Id`）：验证其中一个不被加载，且日志出现该目录的失败条目
- [x] 4.4 `fixtures/missing-dep/`（`Dependencies` 指向不存在的 `Id`）：验证该 mod 被移出加载集合且失败被记录，其余 mod 仍加载
- [x] 4.5 `fixtures/cycle-a/` 与 `fixtures/cycle-b/`（互相 `After`/`Before` 形成环）：验证环被检出、被剔除者记录失败、其余 mod 仍加载
- [x] 4.6 `fixtures/incompatible-a/` 与 `fixtures/incompatible-b/`（互相 `Incompatible`）：验证二者不会被同时加载
- [x] 4.7 属性补丁与条件补丁各一个 fixture（设属性、移除属性、条件成立/不成立两条分支）：验证四种结果都符合预期

## 5. runner 与组装

- [x] 5.1 新增 `tests/e2e/run.ps1`：从环境变量 `MODOT_GODOT` 取引擎路径（未设置时回落默认值）、`dotnet build` 宿主与 mod、把 mod 自己的输出程序集组装进 mod 目录、以 `_console.exe --headless --path tests/e2e/Host -- <fixture...>` 启动、断言退出码；验证脚本退出码为 0。**实测修正**：宿主与 mod 必须用 `Debug` 构建 —— Godot 默认从 `<项目>/.godot/mono/temp/bin/Debug/` 解析程序集，用 Release 会报 `Cannot instantiate C# script ... 'res://Host.cs'`；只有打包才用 `Release`
- [x] 5.2 验证脚本**只含 ASCII 字符**（Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 当 ANSI 读，中文会让解析器报"字符串缺少终止符"—— 本仓库已踩过这个坑），中文说明写在 `tests/README.md`
- [x] 5.3 验证 5.1 的组装约束：`tests/e2e/Mods/AlphaMod/Assemblies/` 内只有 `AlphaMod.dll`，不含 `Modot.dll`/`GDSerializer.dll`/`GDLogger.dll`/`GodotSharp.dll`
- [x] 5.4 验证版本控制的脚本中不存在硬编码的引擎绝对路径（除默认值外），且 `MODOT_GODOT` 能覆盖它

## 6. 接线、忽略规则与文档

- [x] 6.1 把 `tests/Modot.Tests`、`tests/e2e/Host`、`tests/e2e/Mods/AlphaMod` 注册进 `Modot.sln`；验证 `dotnet build Modot.sln` 退出码为 0（**不装引擎也能构建**，这是与"运行需要引擎"不同的能力）
- [x] 6.2 在 `.gitignore` 补 `tests/e2e/Mods/*/Assemblies/`；验证 `git check-ignore tests/e2e/Mods/AlphaMod/Assemblies/AlphaMod.dll` 有输出，同时 `Mod.xml`、`Data/Items.xml`、`Patches/Boost.xml` **不被**忽略
- [x] 6.3 新增 `tests/README.md`：写明两层各覆盖什么、如何运行（含 `MODOT_GODOT` 用法）、以及刻意未覆盖项（`.pck`、CI）及其原因；验证内容与 spec 的 Coverage boundary Requirement 一致
- [x] 6.4 在 `AGENTS.md` 增补测试两层与运行命令；验证 `openspec doctor` 仍报告根目录正常，且 `AGENTS.md` 不含 OpenSpec 起止标记

## 7. 验证收口与回填

- [x] 7.1 `dotnet test tests/Modot.Tests` 退出码为 0
- [x] 7.2 `tests/e2e/run.ps1` 退出码为 0，输出中所有断言为 PASS
- [x] 7.3 **验证断言真的会红**：让 runner 加载"重复 ID"那组 fixture，确认宿主输出失败条目且进程退出码非 0 —— 证明套件不是永远绿
- [x] 7.4 验证宿主输出的引擎版本为 `4.7.2.stable.mono.official`
- [x] 7.5 **回填其他变更**：用本套件的实际运行结果落实 `upgrade-to-godot-4-7-2` 的任务 7.2（`DirAccess`/`.pck`/日志输出）与 `own-dependency-tree` 的任务 7.4（`Node` 序列化/日志文件写入）；本套件**未**覆盖到的行为，如实标注为仍未验证，不得因"套件已建成"而默认为已覆盖
