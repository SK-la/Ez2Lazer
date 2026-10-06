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
| 高 KPS alloc | HitMode valid 结果静态表；输入队列按帧物化；取样去 LINQ |
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

`BenchmarkManiaLaneHotPath` / `ManiaLaneHotPathMicroBenchTest`；`BenchmarkManiaReplaySession`；Hold 消融仅 Debug。
