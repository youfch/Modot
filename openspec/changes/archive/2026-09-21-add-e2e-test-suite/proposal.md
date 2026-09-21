# Proposal

## Why

另外两个进行中的变更都卡在同一个瓶颈上：关键行为只能靠**编译级证据**支撑，而其中一处属于**静默失效**类失败，人工审阅根本发现不了。

- `upgrade-to-godot-4-7-2`：`DirAccess` 的遍历/递归/跨根复制、`ProjectSettings.LoadResourcePack` 加载 `.pck`、`GD.Print`/`GD.PushError` 输出，至今只有编译级证据。
- `own-dependency-tree`：整个方案唯一不能妥协的那条论证 —— **按上游编译的 mod，其 `[Serialize]` 特性必须被识别** —— 依赖类型标识一致。若不一致，反射匹配得到 `null`，补丁与元数据**不报错地失效**。这条只能靠"真实 mod 程序集 + 真实加载过程"证伪。

同时，本机**已经**具备验证条件却完全没有常驻测试：Godot 4.7.2 (.NET) 在 `D:\APP\` 下，且已实测 `--headless` 可跑、stdout 可捕获、退出码可用于断言。目前的"测试"是我临时搭的探针，放在系统临时目录，会随会话丢失，每个后续变更都要重新付一遍搭建成本。

## What Changes

三层验证各归其位，全部纳入版本控制：

- **引擎无关层** `tests/Modot.Tests/`：普通 net10.0 项目，引用 Modot 与其 vendored 依赖，覆盖不需要引擎的行为（元数据解析、标量/集合序列化、`Vector2`/`Vector3` 往返、日志器类型加载不触发文件 I/O）。可在没有 Godot 的机器上运行。
- **引擎运行时层** `tests/e2e/`：
  - `Host/`：一个 Godot 4 C# 项目（宿主），引用 Modot，从命令行接收一组 mod 目录，调用 `ModLoader.LoadMods`，逐条打印 `PASS:`/`FAIL:` 并以退出码收口。
  - `Mods/AlphaMod/`：**一个** mod 项目，编译出含 `[ModStartup]` 与自定义补丁的 mod 程序集；项目目录本身就是 mod 目录（`Mod.xml`、`Data/`、`Patches/` 入库，`Assemblies/` 由构建产出）。
  - `fixtures/`：**多个纯 XML** 的 mod 目录，不参与编译，覆盖加载顺序与失败矩阵 —— 重复 ID、缺依赖、循环依赖、互相不兼容、补丁目标、属性设置/移除、条件补丁。
  - `run.ps1`：runner —— 构建 mod、组装 mod 目录、用 `_console.exe --headless` 运行 Host、断言退出码与输出。
- 引擎路径通过环境变量 `MODOT_GODOT` 注入（默认值指向本机安装位置），**不硬编码进版本控制的脚本**。
- `tests/README.md` 说明两层各覆盖什么、如何运行、哪些行为刻意为未覆盖。
- `Modot.sln` 注册 `Modot.Tests`、`Host`、`AlphaMod`（三者都能在不装引擎的机器上构建）。

明确不在范围内：`.pck` 资源包覆盖（需要额外构建 pck，另起变更）；CI 集成；把 `tests/e2e` 纳入解决方案级 `dotnet test`（它需要引擎，不能进默认测试路径）。

## Capabilities

### New Capabilities

- `verification`: 本仓库如何验证 Modot —— 哪些行为必须由引擎无关测试覆盖、哪些必须由引擎运行时覆盖、mod fixture 如何构造才具备真实性、失败如何被强制暴露，以及引擎位置如何注入。

### Modified Capabilities

（无。`openspec/specs/` 为空，不存在既有能力。）

## Impact

- **新增**：`tests/Modot.Tests/`、`tests/e2e/Host/`、`tests/e2e/Mods/AlphaMod/`、`tests/e2e/fixtures/`、`tests/e2e/run.ps1`、`tests/README.md`。
- **构建**：`Modot.sln` 增加三个项目；`.gitignore` 需补充构建产出的 mod 目录（`tests/e2e/Mods/*/Assemblies/`）、以及 e2e 运行产物。
- **产品代码**：**零改动**。本变更不触碰 `src/`，发布包内容不变。
- **解锁的其他变更**：`upgrade-to-godot-4-7-2` 的任务 7.2 与 `own-dependency-tree` 的任务 7.4 都以本套件为验证载体；本变更交付后才能声称那些运行时行为已验证。
- **环境**：运行 e2e 需要本机 Godot 4.7.2 .NET 版（已具备）；引擎无关层不需要。
