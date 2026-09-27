# 覆盖加载生命周期

## Why

`verification` 能力已有两层测试套件（`normalize-test-suite` 完成其目录、命名与夹具重组），但覆盖面集中在加载生命周期的**成功路径**。最近一轮扩覆盖（跨 mod 补丁、`executeAssemblies: false`、`LoadMod` 单加载）当场抓到两个真实缺陷 —— 说明"继续扩覆盖"是本项目当前最有效的找错手段，而不是把精力放在重构测试代码结构上。

项目已确证的加载生命周期有 7 段：

| # | 阶段 | 职责 |
|---|---|---|
| ① | `LoadModMetadata` | 读 `Mod.xml`、反序列化、`IsValid` 校验 |
| ② | `FilterModMetadata` | 递归剔除依赖未满足者 |
| ③ | `SortModMetadata` | 依赖图拓扑排序、环剔除 |
| ④ | `Mod` 构造 | `LoadResources`(`.pck`) → `LoadData` → `LoadPatches`（惰性）→ `LoadAssemblies`（即时 + 脚本表注册） |
| ⑤ | 补丁应用 | 对**所有已加载** mod 的根节点逐一应用 |
| ⑥ | `StartupMod` | 反射调用 `[ModStartup]` 方法 |
| ⑦ | `LoadMod` | 单加载旁路（不查依赖、不排序） |

其中**失败路径**（④ 的坏资源包与非法补丁）、**带参 `[ModStartup]`**、**重复加载的静态状态**、以及若干补丁/条件类型完全没有覆盖。

## What Changes

- 为 ④ 的两条失败路径补**阴性断言**：损坏的 `.pck`、非法补丁 XML，均须抛 `ModLoadException`
- 覆盖 `[ModStartup]` 的**带参**形态（`ModStartupAttribute(params object[])` 目前零覆盖）
- 覆盖**重复加载**的静态状态语义（`LoadedMods` 跨调用保留）
- 补齐剩余补丁与条件类型：`NodeRemovePatch`、`NodeReplacePatch`、`LogPatch`、`MultiPatch`、`And`/`Or`/`Not`/`ModLoadedCondition`
- 覆盖 `DirectoryExtensions` 的遍历、递归与跨根复制（`res://` → `user://`）

**不改变任何产品行为**：本变更只增加测试。扩覆盖过程中实测到的产品缺陷按"照实固化 + 独立立案"处理，**不在本变更修复**。

## Impact

- 新增夹具、场景与单测，`tests/e2e` 与 `tests/unit` 规模增长
- 若实测到新的产品缺陷，会以断言固化其现状，并另立变更修复
- 无公共 API 变更，无 `BREAKING`
