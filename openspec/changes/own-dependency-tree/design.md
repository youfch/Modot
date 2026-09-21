# Design

## Context

动机见 proposal.md - Why。以下均为本机实测事实，不是推测。

**上游源码与许可**

- `github.com/Carnagion/GDSerializer` 与 `github.com/Carnagion/GDLogger` 均公开、可克隆，`HEAD == tag`（`v2.0.3` / `v1.0.1`），源码完整可重建。
- 两者 `LICENSE` 均为 **MIT，Copyright (c) 2022 Carnagion** → vendor 合法，义务是保留许可证与版权声明。
- 两者的 `csproj` 都用 `Godot.NET.Sdk/3.3.0` + `netstandard2.1`；`GDSerializer` 依赖 `Carnagion.MoreLinq 1.4.0` 与 `System.CodeDom 6.0.0`，`GDLogger` 无包依赖。
- 上游最后提交：`GDSerializer` 2022-08、`GDLogger` 2022-06；`Carnagion.MoreLinq` 唯一最新版本就是 2024 年前的 1.4.0（无从升级）。

**引擎绑定的确切范围（对两个 DLL 做元数据检查得到）**

- 两个 DLL 的 AssemblyReferences 都含 **`GodotSharp`**，而两者 nuspec **都不声明**该依赖。
- `GDSerializer` 的全部 Godot MemberReference 只有 13 个，集中在两处：
  - `VectorSerializer` 以**字段**读取 `Vector2.x`/`.y` 与 `Vector3.x`/`.y`/`.z`（`VectorSerializer.cs:35,39`）。
  - `NodeSerializer` 调用 `Node.GetChildCount()`、`Node.GetChildren()`、`Node.AddChild(child)`，并用 `Activator.CreateInstance` 实例化节点。
- `GodotSharp 4.7.2` 把 `Vector2`/`Vector3` 的公共字段改名为 `X`/`Y`/`Z`（无小写形态）；`Godot.Collections.Array` 这个 TypeReference 只是 `Node.GetChildren()` 的返回类型（Godot 3 返回 `Godot.Collections.Array`，Godot 4 返回 `Array<Node>`）。`ArraySerializer` 面向的是 **BCL 数组**，与 Godot 的 `Array` 无关。
- `GodotSharp 4.7.2` **没有强名称**（public key token 为空），且 `GDSerializer`/`GDLogger` 的程序集版本都是 **`1.0.0.0`**，所以旧的 `GodotSharp 1.0.0.0` 引用仍按简单名绑定成功 —— 失败被推迟到执行那一刻。
- `GDLogger.Log` 整体建立在 Godot 4 已移除的 `Godot.File` 上（`new File()`、`Open`、`ModeFlags.Write`、`StoreLine`、`Flush`、`IsOpen`、`GetPathAbsolute`、`Close`、`Dispose`），并有一个 `static Log()` 构造在类型初始化时立即打开文件、挂 `AppDomain` 事件。
- 移植所需的 Godot 4 对应物全部存在：`FileAccess.Open(path, ModeFlags)`、`FileAccess.ModeFlags.Write`、`StoreLine`、`Flush`、`Close`、`GetPathAbsolute`，以及 `GD.Print`/`GD.PushWarning`/`GD.PushError`。

**引擎无关实验（直接引用 `Modot.dll` + `GodotSharp 4.7.2` 运行，无需引擎）**

```
A OK             Mod.Metadata.Load(Mod.xml) -> Id=alpha, Name=Alpha Mod, Author=probe
B OK             scalar round-trip -> Text=hello, Count=3
C EXPECTED-FAIL  SerializationException: Could not serialize object of Type "Godot.Vector2".
                 System.MissingFieldException: Field not found: 'Godot.Vector3.x'.
                    at Godot.Serialization.Specialized.VectorSerializer.Serialize(...)
```

