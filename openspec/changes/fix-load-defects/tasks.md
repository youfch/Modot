# Tasks

## 1. 缺陷 1：元数据自校验从不执行

- [x] 1.1 复核 `src/GDSerializer/Serialization/Serializer.cs` 的两处 `GetAllMembers<MethodInfo>()`（反序列化侧钩子、序列化侧钩子），确认各自应使用的绑定标志，并把 `[AfterDeserialization]` 那处改为与属性/字段扫描一致的标志（含 `NonPublic`）
- [x] 1.2 把 `tests/unit/Modot.Tests/MetadataTests.cs` 的三个用例从"断言接受"改回"断言拒绝"（`Assert.Throws<ModLoadException>`），并删除关于死代码的注释
- [x] 1.3 验证 `dotnet test tests/unit/Modot.Tests` 退出码为 0；验证 `./tests/e2e/Invoke-E2E.ps1` 全套件仍绿（GDSerializer 是公共依赖，改动会波及所有反序列化路径)
  - 实测：单测 13/13（三个元数据用例由''不抛异常''转为抛 `ModLoadException`）；e2e 15/15 全绿，`alpha`/`pack`/`patches` 等依赖反序列化的场景未受影响

## 2. 缺陷 2：`LoadMod` 重复应用补丁

- [x] 2.1 `src/Modot/Modding/ModLoader.cs` 的 `LoadMod` 去掉多余的 `.Append(mod.Data?.DocumentElement)`（该 mod 已在 `loadedMods` 中），并在注释里写明为何 `loadedMods.Add` 的位置必须保持在补丁应用之前
- [x] 2.2 `tests/e2e/Host/Host.cs` 的 `RunSingleLoad` 把断言由 `applied is 2` 改为 `applied is 1`，并更新注释（不再是"固化缺陷"，而是"每根恰好一次"）
- [x] 2.3 验证 `single-load` 场景 PASS 且 `APPLIED:1`；验证全套件仍绿
  - 实测：`APPLIED:1`（修复前为 2），场景 PASS

## 3. 缺陷 3：`LoadPatches` 的异常契约不一致

- [x] 3.1 `src/Modot/Modding/Mod.cs` 的 `LoadPatches` 按 `Metadata.Load` 的同一形状，把非 `ModLoadException` 的异常包成 `ModLoadException`（注意 `yield return` 不能放在带 `catch` 的 `try` 内）
- [x] 3.2 新增夹具 `fixtures/failures/bad-patch-type/`：补丁根节点类型**存在但不实现 `IPatch`**，补上此前零覆盖的那条路径
- [x] 3.3 验证 `bad-patch` 与 `bad-patch-type` 两个场景的输出都是 `ModLoadException`（不再是 `SerializationException`）；验证全套件仍绿

## 4. 缺陷 4：`Before` / `After` 语义与文档相反（BREAKING）

- [x] 4.1 `src/Modot/Modding/ModLoader.cs` 的 `SortModMetadata` 交换两条图边，使实现符合文档（`After: X` -> X 在本 mod 之后；`Before: X` -> X 在本 mod 之前），并在注释里引用文档语义
- [x] 4.2 更新 `tests/e2e/Host/Host.cs` 的 `RunOrder` 与 `RunBefore` 断言到新语义，并改写两处 `remarks`（不再是"实测反转"，而是"实现与文档一致"）
- [x] 4.3 验证 `order`、`order-before`、`cycle` 场景 PASS；验证全套件仍绿
  - 实测：全部 15 场景 PASS，`order` -> `[after-a,after-b]`、`order-before` -> `[order-b,order-a]`（均符合文档语义且相对输入反转）。中途 `cross-patch` 曾失败：该夹具按旧语义声明 `After`，改为 `Before` 后通过
- [x] 4.4 把 `tests/README.md` 与 `AGENTS.md` 中关于该反转的说明改为已修复

## 5. 缺陷 5：重复加载的静态状态（先实测）

- [x] 5.1 新增场景 `reload`：同一进程内连续两次调用加载入口，打印两次的 mod 数与各数据根下的补丁产物数，按**实测结果**断言
- [x] 5.2 按实测结果决定：若证实重复应用，补充修复任务；若排除，把该场景留作回归护栏并在 `tasks.md` 记录结论

## 6. 收口

- [x] 6.1 `dotnet test tests/unit/Modot.Tests` 退出码为 0，三个元数据用例断言的是"拒绝"
- [x] 6.2 `./tests/e2e/Invoke-E2E.ps1` 退出码为 0，所有场景 PASS
- [x] 6.3 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0，且未引入新警告
- [x] 6.4 更新 `tests/README.md` 的"已知缺陷"表：逐条标注已修复及其新行为
- [x] 6.5 `openspec validate --all --strict` 通过