# Spec Delta

## Purpose

定义 Modot 的日志契约：条目同时进入 Godot 日志与日志文件、按严重度分级、可被订阅，并且日志器在未产生任何条目时不得进行文件 I/O。

## ADDED Requirements

### Requirement: Entries reach the Godot log
Modot SHALL 把日志条目写入 Godot 日志，并按严重度选择写入方式。

#### Scenario: 错误级条目
- **WHEN** 一个 mod 因重复标识符或依赖缺失而加载失败
- **THEN** Godot 日志中出现该条失败信息，且被标记为错误级

#### Scenario: 通知级条目
- **WHEN** 一条通知级信息被写入
- **THEN** 它以通知级进入 Godot 日志，而不是错误级

### Requirement: File logging is lazily opened
Modot SHALL 支持把条目追加写入日志文件，且 SHALL NOT 在日志器类型初始化阶段打开或创建日志文件。

#### Scenario: 未产生条目时不触碰文件系统
- **WHEN** 日志器类型被加载但尚未写入任何条目
- **THEN** 日志文件既未被创建也未被打开，且该类型加载过程不抛异常 —— 因此在没有 Godot 运行时的进程中也安全

#### Scenario: 首条条目落盘
- **WHEN** 启用日志文件后写入第一条条目
- **THEN** 日志文件被创建，且该条目以带时间戳与严重度的单行形式写入

#### Scenario: 日志文件路径可配置
- **WHEN** 调用方设置日志文件路径
- **THEN** 后续条目写入该路径，而不是默认路径

### Requirement: Severity levels and entry event
Modot SHALL 区分通知、警告、错误三级严重度，并 SHALL 在条目被写入时对外发布该条目。

#### Scenario: 三级严重度各自可辨
- **WHEN** 分别写入通知、警告、错误三级条目
- **THEN** 每个条目的严重度与其写入时所用级别一致

#### Scenario: 订阅者收到条目
- **WHEN** 调用方订阅了条目写入事件并随后写入一条条目
- **THEN** 订阅者收到该条目，且其内容与被写入的内容一致

### Requirement: Logger assembly identity
日志器 SHALL 以程序集名 `GDLogger` 提供，并 SHALL 保留 `Godot.Log` 的公共类型全名，使按上游程序集编译的消费方在运行时命中同一类型标识。

#### Scenario: 程序集名与类型全名保持
- **WHEN** 检查 vendored 日志器产物
- **THEN** 程序集名为 `GDLogger`、`AssemblyVersion` 与上游一致，且 `Godot.Log` 与其条目/严重度类型的公共全名与上游一致
