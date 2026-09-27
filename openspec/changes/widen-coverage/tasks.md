# Tasks

## 1. 已完成（补记入流程）

- [x] 1.1 `Dependencies` 满足态：新增夹具 `load-order/dep-base` + `load-order/dep-needs-base` 与场景 `dependency`；输入**故意反向**，断言**顺序无关**。实测 `DEPENDENCY:[dep-needs-base,dep-base]`（等于传入顺序）→ 证实依赖是过滤器而非排序规则，两者都被加载
- [x] 1.2 钉住 `NodeReplacePatch`：新增夹具 `failures/replace-patch` 与预期失败场景 `replace-patch`（`ExpectFailure` + `ExpectOutput = 'different document context'`）。实测抛出并匹配，缺陷从此不只靠文档
- [x] 1.3 `Mod.xml` 边界形态：单测新增三条 —— 缺必填成员**被拒绝**、未知元素**被拒绝**、语法错误抛 `ModLoadException`。前两条**推翻了初版预测**（库比预期严格），断言与测试名均已按实测改正
- [x] 1.4 多文件 `Data/` 合并：`patches` 夹具新增第二个数据文件并断言两个根都被并入、内容完整。只断言存在性；第二文件刻意不含 `Item`（合并顺序不承诺，否则会让现有断言偶发失败）

## 2. GDLogger

- [x] 2.1 **先修前提**：`own-dependency-tree` 任务 7.4 提到的 `EntryWritten` 在 vendored GDLogger 里**不存在**；按真实公开面（`Write`、`Error(string)`、`Error(Exception)`、`Entry`、`MessageSeverity`）重述该任务
- [x] 2.2 判定 `Log.Write` / `Log.Error` 在**无 Godot 运行时**下可否调用（文件是惰性打开的，但 `Write` 可能触及 `GD.Print`）；据此决定放单测层还是 e2e 层，并把判定依据写进注释
- [x] 2.3 按其分层补测试：至少覆盖「写入一条后可观察到该条目」与「文件未打开时不产生文件 I/O」两点。若须在引擎下才可调用，则走 e2e；若引擎无关，则放单测并在 `tests/README.md` 的层覆盖说明里补一行
- [ ] 2.4 收掉 `own-dependency-tree` 的 7.4（前提修正后）

## 3. Node 序列化

- [ ] 3.1 设计载体：需要在引擎里构造并往返的节点树（现有夹具无法复用），先确认哪些节点形态可序列化
- [ ] 3.2 新增 e2e 场景 `node-serialization`：构造节点树 → 序列化 → 反序列化 → 断言结构等价
- [ ] 3.3 把**不支持**的形态（例如引擎类型成员）写成边界断言，让"不支持"与"坏了"区分得开

## 4. 收口

- [x] 4.1 `dotnet test tests/unit/Modot.Tests` 退出码为 0（当前 16 条）
- [x] 4.2 `./tests/e2e/Invoke-E2E.ps1` 退出码为 0，全部场景 PASS（当前 21 个）
- [x] 4.3 `openspec validate --all --strict` 通过
- [x] 4.4 更新 `tests/README.md`：场景表补 `dependency` / `replace-patch`、夹具分组补 `load-order/dep-*` 与 `failures/replace-patch`、缺陷表第 6 行标注"已被 `replace-patch` 场景钉住"、层覆盖说明补 `Mod.xml` 边界与多文件合并