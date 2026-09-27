# Spec Delta

## ADDED Requirements

### Requirement: Load lifecycle coverage
测试套件 SHALL 覆盖加载生命周期的每个阶段，且每个阶段 SHALL 至少有一个场景或单元测试作为载体。

#### Scenario: 阶段与载体一一对应
- **WHEN** 检视已确证的 7 段生命周期清单
- **THEN** 每一段都能指出至少一个断言它的场景或单元测试

#### Scenario: 失败路径有阴性断言
- **WHEN** 加载一个资源包损坏的 mod
- **THEN** 抛出 `ModLoadException`
- **AND** 加载一个补丁文件非法的 mod 也同样抛出 `ModLoadException`

### Requirement: Startup method forms
套件 SHALL 覆盖 `[ModStartup]` 的无参与带参两种形态。

#### Scenario: 带参与无参的启动方法都会被调用
- **WHEN** 以默认开关加载一个同时声明了无参与带参 `[ModStartup]` 方法的 mod
- **THEN** 两个方法都被调用，且带参方法收到声明时给出的参数

#### Scenario: 关闭程序集执行时两者都不运行
- **WHEN** 以 `executeAssemblies: false` 加载同一个 mod
- **THEN** 两个方法都没有被调用

### Requirement: Repeated load state
套件 SHALL 覆盖同一进程内重复加载时的静态状态语义。

#### Scenario: 重复加载被固化
- **WHEN** 在同一进程内连续调用两次加载入口
- **THEN** 第二次的行为被照实断言，且在注释中说明该行为是实测值而非期望值

### Requirement: Patch and condition type matrix
补丁类型与条件类型 SHALL 各自至少被一个夹具使用。

#### Scenario: 补丁类型矩阵
- **WHEN** 检视已覆盖的补丁类型
- **THEN** `NodeAddPatch`、`NodeRemovePatch`、`NodeReplacePatch`、`AttributeSetPatch`、`AttributeRemovePatch`、`LogPatch`、`MultiPatch`、`TargetedPatch`、`ConditionalPatch` 都至少被一个夹具使用

#### Scenario: 条件类型矩阵
- **WHEN** 检视已覆盖的条件类型
- **THEN** `NodeExistsCondition`、`And`、`Or`、`Not`、`ModLoadedCondition` 都至少被一个夹具使用

### Requirement: Directory extension coverage
`DirectoryExtensions` 的遍历、递归与跨根复制 SHALL 在引擎运行时被覆盖。

#### Scenario: 跨根建目录与复制
- **WHEN** 在引擎中以一个根下的源向另一个根下的目标建目录并复制文件
- **THEN** 目标存在，且内容与源一致

#### Scenario: 目录列举成对收尾
- **WHEN** 列举一个目录下的条目
- **THEN** 列举结果完整，且列举状态被正确收尾（可重复列举而不泄漏）

### Requirement: Defects found are pinned not fixed
扩覆盖过程中实测到的产品缺陷 SHALL 以"照实固化"的断言记录，且 SHALL NOT 在本变更内修复。

#### Scenario: 缺陷以现状固化
- **WHEN** 实测到与文档或契约不符的产品行为
- **THEN** 断言记录的是**实测行为**，并在代码注释与 `tasks.md` 中说明这是缺陷、其修复属于另一变更
