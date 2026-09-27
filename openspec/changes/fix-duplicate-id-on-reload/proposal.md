# 修复跨调用重复 ID 抛 ArgumentException

## Why

`reload` 场景（`fix-load-defects` 交付）实测到：同一进程内**第二次** `LoadMods` 同一个目录，抛出未处理的

```
ArgumentException: An item with the same key has already been added. Key: alpha
```

根因：`LoadModMetadata` 的重复 ID 检查只比较**本次调用传入的目录**（一个局部字典 `loadedMetadata.TryAdd`），从不查已加载的静态注册表 `ModLoader.LoadedMods`。于是重复 ID 直到 `LoadMods` 里 `loadedMods.Add(mod.Meta.Id, mod)` 才撞上，抛出字典的原始异常 —— **绕过了既有的"重复 ID"处理路径**。

既有契约是**记录并跳过**（不是抛）：`LoadModMetadata` 对调用内的重复调用 `Log.Error(new ModLoadException(dir, "Duplicate ID"))`，而 `Log.Error` 只写日志、不抛（`Log.Write(new Entry(...))`）。这正是 `duplicate` 场景期望"1 个 mod 加载成功"而不是抛异常的原因。跨调用的重复是同一件事，却走了另一条路。

## What Changes

- `LoadModMetadata` 的查重同时考虑 `ModLoader.LoadedMods` 中已注册的 ID，使**跨调用**的重复与**调用内**的重复走同一条路径（记录 `ModLoadException(..., "Duplicate ID")` 并跳过）

## Impact

- 行为变化：未处理的 `ArgumentException` → 有意的"记录 + 跳过"，与既有契约一致
- `reload` 场景里**故意固化** `ArgumentException` 的断言会被打红，需改为断言"不抛 + 加载 0 个 + 日志含 `Duplicate ID`"
- 结果层面的语义不变：已加载过的 mod 在第二次加载时同样不会被重复加载
- 无公共 API 签名变更，无 `BREAKING`（当前这条路径的现状是**未处理的异常**，任何改动都不会比它更差）