结论：Modot 当前的使用路径在 Godot 4 上正常，失配被隔离在 `VectorSerializer`/`NodeSerializer`，且触及即**抛异常**而非静默写坏数据。

**依赖的真实用法（对编译产物的成员引用检查得到，而不是靠源码 grep）**

- `System.CodeDom`：**必需，不能删**。它由三个成员承载 —— `Microsoft.CSharp.CSharpCodeProvider..ctor`、`System.CodeDom.CodeTypeReference..ctor`、`System.CodeDom.Compiler.CodeDomProvider.GetTypeOutput` —— 被 `Utility/Extensions/TypeExtensions.cs` 的 `GetDisplayName()` 使用：
  ```csharp
  using CSharpCodeProvider provider = new();
  return provider.GetTypeOutput(new(type));
  ```
  该方法的输出会被写进序列化 XML 的 `Type=` 属性（`NodeSerializer`、`ArraySerializer` 都这么做），并在反序列化时被 `GetTypeToDeserialize()` 用作解析目标类型的判据 —— 也就是说它处在**补丁类型解析链路**上，属 mod 数据兼容面。
- `Carnagion.MoreLinq` 提供的类型是 `System.Linq.EnumerableExtensions`（**刻意放在 `System.Linq` 命名空间里、看起来像 BCL**）：Modot 用到 `ForEach`/`Indistinct`/`NotNull`，`GDSerializer` 用到 `ForEach`/`IndexOf`/`Join`，**合计 5 个方法**。
- `JetBrains.Annotations 2026.2.0`（最新稳定）仍以 `lib/netstandard2.0` 提供普通引用程序集，`netstandard2.0` 目标组无依赖；`2022.1.0` 只引 `mscorlib`。

**方法学教训（本变更踩过两次，记下来避免再犯）**

两次"基于源码文本搜索"的结论都是错的：(a) 早先只看 nuspec 就判定 `GDSerializer` 引擎无关，实际其 IL 引用了 `GodotSharp`；(b) 用 `CodeDom|System\.CodeDom` 搜源码得出"零命中"，实际它通过 `Microsoft.CSharp.CSharpCodeProvider` 与 `CodeTypeReference` 使用该包 —— **搜索模式来自命名空间猜测，覆盖不到真实符号名**；而同一次搜索"只用到 3 个 MoreLinq 方法"也漏掉了 `IndexOf` 与 `Join`，实际是 5 个。

此后凡"某依赖是否被使用、用了哪些成员"的判断，一律以**编译产物的成员引用**或**编译器本身**为准，不以文本搜索为准。

**类型标识约束**

- `Modot.dll` 的元数据引用了 `GDSerializer :: Godot.Serialization.SerializeAttribute`、`AfterDeserializationAttribute`、`Serializer` 与 `Godot.Utility.OrderedDictionary<TKey,TValue>` —— 这些类型出现在 Modot 的公共 API 表面上。
- Modot 的程序集加载已是 `AssemblyLoadContext.GetLoadContext(typeof(Mod).Assembly)`，即 mod 程序集与 Modot **共用同一个加载上下文**，同名程序集在该上下文内只加载一次，类型标识可以统一。这是本方案能成立的前提。

## Goals / Non-Goals

**Goals:**

- 让引擎相关的两条路径（序列化器的 `Vector`/`Node`、日志器的文件写入）在 Godot 4 上真正可用。
- 让被冻结的上游依赖进入本仓库的可控范围，并消除"nuspec 看不出 `GodotSharp` 耦合"这类隐形依赖。
- 只声明真实需要的依赖，去掉死依赖与纯编译期依赖的外泄。
- 全程**不改变 mod 数据格式**、**不要求 mod 重编译**。

**Non-Goals:**

- 不支持 Godot 3 程序集（已在 `upgrade-to-godot-4-7-2` 排除）。
- 不 vendor `Carnagion.MoreLinq`（改为在仓库内实现那 5 个方法）。
- 不搬运 `GDLogger` 的 `Log.gd`；不新增序列化能力；不发布到 nuget.org；不改动补丁与加载顺序语义。

