# Modot 测试

两层，边界明确：`unit/` 是**测试项目**（`dotnet test` 能直接跑），`e2e/` 是**运行时 harness**（一群脚本 + 一个 Godot 项目，**不是**测试项目，不要试图 `dotnet test` 它）。

## 第一层：引擎无关（`tests/unit/Modot.Tests`）

- **需要引擎**：否
- **运行**：`dotnet test tests/unit/Modot.Tests`
- **为什么单独一层**：能在没装 Godot 的机器上跑，且"元数据解析、校验、标量/集合序列化"这类断言不需要为每次验证多花几十秒启动引擎
- **覆盖**：`Mod.xml` 元数据解析与字段往返、非法元数据的**现状**（见"已知缺陷"）、标量与集合的序列化往返、`Vector2`/`Vector3` 往返、日志器类型加载不做文件 I/O

**移动测试项目后必须修正 `ProjectReference` 的相对路径** —— 目录每下一层就多一个 `..`。构建才会暴露，测试自身看不出来。

## 第二层：引擎运行时（`tests/e2e`）

- **需要引擎**：是（Godot 4.7.2 .NET 版）
- **运行**：`./tests/e2e/Invoke-E2E.ps1`（`-Scenario alpha` 可只跑一个场景）
- **引擎路径**：取自环境变量 `MODOT_GODOT`，未设置则回落到脚本内默认值。换机器或换引擎版本用环境变量覆盖，**不要**把绝对路径硬编码进版本控制的代码
- **结构**：
  - `Host/` —— Godot C# 项目；接收 `<场景> <mod 目录...>`（`--` 之后），调用 `ModLoader`，逐条打印 `PASS:`/`FAIL:`，以退出码收口
  - `Mods/AlphaMod/` —— **创建 Mod 的项目**：项目目录本身就是可加载的 mod 目录（`Mod.xml`、`Data/`、`Patches/`、`Scenes/` 入库；`Assemblies/` 由 runner 生成并已 gitignore）
  - `fixtures/` —— **纯 XML** 的 mod 目录，不参与编译
  - `Invoke-E2E.ps1` —— runner：构建 → 组装 mod 目录 → 逐场景 headless 运行 → 断言退出码
  - `Update-ModAssets.ps1` —— 单独重建 mod 程序集与 `.pck`（编辑 `Data/`/`Patches/`/`Scenes/` 后需要）

### 场景（runner 会依次跑完并断言退出码）

| 场景 | 断言什么 |
|---|---|
| `alpha` | 加载 `Mod.xml` + 程序集 + 数据，补丁生效，`[ModStartup]` 被执行（marker 文件存在） |
| `order` / `order-before` | 声明式加载顺序**覆盖传入顺序**；两者各自把目录**按相反于声明的顺序**传入，避免"空操作也能通过" |
| `duplicate` / `incompatible` / `missing-dep` | 重复 ID、互不兼容、依赖缺失各自的加载结果 |
| `cycle` | 环形依赖被剔除 |
| `patches` | 属性设置/移除、条件补丁双分支 |
| `pack` | `.pck` 加载 + 场景可达 + **场景引用的 C# 脚本绑定并执行** |
| `cross-patch` | **一个 mod 的补丁改动另一个 mod 的数据**（且不碰自己的数据） |
| `no-assemblies` | `executeAssemblies: false` 时补丁仍生效、`[ModStartup]` 不执行 |
| `single-load` | `LoadMod` 无视缺失依赖照常加载；并固化它对自己数据的补丁次数 |
| `invalid-root` | **预期失败**：元数据根节点不是 `<Mod>` 时必须抛错，而不是"加载了 0 个 mod" |
| `broken-pack` | **预期失败**：`.pck` 不是资源包时抛 `ModLoadException` |
| `bad-patch` | **预期失败**：补丁 XML 不能被解析成 `IPatch` 时必须失败 |

**"预期失败"机制**：场景表里声明 `ExpectFailure = $true` 后，宿主**必须以非零退出码结束**才算通过；若意外以 0 结束则判该场景失败。这套机制反向验证过（去掉标记后相关场景确实判 FAIL、runner 退出码 1）。

**`watch`（不在 runner 里）**：**无参数运行时的默认场景**，也是从编辑器按 F5 会跑到的那个。加载 mod 后**不退出**，打印其 `Data`（能直接看到补丁效果）、程序集数与补丁数，并把打包的场景挂进场景树（附相机与光源，窗口里真看得见），供在编辑器里检查远程场景树。它**永不退出**，所以**不能**加进 runner（会把 runner 挂住）。

