# Proposal

## Why

测试套件是分几次追加搭起来的，现已积累多处不一致（以下为实测盘点，不是猜测）：

- **两层命名风格不一致**：`tests/Modot.Tests/`（PascalCase + `.Tests`，.NET 惯例）与 `tests/e2e/`（小写缩写）并列。
- **`e2e` 其实不是测试项目**：它装的是 harness —— 一个 Godot 宿主程序、一个 mod 程序、纯 XML 夹具、以及驱动脚本。它与"测试项目"并列，容易被误认为测试项目。
- **两处"被加载的 mod"形态不同却看不出来**：`Mods/AlphaMod/`（有代码、是完整 Godot 项目）与 `fixtures/*`（纯 XML）在同一层级下没有语意区分。
- **存在死夹具**：`order-a`/`order-b`（`Before` 方向，**不在 runner 里**，仅留作语义反转的证据）与 `broken`（**不在 runner 里**，只能手动跑）。
- **`fixtures/` 是平的一层**：失败矩阵（`duplicate-*`、`missing-dep`、`cycle-*`、`incompatible-*`）与顺序/成功夹具（`after-*`、`patches`）混在一起。
- **一个东西三个名字**：项目 `AlphaMod.csproj`、代码文件 `Alpha.cs`（类 `Alpha`）、mod id `alpha`。
- **`Scn/` 用缩写**，不符社区与 Godot 的惯例。
- **`Host.cs` 是 14KB 单文件**：8 个场景与断言基础设施全部挤在一起。
- **脚本命名**：`run.ps1`/`assemble.ps1` 用小写动词，不符 PowerShell 的 `Verb-Noun` 惯例。

同时，加载生命周期中**有零覆盖的核心语义**（上一轮已逐项核实）：

- **跨 mod 补丁**：每个 mod 的补丁会作用到「它自己以及之前所有已加载 mod 的数据」上 —— 这是补丁系统的核心语义，而现有夹具全部是单 mod 自打自，这条路径从未被验证。
- **`executeAssemblies: false`**：官方宣传的"只加载数据与资源、不执行代码"安全模式，`ModLoader.LoadMods` 里对应的分支从未走过。
- **`LoadMod`（单个加载）**：零覆盖，且它带一个上游可疑行为 —— 先 `loadedMods.Add(mod)`，随后又从 `LoadedMods.Values` 取值并额外 `.Append(mod.Data?.DocumentElement)`，同一个 `XmlNode` 进数组两次，导致该 mod 自己的补丁对自己数据应用两遍。
- **`Metadata.IsValid()` 的拒绝路径**：非法元数据（`Before`/`After`/`Incompatible` 有交集、含自身 ID、`Dependencies ∩ Incompatible`）本应被拒绝，从未测过。

## What Changes

- **目录按"层"组织**：`tests/unit/Modot.Tests/`（引擎无关测试项目）与 `tests/e2e/`（引擎运行时 harness）。第一层是层名，第二层是项目/资产。
- **`e2e` 内部规范化**：夹具按用途分组，消除死夹具；两处"被加载的 mod"的命名体现其形态差异。
- **命名对齐**：mod 项目的代码文件与类名和项目名一致；`Scn/` 改为 `Scenes/`。
- **`Host.cs` 按场景拆分**，入口与断言基础设施分离。
- **runner 支持"预期失败"场景**，使非法元数据一类夹具能进入 runner 断言，而不是只靠手动跑。
- **补齐四项高风险覆盖**：跨 mod 补丁、`executeAssemblies: false`、`LoadMod` 单加载、`Metadata.IsValid()` 拒绝路径。
- **脚本改用 `Verb-Noun` 命名**。

明确不在范围内：补齐全部 8 个未覆盖的补丁/条件类型（`NodeRemovePatch`、`NodeReplacePatch`、`LogPatch`、`MultiPatch`、`And/Or/Not/ModLoadedCondition`）；`DirectoryExtensions` 的遍历/递归/跨根复制；`Node` 序列化；`GDLogger` 的 `EntryWritten` 与 `Warning`。这些继续留在 `tests/README.md` 的「刻意未覆盖」清单里并写明原因。

## Capabilities

### New Capabilities

（无。本变更不引入新的能力，而是收紧既有的验证能力。）

### Modified Capabilities

- `verification`: 追加对测试套件布局与命名、夹具组织、加载生命周期关键路径的覆盖、以及场景失败预期的要求。

## Impact

- **目录移动牵连**：`Modot.sln`（项目路径）、`.gitignore`（产物路径）、`tests/e2e/run.ps1`（场景与夹具路径）、`tests/README.md`、`AGENTS.md`（运行命令）。
- **新增夹具**：跨 mod 补丁两枚（`base` + `overlay`）、非法元数据若干（元数据校验的拒绝用例）。
- **新增断言**：`Metadata.IsValid()` 拒绝路径进入引擎无关层；跨 mod 补丁、`executeAssemblies: false`、`LoadMod` 进入引擎运行时层。
- **产品代码零改动**：`src/Modot` 不被触碰，发布包内容不变。