## Decisions

### D1 vendor 上游源码，而不是保留 NuGet 包或自行重写

- **保留 NuGet**：Modot 现用路径实测正常，所以这确实可行；但两个包都随 Godot 3 冻结且永不修复，而 Modot 的公共 API 又暴露了序列化器的类型，等于长期向 mod 作者提供一个部分损坏的能力。
- **自行重写**：序列化器真正的输入包括 mod 作者自定义的 `IPatch`/`ICondition` 类型，要保真重现反射式、特性驱动、支持接口多态与 `[AfterDeserialization]` 的语义，风险与工作量远高于"改 2 行 + 重编译"；删掉 `Godot.Serialization.*` 还会破坏公共 API，迫使所有既有 mod 重编译。
- **vendor**：许可证允许、源码完整、改动量极小，且能顺带把隐形依赖显性化。

**结论：两个包都 vendor。**

### D2 新包名，但**保留程序集名**

包名与程序集名必须分开：

- **程序集名必须保持 `GDSerializer` / `GDLogger`**。按上游编译的 mod 与消费方引用的是这两个程序集名；改名会产生**两份类型标识**，使 `[Serialize]` 特性匹配失败并**静默失效**。
- **包名改用 `Modot.GDSerializer` / `Modot.GDLogger`**。上游在 nuget.org 上拥有原包名，我们不该也不需占用；新包名避免同一次还原中的冲突，同时程序集名不变以保住类型标识。
- **保持不签名**（简单名统一是本方案的前提，加签名会引入版本强绑定），且**保持 `AssemblyVersion` 为 `1.0.0.0`**，与上游完全一致，而不是只依赖"简单名统一"这一层保障。
- `src/Modot` 使用常驻 `ProjectReference`，让 `dotnet pack` 自动把两个 vendored 包写进 Modot 的依赖声明。

替代方案：沿用上游包名并把版本抬高投本地源以"遮蔽"上游 —— 透明但对未来发布到公开源是死路，且依赖消费方的源优先级，故不采用。

### D3 修复引擎处理器，而不是删除它们

按用户决定（"一起适配"），保留能力并把它修好：

- `VectorSerializer`：`vector2.x`/`.y`、`vector3.x`/`.y`/`.z` 改为 `X`/`Y`/`Z`。这是**唯一必须改的源码**，只涉及 `VectorSerializer.cs:35,39` 两行；序列化出的文本形态 `(x, y)`/`(x, y, z)` **保持不变**。
- `NodeSerializer`：源码很可能无需改动 —— 它调的三个方法在 Godot 4 中都存在且都带可选尾参；真正的病因是**绑定**（`GetChildren()` 的返回类型在签名里变了）。重新针对 Godot 4.7.2 编译即可让编译器绑定新签名。是否需要改源码由编译错误决定，不预先猜。

替代方案：删除引擎处理器以获得"物理上不可能失败"的引擎无关内核 —— 更干净且本机可完全验证，但会删除公共类型并永久放弃能力，与用户意图相反。

### D4 不把源码吸收进 `Modot.dll`

吸收进单包看似最省事，却是本方案里最危险的一条：Modot 自身也会暴露 `Godot.Serialization.SerializeAttribute`，任何同时引用上游 `GDSerializer` 的消费方或 mod 会持有另一份该类型；Modot 用 `GetCustomAttribute<SerializeAttribute>()` 按类型标识匹配，两份不相等 → 返回 `null` → 补丁与元数据**不报错地失效**。故排除。同样的推理适用于日志器：若把 `Log` 吸收进 Modot 而消费方仍引用上游 `GDLogger`，`Godot.Log` 也会分裂成两个类型。

### D5 日志器：vendor + 适配 + 恢复能力，并把文件句柄改为惰性

