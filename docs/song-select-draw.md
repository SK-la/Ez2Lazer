# 选歌界面 Draw 追踪

记录只解释已经能量到的幅度，以及这些幅度在本仓库里对得上的路径。桌宠和 ez 分析 wedge 不在本仓库，这里只保留它们的时间量级。功能开关是对照，不是待修清单。

游戏内帧数是 Draw 时钟。300fps 一帧是 3.33ms。从这个起点每少 20fps，大约多 0.24ms：

| 帧数 | 帧时间 | 相对 300fps |
| --- | ---: | ---: |
| 300 | 3.33ms | 0 |
| 280 | 3.57ms | +0.24ms |
| 270 | 3.70ms | +0.37ms |
| 250 | 4.00ms | +0.67ms |

液态玻璃约 −20fps，桌宠约 −30 到 −50fps，ez 分析 wedge 约 −30fps，HDR/10bit 约 −20fps。各自都是零点几毫秒，互相独立，没有一个大到能解释整帧。

## 2026-11-11 22:57（北京时间）

### 顶点合批没有成为这 20fps

2026-11-11 14:17（日志上的时间）那一帧：

- `SetMasking` 1，`DrawCalls` 24，`VerticesDraw` 3,396，`VerticesUpl` 1,940
- `Pixels` 15,390,627
- `VBOs in use` 603，`VeldridVertexBuffer` 常驻约 113MB
- `DrawNodeAppl` 19，`Invalidations` 97

这些 Draw 计数是上一帧写进全局统计的值，不是一段时间的平均。之后的实机观察是：顶点合批后 `SetMasking` 稳定在三十多次，帧数没有明显变化。

1,940 个顶点、步长 132 字节，一帧上传约 256KB。收到 64 字节后约 124KB。差 132KB。在 300fps 下是每秒几十 MB 的上传差，落不到 0.24ms。603 个 VBO 的 113MB 是常驻池，不是这一帧的提交时间。

三十次 `SetMasking` 是三十次批次切断。选歌四边形本身像素很少，切断批次省下的是 CPU 提交，不是那 15.4M 像素。所以把 `SetMasking` 从「每个面板一次」收到三十上下，帧数不动，和这次量到的幅度一致。

### 和 0.24ms 对得上的框架路径

像素数才和这个量级同阶。15.4M 像素在 1080p 大约是 7.4 次全屏填充，在 1440p 大约是 4.2 次。分辨率没记，倍数只能二选一。

液态玻璃（`LiquidGlassDrawable` + `SharedBackbufferAcrylic`）：

- 主路径是一帧抓一次后缓冲，缩到一半分辨率，再按 sigma 做横、纵两次模糊，卡片只采样自己的那一块。
- 模糊失败或当前已在帧缓冲里时，每张卡片自己抓一块再模糊两趟。−20fps 更像共享的那一次半分辨率模糊，不像每张卡片各做一套。
- 卡片边缘走圆角 SDF、折射和多次采样；内区一次采样后返回。边缘才是这条着色器贵的部分，内区已经避开了。

HDR/10bit（`VeldridDisplayPresent`）：

- 标准输出直接画到交换链。10bit 和 HDR 先把整帧画进 `R16G16B16A16Float`（每像素 8 字节），再全屏采样到交换链。
- HDR 多一次 sRGB→线性、Rec.709→BT.2020、按纸白转 PQ。
- 这是每帧多一次全屏写和全屏读。−20fps 和这条额外带宽同阶。

两条都在 Draw 时钟上，和「关掉就回到原来的帧数」一致。它们和 `SetMasking` 不是同一类成本。

### 桌宠和 wedge

本仓库没有桌宠，也没有 ez 分析 wedge。−30 到 −50fps 是 0.37–0.67ms，和玻璃、HDR 同一档：Draw 时钟上每帧都在付的账。不需要假设它们在打断批次。

### GC：框架里的 Gen3 是哪一个数

`DotNetRuntimeListener` 听的是运行时 `GCStart`（事件 1）。第二个字段加进 `Collections Gen{n}`。文档里这个字段是 Depth，也就是这次回收的代：

