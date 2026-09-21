# Tasks

## 1. 仓库布局

- [x] 1.1 把 `Modding/`、`Utility/` 迁到 `src/Modot/`，`Modot.csproj` 迁到 `src/Modot/Modot.csproj`；验证：逐文件 `git hash-object` 对比 `HEAD`，所有 `.cs` 的 blob 哈希一致（证明是纯搬迁），且仓库根目录不再有 `.cs` 源文件
- [x] 1.2 把 csproj 中的根级文件引用改写为 `../../`；验证 `dotnet msbuild src/Modot/Modot.csproj -getItem:None -v:q` 解析出 `../../LICENSE`，其 `Pack` 为 `true`、`FullPath` 指向仓库根的 `LICENSE`
- [x] 1.3 新增根级 `Modot.sln` 并注册 `src/Modot/Modot.csproj`；验证 `dotnet build Modot.sln` 退出码为 0。附带发现：.NET 10 的 `dotnet new sln` 默认产出 `.slnx`，需显式传 `--format sln`；产出的 `.sln` 自带 BOM，已按仓库 UTF-8 无 BOM 约定去除
- [x] 1.4 按 design.md - D7 重写 `.gitignore`；验证 `git check-ignore` **双向**通过：10 个必需路径全部不被忽略，32 个噪声路径全部被忽略
- [x] 1.5 验证 `git status --porcelain` 不再报告 `.vs/`、`.omo/`、`.codegraph/`，并把 `Modot.sln`、`openspec/`、`.opencode/` 列为新文件
- [x] 1.6 搬迁取证（**不是**"迁移前基线为绿"，后者在本机不可完成，原因见 design.md - Context）：(a) 搬迁后所有 `.cs` 的 blob 哈希与 `HEAD` 一致；(b) 在 `HEAD` 的临时工作树中构建，失败信息与搬迁后**完全相同**（`MSB3245` 未解析 `GodotSharp`/`GodotSharpEditor` 加相同 `CS0246`），证明失败源于本机缺少 Godot 3 编辑器而非搬迁

## 2. 重定目标到 Godot 4.7.2 / net10.0

- [x] 2.1 把 `Godot.NET.Sdk/3.3.0` 改为 `4.7.2`、`netstandard2.1` 改为 `net10.0`；验证 `dotnet restore` 退出码为 0，`GodotSharp` 改由 NuGet 解析
- [x] 2.2 验证剩余错误只有两类且均属预期：6 处 `CS0246`（`Directory`，Godot 4 已移除，由任务组 4 处理）与 `CS0104`（`OrderedDictionary`，见 design.md - D8）；原先针对 `Godot.Error` 的 `CS0246` 已全部消失，证明 Godot 4 API 已进入编译范围
- [x] 2.3 验证编译输出中没有任何一条错误提到 `GDSerializer`
- [x] 2.4 扫描所有 `[Serialize]` 成员，确认类型只有 `string`、`XmlNode` 与自有接口（`IPatch`、`ICondition` 及它们的 `IEnumerable<T>`），没有引擎类型能流入 GDSerializer

## 3. 用仓库内 Log 取代 GDLogger

- [x] 3.1 原子完成两件事：移除 `GDLogger` 的 `PackageReference`，并新增 `src/Modot/Log.cs`（`internal static class Godot.Log`，`Write(string)` → `GD.Print`、`Error(Exception)` → `GD.PushError`）。必须原子：先加 `Log` 会与 GDLogger 的公共 `Godot.Log` 冲突（`CS0433`），先删没加会因 `Log` 缺失而失败。验证 `dotnet build` 退出码为 0
- [x] 3.2 验证 `project.assets.json` 中 `GDLogger` 出现次数为 0
- [x] 3.3 验证 6 个日志调用点零改动：`ModLoader.cs`（4 处）与 `Patching/LogPatch.cs`（2 处）的 `Log.*` 行与 `HEAD` 逐行一致。（`ModLoader.cs` 因任务 2.2 的 `CS0104` 修复有 1 行必要改动，故按调用点而非整文件比对）

## 4. 目录辅助方法迁移到 DirAccess

- [x] 4.1 把 `DirectoryExtensions.cs` 改为以 `Godot.DirAccess` 为接收者，替换 `Open`/`ListDirBegin`/`GetNext`/`CurrentIsDir`/`GetCurrentDir`/`MakeDirRecursive`/`Copy`；验证 `dotnet build` 退出码为 0
- [x] 4.2 审阅确认 `GetFiles(recursive: true)` 与 `GetDirectories(recursive: true)` 的递归分支与非递归分支实现确实不同（递归分支先取子目录、再逐个打开枚举；非递归分支只列一层）
- [x] 4.3 审阅确认跨根目录的建目录与复制使用绝对路径形态（`DirAccess.MakeDirRecursiveAbsolute`、`DirAccess.CopyAbsolute`），不依赖"实例只能在其被打开的根内操作"这一假设；并补上 `ListDirEnd()`，在枚举结束时关闭列表流
- [x] 4.4 验证 `src/Modot` 下已无 `Godot.Directory`、`Godot.File`、`Assembly.LoadFile` 的**代码**残留（仅注释与文档文字提及）

## 5. 程序集加载改用 AssemblyLoadContext

- [x] 5.1 以 `AssemblyLoadContext.GetLoadContext(typeof(Mod).Assembly) ?? AssemblyLoadContext.Default` 配合 `LoadFromAssemblyPath(Path.GetFullPath(path))` 取代 `Assembly.LoadFile`；验证 `dotnet build` 退出码为 0
- [x] 5.2 审阅确认 `src/Modot` 下唯一的 `catch` 是 `Mod.cs` 中带 `when (exception is not ModLoadException)` 过滤的那一处，无空 catch、无被静默吞掉的失败路径

## 6. 包元数据

- [x] 6.1 把 `PackageVersion` 由 `2.0.2` 改为 `3.0.0`；验证产物文件名为 `Modot.3.0.0.nupkg`
- [x] 6.2 验证 Release 包内含 `lib/net10.0/Modot.dll` 与包根 `LICENSE`，且依赖声明为 `GDSerializer 2.0.3`、`GodotSharp 4.7.2`、`JetBrains.Annotations 2022.1.0`，**无 `GDLogger`**。附带发现：Debug 包会额外声明 `GodotSharpEditor`，故发布必须打包 Release（design.md - D9）

## 7. 验证收口与遗留事项

- [x] 7.1 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0；警告只有既有的 `CS8602`（`ModLoader.cs:112`）、`CS8714`（`EnumerableExtensions.cs:36`）与既有的 `NU5119`（`.gitignore` 无法成为包内容，dotfile 被 NuGet 默认排除），无新增警告
- [ ] 7.2 **运行时验证（本机可做）**，载体是 `add-e2e-test-suite` 交付的 e2e 套件：用本机 Godot 4.7.2 headless 运行，以退出码与输出断言验证 `DirAccess` 的遍历/递归/跨根复制、`ProjectSettings.LoadResourcePack` 加载 `.pck`、`GD.Print`/`GD.PushError` 的日志输出。在该套件交付前，不得声称这些行为已验证
