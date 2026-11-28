# 设置菜单冷打开性能追踪

验收口径是点击后等到完整设置树已加载并挂载、侧栏顺序正确；不会把只显示 General 的时间当作菜单完成。当前设置内容在 MainMenu 完成 offscreen 加载后隐藏准备，通常处于 Intro 后段；不会在游戏加载或 Intro 前段启动。

`EzStartupPreloadTiming` 现在只调度 Song Select。BDSP 完成后的 settings 延迟入口已删除；所有启动设置请求统一由 MainMenu offscreen-ready 触发，MainMenu 入场仅作兜底，`SkipSettingsPreload` 是冷开 A/B 的总开关。

## 改动前基线

2026-11-28 的旧实现测试记录：

- 首次打开至完整菜单 ready：1,245 ms。
- 托管分配：138,893 KB；GC pause：124.0 ms。
- 最大 Update 间隔：93.4 ms。
- 同一面板重复打开：约 0–0.1 ms。

## 当前路径

- 默认在 MainMenu 完成 offscreen 加载后隐藏准备设置树；`SkipSettingsPreload` 只用于冷开 A/B。关闭预加载时，首次打开才以 4 组并行执行 `LoadComponentsAsync`。
- 完成的 section 在 Update 线程按显示顺序逐个挂载；侧栏按钮也按对应 section 顺序挂载。
- Ez 游玩 section 分批创建 11 个 subsection。隐藏的搜索和侧栏在完整内容 ready 前不开放。
- 完整 ready 要求所有 section 与侧栏按钮完成加载和挂载，且 Ez 的 11 个 subsection 已挂入树中。
- Windows 音频设置初始化时原先会连续刷新两次设备列表；ASIO 配置阶段现在复用第一次枚举结果，只刷新一次。这个路径在滚动到 Audio 时执行，不计入 General 首屏耗时样本，也暂不声称它带来多少毫秒收益。
- ready 时实测树中有 12 个 section、12 个侧栏入口、48 个 subsection、246 个设置项、33 个天气控件；所有设置项均达到 `LoadState.Ready`，顺序符合侧栏。内容没有等用户滚动后才创建或挂载。

Framework 的滚动遮罩会推迟屏外 drawable 的首次 `UpdateSubTree()`。Debug 样本有 11 个设置行、约 5,100 个 section 子树 drawable 尚未达到 `IsLoaded`；Release 样本分别为 11 个与约 5,085 个。它们已在完整树中且为 `Ready`；进入视口时才执行首次 Update。`AlwaysPresent` 不会绕过滚动遮罩。这里的完整菜单 ready 指完整层级已存在、设置项已完成后台依赖加载、可供搜索和导航，不代表整棵屏外树都执行过首次 Update。这个首次 Update 的峰值仍是后续的帧时间风险；它与“滚动后才创建或挂载设置内容”不同。

## 当前测量

2026-11-28，本机 Debug NUnit 可视宿主；每次冷开前等待测试宿主空闲 5 秒：

| 样本 | 完整菜单 ready | 最大 Update 间隔 | 托管分配 | GC pause | 同面板重复打开 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 784.6 ms | 30.0 ms | 122,262.1 KB | 101.8 ms | 0.0 ms |
| 2 | 1,134.5 ms | 34.0 ms | 123,152.5 KB | 132.3 ms | 0.1 ms |
| 3 | 844.7 ms | 34.1 ms | 122,110.4 KB | 113.4 ms | 0.0 ms |
| 4 | 1,223.2 ms | 33.0 ms | 123,130.3 KB | 137.2 ms | 0.0 ms |

四次样本的冷开范围为 0.78–1.22 秒，中位数 989.6 ms，比 1,245 ms 基线快 20%。分配中位数约少 12%；GC pause 中位数与基线接近。最大 Update 间隔中位数从 93.4 ms 降至 33.5 ms，但仍超过 60 Hz 的 16.7 ms 单帧预算，不能称为无卡顿。

## 前后对照

| 指标 | 改动前完整菜单 | 当前 Debug 中位数 | 变化 |
| --- | ---: | ---: | ---: |
| 首次打开至完整菜单 ready | 1,245 ms | 989.6 ms | 快 20.5% |
| 最大 Update 间隔 | 93.4 ms | 33.5 ms | 降低 64.1% |
| 托管分配 | 138,893 KB | 122,696 KB | 降低 11.7% |
| GC pause | 124.0 ms | 122.9 ms | 基本持平 |
| 同一面板重复打开 | 0–0.1 ms | 0–0.1 ms | 持平 |

Release 的 593.6–641.9 ms 冷开样本单列记录；它与改动前 Debug 基线配置不同，不在本表中计算收益。

### 隐藏预加载交互对照（Intro 时机模拟）