- 0、1、2 是小对象堆
- 运行时把大对象堆编成第 3 代，钉住对象堆编成第 4 代

大对象堆只跟第 2 代一起收。分配超过大对象堆预算时，运行时把回收代升到 2，然后发一次 Depth=2 的 `GCStart`。框架计数器因此增加 `Collections Gen2`，不会单独增加 `Collections Gen3`。`GC.CollectionCount` 也只有 0、1、2。帧时间图上的 GC 刻度用的就是这三个，看不到 `Collections Gen3`。

同一组里还有 `Size Gen0` 到 `Size Gen3`，来自 `GCHeapStats`。`Size Gen3` 是大对象堆当前字节数。gen0 或 gen1 结束时也会重写它。所以切歌之后可以出现：`Collections Gen0`、`Collections Gen1` 跳几下，`Size Gen3` 变大，`Collections Gen2` 不动。那表示有小垃圾，也有 ≥ 85,000 字节的对象进了大对象堆，第 2 代预算还没满。

下次对一下名字。若是 `Collections Gen3` 在动而 `Collections Gen2` 不动，那是 `GCStart` 的第二个字段等于 3，不是一次漏计的 gen2。若是 `Size Gen3`，就是大对象堆占用。

静止时有时 gen0/gen1 连跳、有时完全不跳：代预算按分配字节算，不按时间算。画面不动且这一帧没有分配，计数就不会动。连跳说明有组件在静止画面上仍在分配。14:17 那帧 `ArrayPool` 租借和归还都是 8,186、在途 1，不是池子漏掉没还。

gen0 暂停多半是抖动。平坦地少 20fps，更像那零点几毫秒的绘制或分配本身，而不是某一次暂停。帧时间里的 GC 段用的是 `GC.GetTotalPauseDuration`，和 `Collections Gen*` 不是同一个数。

### 8000 Hz

活动时线程上限是 8000。不限帧时 Update 和 Draw 都会往上顶，超高帧率会放大分配和 GPU 等待。稳定在 300 附近时，这一帧的预算仍是 3.33ms，功能开关的账记在这 3.33ms 里。OpenGL 且垂直同步打开时，交换缓冲里还有一次等同 `glFinish` 的等待；GPU 填像素的时间会落在这一段。

### 下次日志里有用的字段

场景、平台、CPU、GPU、分辨率、刷新率、渲染器、是否垂直同步，仍然没有。有了分辨率才能把 `Pixels` 换成全屏填充次数。

同一条全局统计里另外记下：

- `SetMasking`、`DrawCalls`、`Pixels`、`VerticesUpl`（确认仍不是主项）
- `# GC` 里 `Collections Gen*` 和 `Size Gen*` 分开抄
- 帧时间图上 Draw、SwapBuffer、GC 三段。玻璃和 HDR 若主因是填充，时间会进 SwapBuffer；静止时的 gen0 若主因是暂停，时间会进 GC

对照仍然是一次只关一个功能，从大约 300fps 的同一起点量。

## 2026-11-11 23:22 GC（静止选歌仍分配）

masking 不再作为优化项。

静止时 gen0/gen1 连跳，表示画面不动也有托管分配。`SongSelect.Update` 每帧执行：

```
mainGridContainer.ColumnDimensions = new[] { new Dimension(...), new Dimension(), new Dimension(...) };
```

`Dimension` 是 class。`GridContainer.ColumnDimensions` 用引用相等，每次新数组都会 `cellLayout.Invalidate()`，随后 `layoutCells()` 里 `getCellSizesAlongAxis` 再 `new float[]`，`distribute()` 再 `Enumerable.Range().Where().ToArray()` 和 `OrderBy`。

窗口宽高不变时列宽不变，这些对象每帧白扔。这和「静止也会连跳 gen0」对得上，和 `ArrayPool` 在途 1（不是泄漏）不矛盾。

已改成只在算出的 maxSize 变化时才赋值。其余选歌 `Update`（面板着色、carousel 深度）以 struct/相等检查为主，不是这条每帧堆分配。

