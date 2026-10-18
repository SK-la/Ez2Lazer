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

