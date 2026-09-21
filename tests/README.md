# Modot 测试

两层，边界明确，不要混用。

## 第一层：引擎无关（`tests/Modot.Tests`）

- **需要引擎**：否
- **运行**：`dotnet test tests/Modot.Tests`
- **覆盖**：`Mod.xml` 元数据解析、标量与集合的序列化往返、`Vector2`/`Vector3` 往返、日志器类型加载不做文件 I/O
- **为什么要单独一层**：能在没装 Godot 的机器上跑，且不必为这些断言启动引擎

## 第二层：引擎运行时（`tests/e2e`）

- **需要引擎**：是（Godot 4.7.2 .NET 版）
- **运行**：`./tests/e2e/run.ps1`（`-Scenario alpha` 可只跑一个场景）
- **引擎路径**：取自环境变量 `MODOT_GODOT`，未设置则回落到脚本内的默认值。换机器或换引擎版本请用环境变量覆盖，**不要**把绝对路径硬编码进版本控制的代码
- **结构**：
  - `Host/` —— Godot C# 项目；接收 `<场景> <mod 目录...>`（`--` 之后），调用 `ModLoader.LoadMods`，逐条打印 `PASS:`/`FAIL:`，以退出码收口
  - `Mods/AlphaMod/` —— **创建 Mod 的项目**：项目目录本身就是可加载的 mod 目录（`Mod.xml`、`Data/`、`Patches/` 入库，`Assemblies/` 由 runner 生成并已 gitignore）
  - `fixtures/` —— **纯 XML** 的 mod 目录，不参与编译，覆盖加载顺序与失败矩阵
  - `run.ps1` —— runner：构建 → 组装 mod 目录 → 逐场景 headless 运行 → 断言退出码
- **场景**（`run.ps1` 会依次跑完并断言退出码）：`alpha`（加载/数据/补丁/启动方法）、`order`（声明覆盖传入顺序）、`duplicate`、`missing-dep`、`patches`（属性设置/移除 + 条件补丁双分支）、`cycle`、`incompatible`、`pack`（`.pck` 加载 + 场景可达 + 脚本绑定限制）
- **`watch`（不在 `run.ps1` 里）**：**无参数运行时的默认场景**，也是从编辑器按 F5 会跑到的那个。加载 mod 后**不退出**，打印它的 `Data`（能直接看到补丁效果）、程序集数与补丁数，并把打包的场景挂进场景树（附相机与光源，所以窗口里真看得见），供在编辑器里检查远程场景树。它**永不退出**，所以**不能**加进 `run.ps1`（会把 runner 挂住）
- **不在 runner 里的 fixture**：`broken/`（缺失合法根节点，用于人工验证"宿主不吞异常、退出码非 0"）、`order-a/`+`order-b/`（`Before` 方向的声明，保留作为 `Before`/`After` 语义反转的证据）

### 三个必须遵守的约束

1. **宿主与 mod 必须用 `Debug` 构建**。Godot 默认从 `<项目>/.godot/mono/temp/bin/Debug/` 解析程序集，用 `Release` 会报 `Cannot instantiate C# script ... 'res://Host.cs'`。只有打包才用 `Release`。
2. **mod 目录的 `Assemblies/` 只能放 mod 自己的程序集**。`Mod.LoadAssemblies` 会把该目录下**所有** `*.dll` 加载进 Modot 所在的同一个加载上下文，拷贝整个构建输出会与已加载的 `Modot.dll`/`GDSerializer.dll`/`GodotSharp.dll` 冲突。
3. **`run.ps1` 只含 ASCII 字符**。Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 当 ANSI 读，中文会让解析器报"字符串缺少终止符"。中文说明写在本文件里。

## `.pck` 资源包（已覆盖）

`tests/e2e/Mods/AlphaMod/Resources/assets.pck` 是**入库的**构建产物（由 `Scn/BoxRot.tscn` 导出），`pack` 场景断言它能被加载。重新生成：

```powershell
& "<引擎>\_console.exe" --headless --path tests/e2e/Mods/AlphaMod `
  --export-pack assets tests/e2e/Mods/AlphaMod/Resources/assets.pck
```

两点实测经验：预设里**不能**写 `platform="PCK"`（Godot 会拒绝注册该预设，报 `Invalid export preset name`），用普通平台值即可 —— 因为 `--export-pack` 只导出**数据**，预设平台不参与产物。另外 `patches` 要写成 `patches=PackedStringArray()`，空 `[]` 会让预设无效。

### 打包场景里的 C# 脚本（已解决）

`.pck` 加载成功、场景可加载可实例化，**且场景引用的 C# 脚本会绑定并执行** —— `pack` 场景断言 `GetScript()` 非空，输出里还能看到 `Scn/BoxRot.cs` 的 `_Ready` 打印的 `BOXROT_READY`。

做到这一点需要在**加载 mod 程序集后把它注册进 Godot 的脚本表**：Godot 按"启动时已知程序集"建立的注册表解析 `res://X.cs`，运行时载入的程序集不在其中；不注册的话场景脚本会**静默绑不上**，引擎日志只给一行 `Cannot instantiate C# script because the associated class could not be found`。Modot 在 `Mod.LoadAssembly` 里调用 `Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(assembly)` 完成注册。

⚠️ **可持续性风险**：`LookupScriptsInAssembly` 是 `ScriptManagerBridge` 目前**唯一的 public 成员**，其源码注释说明今后会改由源生成器接管、并把该类改为 internal。一旦发生，这个注册手段就会失效，需要重新评估。届时 `pack` 场景会失败并提醒。

## 刻意未覆盖

| 未覆盖项 | 原因 |
|---|---|
| `DirectoryExtensions` 的遍历/递归/跨根复制（`res://` → `user://`） | 需要专门的资源/用户目录场景，另起变更 |
| `Node` 序列化 | 需要在引擎中构造节点树，未纳入本次范围 |

## 套件当场抓到、但尚未修复的两个问题

1. **`Vector2`/`Vector3` 往返失败**：`MissingFieldException` —— `GDSerializer` 仍按 Godot 3 的字段名访问 `Vector2.x`/`Vector3.x`，而 Godot 4 已改名 `X`/`Y`/`Z`。由 `own-dependency-tree` 变更修复；修复后 `tests/Modot.Tests` 里那三个向量断言应转绿。
2. **`Before`/`After` 的语义与文档相反**：实测声明 `Before: X` 会把 X 排在**之后**，声明 `After: X` 会排在**之前**，与 `Mod.Metadata` 的两处文档注释相反。因此 `order` 场景断言的是"声明生效且可复现的排列"，而不是文档语义 —— 将来若修正这个反转，该断言会**故意失败**，提醒同步更新。
