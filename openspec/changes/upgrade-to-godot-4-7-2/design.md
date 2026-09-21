# Design

## Context

当前状态：Modot 2.0.2，`Godot.NET.Sdk/3.3.0`、`netstandard2.1`，源码位于仓库根的 `Modding/` 与 `Utility/`。动机见 proposal.md - Why。

已在本机对 `GodotSharp 4.7.2` 元数据核实过的事实（这些事实决定了下面的决策，不是推测）：

- `Godot.Directory` 与 `Godot.File` 类型在 4.7.2 中不存在；`Godot.DirAccess` 与 `Godot.FileAccess` 存在。
- `ProjectSettings.LoadResourcePack(string pack, bool replaceFiles = true, int offset = 0)` 返回 `bool`（文档写明 "Returns true on success"），因此 `Modding/Mod.cs` 中既有的 `!ProjectSettings.LoadResourcePack(path)` 调用**无需改动**。
- `Godot.Error` 与 `Error.Ok` 仍然存在，因此 `Utility/Extensions/ErrorExtensions.cs` 与 `ErrorException.cs` **无需改动**。
- `DirAccess` 的相关成员签名已变化：`ListDirBegin()` 不再接受 `skipNavigational`/`skipHidden`；`GetCurrentDir(bool includeDrive)` 增加参数，但默认值使其可继续以 `GetCurrentDir()` 调用；`CurrentIsDir()` 仍是方法（不是属性）；`CopyAbsolute`/`MakeDirRecursiveAbsolute` 是以绝对路径为准的静态形态。
- `GDLogger` 在 nuget.org 上仅有 1.0.0 与 1.0.1，无 Godot 4 版本。
- `GDSerializer 2.0.3` 的 nuspec 只声明 `Carnagion.MoreLinq 1.4.0` 与 `System.CodeDom 6.0.0`，其 `lib/` 只有 `netstandard2.1`；但它的 **DLL 硬引用了 `GodotSharp`**（从 nuspec 完全看不出来），并公开了 `VectorSerializer`/`NodeSerializer` 等引擎类型序列化器。详见 D10。

环境约束：本机可用 **Godot 4.7.2 (.NET / mono)**（引擎路径见 `AGENTS.md`），但**没有 Godot 3**。迁移**前**（Godot 3）连编译都不可做：`Godot.NET.Sdk/3.3.0` 通过 HintPath `$(GodotProjectDir).mono\assemblies\<config>\GodotSharp.dll` 引用 GodotSharp，也就是要求本机装有 Godot 3 编辑器，而 `GodotSharp 3.3.0` 也不在 NuGet 缓存中 —— 换言之**本项目在迁移前从未构建成功过**。迁移**后**（Godot 4）SDK 改为从 NuGet 解析 GodotSharp，编译变为可做；且因为有 4.7.2 引擎，**运行时验证同样可做**（载体是 `add-e2e-test-suite` 交付的 headless e2e 套件）。

由此推论：任务组 1 不能按"迁移前构建为绿"来验证搬迁无误。已实测替代证据 —— 在 `HEAD` 的临时工作树中构建，失败信息与搬迁后完全相同（同为 `MSB3245` + 同样的 `CS0246`），说明失败源于环境缺失而非搬迁；搬迁本身的正确性由 `git hash-object` 的逐字节一致性证明。

## Goals / Non-Goals

**Goals:**

- 在 Godot 4.7.2 + `net10.0` 上编译通过，且不改变 mod 数据格式、加载顺序与补丁语义。
- 把仓库整理为 `src/<项目名>` 布局，并让解决方案级 `dotnet build` 可用。

**Non-Goals:**

- 对 Godot 3 程序集的兼容（刻意不提供，见 proposal 的 BREAKING 条目）。
- 新功能、格式演进、以及把引擎相关路径纳入本机可运行的自动化测试。
- 重构 `ModLoader` 的加载顺序或补丁叠加语义。

## Decisions

### D1 目标框架选 `net10.0`，而不是 `net8.0`

`Godot.NET.Sdk/4.7.2` 的 `GodotSharp` 以 `net8.0` 发布，`net10.0` 消费方可以直接引用。选 `net10.0` 以对齐用户指定的运行时（本机已安装 10.0.201 与 10.0.401 SDK）。代价是 `net8.0`/`net9.0` 消费方无法引用，这是刻意的取舍并已在 proposal 中标注 BREAKING。

替代方案：固定 `net8.0` 以获得更宽的兼容面 —— 放弃，因为用户明确要求 .NET 10。

### D2 用仓库内 `Godot.Log` 取代 `GDLogger`

