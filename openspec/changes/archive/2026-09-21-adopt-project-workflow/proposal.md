# Proposal

## Why

本项目此前没有任何变更管理流程：改动直接落在分支上，缺少"先写清楚再实现"的检查点；仓库也没有面向 AI 编码代理的工程约定文件。结果是本机工具（构建产物去向）与文档语言都没有约定，代理每次都要重新推断，而用户已经因为一次不可控的大改动回退过整个分支。

用户已明确要求本项目**全面使用 OpenSpec 工作流**、后续文档使用中文，并要求给出 AGENTS.md 与本地 NuGet 产物去向。

## What Changes

- 引入 OpenSpec 工作流：`openspec/`（`config.yaml`、`specs/`、`changes/`）与 `.opencode/`（6 个 `opsx-*` 命令与 6 个 `openspec-*` 技能）。
- 在 `openspec/config.yaml` 的 `context` 中固化项目上下文（仓库布局、目标平台、依赖、构建与验证命令、打包方式、提交纪律、语言约束），使该约束对之后每一次 propose/apply 自动生效。
- 新增根级 `AGENTS.md`（简体中文），作为 AI 代理的工程约定入口：仓库结构、构建/测试/打包命令、Godot 4.7.2 + net10.0 约束、OpenSpec 工作流、文档语言、以及"未经明确指示不得 commit/push"。
- 新增 `scripts/pack-local.ps1`，把 `.nupkg` 输出到 `D:/GNuget`；按用户决定，**该脚本本身加入 `.gitignore`**（属本机工具，不入库）。
- 固化文档语言约定：OpenSpec 制品使用简体中文，结构性标题与 `SHALL`/`MUST` 关键词保留英文。

明确不在范围内：任何 `src/Modot` 的产品代码改动；发布包内容变更；把本地 NuGet 目录做成团队共享的包源。

## Capabilities

### New Capabilities

（无。本变更不改变任何产品行为：它引入的是开发流程、代理约定与本机工具，因此按 OpenSpec 的规则以 `skip_specs: true` 声明，不新增 spec。）

### Modified Capabilities

（无。没有任何既有 spec 的需求发生变化。）

## Impact

- **新增文件**：`openspec/config.yaml`、`openspec/specs/.gitkeep`、`openspec/changes/archive/.gitkeep`、`.opencode/commands/opsx-*.md`（6 个）、`.opencode/skills/openspec-*/SKILL.md`（6 个）、`AGENTS.md`、`scripts/pack-local.ps1`。
- **`.gitignore`**：新增 `scripts/pack-local.ps1` 忽略规则；Godot / .NET / IDE 的完整忽略清单由 `upgrade-to-godot-4-7-2` 统一重写（它必须改动该文件以解除 `*.sln` 的忽略），策略见该变更的 design.md - D7。
- **产品代码与发布包**：零影响，`src/Modot` 不被触碰，`.nupkg` 内容不变。
- **本机环境**：`D:/GNuget` 目录已存在，作为本机 NuGet 落地区。
- **约束**：`AGENTS.md` 必须**不含** OpenSpec 的起止标记，否则 openspec 1.13.1 会把它当作遗留文件清理（见 design.md - D2）。