### 夹具分组

```
fixtures/
  load-order/   after-a, after-b, order-a, order-b      -> order / order-before
  failures/     duplicate-a/b, missing-dep, cycle-a/b,
                incompatible-a/b                         -> 对应失败场景
                broken-pack, bad-patch                   -> 预期失败场景
  invalid/      invalid-root                            -> 预期失败场景
  patches/      patches                                 -> 补丁/条件类型矩阵
  cross-patch/  base（提供数据）, overlay（补丁指向 base）  -> cross-patch
```

每个夹具都**被某个场景使用**，无死夹具；夹具目录名与其 mod id 一致。

### 四个必须遵守的约束

1. **宿主与 mod 必须用 `Debug` 构建**。Godot 默认从 `<项目>/.godot/mono/temp/bin/Debug/` 解析程序集，用 `Release` 会报 `Cannot instantiate C# script ... 'res://Host.cs'`。只有打包才用 `Release`。
2. **mod 目录的 `Assemblies/` 只能放 mod 自己的程序集**。`Mod.LoadAssemblies` 会把该目录下**所有** `*.dll` 加载进 Modot 所在的同一个加载上下文，拷贝整个构建输出会与已加载的 `Modot.dll`/`GDSerializer.dll`/`GodotSharp.dll` 冲突。
3. **两个 `.ps1` 都只含 ASCII 字符**。Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 当 ANSI 读，中文会让解析器报"字符串缺少终止符"。中文说明写在本文件里。
4. **改完脚本要复核非 ASCII 字符数为 0**；改完目录结构要复核 `ProjectReference` 与 runner 里的夹具路径。

## `.pck` 资源包

`tests/e2e/Mods/AlphaMod/Resources/assets.pck` 是**构建产物，不入库**（`.gitignore` 忽略 `tests/e2e/Mods/*/Resources/`），由 `Scenes/BoxRot.tscn` 导出。`pack` 场景断言它能被加载。

`Invoke-E2E.ps1` **每次运行前都会重新导出**它 —— 它没入库，所以不能假设仓库里已存在；这也顺带把**导出管线本身**置于测试之下，而不只是测它的产物。单独重建：

```powershell
./tests/e2e/Update-ModAssets.ps1
```

两点实测经验：预设里**不能**写 `platform="PCK"`（Godot 会拒绝注册该预设，报 `Invalid export preset name`），用普通平台值即可 —— 因为 `--export-pack` 只导出**数据**，预设平台不参与产物。另外 `patches` 要写成 `patches=PackedStringArray()`，空 `[]` 会让预设无效。

### 打包场景里的 C# 脚本（已解决，但有可持续性风险）

`.pck` 加载成功、场景可加载可实例化，**且场景引用的 C# 脚本会绑定并执行** —— `pack` 场景断言 `GetScript()` 非空，输出里还能看到 `Scenes/BoxRot.cs` 的 `_Ready` 打印的 `BOXROT_READY`。

做到这一点需要在**加载 mod 程序集后把它注册进 Godot 的脚本表**：Godot 按"启动时已知程序集"建立的注册表解析 `res://X.cs`，运行时载入的程序集不在其中；不注册的话场景脚本会**静默绑不上**，引擎日志只给一行 `Cannot instantiate C# script because the associated class could not be found`。Modot 在 `Mod.LoadAssembly` 里调用 `Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly)` 完成注册。

⚠️ **可持续性风险**：`LookupScriptsInAssembly` 是 `ScriptManagerBridge` 目前**唯一的 public 成员**，其源码注释说明今后会改由源生成器接管、并把该类改为 internal。一旦发生，这个注册手段就会失效，需要重新评估。届时 `pack` 场景会失败并提醒。

## 加载生命周期与覆盖

`ModLoader` 的加载过程分为 7 段，扩覆盖时按这张表核对"哪一段还没有载体"：

