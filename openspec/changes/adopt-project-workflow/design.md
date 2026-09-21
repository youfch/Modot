# Design

## Context

动机见 proposal.md - Why。与方案相关的现状：

- 仓库此前没有 `openspec/` 与 `.opencode/`；用户已明确要求全面采用 OpenSpec 工作流。
- 已在本机核实 OpenSpec CLI 版本为 1.13.1，其 `init` 支持 `--tools` 与 `--language` 非交互选项，且 `--language` 的取值是**自由文本**，会被写入 `openspec/config.yaml` 的 `context` 字段，形如 `Language: <value>` 与 `All artifacts must be written in <value>.`。
- 已在本机核实 1.13.1 的 `legacy-cleanup` 模块会把**根级 `AGENTS.md`** 视为遗留文件：它读取根级 `AGENTS.md`，当且仅当内容同时包含 OpenSpec 的 start 与 end 标记时判定为 OpenSpec 遗留文件并删除。1.13.1 中的 `agents` 工具也不再生成根级 `AGENTS.md`，而是生成 `.agents/skills/`。
- 全局 OpenSpec 配置的 profile 为 `custom`，工作流包含 propose、explore、apply、update、sync、archive，`delivery: both`（命令与技能都生成）。
- `D:/GNuget` 目录已存在。

## Goals / Non-Goals

**Goals:**

- 让"先提案、后实现、再归档"成为本项目的默认路径，并把项目上下文（含语言约束）固化到工具能读到的地方，而不是散落在每次对话里。
- 给 AI 代理一个稳定、中文的工程约定入口。

**Non-Goals:**

- 不配置 CI、不引入远端包源、不把 `D:/GNuget` 变成团队共享源。
- 不改动 OpenSpec 的工作流集合（沿用全局 profile 的 6 个工作流），也不新增自定义 schema。
- 不把本地脚本纳入版本控制（用户明确要求忽略）。

## Decisions

### D1 语言约束放在 `openspec/config.yaml` 的 `context`，而不是每个制品里重复

`context` 会被 OpenSpec 在每次生成制品时读取并作为约束作用于模型，是贯彻语言要求的唯一权威位置；在每个制品里重复声明既冗余又容易不一致。同时把项目其余上下文（布局、目标平台、构建命令、提交纪律）一并写入，避免在每次 propose 时重新推断。

替代方案：只依赖用户口头要求 —— 放弃，跨会话不成立。

### D2 `AGENTS.md` 由人工维护，且不得包含 OpenSpec 起止标记

1.13.1 已不再生成根级 `AGENTS.md`，并把带 OpenSpec start/end 标记的根级 `AGENTS.md` 判定为**遗留文件**，会在 `init`/`update` 的清理流程中删除。因此本设计选择：

- `AGENTS.md` 由人（或代理）手工编写，面向项目约定，而不是由 OpenSpec 托管。
- 文件中**禁止**出现 OpenSpec 的 start/end 标记，以免在未来的 `openspec update` 中被清掉。

这是一条容易踩的坑，因此写成任务 2.1/2.2 的显式验证项而非仅靠约定。

替代方案：使用 `--tools opencode,agents` 让工具生成 —— 放弃，`agents` 在 1.13.1 生成的是 `.agents/skills/`，与本项目只使用 OpenCode 的事实不符，且仍不会生成根级 `AGENTS.md`。

### D3 `scripts/pack-local.ps1` 入库外置（按用户决定）

脚本把 `.nupkg` 输出到 `D:/GNuget` 并**自身加入 `.gitignore`**。这意味着脚本内容不在评审范围内，只有"产出落到 `D:/GNuget`"这一行为是约定。这是用户的明确选择，本设计如实记录其后果：其他开发者不会自动获得该脚本，需要自行准备等价脚本。

替代方案：(a) 脚本入库、把 `D:/GNuget` 参数化 —— 更可复现，但被用户否决；(b) 用 `nuget.config` 声明本地源 —— 放弃，用户选择脚本方式。

### D4 用 `--language` 而非手改 `config.yaml` 写入语言

`openspec init --language "Simplified Chinese"` 会把语言写入 `context`，这是工具认可的入口，能保证格式与后续 `openspec update` 的兼容；手改 `config.yaml` 只用于补充项目其余上下文。

### D5 忽略规则采用"宁可写全"策略

按用户要求，凡是构建产物、编辑器/IDE 生成物、本机工具目录一律加入 `.gitignore`，而不是出问题后再逐个补。完整清单与取舍落在 `upgrade-to-godot-4-7-2` 的 design.md - D7 —— 由那个变更承载，是因为它必须改动 `.gitignore` 才能解除 `*.sln` 的忽略，忽略规则与它强绑定。本变更只补自己引入的本机脚本，不重复定义其余规则，避免同一文件两处维护。

刻意**不**忽略：`.editorconfig`、`Modot.sln`、`openspec/`、`.opencode/`、`AGENTS.md`。

### D6 `pack-local.ps1` 必须只含 ASCII（实测发现）

首版脚本用中文写注释与输出，实测**直接解析失败**：`Write-Host "已输出到 $OutputDirectory："` 报"字符串缺少终止符"。原因是 Windows PowerShell 5.1 会把**无 BOM** 的 `.ps1` 文件按 ANSI 代码页读取，UTF-8 的中文多字节序列因此被撕裂，引号配对被打断。

这与仓库"所有文本文件使用 UTF-8 无 BOM"的约定直接冲突，且不能靠加 BOM 绕过（BOM 本身被约定禁止）。因此决策：**`.ps1` 脚本一律只写 ASCII**，中文说明改放到 `.md`。这条规则同时写进脚本头部的注释与 AGENTS.md，避免后来者再踩。

替代方案：(a) 给 `.ps1` 加 UTF-8 BOM —— 放弃，违反仓库编码约定，且会让 `.ps1` 与其他文本文件的编码规则分叉；(b) 要求用 PowerShell 7+ 运行 —— 放弃，本机默认是 5.1，脚本必须开箱可用。

## Risks / Trade-offs

- [`AGENTS.md` 被 OpenSpec 当作遗留文件删除] → 由 D2 的"不含起止标记"约束 + 任务 2.2 的 `openspec doctor` 复核共同防住。
- [`.gitignore` 忽略 `pack-local.ps1` 导致该脚本无法被评审、也无法被他人复用] → 用户明确要求，已在 D3 记录后果。
- [`.opencode/` 与 `openspec/` 入库后，未来 OpenSpec 升级会重写这些文件并产生噪音 diff] → 属可接受成本；用 `openspec update` 主动升级而非手工编辑，可让 diff 保持工具生成的一致性。
- [项目上下文长期不更新会误导后续制品] → 把上下文当作活文档，在影响构建/布局的变更中一并更新（在 `upgrade-to-godot-4-7-2` 中已写入目标平台与布局）。

## Migration Plan

无灰度。回滚方式：删除 `openspec/`、`.opencode/`、`AGENTS.md`、`scripts/pack-local.ps1` 并撤销 `.gitignore` 中的一行即可回到变更前状态。

## Open Questions

（无。）
