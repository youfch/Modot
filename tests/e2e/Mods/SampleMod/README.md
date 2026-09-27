# Sample Mod

一个**给人看**的 mod，不是给自动化套件跑的（套件用的是 `Mods/AlphaMod` 与 `fixtures/`）。

## 怎么跑

```powershell
./tests/e2e/Watch-Mod.ps1                                   # 默认就是它（带上它依赖的 alpha）
./tests/e2e/Watch-Mod.ps1 -ModDirectory tests/e2e/Mods/SampleMod
```

会开一个有窗口的 Godot（`watch` 场景，永不退出），打印加载结果，并把打包的场景挂进场景树。

## 加载后该看到什么

打印出来的 `Data` 应该长成**近似**这样（属性顺序不保证）：

```xml
<Data patched-by-sample="yes" reward="granted">
  <Items>
    <Item id="sample-sword" damage="10"/>
  </Items>
  <Config>
    <Setting key="greeting" value="hello"/>
  </Config>
  <Banner text="added by sample"/>
</Data>
```

逐条对应：

| 看到的东西 | 来自 | 说明 |
|---|---|---|
| `<Config>` 与 `<Items>` **并列** | `Data/Config.xml` + `Data/Items.xml` | 两个数据文件被**合并**进同一个文档 |
| `patched-by-sample="yes"`（在数据根上） | `Patches/BoostRoot.xml` | 无 `Targets` 的补丁作用于**数据根** |
| `sample-shield` **消失** | `Patches/DropShield.xml` | `TargetedPatch` + `NodeRemovePatch` |
| `sample-sword` 上**没有** `temp` | `Patches/StripTemp.xml` | `AttributeRemovePatch` 只删属性、不删节点 |
| `<Banner>` 出现 | `Patches/AddBanner.xml` | `NodeAddPatch` 追加到数据根 |
| `reward="granted"` | `Patches/ConditionalReward.xml` | **最值得看的一条**：它同时要求 `Config` 存在（合并成功才有）与 `alpha` 已加载（依赖满足才有）—— 一个属性同时证明了合并、依赖与条件三件事 |

## 一个容易忽略、但很能说明问题的现象

**`alpha` 的数据也会被 `sample` 的补丁改到。** `LoadMods` 把每个 mod 的补丁应用到**所有已加载的数据根**上（这正是跨 mod 补丁的实现方式），所以打印 `alpha` 的 `Data` 时你会看到：

```xml
<Data patched-by-sample="yes" reward="denied">
  <Items><Item id="alpha-sword"/></Items>
  <Boosted by="alpha"/>
  <Banner text="added by sample"/>
</Data>
```

三件事同时可读：

1. `patched-by-sample="yes"` 与 `<Banner>` 出现在 `alpha` 上 → **跨 mod 补丁**确实生效
2. **`reward` 在两个根上相反**：`sample` 自己那边是 `granted`，`alpha` 这边是 `denied` → 条件**按每个数据根各自求值**。`NodeExistsCondition` 找的是 `Config`，只有 `sample` 的文档里有它，所以那个 `And` 只在 `sample` 那边成立。**同一条补丁、在两个根上得到相反结果** —— 这是"补丁作用于所有根"最直观的证据
3. 数据里会出现源码文件中的**注释**（`<!--` 开头那段）→ `Mod.LoadData` 会保留 `Data/*.xml` 里的注释节点，不是只搬元素

## 想自己试探的话

- **去掉 `Data/Config.xml`** → `reward` 应变成 `denied`（条件之一失败）
- **去掉 `Mod.xml` 里的 `<Dependencies>`** → 仍会加载，但 `reward` 变 `denied`（`alpha` 不再保证已加载）
- **把 `Patches/DropShield.xml` 的 `Targets` 改成 `//Item[@id='sample-sword']`** → 换成剑被删掉
- **加一个 `Patches/` 下根节点是 `NodeReplacePatch` 的文件** → 会**加载失败**（`ArgumentException: The node to be inserted is from a different document context`）。这是**已知缺陷**（见 `tests/README.md` 缺陷表第 6 行），不是你的写法错了
