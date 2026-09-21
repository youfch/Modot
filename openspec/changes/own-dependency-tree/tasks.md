# Tasks

## 1. 引入序列化器源码与项目

- [x] 1.1 在上游 tag `v2.0.3` 取得 `GDSerializer` 源码，把 `Serialization/`、`Utility/` 与 `LICENSE`、`README.md` 放入 `src/GDSerializer/`，**保留 MIT 许可证与 `Copyright (c) 2022 Carnagion` 声明**；验证 `src/GDSerializer` 下 `.cs` 文件数为 20（`Serialization/` 16 + `Utility/` 4），且 `LICENSE` 存在并含 MIT 与 Carnagion 版权行
- [x] 1.2 新增 `src/GDSerializer/GDSerializer.csproj`：`Godot.NET.Sdk/4.7.2`、`net10.0`、`RootNamespace Godot`、`Nullable enable`、`GenerateDocumentationFile`；`PackageId` 用 **`Modot.GDSerializer`**、`PackageVersion` 用 `4.0.0`、保留 `Title`/`Authors`/`Description`/`RepositoryUrl`/`PackageLicenseFile`；**保留 `System.CodeDom` 引用**（`TypeExtensions.GetDisplayName` 依赖它，见 design.md - D7），并**不升级其版本**；`Content`/`None` 的仓库级文件相对路径按新位置修正；验证 `dotnet msbuild src/GDSerializer/GDSerializer.csproj -getItem:None -v:q` 解析出 `LICENSE` 且 `Pack` 为 `true`
- [x] 1.3 验证产物的程序集名为 `GDSerializer`（**不是** `Modot.GDSerializer`）且 `AssemblyVersion` 为 `1.0.0.0`，与上游完全一致

## 2. 引入日志器源码与项目

- [x] 2.1 在上游 tag `v1.0.1` 取得 `GDLogger` 源码，把 `Log.cs` 与 `LICENSE`、`README.md` 放入 `src/GDLogger/`（**不搬 `Log.gd`**，它是面向 GDScript 用户的分发物且 Modot 为纯 C#）；验证 `src/GDLogger` 下 `.cs` 文件数为 1，且 `LICENSE` 含 MIT 与 Carnagion 版权行
- [x] 2.2 新增 `src/GDLogger/GDLogger.csproj`：`Godot.NET.Sdk/4.7.2`、`net10.0`、`RootNamespace Godot`、`Nullable enable`、`GenerateDocumentationFile`；`PackageId` 用 **`Modot.GDLogger`**、`PackageVersion` 用 `2.0.0`、保留上游元数据与 `PackageLicenseFile`；验证 `dotnet msbuild` 解析出 `LICENSE` 且 `Pack` 为 `true`
- [x] 2.3 验证产物的程序集名为 `GDLogger` 且 `AssemblyVersion` 为 `1.0.0.0`，与上游完全一致

## 3. 引擎适配

- [x] 3.1 把 `Serialization/Specialized/VectorSerializer.cs` 中 `vector2.x`/`.y` 与 `vector3.x`/`.y`/`.z` 改为 `X`/`Y`/`Z`；验证 `dotnet build Modot.sln` 退出码为 0
- [x] 3.2 处理 `NodeSerializer`：若编译报错则按提示调整（`GetChildren()` 的返回类型在 Godot 4 由 `Godot.Collections.Array` 变为 `Array<Node>`，`AddChild()` 增加可选尾参）；若编译通过则确认其 `Node` 成员调用已重新绑定到 Godot 4 签名。结论以编译器输出为证据，不靠推断
- [x] 3.3 把 `GDLogger/Log.cs` 的 `Godot.File` 移植到 `Godot.FileAccess`：`new File()` 字段改为可空 `FileAccess?`，`Open` 改为 `FileAccess.Open(p, FileAccess.ModeFlags.Write)`，`IsOpen()` 改为判定 `null`，`StoreLine`/`Flush`/`Close`/`GetPathAbsolute` 同名直译，`Dispose` 走 `GodotObject.Dispose`；验证 `dotnet build Modot.sln` 退出码为 0
- [x] 3.4 把日志器的文件句柄改为**惰性获取**：删除 `static Log()` 构造中的文件打开与 `AppDomain` 事件注册，改为首次真正写条目时才打开文件；`FilePath` 仍可设置；验证类型加载路径（`Log` 被引用但未写条目）不做任何文件 I/O
- [x] 3.5 验证三个引擎序列化/写入路径的失败面已被覆盖：搜索 `src/GDSerializer` 与 `src/GDLogger` 中已无 `Vector[23]\.x\b`、`new File(`、`File.ModeFlags`、`IsOpen(`、`Godot.File\b` 的**代码**残留（注释与文档提及不算）