`GDLogger` 的 `Log` 使用 `Godot.File`，而 Godot 4 已移除该类型，且上游没有 Godot 4 版本，因此必须替换。只重建 Modot 实际用到的两个成员：`Write(string)` → `GD.Print`、`Error(Exception)` → `GD.PushError`；`GDLogger` 的写文件能力刻意不重建，并在 XML 注释中写明。

声明为 `internal static`，以避免与仍然引用 `GDLogger` 的消费方发生类型冲突；命名与命名空间保持 `Godot.Log`，使 `ModLoader.cs` 与 `LogPatch.cs` 的 6 个调用点零改动 —— 这一点由任务 3.3 用 `git diff` 验证。

替代方案：把日志抽象为可注入接口以便测试 —— 放弃，会扩大公共 API 面与改动量，超出本次范围。

### D3 `DirectoryExtensions` 迁到 `DirAccess`

Godot 4 的 `DirAccess` 不再是"可以反复 `Open` 的单一实例"：`Open(path)` 返回**新的**实例，而 `Copy`、`MakeDirRecursive` 等同时存在静态与实例两套语义。设计如下：

- 每次操作基于由源/目标路径 `Open` 出来的 `DirAccess` 实例，不再复用单一实例。
- 跨根目录的建目录与复制（例如 `res://` → `user://`）使用绝对路径形态的 API，避免"实例只能在其被打开的根内创建目录"这一 Godot 4 行为造成失败。
- 随按签名变化同步调整调用：`ListDirBegin()`（无参），并用 `ListDirEnd()` 在枚举结束时关闭列表流（原先由 Godot 3 的全局 `Directory` 隐式承担）。

接收者类型由 `Godot.Directory` 变为 `Godot.DirAccess`，属不可避免的公共 API 变更（proposal 已标注 BREAKING）。

替代方案：(a) 删除 `DirectoryExtensions` —— 放弃，公共 API 缩减更多，且 wiki 记载的 `res://`→`user://` 复制用法会失效；(b) 保留签名但内部改用 BCL `System.IO` —— 放弃，`res://` 是引擎虚拟路径，`System.IO` 无法访问。

### D4 程序集加载改用 `AssemblyLoadContext`

`Assembly.LoadFile` 会把程序集载入隔离上下文。mod 的启动方法是通过反射查找 `ModStartupAttribute` 再调用的；若 mod 程序集对 Modot 程序集的引用解析到另一份类型标识，`GetCustomAttribute<ModStartupAttribute>()` 会返回 `null`，启动方法静默不执行 —— 这是一个不报错的失败模式，必须在结构上排除。

改为通过 `AssemblyLoadContext.GetLoadContext(typeof(Mod).Assembly)`（为 `null` 时回退 `AssemblyLoadContext.Default`）取得宿主上下文，再调用 `LoadFromAssemblyPath`，保证类型标识与宿主一致。注意 `LoadFromAssemblyPath` 要求绝对路径，因此需要 `Path.GetFullPath`。

替代方案：保留 `Assembly.LoadFile` —— 放弃，其行为依赖解析顺序，存在上述静默失效风险。

### D5 布局：`src/<项目名>` + 根级解决方案

源码迁入 `src/Modot/`，`Modot.csproj` 随迁。因 csproj 中的 `<Content>`/`<None>` 引用了仓库根的 `.gitignore`、`LICENSE`、`README.md`，需要改写为 `../../` 前缀，否则打包会丢失 `LICENSE`（`PackageLicenseFile` 依赖它）。

新增 `Modot.sln` 以便解决方案级 `dotnet build`/`dotnet test` 工作。注意当前 `.gitignore` 忽略 `*.sln`，与"新增 sln 并入库"直接冲突，必须一并移除该规则。

### D6 先搬迁、后迁移，分两段验证

搬迁（任务组 1）与迁移（任务组 2 起）分开落地，且任务 1.5 要求在搬迁后、迁移前先验证 `Godot.NET.Sdk/3.3.0` 下构建仍为绿，以此把"移动文件引入的破坏"与"Godot 4 不兼容引入的破坏"区分开。

替代方案：一次性改完 —— 放弃，出问题时无法二分定位。

### D7 `.gitignore` 一次性补全（Godot / .NET / IDE），采用"宁可写全"策略

**为什么由本变更承载**：本次迁移把构建产物的中间目录从 Godot 3 的 `.mono/` 换成 Godot 4 的 `.godot/`，同时新增了必须入库的 `Modot.sln`，而当前 `.gitignore` 恰好忽略 `*.sln`。也就是说，忽略规则的修订与本次迁移强绑定：不改就写不进 sln，或者新构建产物与 IDE 目录会污染工作区。因此由本变更一次性写全，而不是留待后续逐个按需添加。

