# AGENTS.md

面向 AI 编码代理与协作者的工程约定。面向人的贡献指南见 `CONTRIBUTING.md`，变更流程与规格见 `openspec/`。

## 项目是什么

**Modot** 是面向 Godot 应用的运行时 mod 加载器（C# 库，以 NuGet 包分发），可以加载 mod 的 C# 程序集、XML 数据与资源包（`.pck`），按各 mod 声明的加载顺序排序，并不执行代码地修补其它 mod 的 XML 数据。

上游 `Carnagion/Modot` 已冻结在 Godot 3。本仓库是 fork，自行承担 Godot 4 的迁移与后续维护。

## 仓库结构

```
Modot.sln              解决方案（经典 .sln 格式）
src/Modot/             库本体，唯一的产品代码项目
  Modding/             Mod、ModLoader、ModLoadException、ModStartupAttribute
  Modding/Patching/    补丁系统：IPatch 及其实现、Conditions/
  Utility/             ErrorException 与 Extensions/
src/GDSerializer/      序列化器（vendor 自上游 GDSerializer，已适配 Godot 4）
src/GDLogger/          日志器（vendor 自上游 GDLogger，已适配 Godot 4）
tests/Modot.Tests/     第一层测试：引擎无关，不需要引擎
tests/e2e/             第二层测试：需要引擎（Godot 4.7.2）
openspec/              OpenSpec 工作区
  config.yaml          项目上下文（含语言约束）
  specs/               已归档的能力规格
  changes/             进行中与已归档的变更
.opencode/             OpenSpec 为 opencode 生成的命令与技能（工具生成，不要手改）
scripts/               本机工具脚本
```

## 构建、测试、打包

```powershell
dotnet build Modot.sln        # 解决方案级构建
dotnet test tests/Modot.Tests    # 第一层：引擎无关的测试
./tests/e2e/run.ps1              # 第二层：需要引擎（Godot 4.7.2 .NET 版）
./scripts/pack-local.ps1      # 打包到本机 NuGet 落地区 D:/GNuget
```

两个容易踩的构建坑：

- **打包必须用 `Release`**。`Godot.NET.Sdk` 只在 `Debug` 配置下引用 `GodotSharpEditor`，用 Debug 打包会把 `GodotSharpEditor` 写进发布包的依赖声明里。
- **`dotnet pack` 在本 SDK 下不会自动构建**，会报 `NU5026`。先 `dotnet build`，再 `dotnet pack --no-build`。
- `scripts/pack-local.ps1` 已加入 `.gitignore`（属本机工具），因此新克隆的仓库里没有它，需要自行准备等价脚本。

## 目标平台与硬约束

- 目标为 `Godot.NET.Sdk/4.7.2` 与 `net10.0`。Godot 4.7.2 要求 .NET 8 及以上，本项目固定 `net10.0`，因此只有 `net10.0` 消费方能引用本包（`net8.0`/`net9.0` 会报 `NU1202`）。
- **不要引入绑定 Godot 3 的包**。`Godot.Directory` 与 `Godot.File` 在 Godot 4 中已不存在，对应 `Godot.DirAccess` 与 `Godot.FileAccess`。
- **凡引用 `GodotSharp` 的第三方依赖，必须把源码 vendor 进 `src/` 一起适配，不得以 NuGet 包形式依赖**。这类包与 Godot 版本强耦合（Godot 4 移除 `Godot.Directory`/`Godot.File`、把 `Vector2`/`Vector3` 的字段改名 `X`/`Y`/`Z` 等都足以让它们失效），而它们通常已随上游冻结、永远不会自行修复。
  **判据不能看 nuspec**：`GDSerializer` 与 `GDLogger` 的 nuspec 都**没有**声明 `GodotSharp`，但它们的 DLL 都引用了它。正确做法是**读每个包内 DLL 的程序集引用**。`Godot.NET.Sdk` 提供的 `GodotSharp`/`GodotSharpEditor` 本身不在此列。
  当前 `src/` 下的 vendor 项目：`src/GDSerializer`、`src/GDLogger`。
- **`DirAccess` 不是可复用的全局实例**：`DirAccess.Open(path)` 返回绑定到该路径的新实例。跨文件系统根的建目录与复制（例如 `res://` → `user://`）必须使用绝对路径形态（`DirAccess.MakeDirRecursiveAbsolute`、`DirAccess.CopyAbsolute`），因为实例方法只能在其被打开的那个根内操作。列举目录时用 `ListDirBegin()` 开始、用 `ListDirEnd()` 结束。
- **程序集加载使用 `AssemblyLoadContext`**，不要用 `Assembly.LoadFile`：后者会把程序集载入隔离上下文，使 `ModStartupAttribute` 可能解析到另一份类型标识，导致 `[ModStartup]` 方法被静默跳过（不报错的失败模式）。
- mod 程序集必须针对 Godot 4 编译；Godot 3 编译的程序集不受支持。
- **`GDSerializer` 与 `GDLogger` 已 vendor 进 `src/`**，以 `ProjectReference` 引用。包名用 `Modot.GDSerializer` / `Modot.GDLogger`，但**程序集名必须保持 `GDSerializer` / `GDLogger`、`AssemblyVersion` 保持 `1.0.0.0`** —— Modot 的公共元数据与 mod 程序集的类型标识都依赖它，不要"顺手"把两者改成一致。
  两者的 Godot 4 适配已完成：`VectorSerializer` 改读 `Vector2`/`Vector3` 的 `X`/`Y`/`Z`（Godot 4 改了字段名，沿用旧名会抛 `MissingFieldException`，实测）；日志器从已移除的 `Godot.File` 移植到 `FileAccess`，并把文件句柄改为**惰性**获取，使类型初始化不做 I/O。
  上游的 `System.CodeDom` 依赖**保留且刻意不升级**：它驱动 `TypeExtensions.GetDisplayName` 的输出，而那段类型名会被写进序列化 XML 的 `Type=` 属性、并作为补丁类型的解析判据。
  Modot 自身只序列化 `string`、`XmlNode` 与自有接口，**不要**把引擎类型放进 `[Serialize]` 成员。