两个新的 Release 可视测试在独立进程中分别运行，完整菜单计数和 ready 判据相同（12 sections、46 subsections、244 rows、33 weather controls）。冷开测量从 `Show()` 到完整树 ready；预加载测量从隐藏的 `BeginLoadingSections()` 到完整树 ready，再单独测首次 `Show()`。

| 阶段 / 指标 | 冷开：首次点击开始加载 | Intro 式隐藏预加载 | 首次点击变化 |
| --- | ---: | ---: | ---: |
| 完整菜单 ready | 787.2 ms | 预加载已花 757.6 ms；Show 后 2.1 ms | 点击后 ready 快 99.7% |
| `Show()` 同步调用 | 2.29 ms | 2.11 ms | 基本相同 |
| 阶段内最大 Update 间隔 | 85.7 ms | 90.8 ms（预加载阶段） | 卡顿转移到点击前，峰值略高 |
| 托管分配 | 64,418.9 KB | 63,636.1 KB 预加载；24.0 KB 首次 Show | 同等总准备成本移出点击路径 |
| GC pause | 47.7 ms | 42.9 ms 预加载；0 ms 首次 Show | 随准备工作移出点击路径 |
| 第二次/后续打开 ready | 0.0–0.1 ms | 0.0–0.1 ms | 保持秒开 |

这表明隐藏预加载显著降低设置点击后的树就绪延迟，但**没有减少冷启动总工作量**。隐藏预加载最大 Update 约 91 ms，可能影响动画帧；后续启动优化需要降低这个峰值。`Show()` 到 ready 的约 2 ms 是更新线程状态/完整树 ready，不代表已经 GPU present；现有 600 ms 面板过渡仍保留。

真实 `Intro → MainMenu` 集成测试当前无法给出有效帧数据：`OsuGameTestScene` 在预加载前创建 `BackgroundScreenDefault` 时，默认 `WeatherOvercastRainWind=4.5` 超出 `WeatherEffectContainer` 的 `[-2, 2]` 范围并抛异常。没有改天气生产代码来绕过此问题，也没有对用户数据目录启动游戏。生产启动追踪现会记录 settings preload requested / scheduled / hidden begin / ready，可用 `EZ2LAZER_STARTUP_TRACE=1` 在独立运行中验证它是否赶在 MainMenu 入场前完成。

同日另用 Release 配置复测两次，完整菜单 ready 为 641.9 ms 与 593.6 ms，托管分配约 49.3 MB，GC pause 为 22.6 ms 与 31.8 ms；同面板重复打开仍为 0.0–0.1 ms。Release 峰值 Update 仍为 27.6–30.6 ms，其中一次最大 GC pause 为 24.2 ms。Release 样本与旧 Debug 基线配置不同，不直接计算跨配置百分比。

`TestFreshSettingsPanelAfterActivatorCacheWarmup` 的同宿主对照中，首个完整面板 ready 为 1,335.1 ms、分配 124,191.8 KB、GC pause 150.4 ms，反射激活器增量 503；随后新建面板为 159.0 ms、分配 31,454.6 KB、GC pause 35.5 ms，反射激活器增量 0。新面板样本同时享受 JIT、资源和其他进程级缓存升温，不能把全部差值归因于反射；它仍表明首次依赖激活器构造是主要冷路径，且同一面板复开 0.1 ms 与“新建面板 159 ms”是两个不同场景。

尝试过把 12 个 section 各自作为独立 `LoadComponentsAsync` 任务提交，以便 worker 动态平衡；单次 Debug 样本恶化到 1,325.8 ms / 53.6 ms 峰值 Update，因此已撤回，保留 4 组方案。该实验只有一个样本，不外推为普遍结论。

首帧追踪定位到可见 General 树的首次 Update：Language subsection 17.4 ms、整个 General section 21.6 ms；另一轮 General 达到 67.2 ms。section 的依赖加载在后台并行完成，因此这些峰值来自 Update 线程首次 Update / LoadComplete / 布局与分配压力，而不是剩余 section 等滚动后才创建。超预算的 Update 间隔仍需继续拆分和验证。

## 验证

- `osu.Game.Tests.Visual.Settings.TestSceneSettingsPanel`：15/15 通过。
- 冷开性能测试在完整菜单 ready 时停止计时，并检查完整 section、侧栏、subsection、设置项、天气控件数量及顺序；不再用搜索强制加载其余树。
- 另加 Audio 侧栏导航场景，验证设备设置行滚入视口后完成首次加载。
- 另加整菜单侧栏导航场景，逐个进入 12 个 section，确认首屏可见设置项完成 Update，且完整菜单仍然挂载。
- 同面板两次重复打开仍为 0.0–0.1 ms。
- Rider MCP 对设置面板和测试文件执行 Roslyn 检查；本次修订引入的问题在复查后清零。

测量来自 Debug 可视测试宿主，不等同 Release 玩家机器。当前数据证明完整菜单冷开快于记录基线，重复打开速度未退化；它还没有证明所有屏外 drawable 首次 Update 都无卡顿。
