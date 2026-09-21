# verification Specification

## Purpose
定义本仓库如何验证 Modot：哪些行为由引擎无关测试覆盖、哪些必须由引擎运行时覆盖、mod fixture 如何构造才具备真实性、失败如何被强制暴露，以及引擎位置如何注入。

## Requirements

### Requirement: Engine-free layer runs without an engine
仓库 SHALL 提供一个引擎无关的测试层，覆盖所有不依赖 Godot 运行时的行为，且 SHALL 能在未安装引擎的机器上完整执行。

#### Scenario: 无引擎机器上可跑
- **WHEN** 在一台没有安装 Godot 的机器上运行引擎无关测试层
- **THEN** 测试全部执行并给出明确的通过/失败结论，不因缺少引擎而失败、跳过或降级为警告

#### Scenario: 覆盖序列化契约
- **WHEN** 引擎无关层运行
- **THEN** 它断言 `Vector2`/`Vector3` 的序列化往返成功且分量在 float 精度内相等，并断言 mod 元数据可由 2.x 格式的 `Mod.xml` 解析出来

#### Scenario: 覆盖日志器不在加载时做 I/O
- **WHEN** 引擎无关层加载日志器类型但不写入任何条目
- **THEN** 断言不抛异常、且未创建日志文件

### Requirement: Runtime behaviour verified through headless engine runs
所有依赖 Godot 运行时的行为 SHALL 通过 headless 运行真实引擎来验证，且 SHALL NOT 仅以编译成功或代码审阅代替。

#### Scenario: 引擎运行时层运行
- **WHEN** 引擎运行时层被运行
- **THEN** 它启动真实引擎、加载 mod 目录，并对加载结果、补丁效果与启动方法执行情况作出断言

#### Scenario: 未运行时不声称已验证
- **WHEN** 某个运行时行为尚未由该层覆盖
- **THEN** 依赖它的变更不得声称该行为已验证

### Requirement: Mod fixtures are built like real mods
测试用的 mod SHALL 以与真实 mod 作者相同的方式构建 —— 独立项目、编译为程序集、引用 Modot —— 使序列化特性与启动方法的类型标识路径被真实覆盖。

#### Scenario: 特性与启动方法被真实解析
- **WHEN** 引擎运行时层加载一个由独立项目编译、引用 Modot 的 mod 程序集
- **THEN** 该程序集中用 `[Serialize]` 标注的成员被识别并反序列化、`[ModStartup]` 标注的方法被调用；若任一者因类型标识不一致而被静默忽略，测试失败

#### Scenario: mod 目录只携带 mod 自身程序集
- **WHEN** runner 组装一个带代码的 mod 目录
- **THEN** 其 `Assemblies/` 目录只包含该 mod 自己的程序集，不包含 Modot 或其依赖的程序集，从而避免同名程序集在同一加载上下文中冲突

### Requirement: Failure is loud
测试 SHALL 在任一断言失败时以非零退出码结束，并 SHALL 在输出中标明失败的断言；SHALL NOT 把失败计为跳过。

#### Scenario: 断言失败即红
- **WHEN** 任一断言失败
- **THEN** 进程以非零退出码结束，且输出中可定位到是哪一条断言失败

#### Scenario: 全部通过才为绿
- **WHEN** 所有断言通过
- **THEN** 进程以 0 退出，且输出中给出通过的断言数量

### Requirement: Engine location is injected
测试 SHALL 从环境变量取得引擎位置并提供默认值，SHALL NOT 把引擎路径硬编码进版本控制的脚本。

#### Scenario: 通过环境变量切换引擎
- **WHEN** 设置了指向引擎可执行文件的环境变量
- **THEN** runner 使用该路径，且版本控制的脚本中不存在硬编码的绝对路径

#### Scenario: 输出可被捕获
- **WHEN** runner 在 Windows 上以 headless 方式启动引擎
- **THEN** 它使用能够附着 stdout 的控制台版可执行文件，使断言可以读取引擎输出

### Requirement: Coverage boundary is documented
仓库 SHALL 记录两层测试各自覆盖什么、以及哪些行为刻意为未覆盖。

#### Scenario: 边界可查
- **WHEN** 查阅仓库内的测试说明文档
- **THEN** 能区分"由引擎无关层覆盖"、"由引擎运行时层覆盖"与"刻意为未覆盖"，且未覆盖项给出原因
