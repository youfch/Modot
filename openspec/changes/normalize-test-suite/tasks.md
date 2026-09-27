# Tasks

## 1. 目录与脚本重命名

- [x] 1.1 用 `git mv` 把 `tests/Modot.Tests/` 移到 `tests/unit/Modot.Tests/`；验证 `dotnet test tests/unit/Modot.Tests` 退出码为 0 且 10 个既有 `[Fact]` 全部通过
- [x] 1.2 用 `git mv` 把 `tests/e2e/run.ps1` 改名为 `Invoke-E2E.ps1`、`tests/e2e/assemble.ps1` 改名为 `Update-ModAssets.ps1`，并同步两者互相调用的路径与注释；验证两个脚本的**非 ASCII 字符数为 0**
- [x] 1.3 用 `git mv` 把 `tests/e2e/Mods/AlphaMod/Scn/` 改名为 `Scenes/`，同步 `BoxRot.tscn` 里的 `res://Scenes/BoxRot.cs`、以及宿主里 `res://Scenes/BoxRot.tscn` 的路径；验证 `dotnet build Modot.sln` 退出码为 0
- [x] 1.4 用 `git mv` 把 `Mods/AlphaMod/Alpha.cs` 改名为 `AlphaMod.cs` 并把类 `Alpha` 改名为 `AlphaMod`（mod id **保持** `alpha`）；验证文件名为 `AlphaMod.cs` 且其中类名为 `AlphaMod`
- [x] 1.5 重新导出 pck（`./tests/e2e/Update-ModAssets.ps1`）；验证 `pack` 场景仍 PASS，且 pck 内含 `Scenes/BoxRot.tscn`

## 2. 夹具重组

- [ ] 2.1 建 `fixtures/load-order/`、`fixtures/failures/`、`fixtures/invalid/`、`fixtures/cross-patch/`，用 `git mv` 把现有夹具归位：`after-a/b` → `load-order/`；`duplicate-a/b`、`missing-dep`、`cycle-a/b`、`incompatible-a/b` → `failures/`；`patches/` 原地保留
- [ ] 2.2 把 `order-a`/`order-b` 移入 `load-order/` 并**加入 runner**（新增 `order-before` 场景断言 `Before` 方向的排列），验证该场景 PASS
- [ ] 2.3 把 `broken/` 移入 `fixtures/invalid/invalid-root/`，并在 runner 中引用它；验证 `git check-ignore` 不忽略它
- [ ] 2.4 逐个检查 `fixtures/` 下每个夹具，验证它要么被某个场景使用、要么在 `tests/README.md` 中被明确标注用途（无死夹具）

## 3. Host 拆分

- [ ] 3.1 把各场景方法移入 `tests/e2e/Host/Scenarios/<场景>Scenario.cs` 的 `partial class Host` 分片，`Host.cs` 只保留入口（参数解析、默认场景、收口）与 `Check`/`Fail`；验证 `Host.cs` 行数降到 200 行以内
- [ ] 3.2 验证拆分后 `dotnet build Modot.sln` 退出码为 0、且全部既有场景仍 PASS（Godot 的脚本绑定未受影响）

## 4. runner 的"预期失败"机制

- [ ] 4.1 给场景表加 `ExpectFailure` 支持：声明为预期失败时，宿主必须**以非零退出码**结束才算通过；若意外以 0 结束则判该场景失败
- [ ] 4.2 用 `invalid-root` 夹具加一个预期失败场景，验证常规运行下该场景判 PASS
- [ ] 4.3 反向验证：临时去掉该场景的 `ExpectFailure` 后运行，它应判 FAIL —— 证明该机制不是永远通过（验证后恢复）

## 5. 四项覆盖补齐

- [ ] 5.1 新增 `cross-patch/base`（提供数据）与 `cross-patch/overlay`（补丁指向 base 的节点）；新增场景 `cross-patch`，断言**base 的数据被 overlay 的补丁改动**（而不是 overlay 自己的数据）
- [ ] 5.2 新增场景 `no-assemblies`：以 `executeAssemblies: false` 加载 AlphaMod，断言其补丁仍生效、且 `[ModStartup]` 的 marker 文件**不存在**
- [ ] 5.3 新增场景 `single-load`：用 `LoadMod` 加载一个声明了不存在依赖的夹具，断言它**照常加载**（证明不查依赖、不排序）；并实测该 mod 对自己数据的补丁效果，把**实际结果**固化为断言
- [ ] 5.4 在 `tests/unit/Modot.Tests/MetadataTests.cs` 增加拒绝用例：`Before`/`After`/`Incompatible` 存在交集、含自身 ID、`Dependencies ∩ Incompatible` 相交 —— 三种形态各断言抛 `ModLoadException`
- [ ] 5.5 若 5.3 实测出 `LoadMod` 会对自己数据**重复应用补丁**，如实断言该行为并作为独立议题记录；**不得**顺手修改 `src/Modot`（属另一变更）

## 6. 文档与接线同步

- [ ] 6.1 更新 `Modot.sln` 中 `Modot.Tests` 的项目路径为 `tests\unit\Modot.Tests\Modot.Tests.csproj`；验证 `dotnet build Modot.sln` 退出码为 0
- [ ] 6.2 更新 `.gitignore` 中的 e2e 产物路径（`tests/e2e/Mods/*/Assemblies/`、`tests/e2e/Mods/*/*.sln`、`export_presets.cfg` 例外）；验证 `git check-ignore` 双向仍正确（产物被忽略、源码与夹具不被忽略）
- [ ] 6.3 更新 `tests/README.md`：层结构（`unit/` 与 `e2e/`）、**`e2e` 是 harness 而非测试项目**、夹具分组表、新场景清单、脚本新名、以及"刻意未覆盖"清单
- [ ] 6.4 更新 `AGENTS.md`：层目录、测试命令（新脚本名与 `tests/unit/...` 路径）、以及"`e2e` 是 harness"这一点
- [ ] 6.5 逐条实跑 `tests/README.md` 与 `AGENTS.md` 里给出的命令，验证可直接复制运行（无失效路径）

## 7. 验证收口

- [ ] 7.1 `dotnet test tests/unit/Modot.Tests` 退出码为 0，且断言数大于 10（含新增的元数据拒绝用例）
- [ ] 7.2 `./tests/e2e/Invoke-E2E.ps1` 退出码为 0，所有场景 PASS（含新增的 `order-before`、`invalid-root`、`cross-patch`、`no-assemblies`、`single-load`）
- [ ] 7.3 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0，且未引入新警告
- [ ] 7.4 全仓搜索 `Scn/`、`run.ps1`、`assemble.ps1`、`tests/Modot.Tests`，验证只在历史记录或无关处出现，没有遗留的旧路径引用
- [ ] 7.5 `openspec validate --all --strict` 通过
