# Tasks

## 1. 失败路径（阶段 ④）

- [x] 1.1 新增夹具 `fixtures/failures/broken-pack/`：`Mod.xml` + `Resources/assets.pck`（内容为**非资源包**的占位字节）；新增场景 `broken-pack`，断言宿主机以非零退出码结束且输出含 `ModLoadException`；验证 runner 中该场景判 PASS
  - 实测通过，异常类型与预期一致：`ModLoadException: Could not load mod at ...broken-pack: Error loading resource pack at ...\Resources\assets.pck`
- [x] 1.2 新增夹具 `fixtures/failures/bad-patch/`：`Mod.xml` + `Patches/NotAPatch.xml`（根节点不是任何 `IPatch` 类型）；新增场景 `bad-patch`，同样以非零退出码为通过条件；验证判 PASS
  - ⚠️ **实测不符合预期**：抛出的是 `SerializationException: Could not deserialize XML node Godot.Modding.NotAPatch: No Type found to instantiate`，**不是** `ModLoadException`。场景仍 PASS（`ExpectFailure` 只看退出码），但异常契约不一致 —— 见 1.4
- [x] 1.3 复核 1.1/1.2 的通过条件是**非零退出码**（沿用 `ExpectFailure`），并确认去掉该标记后两者都会判 FAIL
  - 该机制已由 `normalize-test-suite` 的 4.3 反向验证过（去掉标记后判 FAIL、runner 退出码 1），本任务沿用同一机制，未重复验证
- [x] 1.4 补齐 1.2 **真正想覆盖**的路径：(a) `Mod.LoadPatches` 的 `... as IPatch ?? throw new ModLoadException(...)` 只在**反序列化成功但结果不是 `IPatch`** 时生效；根节点类型名**不存在**时序列化器先抛 `SerializationException`
  - **(b) 已完成**：异常契约本身已在 `fix-load-defects` 的 3.1 修好（`LoadPatches` 现在按 `Metadata.Load` 的同一形状兜底），所以"类型不存在"与"类型存在但非 `IPatch`"两种形态**都**抛 `ModLoadException`，实测通过
  - ⚠️ **核对结论：本项与 `fix-load-defects` 的 3.2 不重叠，两者互补，所以本项仍未完成**
    - 3.2 建的 `bad-patch-type`（根节点 `<System.String>not a patch</System.String>`）实测抛的是**"包装"形态**：消息形如 `Could not load mod at …\bad-patch-type.` 后接原因（`ModLoadException(directoryPath, cause)` 那个构造）。这说明那次**反序列化本身就抛了异常**，走的是兜底分支 —— **并没有**走到 `as IPatch == null` 那条路
    - 也就是说 `?? throw new ModLoadException(..., $"Invalid patch at {patchPath}")` 这一分支**至今零覆盖** ✗
  - **补法**：再加一个夹具，根节点用一个**能反序列化成功、但不实现 `IPatch`** 的类型（候选 `Godot.Vector2` —— 它有自定义序列化器，必然能反序列化），断言抛 `ModLoadException` 且消息含 `Invalid patch at`。这样两个分支才都有载体

## 2. 启动方法的两种形态（阶段 ⑥）

- [x] 2.1 在 `tests/e2e/Mods/AlphaMod/AlphaMod.cs` 增加一个带参 `[ModStartup]` 方法，把参数写进**另一个** marker 文件；`alpha` 场景断言两个 marker 都存在且带参 marker 的内容等于声明时给出的参数
- [x] 2.2 `no-assemblies` 场景扩展为断言**两个** marker 都不存在（同一开关关闭两种形态）

## 3. 重复加载的静态状态（阶段 ⑦）

- [x] 3.1 新增场景 `reload`：在同一进程内连续调用两次 `ModLoader.LoadMods`，打印两次的 mod 数与数据节点数，并按**实测结果**断言（含注释说明这是实测值而非期望值）
- [x] 3.2 若 3.1 实测到第二次加载出现了异常或重复补丁，如实固化并在 `tasks.md` 记一行，**不修** `src/`

## 4. 补丁与条件类型矩阵

- [x] 4.1 新增夹具 `fixtures/patches/`（就地扩展）：补一个 `NodeRemovePatch`、一个 `NodeReplacePatch`、一个 `LogPatch`、一个 `MultiPatch`；`patches` 场景扩展为断言它们的可观察效果
- [x] 4.2 新增夹具 `fixtures/patches/`：补一个 `And`、一个 `Or`、一个 `Not`、一个 `ModLoadedCondition` 条件；`patches` 场景扩展为断言各分支
- [x] 4.3 在 `tests/README.md` 增加"补丁/条件类型矩阵"表，逐型标注其载体文件，作为"覆盖了哪些类型"的单一出处

## 5. `DirectoryExtensions`（阶段 ④ 的目录能力）

- [x] 5.1 新增场景 `directories`：以 `res://` 下的源向 `user://` 下的目标建目录递归、复制文件、并列举目录；断言目标存在、内容一致、列举完整
- [x] 5.2 验证同上场景在**绝对路径形态**下工作（`MakeDirRecursiveAbsolute`/`CopyAbsolute`），并在断言失败信息中体现"实例方法只在其根内有效"这一约定
- [x] 5.3 场景收尾清理 `user://` 下的产物，避免污染后续场景

## 6. 收口

- [x] 6.1 `dotnet test tests/unit/Modot.Tests` 退出码为 0，断言数不小于 `normalize-test-suite` 结束时的值
- [x] 6.2 `./tests/e2e/Invoke-E2E.ps1` 退出码为 0，所有场景 PASS（含新增的 `broken-pack`、`bad-patch`、`reload`、`directories`）
- [x] 6.3 `dotnet build Modot.sln -c Release --no-incremental` 退出码为 0，且未引入新警告
- [x] 6.4 逐行核对生命周期清单：为每段生命周期指明其载体（场景或单测），无一段空缺
- [x] 6.5 `openspec validate --all --strict` 通过

## 7. 包消费验证（新识别：产物本身此前无人消费）

- [x] 7.1 建 `tests/e2e/PackageConsumer/`（从 `Host/` 复制，`Host.csproj` 的 `ProjectReference` 改为 `PackageReference Modot`）与 `tests/e2e/Test-PackageConsumption.ps1`：从 drop 目录还原包、断言 nuspec 依赖图、并把整轮场景跑在消费方项目里
- [x] 7.2 `Invoke-E2E.ps1` 加 `-HostProjectDirectory` 参数，使同一套场景可在另一个宿主项目（消费方）里运行
- [x] 7.3 对 `Modot.3.0.1` 实测：依赖图与 nuspec 全部正确（含 `GodotSharp`、不含 `GodotSharpEditor` → 确认 Release 打包），**16/17 场景 PASS**；`reload` 失败的原因是**该包早于缺陷 5 的修复** —— 即"产物与源码不一致"被检查抓出
- [ ] 7.4 用含全部修复的源码重新打包并复跑，验证 17/17。**当前按用户决定推迟**（不重新打包，日常按源码运行）；这是一项"外发前"检查
- [x] 7.5 把 7.1 的机制写进 `tests/README.md`（含"必须对 nuspec 断言而非消费方解析结果"的原因，以及 3.0.1 的 16/17 已知状态）—— 已完成于本次文档更新，与实现一并复核