按用户决定恢复完整能力（文件日志、三级严重度、`EntryWritten` 事件），同时必须解决上游的一个结构性问题：它的 `static Log()` 构造在**类型初始化时**就打开 `user://Log.txt` 并挂 `AppDomain` 钩子，这意味着只要碰到 `Log` 类型就会做 I/O —— 在没有引擎的进程里会直接抛 `TypeInitializationException`。

决策：把文件句柄改为**惰性获取**——类型初始化不做 I/O，首次真正写条目时才打开文件；`FilePath` 仍可设置以改变落点。这样公共 API 与能力**完全保留**（spec 的 File logging 与 Severity/event 两个 Requirement），只是把"何时打开文件"从类型初始化推迟到首次写入。副作用严格更优：不再有"没用日志却创建了文件"的行为，也让类型加载在无引擎环境中安全。

移植对应关系：`new File()` → 可空的 `FileAccess?`；`File.Open(p, ModeFlags.Write)` → `FileAccess.Open(p, FileAccess.ModeFlags.Write)`；`IsOpen()` → 判 `null`；`StoreLine`/`Flush`/`Close`/`GetPathAbsolute` 同名直译；`Dispose` → `GodotObject.Dispose`。

### D6 在仓库内实现那 5 个扩展方法并去掉 `Carnagion.MoreLinq`

整套项目实际只用到 **5 个**方法：Modot 的 `ForEach`/`Indistinct`/`NotNull` 与 `GDSerializer` 的 `ForEach`/`IndexOf`/`Join`。为一个 2022 年后不再更新的上游包承担这份依赖不值得；在仓库内实现这 5 个扩展方法即可消除它。

注意两点：**其一**，这些方法由上游放在 `System.Linq.EnumerableExtensions` 这个类型里，命名空间与 BCL 的 `System.Linq` 重合 —— 因此替换实现要么也放在 `System.Linq` 命名空间（与上游行为一致、调用点零改动），要么放进自有命名空间并补齐 `using`；**其二**，`.Indistinct()` 是上游自定义语义（"存在重复"），不能想当然地用 `Distinct().Count()` 之类的 BCL 组合替换，必须按其实际语义实现。

**是否需要更多方法由编译器说了算，不靠枚举**：删掉 `PackageReference` 后构建，缺什么补什么 —— 因为本次已经证明"按名字猜"的枚举会漏（漏了 `IndexOf`/`Join`）。

### D7 保留 `System.CodeDom`，且刻意不升级版本

先前曾据一次源码文本搜索把它判为"死依赖"并决定删除 —— **该判断是错的**，实测其成员引用被 `TypeExtensions.GetDisplayName()` 使用，而该方法的输出进入序列化 XML 的 `Type=` 属性、参与补丁类型解析（见 Context）。删除它会导致类型名生成失败，进而破坏补丁反序列化。

因此决策：**保留 `System.CodeDom`**。同时**刻意不升级它的版本**（当前 `6.0.0`）：它驱动的正是那个被写进 mod 数据、且必须保持稳定的类型名文本，而升级它没有任何收益，属于"碰了只会引入风险"的改动。若将来确有升级需求，必须与 `GetDisplayName` 的文本形态回归一起评估。

顺带一提：它在 .NET 5+ 上**不是**共享框架的一部分，必须作为 NuGet 包显式引用（只有在 .NET Framework 上它才内建于框架）—— 所以它并非"免费的系统库"，但它是 Microsoft 维护的包，无供应链或弃维护风险。

### D8 `JetBrains.Annotations` 升到 `2026.2.0` 并加 `PrivateAssets="all"`

`2022.1.0` → `2026.2.0`（最新稳定；`netstandard2.0` 目标组无依赖，仍是普通引用程序集，`[PublicAPI]`/`[UsedImplicitly]`/`[MustUseReturnValue]` 均仍在）。同时加 `PrivateAssets="all"`：它是纯编译期特性包，`Modot.dll` 的元数据里引用它的特性并不需要消费方在运行时解析它，因此不该出现在发布包的依赖声明里（当前它确实在，逼消费方还原一个运行时无用的包）。

