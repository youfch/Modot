# Design

## Context

现状盘点与动机见 proposal.md - Why。与本方案相关的已核实事实：

- 引擎无关层是**真正的测试项目**（xUnit，10 个 `[Fact]`），`dotnet test tests/Modot.Tests` 可独立运行。
- 引擎运行时层**不是测试项目**：`Host/` 是一个 Godot C# 程序（`project.godot` + `Host.tscn` + `Host.cs`），`Mods/AlphaMod/` 是另一个程序兼 mod 目录，`fixtures/*` 是纯 XML 数据，`run.ps1` 是驱动。
- `Host.cs` 现为 14KB，含 8 个场景方法（`RunAlpha`/`RunOrder`/`RunDuplicate` 等）与断言基础设施（`Check`/`Fail`/`_Ready` 的参数解析与收口）。
- 夹具现状：`fixtures/` 平铺 13 个目录；`after-*` 在 runner 里（After 方向），`order-*` 不在（Before 方向，留作语义反转证据），`broken` 不在（手动验证"宿主不吞异常"）。
- 覆盖缺口（上一轮逐项核实）：跨 mod 补丁、`executeAssemblies: false`、`LoadMod`、`Metadata.IsValid()` 拒绝路径均零覆盖。
- `LoadMod` 的上游可疑行为：`loadedMods.Add(mod)` 之后又从 `LoadedMods.Values` 取值并额外 `.Append(mod.Data?.DocumentElement)`，同一 `XmlNode` 进数组两次 → 该 mod 的补丁对自己数据应用两遍。

## Goals / Non-Goals

**Goals:**

- 让 `tests/` 的结构与命名一眼可读：层 → 项目/资产，形态差异在命名上可见。
- 消除死夹具与"一个东西多个名字"。
- 把加载生命周期里**语义最重、且零覆盖**的四条路径纳入断言。
- 让失败路径的夹具能进常规运行，而不是只能手动跑。

**Non-Goals:**

- 不追求覆盖率数字；补齐全部 8 个未覆盖的补丁/条件类型。
- `DirectoryExtensions` 的遍历/递归/跨根复制、`Node` 序列化、`GDLogger` 的 `EntryWritten`/`Warning` 仍为"刻意未覆盖"。
- 不改 `src/Modot` 的产品代码。

## Decisions

### D1 层目录用 `unit/` 与 `e2e/`（用户选定）

`tests/unit/Modot.Tests/`（引擎无关测试项目）与 `tests/e2e/...`（引擎运行时 harness）。第一层是**层名**，第二层才是项目/资产；两层命名风格一致（同为小写层名）。

替代方案：`tests/Modot.Tests` + `tests/Modot.E2E`（并列项目式名）—— 放弃，因为 `e2e` 不是测试项目，用项目式名会加剧误解。

### D2 `e2e` 保持小写，并在文档中明确它是 harness

`e2e` 层里没有测试项目：宿主与 mod 都是**可运行的程序**，夹具是数据，脚本是驱动。`tests/README.md` 与 `AGENTS.md` 都必须写明这一点，避免后来者把它当测试项目去找。

### D3 夹具按用途分组

```
tests/e2e/fixtures/
  load-order/     after-a, after-b            （声明生效，After 方向）
  failures/       duplicate-a, duplicate-b, missing-dep, cycle-a, cycle-b, incompatible-a, incompatible-b
  invalid/        invalid-root               （缺失合法根节点，预期失败）
  patches/        patches                    （属性设置/移除 + 条件双分支）
  cross-patch/    base, overlay               （新增：跨 mod 补丁）
```

分组名表达**用途**而不是被加载者的名字。`patches/` 里再加 `cross-patch/` 会混层，因此跨 mod 补丁单列。

### D4 死夹具的处理：不删除，纳入 runner 或明确标注

- `order-a`/`order-b`（Before 方向）：**保留并纳入 runner**。它们与 `after-*` 是一对，正好覆盖 `Before`/`After` 两个方向；只测 `After` 会漏掉一半语义。
- `invalid-root`（原 `broken`）：**保留并纳入 runner**，靠 D6 的"预期失败"机制，而不是只手动跑。

替代方案：直接删掉两者 —— 放弃。前者会丢掉一个方向的覆盖，后者会丢掉"宿主不吞异常"的断言。

### D5 mod 代码文件与类名对齐项目名；mod id 保持 `alpha`