**忽略范围**（按用户要求"该忽略的部分都加入忽略"）：

- Godot 4：`.godot/`、`.import/`、`export_presets.cfg`、`*.translation`、`/android/`、`mono_crash.*.json`；并保留 Godot 3 遗留的 `.mono/`（仓库当前仍有该目录）。
- .NET：`bin/`、`obj/`、`*.nupkg`、`*.snupkg`、`TestResults/`、`coverage*.xml`、`coverage*.json`、`coverage*.info`、`*.binlog`、`*.tlog`、`artifacts/`。
- Visual Studio：`.vs/`、`*.suo`、`*.user`、`*.userosscache`、`*.sln.docstates`。
- VS Code：`.vscode/`。
- Rider / ReSharper：`.idea/`、`*.sln.iml`、`_ReSharper*/`、`*.DotSettings.user`。
- 操作系统：`.DS_Store`、`Thumbs.db`、`desktop.ini`。
- 本机工具与本地索引：`.omo/`、`.codegraph/`。

**刻意保留入库**（不得被上述规则误伤）：`.editorconfig`、`Modot.sln`、`openspec/`、`.opencode/`、`AGENTS.md`、`CONTRIBUTING.md`、`README.md`、`LICENSE`。

**取舍**：`.vscode/` 采用**整体忽略**，未采用"忽略但用 `!` 例外保留 `settings.json`/`launch.json`/`tasks.json`"这一常见惯例 —— 因为用户明确要求忽略 VS Code。若之后需要共享调试或任务配置，再把该行改成带 `!` 例外的写法即可。

**验证手段**：用 `git check-ignore` 而不是只看 `git status`。前者能同时证明"该忽略的被忽略"和"不该忽略的没被忽略"，比 `git status` 更强（见任务 1.4）。

### D8 `OrderedDictionary` 在 net10.0 下产生歧义（实测发现）

迁移到 `net10.0` 后，`ModLoader.cs` 中未限定的 `OrderedDictionary<string, Mod>` 变成歧义引用（`CS0104`）：.NET 9 起 BCL 新增了 `System.Collections.Generic.OrderedDictionary<TKey, TValue>`，与该文件通过 `using Godot.Utility;` 引入的 `Godot.Utility.OrderedDictionary<TKey, TValue>`（来自 GDSerializer）撞名。

决策：**显式限定为 `Godot.Utility.OrderedDictionary<string, Mod>`**，即保留原有类型，只做消歧。理由：这是纯歧义修复，不改变行为 —— 该字段的语义（保持插入顺序、以 `IReadOnlyDictionary<string, Mod>` 对外暴露）与枚举顺序都与原实现一致；换成 BCL 版本虽然更"现代"，但会改变实现（例如删除复杂度）而无任何收益。该改动是 `ModLoader.cs` 里唯一的一行，且不影响 6 个日志调用点（任务 3.3 按调用点而非整文件比对）。

注意：这是把目标框架抬到 `net10.0` 才会暴露的问题，`netstandard2.1` 下不存在。它也是"只做编译级验证就能发现"的一类问题里最典型的一个。

### D9 发布打包必须用 `Release`（实测发现）

`Godot.NET.Sdk/4.7.2` 的 `Sdk.targets` 只在 `$(Configuration) == 'Debug'` 时引用 `GodotSharpEditor`。实测对比同一个项目的两种包：

- Debug 包的依赖：`GDSerializer`、`GodotSharp`、**`GodotSharpEditor`**、`JetBrains.Annotations`
- Release 包的依赖：`GDSerializer`、`GodotSharp`、`JetBrains.Annotations`

`GodotSharpEditor` 是编辑器侧程序集，作为已发布库的依赖既无意义也会给消费方带去多余的还原负担。因此 `scripts/pack-local.ps1` 默认并强制使用 `Release`（design.md 属于本变更，脚本落在 `adopt-project-workflow`），并把该约束写入 AGENTS.md 与本文件，避免后来者用 Debug 打包。

### D10 `GDSerializer` 是引擎绑定的，但失配路径被隔离（实测，推翻早先判断）

**早先判断是错的。** 早先根据 nuspec 认为 `GDSerializer` 引擎无关，实际用元数据检查 `GDSerializer.dll` 后推翻：

