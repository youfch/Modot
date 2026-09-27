# Spec Delta

## ADDED Requirements

### Requirement: Duplicate IDs are reported the same way across calls
重复 ID SHALL 无论在**同一次加载调用内**还是**跨调用**，都以同一种方式报告。

#### Scenario: 调用内的重复 ID 记录并跳过
- **WHEN** 一次加载调用传入两个相同 ID 的 mod 目录
- **THEN** 只加载其中一个，另一个被记录为 `Duplicate ID` 错误并跳过

#### Scenario: 跨调用的重复 ID 同样记录并跳过
- **WHEN** 同一进程内再次加载一个已经加载过的 mod 目录
- **THEN** 第二次加载不抛异常，且返回零个 mod
- **AND** 日志中包含 `Duplicate ID` 原因
- **AND** 不抛出字典的 `ArgumentException`

#### Scenario: 已加载数据不受第二次加载影响
- **WHEN** 第二次加载因重复 ID 被跳过
- **THEN** 第一次加载的数据没有被再次打补丁