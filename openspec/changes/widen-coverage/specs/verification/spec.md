# Spec Delta

## ADDED Requirements

### Requirement: Dependency satisfaction is covered
`Dependencies` 的**满足**路径 SHALL 被覆盖，而不只是缺失路径。

#### Scenario: 满足的依赖保留依赖者
- **WHEN** 两个 mod 同时加载，其中一个声明了对另一个的依赖
- **THEN** 两者都被加载，依赖者不因依赖被满足而遭剔除

#### Scenario: 依赖不参与排序
- **WHEN** 依赖者被放在被依赖者**之前**传入
- **THEN** 加载顺序不因该依赖声明而改变

### Requirement: Known-broken behaviour is pinned by a test
已确证不可用的行为 SHALL 由测试钉住，而不只写在文档里。

#### Scenario: 坏类型以预期失败被钉住
- **WHEN** 某个补丁类型对任何实际用法都抛异常
- **THEN** 存在一个预期失败场景，断言其失败**与具体的失败消息**
- **AND** 该场景在该类型被修复时转为失败，从而指出变更点

### Requirement: Metadata edge shapes are covered
`Mod.xml` 的边界形态 SHALL 被覆盖。

#### Scenario: 缺必填成员或含未知元素都被拒绝
- **WHEN** `Mod.xml` 缺少某个必填成员，或含有匹配不到成员的未知元素
- **THEN** 加载抛出 `ModLoadException`

#### Scenario: 语法错误被报告
- **WHEN** `Mod.xml` 不是合法 XML
- **THEN** 加载抛出 `ModLoadException`

### Requirement: Multi-file data merge is covered
`Data/` 下多个文件的合并 SHALL 被覆盖。

#### Scenario: 每个数据文件的根都被并入
- **WHEN** 一个 mod 的 `Data/` 下有多个 XML 文件
- **THEN** 合并结果同时包含各文件的根节点与其内容
- **AND** 断言只取存在性、不取顺序（`GetFiles` 不承诺顺序）

### Requirement: Logger behaviour is covered
日志器 SHALL 有超出"加载类型不做 I/O"之外的覆盖，且覆盖对象 SHALL 与其真实公开面一致。

#### Scenario: 覆盖真实存在的行为
- **WHEN** 为日志器编写测试
- **THEN** 断言的对象是其公开面实际提供的成员，而不是文档或任务描述里设想的成员