# Spec Delta

## Purpose

定义 Modot 发布包的依赖清单契约：只声明运行时会实际加载的依赖、不残留未使用的依赖、编译期专用包不外泄，且 vendored 依赖以新包名发布同时保留上游程序集名。

## ADDED Requirements

### Requirement: Godot-referencing dependencies are vendored
任何引用 `GodotSharp` 的第三方依赖 SHALL 以源码形式纳入 `src/` 并随 Godot 版本适配，SHALL NOT 以 NuGet 包形式依赖；由 `Godot.NET.Sdk` 提供的引擎程序集本身不在此列。

#### Scenario: 依赖树中不存在第三方 Godot 绑定包
- **WHEN** 枚举 Modot 解析出的全部包，并读取每个包内 DLL 的程序集引用
- **THEN** 除 `GodotSharp`/`GodotSharpEditor` 之外，没有任何包的 DLL 引用 `GodotSharp`；若存在，它必须已被 vendor 进 `src/`

#### Scenario: 判据不依赖包元数据
- **WHEN** 判断某个依赖是否需要 vendor
- **THEN** 依据是它 DLL 的程序集引用，而不是它的 nuspec —— 因为 `GDSerializer` 与 `GDLogger` 的 nuspec 都未声明 `GodotSharp`，仅看元数据会漏判

### Requirement: Only real runtime dependencies are declared
发布包 SHALL 只声明运行时会实际加载的依赖，SHALL NOT 声明纯编译期专用的包，SHALL NOT 残留未被任何代码使用的包。

#### Scenario: 编译期专用包不外泄
- **WHEN** 检查 `Modot.nupkg` 的依赖列表
- **THEN** 其中不含 `JetBrains.Annotations`

#### Scenario: 未使用的包被移除
- **WHEN** 检查本仓库产出的三个包的依赖列表
- **THEN** 其中不含 `Carnagion.MoreLinq`

#### Scenario: 真实依赖被保留
- **WHEN** 检查序列化器包的依赖列表
- **THEN** 其中包含 `System.CodeDom`，因为类型名生成在运行时依赖它，删掉会破坏补丁类型解析

#### Scenario: 被移除的包确实无用
- **WHEN** 移除某个包后重新构建
- **THEN** 构建成功，不发生因缺少该包而导致的编译错误

### Requirement: Vendored dependencies keep assembly identity
每个 vendored 依赖 SHALL 以不同于上游的包名发布，SHALL 保留上游的程序集名，且发布包的依赖声明 SHALL 指向该 vendored 包名。

#### Scenario: 包名与程序集名分离
- **WHEN** 检查 `Modot.nupkg` 的依赖声明与 `Modot.GDSerializer` 包内的程序集
- **THEN** 依赖声明的包名为 `Modot.GDSerializer`，而该包内的程序集名为 `GDSerializer`

#### Scenario: 日志器同样处理
- **WHEN** 检查 `Modot.nupkg` 的依赖声明与 `Modot.GDLogger` 包内的程序集
- **THEN** 依赖声明的包名为 `Modot.GDLogger`，而该包内的程序集名为 `GDLogger`

#### Scenario: vendored 包自带许可证
- **WHEN** 检查任一 vendored 包的产物
- **THEN** 包内包含其 MIT 许可证文件与上游版权声明

### Requirement: Packages land in dependency order
本机打包 SHALL 按依赖顺序产出三个包，使依赖在消费者之前可用。

#### Scenario: 三个包都已落地
- **WHEN** 本机打包脚本执行完成
- **THEN** 落地区同时存在序列化器包、日志器包与 Modot 包，且前两者先于 Modot 包写入

#### Scenario: 不含已移除的依赖
- **WHEN** 检查三个包的依赖列表
- **THEN** 都不含 `GDLogger` 这一上游包名
