# 性能 / FPS

观测口径与已定案根因。判定语义不在此文件。

---

## 1. 观测

| 指标 | 含义 |
|------|------|
| FPS | 看稳定态，非峰值 |
| `Work` | 本线程本帧工作；后台 Task **不计**入 |
| `SwapBuffer` | 多呈驱动/等待，非自身绘制变重 |
| `FBORedraw` / Upl | 次数≠耗时 |

比较须同启动方式、同皮肤，只改一个变量。

---

## 2. 已定案（判定相关）

| 现象 | 修复 |
|------|------|
| 越打越卡 | `pressTimes` 有界 + MissTiming 零分配 |
| 列/LN 多越卡 | AutoMiss → Column late-deadline（非每 Drawable 每帧） |
| LN 按住掉帧（Default/Triangles） | LN-HOLD-FBO：按住跳过减法 FBO 重绘 |
| 高 KPS alloc | JudgeMode valid 结果静态表；输入队列按帧物化；取样去 LINQ |
| 选歌停 3–5s 掉帧 | BDSP 开工延迟 5s；DetachedStore 每帧 Drain≤24 |
| 传统输出播歌掉大量 FPS | 框架振幅分析限频（非 8000Hz FFT） |

黄金标准：优化不得破 `TestSceneReplaySessionParity` / CrossSource / Precedence parity。

---

## 3. 未结 backlog

| 代号 | 说明 |
|------|------|
| STATE-ONE | O2/BMS 可变状态合并（暂缓） |
| BMS-ROUTE-COL | tail BMS 路由列级化 |
| HOLD-TAIL-FAST | 列级 tail release |
| SOUND-DECOUPLE | 判定与按键预览音解耦（仅 profile 证阻塞） |
| COLUMN-EARLY-OUT | 路由成功早退（须先调派发，免伤 Recorder） |

不做：框架共享 KeyBinding 队列重建（已证伪）。

---

## 4. Bench 入口

### 密度口径（Mania 同类测试统一）

| 项 | 标定值 |
|----|--------|
| 每列 KPS | 10 / 15 / 20 |
| 画面驻留（`IScrollingInfo.TimeRange`） | 300 / 500 / 700 ms |
| 每列瞬时存活数 | 每列 KPS × 驻留秒数（10→3/5/7，15→4.5/7.5/10.5，20→6/10/14） |
| 全键盘合计 KPS | 每列 KPS × 列数（4 列 40/60/80，10 列 100/150/200，16 列 160/240/320） |

- 入口参数是 `ManiaDenseScrollWorkload.PerColumnKps`；`PeakKps` 只是 `Keys × PerColumnKps` 的派生值。
- `ManiaLaneHotPathWorkload.ConcurrentAlivePerColumn` 是「判定窗重叠区里的未判定条数」，不是滚动驻留数，因此不套 300–700 ms。
- 每列存活最多 14 个，远低于 `Column` 每列 50 的池上限；池 excess 只用于排除，不作为预期主因。

### 入口

| 入口 | 覆盖 |
|------|------|
| `BenchmarkManiaDenseScroll` | headless、固定 1/60 s 步长；列数 / 每列 KPS / 驻留 / tap-vs-hold；update 线程时间与分配 |
| `TestSceneManiaDenseScrollCost`（DEBUG） | 真实 drawable + 皮肤；每秒输出 `[DENSE-SCROLL]`，含池占用、GC、判定计数与消融开关 |
| `BenchmarkManiaLaneHotPath` / `ManiaLaneHotPathMicroBenchTest` | 判定热路径下限（每列 10 / 15 / 20 KPS，无 drawable） |
| `BenchmarkManiaReplaySession` | 4K 离线 replay；`PerColumnKps` 派生全局间隔，另保留 80 ms 低密度参考组 |
| `TestSceneManiaHoldDrawCost`（DEBUG） | LN 绘制消融；hold 数与间距按每列 KPS × 驻留派生 |
| `ManiaDenseScrollAllocTest` | CI 守卫：10 列 × 20 KPS × 700 ms 纯 tap；只断言存活数、池 excess 与每帧分配量级 |

### 标定结果（10 列，DEBUG 场景，固定 1/60 s 模拟步长）

`TestSceneManiaDenseScrollCost.TestCalibrationSweep` 每个配置取 3 次样本，更新耗时取中位数。数值是测试宿主上的 update 帧时，不是 GPU 帧时，只用于同机排序与回归对比。DEBUG 构建的 `alloc/frame` 含调试路径开销，量级比 Release 高一个数量级，只能做同构 A/B，不要和全矩阵的 `KiB/帧` 直接比。

| 配置 | 存活 | update ms | alloc/frame |
|------|------|-----------|-------------|
| 每列 10 KPS × 700 ms | 95 | 1.2 | 89 KiB |
| 每列 15 KPS × 700 ms | 138 | 1.2 | 124 KiB |
| 每列 20 KPS × 700 ms | 181 | 3.0 | 115 KiB |
| 每列 20 KPS × 300 ms | 101 | 1.1 | 102 KiB |
| 每列 20 KPS × 500 ms | 141 | 1.3 | 131 KiB |
| 每列 20 KPS × 700 ms，50% hold | 221 | 9.8（4.5 倍） | 353 KiB（2.8 倍） |
| 4 列 × 20 KPS × 700 ms | 73 | 0.5 | 44 KiB |
| 16 列 × 20 KPS × 700 ms | 253 | 1.7 | 145 KiB |