## 4. Modot 侧接线

- [x] 4.1 把 `src/Modot/Modot.csproj` 的 `PackageReference Include="GDSerializer" Version="2.0.3"` 与 `Include="GDLogger" Version="1.0.1"` 换成指向 `../GDSerializer/GDSerializer.csproj` 与 `../GDLogger/GDLogger.csproj` 的 `ProjectReference`；验证 `dotnet build Modot.sln` 退出码为 0，且 `src/Modot` 的 `project.assets.json` 中不再出现来自 nuget.org 的 `GDSerializer`/`GDLogger` 包条目
- [x] 4.2 删除 `src/Modot/Log.cs` 垫片，改由 vendored `GDLogger` 提供 `Godot.Log`；验证 `dotnet build` 退出码为 0，且 `ModLoader.cs`（4 处）与 `Patching/LogPatch.cs`（2 处）的 `Log.*` 调用行与变更前**逐行一致**
- [x] 4.3 验证 `Log` 解析到的类型来自程序集 `GDLogger`（而不是 `Modot`），即垫片已彻底移除、不存在同名类型并存
- [ ] 4.4 在仓库内实现 MoreLinq 提供的 **5 个**扩展方法（Modot 侧 `ForEach`/`Indistinct`/`NotNull`；`GDSerializer` 侧 `ForEach`/`IndexOf`/`Join`）：按各自实际语义实现，`.Indistinct` **返回出现次数大于一的元素**（由 `Mod.Metadata.IsValid` 的调用点与注释"这些列表不得有交集、也不得含自身 ID"反推），并保留原调用点写法不变；因这些方法在上游位于 `System.Linq.EnumerableExtensions`（与 BCL 的 `System.Linq` 同名空间），需决定是沿用同名空间以保调用点零改动，还是放入自有空间并补 `using`；验证 `dotnet build Modot.sln` 退出码为 0
- [ ] 4.5 验证这 5 个扩展方法的位置不与 `Godot.Utility.Extensions`、`System.Linq` 的既有类型冲突（含不与 BCL 的 `Enumerable`/`EnumerableExtensions` 产生歧义），且 `dotnet build` 退出码为 0

## 5. 依赖清理

- [ ] 5.1 验证 `System.CodeDom` **仍然存在且未被移除**：`src/GDSerializer/GDSerializer.csproj` 保留该 `PackageReference`，Modot 的还原图中仍能解析到它；并验证 `TypeExtensions.GetDisplayName` 的输出形态与上游一致（记录基线文本），因为该文本进入 mod 数据的 `Type=` 判据
- [ ] 5.2 移除 `Carnagion.MoreLinq` 的全部引用（`src/GDSerializer` 与 `src/Modot`），**不以推断为准**：移除后构建，由编译器穷举缺口，缺什么补什么；验证 `dotnet build Modot.sln` 退出码为 0 且还原图与包依赖列表中不含 `Carnagion.MoreLinq`
- [x] 5.3 把 `JetBrains.Annotations` 由 `2022.1.0` 升到 `2026.2.0` 并加 `PrivateAssets="all"`；验证 `dotnet build` 退出码为 0，且 `[PublicAPI]`/`[UsedImplicitly]`/`[MustUseReturnValue]` 仍然解析（无 CS0246/CS0121）
- [ ] 5.4 把 `src/GDSerializer` 与 `src/GDLogger` 注册进 `Modot.sln`（归入 `src` 解决方案文件夹）；验证 `dotnet build Modot.sln` 退出码为 0 且三个项目都在解决方案内
- [ ] 5.5 做一次穷尽核查并留下可复用命令：枚举 Modot 解析出的全部包（含传递依赖），**读取每个包内 DLL 的程序集引用**，确认除 `GodotSharp`/`GodotSharpEditor` 外没有任何第三方 DLL 引用 `GodotSharp`；验证输出里只剩两个 vendored 项目而它们已不以包形式出现。**判据不得使用 nuspec 或源码文本搜索** —— 两者都会漏判（`GDSerializer` 与 `GDLogger` 的 nuspec 都未声明该依赖），把实际命令记进 `AGENTS.md` 或 `tests/README.md` 供后续新增依赖时复用

