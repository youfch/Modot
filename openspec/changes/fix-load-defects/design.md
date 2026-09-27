# Design

## D1 缺陷 1 修在钩子查找，不改 `IsValid` 的可见性

把 `Metadata.IsValid` 改成 `public` 能让它被发现，但那是**碰巧生效**：`[AfterDeserialization]` 这个特性本身没有任何"只能标注 public 方法"的约束，真正的缺陷在查找侧。修查找侧，则**所有**类型上的私有钩子方法都恢复工作；修 `IsValid` 的可见性，则只是让这一个类绕开缺陷，下一个人写私有钩子仍会静默失效。

同一文件另一处 `GetAllMembers<MethodInfo>()`（序列化侧钩子）属同一缺陷类别，需一并复核后同样处理 —— 两处不一致会让"钩子有时生效有时不生效"更难诊断。

## D2 缺陷 2 删 `.Append`，而不是挪 `loadedMods.Add` 的位置

看起来两种改法都能消除重复：删掉多余的 `.Append`，或把 `Add` 挪到汇总之后。**必须选前者**，因为 `ModLoader.LoadedMods` 在补丁应用期间就要包含**正在加载的这个 mod** —— `ModLoadedCondition` 这类条件按 `LoadedMods` 判断"某 mod 是否已加载"，若 `Add` 挪到补丁应用之后，mod 会在应用自己的补丁时看不到自己，语义反而更错。

所以正确读法是：`Add` 的位置是对的，`Append` 是重复的。

## D3 缺陷 3 的包法与 `Metadata.Load` 逐字对齐

`Metadata.Load` 已经是这个形状：

```csharp
catch (Exception exception) when (exception is not ModLoadException)
{
    throw new ModLoadException(directoryPath, exception);
}
```

`LoadPatches` 照抄同一形状。**同一个契约在两处应当只有一种写法** —— 这也是缺陷 3 的成因（当初只有一处实现了兜底）。注意 `yield return` 不能出现在带 `catch` 的 `try` 块内，所以补丁先赋给局部变量、在 `try/catch` 之后 `yield`。

## D4 缺陷 4 是 BREAKING，测试同步改断言

用户已决定"改实现符合文档"。交换 `SortModMetadata` 的两条图边即可：

- `After`：文档说"应在此 mod **之后**加载的 ID"，所以边应是 `after -> this`
- `Before`：文档说"应在此 mod **之前**加载的 ID"，所以边应是 `this -> before`

（现有实现正好相反。）

**影响面必须写清**：依赖旧反向行为的 mod，加载顺序会翻转。因此提案标 BREAKING。同时 `order`、`order-before` 两个场景**故意**断言的是旧行为，修复后必须同步改为新语义 —— 它们当初就是为这一刻准备的。

## D5 缺陷 5 先实测再决定

"同进程二次加载会把第一次的数据再打一遍补丁"目前只是**从代码读出的怀疑**。在未证实的假设上改 `src/` 是错的 —— 而且"二次加载的正确语义"本身是产品决策（是幂等？是叠加？是拒绝？）。所以：先用 `reload` 场景实测，再决定，**不在本变更内处理**。

## D6 每修一条都跑 e2e 全套件

这是"最终实测"的要求：修完不只跑单测，而是跑 `Invoke-E2E.ps1` 的**全部场景**。理由很直接 —— 缺陷 1 的修复动的是 `GDSerializer`，它会波及其它所有反序列化路径；缺陷 4 的修复动的是排序，它会波及所有依赖加载顺序的场景。单测覆盖不到这些交叉影响。

顺序：每修一条 → 跑全套件 → 绿了再修下一条。这样出红时责任清晰。