- 成本增量排序：hold/LN > 皮肤组件（Ez2 / EzPro 约为 Triangles 的 1.3–2 倍）> 列数 / 密度 > 池扩张（全部配置 excess = 0，只有预热期扩容）。
- `ColumnWatcher` 订阅与 Ez2/EzPro 每帧布局写入在 Ez2 皮肤下关闭后，update 与分配都落在相邻基线的抖动区间内，没有可归因收益；皮肤订阅框架不列为当前优先项。

### 全矩阵（Release，headless，`--job short`）

`BenchmarkManiaDenseScroll` 每次调用跑 60 帧，下表的 `ms/帧` 与 `KiB/帧` 已除以 60。tap 组与 50% hold 组各 27 格。只反映 update 线程，不含绘制；short job 的单格误差较大，读趋势不读单格。

各列 `ms/帧`（列 = 每列 KPS / 驻留 ms）：

| 列数 | hold | 10/300 | 10/500 | 10/700 | 15/300 | 15/500 | 15/700 | 20/300 | 20/500 | 20/700 |
|------|------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| 4 | 0 | 0.12 | 0.14 | 0.17 | 0.22 | 0.17 | 0.33 | 0.28 | 0.35 | 0.39 |
| 4 | 50% | 0.52 | 0.50 | 0.60 | 0.60 | 1.54 | 2.24 | 2.00 | 2.26 | 3.55 |
| 10 | 0 | 0.47 | 0.44 | 0.44 | 0.41 | 0.55 | 0.62 | 0.45 | 0.55 | 0.72 |
| 10 | 50% | 2.72 | 1.94 | 3.26 | 2.24 | 3.65 | 2.78 | 2.20 | 4.05 | 8.92 |
| 16 | 0 | 0.60 | 0.51 | 1.63 | 1.12 | 1.21 | 3.20 | 1.93 | 1.34 | 2.26 |
| 16 | 50% | 2.35 | 3.57 | 9.86 | 6.14 | 8.34 | 12.69 | 8.33 | 10.18 | 13.29 |

各列 `KiB/帧`：

| 列数 | hold | 10/300 | 10/500 | 10/700 | 15/300 | 15/500 | 15/700 | 20/300 | 20/500 | 20/700 |
|------|------|--------|--------|--------|--------|--------|--------|--------|--------|--------|
| 4 | 0 | 4.2 | 4.2 | 4.2 | 6.2 | 6.2 | 6.2 | 8.3 | 8.3 | 8.3 |
| 4 | 50% | 16.7 | 16.7 | 16.6 | 25.5 | 24.9 | 25.0 | 33.9 | 33.4 | 33.4 |
| 10 | 0 | 10.5 | 10.4 | 10.4 | 15.6 | 15.6 | 15.6 | 20.8 | 20.8 | 20.8 |
| 10 | 50% | 42.4 | 41.7 | 41.8 | 63.9 | 62.4 | 62.7 | 84.8 | 83.3 | 87.7 |
| 16 | 0 | 16.8 | 16.6 | 17.2 | 24.9 | 25.0 | 25.7 | 33.3 | 33.3 | 34.6 |
| 16 | 50% | 67.9 | 66.5 | 68.8 | 102.2 | 104.3 | 103.1 | 135.1 | 134.4 | 140.4 |

- tap 组的分配随「列数 × 每列 KPS」线性增长，与驻留无关：同一 KPS 下 300 / 500 / 700 ms 三格的 `KiB/帧` 完全相同（10 列 10 KPS 均为 623 KiB，15 KPS 均为 935，20 KPS 均为 1247）。折算下来 tap 每条 note 约 6.2 KiB，hold 每条约 25 KiB，所以表里的 `KiB/帧` 实际是「每秒生成的 note 数 × 单条生成成本」，不是稳态每帧的常驻分配。
- hold 的分配是 tap 的约 4 倍，来源是每条 LN 多出的 head / tail / body 三个嵌套 drawable、两个 proxy 与对应皮肤组件；要降分配只能降单条 LN 的生成成本，与驻留长度无关。
- hold 组在同样存活数下是 tap 的 4–12 倍 update、4 倍分配；16 列 × 20 KPS × 700 ms、50% hold 单帧 update 13.3 ms，已占 60 FPS 帧预算的 80%。这是本机数据里唯一逼近预算的组合。
- 16 列的 tap 单格在 15/700 与 20/700 偏高，与 10 列同格的线性外推不符，属 short job 抖动（该格 StdDev 达 8–21 ms），不作为结论。
- 本基准的场景里没有 `DrawableManiaRuleset`，每条存活 drawable 都在 `UpdateAfterChildren` 里按帧走 `UpdateResult(false)`；列级路由的实战路径（`ColumnRoutesInput`）只在到期时轮询。因此这里的绝对 ms 和 hold/tap 倍率偏悲观，用于同机比较与回归，不外推到实战绝对值。
- 池：每列 20 KPS × 700 ms 的 10 列池会从初始 10 扩到约 19/列后复用；`DrawableNote` 池 190 个，仍低于每列上限 50。

