# Spec Delta

## Purpose

定义 Modot 所支持的 Godot 引擎与 .NET 运行时版本、对为更早版本编写的 mod 数据给出的向后兼容承诺，以及加载失败必须产出的诊断。

## ADDED Requirements

### Requirement: Supported engine and runtime
Modot SHALL 针对 `Godot.NET.Sdk/4.7.2` 构建并以 `net10.0` 为目标框架，从而可被运行在 Godot 4.7.2 及以上 .NET 版引擎中的 `net10.0` 项目引用。

#### Scenario: net10.0 消费方可以引用
- **WHEN** 一个 `net10.0` 的 Godot 4.7.2 项目引用 Modot 3.0.0
- **THEN** 包还原与构建成功，且不出现目标框架不兼容错误

#### Scenario: 低于 net10.0 的消费方被明确拒绝
- **WHEN** 一个 `net8.0` 项目引用 Modot 3.0.0
- **THEN** 包还原失败并报告目标框架不兼容，而不是静默降级到其他版本

### Requirement: Mod data backward compatibility
Modot SHALL 在无需修改任何文件的前提下加载按 Modot 2.x 格式编写的 mod 目录，即 `Mod.xml` 的元数据字段、`Data/**/*.xml` 与 `Patches/**/*.xml` 的格式与解释方式保持向后兼容。

#### Scenario: 2.x 格式的 mod 目录原样加载
- **WHEN** 一个沿用 Modot 2.x 格式的 mod 目录（含 `Mod.xml`、`Data/`、`Patches/`）被传入 `ModLoader.LoadMods`
- **THEN** 该 mod 成功加载、其补丁被应用，且该 mod 目录中的任何文件都无需改动

#### Scenario: 合并后的 Data XML 与 2.x 一致
- **WHEN** 同一组 mod 目录分别由 Modot 2.x 与 Modot 3.0.0 加载
- **THEN** 各 mod 合并得到的 `Data` XML 文档内容一致

### Requirement: Mod assemblies must target Godot 4
Modot SHALL 支持加载以 Godot 4 为目标编译的 mod 程序集，并 SHALL NOT 声称支持以 Godot 3 `GodotSharp` 编译的程序集。

#### Scenario: Godot 4 程序集的启动方法被调用
- **WHEN** 某 mod 的 `Assemblies/` 下存在一个针对 Godot 4 编译、且含有 `[ModStartup]` 静态方法的程序集
- **THEN** 加载该 mod 时会调用该方法

### Requirement: Load failures reach the Godot log
Modot SHALL 把导致 mod 无法加载的原因写入 Godot 日志，并 SHALL NOT 依赖任何绑定 Godot 3 的日志包。

#### Scenario: 重复标识符
- **WHEN** 两个 mod 目录声明相同的 `Id` 并被一起传给 `ModLoader.LoadMods`
- **THEN** 日志中出现该目录的加载失败信息，且其中一个 mod 不被加载

#### Scenario: 依赖缺失
- **WHEN** 某 mod 声明的 `Dependencies` 中存在未被加载的标识符
- **THEN** 该 mod 被移出加载集合且失败被记录，其余 mod 仍按顺序加载

#### Scenario: 互相不兼容
- **WHEN** 两个 mod 的 `Incompatible` 互相指向对方并被一起加载
- **THEN** 日志中出现不兼容的失败信息，且二者不会被同时加载