### D9 删除 `src/Modot/Log.cs` 垫片

vendored `GDLogger` 提供的是 `public static class Godot.Log`，而 Modot 当前的垫片是 `internal static class Godot.Log` —— 同名同命名空间的两个类型不能并存（且两者行为不同，会形成静默分叉）。决策：**删除垫片**，让 6 个调用点直接解析到 vendored `GDLogger`。垫片是 `internal`，不属于公共 API，删除不构成 API 变更；调用点本身零改动，这一点由任务验证。同时因为日志能力是"恢复"，`Log.Warning` 等新成员成为可用，但 Modot 现有代码不强制使用它们（保持最小改动）。

### D10 验证边界

- `Vector2`/`Vector3`：纯托管结构体，**本机可完整验证**（构造 + 往返 + 文本形态断言），且这正是当前坏掉的路径。
- 日志器的**惰性加载**：`Log` 类型加载而不写条目这一场景**本机可验证**（断言不创建文件、不抛异常）。
- `Node`、`FileAccess`、`GD.Print`/`GD.PushWarning`/`GD.PushError`：需要真实引擎。本机**有** Godot 4.7.2（引擎路径与 headless 用法见 `AGENTS.md`），因此这些由 `add-e2e-test-suite` 交付的 headless e2e 套件验证；在该套件交付前不得声称已验证。

## Risks / Trade-offs

- [`NodeSerializer` 与日志文件写入需要引擎，而本变更交付时 e2e 套件尚未落地] → 由 `add-e2e-test-suite` 交付的 headless 套件覆盖；在套件交付前，编译级验证 + 代码审阅是本机能提供的证据，且不得声称运行时行为已验证。
- [惰性打开改变了文件创建时机] → 这是刻意的行为改进（不再"没用日志却创建文件"），并已在 spec 的 File logging Requirement 中用 Scenario 固定下来。
- [删掉 MoreLinq 时漏改用法] → 不以推断为准：移除 `PackageReference` 后构建，由编译器穷举缺口；`.Indistinct()` 按上游实际语义实现而非等价替换。
- [消费方曾显式引用上游 `GDSerializer`/`GDLogger`] → 同名程序集在同一 `AssemblyLoadContext` 内只加载其一，Modot 会先加载自己那份；mod 侧因简单名统一而命中同一份，行为符合预期。
- [mod 在自己的 `Assemblies/` 里附带同名的 `GDSerializer.dll`/`GDLogger.dll`] → `Mod.LoadAssemblies` 会把该目录下所有 dll 加载进同一上下文，同名程序集二次加载会冲突。这是既有行为，本变更不改变，但需在 `AGENTS.md` 明确提示 mod 作者不要附带这两个程序集。
- [vendor 后失去上游修复] → 上游自 2022 年起无提交，实际不存在可失去的修复；保留 MIT 许可证与版权声明以合规。
- [包名与程序集名不一致容易让人"顺手改成一致"] → 在 design.md 与 `AGENTS.md` 双重记录理由，任务 6.x 要求把该约束写进 `AGENTS.md`。
- [三个包的构建顺序错误] → 打包脚本按依赖顺序产出，并用任务显式验证三个包都已落地。

## Migration Plan

一次性引入，无灰度。回滚：`git revert` 本次提交，并把 `src/Modot/Modot.csproj` 的两个 `ProjectReference` 还原为 `PackageReference Include="GDSerializer" Version="2.0.3"` 与 `Include="GDLogger" Version="1.0.1"`、恢复 `src/Modot/Log.cs`、恢复 `Carnagion.MoreLinq` 引用（`System.CodeDom` 全程保留，回滚无需处理）。

## Open Questions

（无。会影响规格、方案与任务拆分的未知项均已在本机核实或由用户决定。）