- 其 AssemblyReferences 含 **`GodotSharp`**（`netstandard`、`GodotSharp`、`System.CodeDom`、`MoreLinq`）。nuspec 不声明该依赖，因此 NuGet 层面完全看不出来。
- 它引用的 Godot 类型只有 5 个：`Godot.Collections.Array`、`Godot.GD`、`Godot.Node`、`Godot.Vector2`、`Godot.Vector3`。
- 它发出的 Godot 成员引用里，**`Godot.Vector2.x/y` 与 `Godot.Vector3.x/y/z` 是以字段（`ldfld`）读取的**；而 `GodotSharp 4.7.2` 把这两个类型的字段改名为 PascalCase 的 `X`/`Y`/`Z`，没有小写形态。因此这些指令在 Godot 4 上绑定不上。
- `GodotSharp 4.7.2` **没有强名称**（public key token 为空），所以旧的 `GodotSharp 1.0.0.0` 引用仍会按简单名绑定到 `4.7.2.0` —— 失败被推迟到真正执行那条指令的时刻，这正是它不易被发现的原因。

**引擎无关的实测证据**（无引擎，直接引用 `Modot.dll` + `GodotSharp 4.7.2` 运行）：

- `Mod.Metadata.Load(Mod.xml)` **成功**（Modot 真实元数据路径）。
- 标量 POCO 的 `Serializer` 往返 **成功**。
- 含 `Godot.Vector2` 成员的 POCO **按预期失败**：`SerializationException: Could not serialize object of Type "Godot.Vector2"`，内部 `MissingFieldException: Field not found: 'Godot.Vector3.x'`，栈顶为 `VectorSerializer.Serialize`。

**结论**：失配被隔离在 `VectorSerializer`（以及 `NodeSerializer`），Modot 自身的可序列化面（只有 `string`、`XmlNode`、自有接口，见任务 2.4）**不经过**该路径，所以本变更范围内**保持可用**；而且一旦被触及是**抛异常**而不是静默写坏数据。

**决策**：本变更**不**处理该依赖，只如实记录。理由：(a) Modot 的使用路径已实测正常；(b) 修好它需要把上游源码 vendor 进仓库并决定发布形态（独立 fork 包 / 吸收进 `Modot.dll` / 仅开发期 `ProjectReference`），涉及 `Godot.Serialization.*` 与 `Godot.Utility.OrderedDictionary` 的**类型标识**问题（这些类型出现在 Modot 的公共元数据里，且 mod 程序集在运行时按 `AssemblyLoadContext` 加载），属独立决策，应另起变更。风险与取舍已在下方列出。

## Risks / Trade-offs

- [`GDSerializer` 的向量/节点序列化在 Godot 4 上已坏（实测），而 Modot 的公共元数据暴露了它的 `[Serialize]` 特性与 `VectorSerializer`/`NodeSerializer`] → 本变更如实记录并在 AGENTS.md 加上警告；mod 作者若把引擎类型放进 `[Serialize]` 成员会拿到 `MissingFieldException`。彻底解决需另起变更决定 vendor 形态与发布方式（见 D10）。

- [无法在本机取得迁移前的"构建为绿"，因此不能用它证明搬迁无损] → 按 Context 用两项替代证据替代：`git hash-object` 逐字节一致，以及在 `HEAD` 的临时工作树中构建得到相同失败信息。
- [`DirAccess` 与 Godot 3 的 `Directory` 语义不完全等价，且本机无法运行时验证] → 逐分支实现并保留"递归与非递归两个分支必须不同"的自检（任务 4.2）；运行时行为作为遗留事项交给装有引擎的机器（任务 7.2）。
- [运行时行为需要引擎，而本变更交付时 e2e 套件尚未落地] → 本机有 Godot 4.7.2，这类验证交给 `add-e2e-test-suite` 交付的 headless e2e 套件执行，以退出码与输出为证据；在该套件交付前，本变更不得声称这些行为已验证。
- [移除 `GDLogger` 会丢失写文件日志] → 刻意取舍，在 `Log` 的 XML 注释中说明。
- [`net10.0` 收窄消费方范围] → 刻意取舍，已在 proposal 标注 BREAKING。
- [修改 `.gitignore` 取消忽略 `*.sln` 可能让本机其他临时 sln 进入版本控制] → 通过任务 1.4 用 `git status --porcelain` 确认只有预期的 `Modot.sln` 出现。

## Migration Plan

一次性迁移，无灰度发布。回滚方式：`git revert` 本次提交；本次变更前的仓库状态即上游 `stable`。

## Open Questions

（无。所有会影响规格、方案与任务拆分的未知项均已在本机核实。）