下次静止选歌把 `# GC` 的 `Collections Gen0/Gen1` 和改前比。若仍连跳，再查 DEBUG 下 carousel 的 250ms 过滤字符串，以及展开的预览 overlay。

## 2026-11-12 13:06 静止选歌 GC

`SongSelect.Update` 列宽那刀之后，静止仍约 2.5s 一次 `Collections Gen1`。13:06:59 快照：

- `Collections Gen0` 400 / `Gen1` 261 / `Gen2` 7。Gen0 仍在涨，不是冻在 170。
- `Size Gen1` 583,744 是占用。终结队列 176（此前 300–400 浮动）是待终结死对象。
- `FBORedraw` 0。`UniformUpl` 5。
- Native `VeldridUniformBufferStorage1` 69,552 字节，用户见 50–70k 循环。这是字节不是个数。
- Native `VeldridVertexBuffer1` 121,257,180。`VBufOverflow` 50 是 batch 填满换下一块，不是 50 次新分配。VBO 池不作为这一刀。

共享半分辨率模糊每帧 `CreateUniformBuffer` 两次。已改成持有一块 `BlurParameters` UBO 复用，并去掉 `drawInto` 的每帧委托。

改后对照：Native `VeldridUniformBufferStorage1` 钉在 52224，静止和切歌都不动。`VeldridVertexBuffer1` 约 117MB，滚 panel 到 132MB 再回落。这一刀成立。

## 2026-11-12 切歌终结队列

切歌约 1s 后 `Finalization queue length` 可从几十冲到约 15000 再掉回。不是泄漏，也不再是 UBO。

`logs/*.performance.log` 里 `Finalizing object:` 主要是 Realm：`GenericManagedAccessor`、`ObjectHandle`、`RealmList<BeatmapInfo>` / `RealmNamedFileUsage`。连切数分钟后 gen0/1 变快、掉 20–40fps，和这批托管访问器堆到 gen2 再集中收对得上。

`ensureGlobalBeatmapValid` 每次切歌都 `QueryBeatmap(b => b.ID == id)`。这条会 `Detach` 整套（含 Files），只为判断谱面还在不在。已改成主键 `Find`，不再克隆。

切一首约 1s 后终结队列仍约 1 万，连切叠加到 6 万以上。carousel 的 set 没有 Files，`GetWorkingBeatmap` 每次都 `Find<BeatmapInfo>.Detach()`（MaxDepth 2，整套含 Files）。已改成缓存命中直接返回；未命中只把 Files/外部托管字段拷到现有 detached set。

静止时 Drawable `Total constructed` 每秒约 +30（两次、每次 +15）是另一条，不是这条 Detach。夹具 `TestSceneSongSelectDrawCost` 量了静止窗口：只有 carousel/wedge 的场景是 0/s，说明来源在 SongSelect 内容之外。

DEBUG 构建打开统计窗口（Ctrl+F2）时会每 2s 在 performance 日志写 `[drawable-construct] <类型> +N`，用来点名这批构造。

点名结果：静止增量固定为 `StatisticsItem +10 / SpriteText +20` 每 2 秒，正好是每秒 5 条新统计项 ×（1 个 StatisticsItem + 2 个 SpriteText）。来源是统计浮层自己：`TypePerformanceMonitor` 每约 1 秒 `GlobalStatistics.Clear("Update (Top CPU consumers)")` 后用带类型名的新名字重新注册 5 条，`GlobalStatisticsDisplay` 每次都得新建 `StatisticsItem`。Update/Draw 两条线程各跑一次，就是每秒 30 个 Drawable。关掉浮层即 0/s。修法是改成固定 5 个槽位、类型名与耗时放进 value（framework `dd24084bb`）。

连续快速切歌时框架报 `PanelBeatmapStandalone took 34ms to load (and blocked the update thread)`：池里没有现成面板时 `DrawablePool.Get` 会在更新线程 `LoadComponent`。过期面板还要 fade ~200ms 才归还。standalone 池从 100 加到 250。

切歌 `live update read fast` 约 +1560：`MusicController.changeBeatmap` 把整库包成 `RealmLive` 只为判断切歌动画方向。已改成直接扫 `BeatmapSetInfo`，不再包 Live。
