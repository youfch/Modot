# Spec Delta

## ADDED Requirements

### Requirement: Metadata validation runs
`Mod.Metadata` 的自校验 SHALL 在反序列化后真正执行；非法的加载顺序与依赖声明 SHALL 被拒绝。

#### Scenario: 自相矛盾的元数据被拒绝
- **WHEN** 元数据把自己的 ID 写进 `Incompatible`、或让同一 ID 同时出现在 `Before` 与 `After`、或让 `Dependencies` 与 `Incompatible` 相交
- **THEN** 加载抛出 `ModLoadException`

#### Scenario: 合法元数据不受影响
- **WHEN** 加载声明了互不相交的依赖与加载顺序的元数据
- **THEN** 正常加载

### Requirement: Declared load order matches its documentation
`Before` 与 `After` SHALL 具有其文档注释所述的语义。

#### Scenario: Before 把目标排在前面
- **WHEN** 一个 mod 声明 `Before: X` 且两者都被加载
- **THEN** X 排在该 mod **之前**

#### Scenario: After 把目标排在后面
- **WHEN** 一个 mod 声明 `After: X` 且两者都被加载
- **THEN** X 排在该 mod **之后**

### Requirement: Patches apply once per data root
每次加载 SHALL 对每个数据根节点**恰好**应用一次补丁。

#### Scenario: 单加载不再重复应用
- **WHEN** 用 `LoadMod` 加载一个带非幂等补丁（`NodeAddPatch`）的 mod
- **THEN** 该补丁在其数据根下产生的节点只出现**一次**

#### Scenario: 多加载保持每根一次
- **WHEN** 用 `LoadMods` 加载同样带非幂等补丁的 mod
- **THEN** 该补丁在其数据根下产生的节点同样只出现一次

### Requirement: Consistent failure type for invalid patches
补丁无法加载时 SHALL 抛 `ModLoadException`，且不因失败形态而改变。

#### Scenario: 两种失败形态都是 ModLoadException
- **WHEN** 一个补丁文件的根节点类型名**不存在**
- **THEN** 抛 `ModLoadException`
- **AND** 根节点类型**存在**但不实现 `IPatch` 时同样抛 `ModLoadException`