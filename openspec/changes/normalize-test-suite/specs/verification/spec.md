# Spec Delta

## ADDED Requirements

### Requirement: Test suite layout and naming
测试套件 SHALL 按"层"组织，第一层为层名、第二层为该项目或资产；层目录名 SHALL 与另一层保持同一命名风格。

#### Scenario: 两层对称
- **WHEN** 检查 `tests/` 下的目录结构
- **THEN** 存在两个层目录，分别承载引擎无关测试与引擎运行时测试，且两者命名风格一致（同为小写层名或同为项目式名）

#### Scenario: 引擎无关层是可执行的测试项目
- **WHEN** 在引擎无关层目录下运行 `dotnet test`
- **THEN** 该层作为一个测试项目被识别并执行，且不需要引擎

#### Scenario: 引擎运行时层被标明为 harness
- **WHEN** 查阅测试说明文档
- **THEN** 引擎运行时层被明确标注为 harness（宿主程序 + mod + 夹具 + 驱动脚本），而不是测试项目

### Requirement: Fixture organization
引擎运行时层的夹具 SHALL 按用途分组，SHALL NOT 存在不被任何场景或文档使用的夹具。

#### Scenario: 夹具按用途分组
- **WHEN** 检查夹具目录
- **THEN** 承载加载顺序、失败矩阵、补丁等不同用途的夹具位于各自的分组下，而不是平铺在同一层

#### Scenario: 没有死夹具
- **WHEN** 逐个检查每个夹具目录
- **THEN** 每个夹具要么被某个场景使用，要么在测试说明文档中被明确标注其用途

### Requirement: Loading lifecycle coverage
测试 SHALL 覆盖加载生命周期中的以下关键路径，且每条 SHALL 有可独立判定的断言。

#### Scenario: 跨 mod 补丁
- **WHEN** 一个 mod 的补丁作用于另一个 mod 的数据
- **THEN** 断言目标 mod 的数据确实被修改，从而验证"补丁作用到自身及所有先前已加载 mod 的数据"这一语义

#### Scenario: 关闭程序集执行
- **WHEN** 以 `executeAssemblies: false` 加载 mod
- **THEN** 断言其补丁仍然生效、而其 `[ModStartup]` 方法**没有**被执行

#### Scenario: 单个 mod 加载
- **WHEN** 用单个 mod 加载入口加载一个声明了依赖或顺序关系的 mod
- **THEN** 断言它不进行依赖检查与排序即被加载，且其自身数据的补丁效果被实测并记录

#### Scenario: 元数据校验的拒绝路径
- **WHEN** 元数据包含使其非法的内容（各列表之间存在交集、或含自身标识、或依赖与不兼容相交）
- **THEN** 加载以明确的失败被拒绝，而不是接受该元数据

### Requirement: Scenario failure expectation
驱动脚本 SHALL 支持声明"预期失败"的场景，使失败路径的夹具能以断言形式进入常规运行，而不是只能手动执行。

#### Scenario: 预期失败场景被判为通过
- **WHEN** 一个场景被声明为预期失败，且宿主按预期以非零退出码结束
- **THEN** 该场景在常规运行中被判为通过

#### Scenario: 预期失败场景意外成功被判为失败
- **WHEN** 一个场景被声明为预期失败，但宿主以 0 结束
- **THEN** 该场景被判为失败