## 文件编码与格式

- 所有文本文件使用 **UTF-8 无 BOM**；缩进、行尾与命名遵循 `.editorconfig`。
- **`.ps1` 脚本必须只含 ASCII 字符**。Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 当作 ANSI 读取，非 ASCII 字符会让解析器报"字符串缺少终止符"。中文说明请写在 `.md` 文件里，不要写进 `.ps1`。
- **不要用 `Get-Content -Raw` 读写无 BOM 的 UTF-8 文本文件**。PowerShell 5.1 会按 ANSI 解码它，再用 UTF-8 写回就成了双重编码的乱码（本仓库已被此坑坏过一次 `tasks.md`）。改文本用专门的编辑工具；确需脚本化时用 `[System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)` 读、用 `[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))` 写。
- 解决方案使用经典 `Modot.sln`。注意 .NET 10 的 `dotnet new sln` 默认生成 `.slnx`，需要显式传 `--format sln`。
- 公共 API 的破坏性变更必须在变更提案里标注 `BREAKING`。

## 忽略规则策略

凡是构建产物、编辑器/IDE 生成物、本机工具目录，**一律**加入 `.gitignore`，不要等出问题再逐个补。全量清单以 `.gitignore` 为准，重点包括：`.godot/`、`.mono/`、`.import/`、`bin/`、`obj/`、`*.nupkg`、`.vs/`、`.vscode/`、`.idea/`、`.omo/`、`.codegraph/`、`scripts/pack-local.ps1`。

必须保留入库：`.editorconfig`、`Modot.sln`、`openspec/`、`.opencode/`、`AGENTS.md`、`README.md`、`CONTRIBUTING.md`、`LICENSE`。

改完忽略规则要用 `git check-ignore` 做**双向**验证（该忽略的被忽略、不该忽略的没被忽略），不要只看 `git status`。

## OpenSpec 工作流（本项目的默认路径）

任何非平凡的改动都走 OpenSpec，**不要直接改代码**：

1. **提案**：`/opsx-propose` 或 `openspec new change <kebab-case-名字>`，产出 `proposal.md`、`specs/<能力>/spec.md`、`design.md`、`tasks.md`
2. **实施**：评审通过后 `/opsx-apply`，边做边在 `tasks.md` 勾选
3. **归档**：完成后 `/opsx-archive`，把规格沉淀进 `openspec/specs/`

常用命令：

```powershell
openspec list                              # 列出进行中的变更
openspec status --change <名字>            # 制品完成度
openspec validate --all --strict           # 校验
openspec show <名字>                       # 查看某个变更或规格
openspec doctor                            # 检查工作区健康
```

约定：

- 制品正文用**简体中文**，结构性标题与 `SHALL`/`MUST` 关键字保留英文。该约束写在 `openspec/config.yaml` 的 `context` 字段里，是权威来源，改动流程时一并维护。
- `.opencode/` 下的命令与技能由 OpenSpec 生成，用 `openspec update` 升级，不要手改。
- 不改变任何产品行为的改动（纯重构、工具、文档）在变更的 `.openspec.yaml` 里设 `skip_specs: true`，不要为了通过校验而虚构需求。
- **不要在本文件中加入 OpenSpec 的起止标记**（即形如 HTML 注释包裹的 OPENSPEC START/END 标记）。OpenSpec 会把含该标记的根级 `AGENTS.md` 判定为遗留文件，并在 `init`/`update` 时删除它。

## 提交纪律

- **未经明确指示，不得执行 `git commit` 或 `git push`。**
- 提交信息遵循 `CONTRIBUTING.md`：首字母大写、使用祈使语气。
- 不要删除或改写他人的手工修改。若发现文件内容与预期不符，先提示并询问，再动手。

## 引擎与验证能力

本机可用 **Godot 4.7.2 (.NET / mono)**：

- 引擎（GUI）：`D:\APP\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe`
- 引擎（**控制台**）：同目录下的 `Godot_v4.7.2-stable_mono_win64_console.exe` —— Windows 上**必须用这个**才能捕获 stdout，脚本化运行一律用它
- 版本串：`4.7.2.stable.mono.official`

验证分三层，不要跳过或混淆：

- **编译级**：`dotnet build`，不需要引擎
- **引擎无关测试**：纯 .NET 逻辑（元数据解析、标量/集合序列化、`Vector2`/`Vector3` 往返、日志器类型加载等）
- **运行时（需要引擎）**：`DirAccess` 的遍历/递归/跨根复制、`ProjectSettings.LoadResourcePack` 加载 `.pck`、`GD.Print`/`GD.PushError` 的输出、`Node` 序列化、日志文件写入

运行时验证用 headless 运行，例如：

```powershell
& "<引擎目录>\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path <项目目录> --script res://<脚本>
```

`--path` 指定项目、`--headless` 不建窗口、退出码可用于断言、stdout 可被脚本捕获。**引擎路径不要硬编码进版本控制的脚本**，走环境变量并提供默认值。

注意引擎只有 **4.7.2**、没有 Godot 3，所以迁移前（Godot 3）连编译都不可做：`Godot.NET.Sdk/3.3.0` 通过 `<项目目录>\.mono\assemblies\` 的 HintPath 引用 `GodotSharp`，需要本机装有 Godot 3 编辑器。这一点仍然成立。
