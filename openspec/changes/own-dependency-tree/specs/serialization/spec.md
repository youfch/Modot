# Spec Delta

## Purpose

定义 Modot 通过 vendored 的 `GDSerializer` 所提供的 XML 序列化契约：哪些类型在 Godot 4 上必须可被 (反)序列化、mod 数据的文本格式如何保持稳定，以及序列化器程序集的身份要求。

## ADDED Requirements

### Requirement: Vector (de)serialization on Godot 4
序列化器 SHALL 在 Godot 4 上正确 (反)序列化 `Godot.Vector2` 与 `Godot.Vector3`，且 SHALL NOT 因访问 Godot 3 时代的字段名而失败。

#### Scenario: Vector2 往返
- **WHEN** 一个含 `[Serialize] public Vector2` 成员的对象被序列化，再以同一类型反序列化
- **THEN** 两个方向都成功（不抛 `MissingFieldException`），且反序列化得到的两个分量与原始值在 float 精度内相等

#### Scenario: Vector3 往返
- **WHEN** 一个含 `[Serialize] public Vector3` 成员的对象被序列化，再以同一类型反序列化
- **THEN** 两个方向都成功，且反序列化得到的三个分量与原始值在 float 精度内相等

### Requirement: Node (de)serialization on Godot 4
序列化器 SHALL 在 Godot 4 上 (反)序列化 `Godot.Node` 及其派生类型，包括其可序列化成员与子节点层级。

#### Scenario: 带子节点的 Node 往返
- **WHEN** 一个含有可序列化成员与若干子节点的 `Node` 被序列化，再反序列化
- **THEN** 成员值被还原，且子节点数量与层级与原始一致

#### Scenario: 无子节点的 Node 序列化
- **WHEN** 一个没有任何子节点的 `Node` 被序列化
- **THEN** 结果不包含子节点元素，且不抛异常

### Requirement: Non-engine types unaffected
序列化器 SHALL 继续支持 BCL 类型与实现了自有接口的类型，且这些路径 SHALL NOT 需要引擎实例。

#### Scenario: 标量与集合往返
- **WHEN** 一个只含 `string`、`int` 与 `IEnumerable<T>` 成员的对象被序列化并反序列化
- **THEN** 往返成功，且不需要任何引擎实例

### Requirement: Stable mod data format
序列化器 SHALL 保持上游 2.0.3 的 XML 文本格式，使为 Modot 2.x 编写的 mod 数据无需修改即可加载。

#### Scenario: 2.x 元数据原样加载
- **WHEN** 一个采用 Modot 2.x 格式的 `Mod.xml` 被交给 `Mod.Metadata.Load`
- **THEN** 解析成功，且 `Id`、`Name`、`Author` 等字段值与文件内容一致

#### Scenario: 向量文本格式不变
- **WHEN** 一个 `Vector2` 或 `Vector3` 被序列化
- **THEN** 元素名与文本内容与上游 2.0.3 一致（文本形如 `(x, y)` 与 `(x, y, z)`），因此该变更不改变 mod 作者已依赖的文本形态

### Requirement: Stable type-name discriminator
序列化器 SHALL 以与上游一致的机制与文本形态生成类型名，并将其写入序列化输出作为类型判据，使按该格式编写的 mod 数据在后续版本中仍能被解析。

#### Scenario: 类型名写入序列化输出
- **WHEN** 一个多态对象（例如补丁或条件）被序列化
- **THEN** 输出中带有其类型名判据，且该文本由与上游相同的机制生成（不是另起一套类型名格式）

#### Scenario: 判据可被反解
- **WHEN** 含类型名判据的 XML 被反序列化
- **THEN** 目标类型被正确解析并实例化，不需要 mod 作者改动其 XML

### Requirement: Serializer assembly identity
序列化器 SHALL 以程序集名 `GDSerializer` 提供，并 SHALL 保留 `Godot.Serialization.*` 与 `Godot.Utility.*` 的公共类型全名，使按上游程序集编译的 mod 在运行时命中同一类型标识。

#### Scenario: 程序集名与类型全名保持
- **WHEN** 检查 vendored 序列化器产物
- **THEN** 程序集名为 `GDSerializer`、`AssemblyVersion` 与上游一致，且 `Godot.Serialization.SerializeAttribute`、`Godot.Serialization.Serializer`、`Godot.Utility.OrderedDictionary<TKey, TValue>` 等类型全名与上游一致

#### Scenario: mod 标注的特性可被识别
- **WHEN** 一个按上游 `GDSerializer` 编译、并用 `[Serialize]` 标注成员的 mod 程序集与 Modot 在同一加载上下文中加载
- **THEN** 反序列化能识别该特性并应用其语义，而不是因类型标识不同而静默忽略
