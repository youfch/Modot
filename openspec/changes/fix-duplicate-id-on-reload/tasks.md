# Tasks

## 1. 修复

- [x] 1.1 `src/Modot/Modding/ModLoader.cs` 的 `LoadModMetadata` 把 `ModLoader.LoadedMods` 中已注册的 ID 纳入重复判断，使跨调用重复走既有的 `ModLoadException(..., "Duplicate ID")` 路径；注释说明为什么查重必须看注册表而不只是本次入参
- [x] 1.2 `tests/e2e/Invoke-E2E.ps1` 给场景表加 `ExpectOutput` 支持：声明后宿主输出必须包含该文本，否则判失败（与既有 `ExpectFailure` 互补）；捕获输出后仍要把它打印出来，不要吞掉日志
- [x] 1.3 `tests/e2e/Host/Host.cs` 的 `RunReload` 把断言由 `ArgumentException` 改为：不抛异常、返回 0 个 mod、首次数据未被再次打补丁；更新 remarks（不再是"固化缺陷"，而是"契约一致"）
- [x] 1.4 `reload` 场景加 `ExpectOutput = 'Duplicate ID'`，把"为什么是 0 个"也纳入断言
- [x] 1.5 验证 `reload` 场景 PASS；验证 `duplicate`、`incompatible`、`single-load` 与其余场景仍 PASS

## 2. 收口

- [x] 2.1 `dotnet test tests/unit/Modot.Tests` 退出码为 0
- [x] 2.2 `./tests/e2e/Invoke-E2E.ps1` 退出码为 0，全部场景 PASS
- [x] 2.3 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0，且未引入新警告
- [x] 2.4 更新 `tests/README.md`：缺陷表第 5 行标为已修复；场景表补 `ExpectOutput` 机制的说明；`reload` 的断言描述同步
- [x] 2.5 `openspec validate --all --strict` 通过