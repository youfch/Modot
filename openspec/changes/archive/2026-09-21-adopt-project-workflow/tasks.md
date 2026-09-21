# Tasks

## 1. OpenSpec 工作区

- [x] 1.1 运行 `openspec init . --tools opencode --language "Simplified Chinese"`；验证生成 `openspec/config.yaml`、`openspec/specs/.gitkeep`、`openspec/changes/archive/.gitkeep`，以及 `.opencode/commands/opsx-*.md`（6 个）与 `.opencode/skills/openspec-*/SKILL.md`（6 个）
- [x] 1.2 在 `openspec/config.yaml` 的 `context` 中补齐项目上下文（仓库布局、目标平台、依赖、构建与验证命令、打包方式、提交纪律、语言约束）；验证 `openspec context --json` 解析出根路径 `E:\Work\Hub\Modot`，且 `openspec config list` 可读出配置
- [x] 1.3 验证 `openspec doctor` 报告根目录正常（无遗留文件、无坏引用）
- [x] 1.4 把 `openspec/` 与 `.opencode/` 纳入版本控制；验证 `git status --porcelain` 把两者列为未跟踪的新文件（本项目约定由用户决定何时提交）

## 2. AGENTS.md

- [x] 2.1 新增根级 `AGENTS.md`（简体中文），覆盖仓库结构、构建/测试/打包命令、Godot 4.7.2 + net10.0 约束、OpenSpec 工作流入口、文档语言、"构建产物/IDE 生成物/本机工具一律加入忽略"的策略、`.ps1` 仅限 ASCII 的规则，以及未经明确指示不得 commit/push；验证文件存在、UTF-8 无 BOM，且**不含** OpenSpec 的起止标记
- [x] 2.2 再次运行 `openspec doctor` 并验证仍报告根目录正常，确认 `AGENTS.md` 未被判定为 OpenSpec 遗留文件（用与 openspec 相同的判定式复核：起止标记字符串均不存在，因此判定为否）

## 3. 本地 NuGet 产物

- [x] 3.1 新增 `scripts/pack-local.ps1`，把 `.nupkg` 输出到 `D:/GNuget`；验证脚本退出码为 0 且 `D:/GNuget/Modot.3.0.0.nupkg` 生成。脚本刻意只含 ASCII：Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 当 ANSI 读取，含中文会直接解析失败（见 design.md - D6）
- [x] 3.2 验证 `scripts/pack-local.ps1` 已被忽略：`git check-ignore scripts/pack-local.ps1` 有输出，且 `git status --porcelain` 不报告该脚本（该规则随 `upgrade-to-godot-4-7-2` 的 D7 一并落地，见 design.md - D5）

## 4. 语言约束生效性

- [x] 4.1 验证 `openspec/config.yaml` 的 `context` 含简体中文约束，且本变更与 `upgrade-to-godot-4-7-2` 的制品正文均为简体中文（结构性标题与 `SHALL`/`MUST` 除外）