| # | 阶段 | 载体 |
|---|---|---|
| ① | `LoadModMetadata`：读 `Mod.xml`、反序列化、`IsValid` 校验 | 单测 + `invalid-root`（`IsValid` 是死代码，见缺陷 1） |
| ② | `FilterModMetadata`：递归剔除依赖未满足者 | `missing-dep`、`single-load` |
| ③ | `SortModMetadata`：拓扑排序、环剔除 | `order`、`order-before`、`cycle`、`duplicate`、`incompatible` |
| ④a | `LoadResources`（`.pck`） | `pack`、`broken-pack`（失败路径） |
| ④b | `LoadData`（`Data/*.xml`） | 全部加载类场景 |
| ④c | `LoadPatches`（**惰性**） | `patches`、`cross-patch`、`bad-patch`（失败路径） |
| ④d | `LoadAssemblies`（**即时** + 脚本表注册） | `alpha`、`pack` |
| ⑤ | 补丁应用（对**所有已加载** mod 的根节点） | `patches`、`cross-patch`、`single-load` |
| ⑥ | `StartupMod`：反射调用 `[ModStartup]` | `alpha`、`no-assemblies` |
| ⑦ | `LoadMod`：单加载旁路 | `single-load` |

## 缺陷（由扩覆盖实测抓到）

三个已修复（见 `fix-load-defects` 变更），一个待处理：

| # | 缺陷 | 实测证据 | 状态 |
|---|---|---|---|
| 1 | `Metadata.IsValid` 是**死代码**：`[AfterDeserialization]` 钩子查找只匹配 public 成员 | 3 个单测此前实测"不抛异常" | ✅ **已修复** —— 钩子查找改用含 `NonPublic` 的显式绑定标志；三个用例现在断言**拒绝** |
| 2 | `LoadMod` 对自己数据**重复应用补丁**（同一根节点入列两次） | `APPLIED:2` | ✅ **已修复** —— 去掉多余的 `.Append`；现为 `APPLIED:1` |
| 3 | `LoadPatches` 不包 `SerializationException`，异常契约不一致 | `bad-patch` 曾抛 `SerializationException` | ✅ **已修复** —— 按 `Metadata.Load` 的同一形状兜底；现在抛 `ModLoadException` |
| 4 | `Before`/`After` 语义与文档注释相反 | `order`/`order-before` 的 `INPUT`/`ORDER` 对比 | ✅ **已修复** —— `SortModMetadata` 建的是"后继"图，而 `TopologicalSort` 的 `dependencies` 参数要求的是"前驱"；交换两条边即符合文档 |
| 5 | 同进程**第二次** `LoadMods` 抛未处理的 `ArgumentException`（重复键） | `reload` 场景：`RELOAD-SECOND-THREW:ArgumentException` | ⏳ **待处理**（本轮新查出）—— `LoadModMetadata` 的查重只看**本次调用内**的目录，从不查已加载注册表，于是重复 ID 直到 `loadedMods.Add` 才撞上，绕过既有的 `ModLoadException("Duplicate ID")` 契约 |

**缺陷 4 的根因值得记住**：`SortModMetadata` 的图按其注释是"在元素**之后**加载的节点"，而 `TopologicalSort` 的选择器语义是"**先于**元素加载的节点"（它先递归访问依赖，才把元素入列）。两者相反 —— 所以这不是"两边写反了"，而是**图的约定与排序器参数的约定不一致**。

修复时 `cross-patch` 夹具当场失败：它是按**旧**语义声明的 `After`，改成 `Before` 才符合文档语义。这一条正是"每修一条就跑全套件"的价值 —— 夹具是按错语义写的，只有端到端测试抓得住。

**缺陷 1 与 4 的修复都是 BREAKING**：缺陷 1 修复后，此前被静默接受的非法元数据会被**拒绝**，这类 mod 将不再加载；缺陷 4 修复后，依赖旧反向行为的 mod 加载顺序会**翻转**。已修复的 1/2/3 有测试兜底 —— 回归会让用例重新打红。

## 刻意未覆盖

| 未覆盖项 | 原因 |
|---|---|
| `[ModStartup]` 的**带参**形态 | 正在 `cover-load-lifecycle` 变更中补齐 |
| 同一进程内**重复加载**的静态状态语义 | 同上（属语义决策，需先定义期望的二次加载语义） |
| 剩余补丁/条件类型的语义穷尽 | 同上（矩阵只要求每型至少一次） |
| `DirectoryExtensions` 的遍历/递归/跨根复制 | 同上（需 `res://` -> `user://` 跨根场景） |
| `Node` 序列化 | 需要在引擎中构造节点树，未纳入范围 |

## 维护约定

- **新增场景必须同步更新上面的场景表、夹具分组与生命周期表**，本文件是"覆盖了什么"的单一出处
- 新增夹具时：目录名 = mod id，并确认它**被某个场景使用**（无死夹具）
- 新增失败类场景优先用 `ExpectFailure`，不要另写一套判定