`Mods/AlphaMod/Alpha.cs`（类 `Alpha`）→ `Mods/AlphaMod/AlphaMod.cs`（类 `AlphaMod`）：一个东西一个名字。

**mod id 保持 `alpha`**：夹具 id 的风格是短横线小写（`order-a`、`missing-dep`），`alpha` 与之一致；把它改成 `AlphaMod` 反而破坏 id 风格且牵动多数断言，收益不足。

### D6 runner 支持"预期失败"场景

场景表增加 `ExpectFailure` 标志：声明为预期失败时，宿主**必须**以非零退出码结束才算通过；若意外以 0 结束则判失败。这让非法元数据一类夹具进入常规运行。

这样"宿主不吞异常"这条当前只能手动验证的断言，变成了自动断言。

### D7 `Host.cs` 按场景拆分

`Host.cs` 只保留入口（参数解析、默认场景、收口）与断言基础设施（`Check`/`Fail`）；各场景移入 `Scenarios/` 下的 `partial class Host` 分片（每个场景一个文件）。保持 Godot 的脚本绑定不变（仍是同一个类 `Host`）。

替代方案：把场景抽成独立类 —— 放弃，它们需要 `Check`/`Fail` 的失败计数与 `_Ready` 的收口，partial 分片改动最小。

### D8 脚本改用 PowerShell 的 `Verb-Noun`

`run.ps1` → `Invoke-E2E.ps1`；`assemble.ps1` → `Update-ModAssets.ps1`。

代价：`tests/README.md` 与 `AGENTS.md` 里的命令示例必须同步更新（属任务范围内）。脚本**仍必须只含 ASCII**（仓库既有约定）。

### D9 `Scn/` → `Scenes/`

不用缩写。`.`tscn` 与 `.cs` 的 Godot 侧引用（`res://Scenes/BoxRot.cs`）随之更新，且**必须重新导出 pck** —— 否则夹具里的 pck 与源码不一致。

### D10 四项覆盖的夹具设计

1. **跨 mod 补丁**：`cross-patch/base` 提供数据（如 `<Items><Item id="base"/></Items>`），`cross-patch/overlay` 的补丁往 base 的节点上加属性或加子节点。场景断言 **base 的数据被 overlay 改动**（而非 overlay 自己的数据）—— 这才是"补丁作用到先前 mod"的实证。
2. **`executeAssemblies: false`**：复用 `Mods/AlphaMod`，以该开关加载 → 断言补丁仍生效、而 `[ModStartup]` 的 marker 文件**不存在**。
3. **`LoadMod` 单加载**：用一个声明了 `Dependencies` 指向不存在 id 的夹具（原本会被 `LoadMods` 过滤掉）→ 断言 `LoadMod` **照常加载**（证明它不查依赖不排序）；并实测它对自己数据的补丁效果，把"是否重复应用"这一可疑行为的**实际结果**固化成断言（若确实重复，则如实断言并记录为独立议题）。
4. **元数据拒绝**：引擎无关层新增用例，覆盖 `Before`/`After`/`Incompatible` 有交集、含自身 ID、`Dependencies ∩ Incompatible` 三种非法形态 → 断言抛 `ModLoadException`。

## Risks / Trade-offs

- [重命名牵连 `Modot.sln`/`.gitignore`/文档，漏改会导致构建或运行失败] → 任务里把"改完后 `dotnet build Modot.sln` 与全套 e2e 仍绿"作为显式验证项。
- [`Scn/` 改名后若忘记重新导出 pck，夹具的 pck 与源码不一致，`pack` 场景会失败] → 任务里要求重新导出并断言 `pack` 场景通过。
- [第 3 项可能实测出 `LoadMod` 确实重复应用补丁] → 这不是本变更要修的问题（属 `src/`），任务要求**如实固化实测行为并作为独立议题上报**，而不是顺手改产品代码。
- [分组后 runner 的夹具路径全部要改，漏一处会让对应场景失败] → 全套 e2e 必须绿，这是验收条件。
- [脚本改名会让文档里的旧命令失效] → 任务要求同步更新 `tests/README.md` 与 `AGENTS.md`，并以"文档里的命令可直接复制运行"为验收。

## Migration Plan

一次性重命名与补齐，无灰度。回滚：`git revert` 本次提交；`src/Modot` 未被触碰，因此回滚不影响产品。

## Open Questions

（无。）
