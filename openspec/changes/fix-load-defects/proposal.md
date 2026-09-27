# 修复加载路径上的四个实测缺陷

## Why

扩覆盖实测到四个缺陷（前三个由 `normalize-test-suite` 的四项覆盖与 `cover-load-lifecycle` 的失败路径抓到），目前都以"照实固化"的断言记录在案 —— 断言的是**实测行为**，并在注释里写明"修复时该断言会故意失败"。测试变更不掺产品行为变更，所以修复在此立案。

| # | 缺陷 | 实测证据 |
|---|---|---|
| 1 | `Metadata.IsValid` 是死代码：`[AfterDeserialization]` 钩子只匹配 public 成员，而它是 `private` | 3 个单测实测"不抛异常" |
| 2 | `LoadMod` 把同一个数据根节点入列两次，对自己数据重复应用补丁 | `APPLIED:2` |
| 3 | `LoadPatches` 不包 `SerializationException`，异常契约不一致 | `bad-patch` 抛 `SerializationException` |
| 4 | `Before`/`After` 的语义与 `Mod.Metadata` 的两处文档注释正好相反 | `order`/`order-before` 的 `INPUT` / `ORDER` 对比 |

## What Changes

- **缺陷 1**：`GDSerializer` 的 `[AfterDeserialization]` 钩子查找改用与属性/字段扫描相同的绑定标志（含 `NonPublic`），使 `IsValid` 真正执行；同文件另一处 `GetAllMembers<MethodInfo>()`（序列化侧钩子）需一并复核
- **缺陷 2**：`ModLoader.LoadMod` 去掉多余的 `.Append(mod.Data?.DocumentElement)` —— 该 mod 已在 `loadedMods` 里，其根节点已被枚举到
- **缺陷 3**：`Mod.LoadPatches` 按 `Metadata.Load` 的同一模式，把非 `ModLoadException` 的异常包成 `ModLoadException`
- **缺陷 4**：交换 `SortModMetadata` 的两条图边，使实现符合文档语义

## Impact

- **BREAKING（语义）**：
  - 缺陷 4 修复后 `Before: X` 会把 X 排在**前面**（此前是后面）。依赖旧反向行为的 mod，加载顺序会**翻转**
  - 缺陷 1 修复后，此前被静默接受的非法元数据会被**拒绝**，这类 mod 将不再加载
- `LoadMod` 对自己数据的补丁次数由 2 变 1
- 未知类型补丁的异常类型由 `SerializationException` 变 `ModLoadException`
- 所有"固化实测行为"的断言会被**故意打红**，需同步改为断言修正后的行为：`order`、`order-before`、`single-load` 三个场景 + `MetadataTests` 的三个用例
- 无公共 API 签名变更