## 6. 打包

- [ ] 6.1 更新 `scripts/pack-local.ps1`（已 gitignore）按依赖顺序打包三个包：`src/GDSerializer` → `src/GDLogger` → `src/Modot`，均 `Release`、均输出到 `D:/GNuget`；验证脚本退出码为 0
- [ ] 6.2 验证 `D:/GNuget` 下同时存在 `Modot.GDSerializer.4.0.0.nupkg`、`Modot.GDLogger.2.0.0.nupkg`、`Modot.3.0.0.nupkg`，且 `Modot.nuspec` 的依赖声明是 `Modot.GDSerializer` 与 `Modot.GDLogger`
- [ ] 6.3 验证三个包的依赖列表中都不含 `GDLogger`（上游包名）、`GDSerializer`（上游包名）、`Carnagion.MoreLinq`、`JetBrains.Annotations`；而 `System.CodeDom` **应当出现**在序列化器包的依赖里（它是 `GetDisplayName` 的必需依赖，见 5.1）
- [ ] 6.4 验证两个 vendored 包各自内含其程序集（`GDSerializer.dll` / `GDLogger.dll`）与包根 `LICENSE`

## 7. 验证收口与遗留事项

- [x] 7.1 **引擎无关验证（本机可完成）**：用一次性 net10.0 探针引用 vendored 产物与 `GodotSharp 4.7.2`，断言：(a) `Vector2(1,2)`/`Vector3(1,2,3)` 的 Serialize→Deserialize 往返成功且分量在 float 精度内相等，无 `MissingFieldException`；(b) 向量序列化文本形如 `(1, 2)`/`(1, 2, 3)`，与上游格式一致；(c) `Mod.Metadata.Load(Mod.xml)` 与标量/集合往返仍成功；(d) **加载 `Godot.Log` 类型但不写条目**时不抛异常且不创建日志文件
- [ ] 7.2 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0，记录警告数并确认未引入新警告（既有为 `CS8602`、`CS8714`、`NU5119`）
- [ ] 7.3 更新 `AGENTS.md`：把 `GDSerializer`/`GDLogger` 两个条目改写为"vendored fork：包名 `Modot.*`、程序集名保持上游、已适配 Godot 4"；说明包名与程序集名必须分离的理由；加上"mod 不要在自己目录里附带 `GDSerializer.dll`/`GDLogger.dll`"的提示；移除已不存在的 `Log.cs` 垫片说明；验证 `openspec doctor` 正常且 `AGENTS.md` 不含 OpenSpec 起止标记
- [ ] 7.4 **引擎运行时验证（本机可做）**，载体是 `add-e2e-test-suite` 交付的 e2e 套件：用 Godot 4.7.2 headless 运行并以退出码/输出断言，验证 `NodeSerializer` 的带子节点 `Node` 往返、日志器的文件写入与 `EntryWritten` 事件、`GD.Print`/`GD.PushWarning`/`GD.PushError` 的输出。**现状（套件已交付，覆盖不完整）**：`tests/e2e` **未覆盖** `Node` 序列化，也**未覆盖**日志器的文件写入与 `EntryWritten` 事件（那两个行为要先 vendor `GDLogger` 才有对象可测）；`tests/Modot.Tests` 目前只覆盖了「日志器类型加载不做文件 I/O」。因此本任务仍**未验证**，不得因套件已建成而默认覆盖
