# stable mania 长条（LN）判定机制

`HW50` 指 `HitWindow50`，`hitWindowEarly` 是判「是否还没到位」的提前量。

## 1. 每帧判定

```mermaid
flowchart TD
    A[该列最早的未判物件] --> B{IsHit}
    B -->|是| Z[跳过]
    B -->|否| C{该列已被占用<br/>或 now 早于 Start 减 hitWindowEarly}
    C -->|是| Z
    C -->|否| D[处理按键边沿]
    D -->|松到按| E[TimePress 写成当前时刻<br/>结算头一次]
    D -->|持续按住| F[每 100ms 涨一格连击]
    D -->|按到松| G[TimeRelease 写成当前时刻<br/>判整条]
    D -->|无边沿| H{仍在按住<br/>且 now 不早于 End 加 HW50}
    H -->|是| I[按该帧时刻判整条]
    H -->|否| Z
```

## 2. 判整条 LN

```mermaid
flowchart TD
    A[判整条] --> B{IsHit}
    B -->|是| Z[返回已有结果]
    B -->|否| C{从未按下<br/>且 now 早于 Start 加 HW50}
    C -->|是| C1[本次不判]
    C -->|否| D{now 早于 End 减 HW50}
    D -->|是| E{Pressed}
    E -->|是| E1[本次不判]
    E -->|否| E2[标记断连 HasHoldBreak<br/>记一次 ComboBreak]
    D -->|否| F{Pressed<br/>且 now 早于 End 加 HW50}
    F -->|是| F1[持过尾<br/>本次不判]
    F -->|否| G[进入终判]
    G --> H{Pressed}
    H -->|是| H1[松开基准取当前时刻]
    H -->|否| H2[松开基准取镜像后的 TimeRelease]
    H2 --> I{松开基准早于 End 减 HW50}
    I -->|是| M[Miss]
    I -->|否| H1
    H1 --> K[按下基准取镜像后的 TimePress<br/>按下误差 = 按下基准与 Start 之差<br/>合计误差 = 按下误差加松开误差<br/>按按下误差与合计误差双尺取档]
    K --> L{已断连且档位优于 200}
    L -->|是| N[封顶 Good200]
    L -->|否| O[保持原档<br/>最低 Meh50]
```

## 3. 状态迁移

```mermaid
stateDiagram-v2
    [*] --> 未接
    未接 --> 按住: 上升沿<br/>TimePress 写成当前时刻<br/>结算头
    未接 --> 已判: 从未按下且超时<br/>按下基准取 End 减 1<br/>松开基准取 0<br/>结果 Miss

    未接 --> 未接: 从未按下且帧钟跨过 Start 加 HW50<br/>HasHoldBreak 置位<br/>记一次 ComboBreak
    按住 --> 未接: 松手早于 End 减 HW50<br/>HasHoldBreak 置位<br/>记一次 ComboBreak
    未接 --> 按住: 重抓<br/>TimePress 被覆盖成重抓时刻

    按住 --> 已判: 松手落在 End 减 HW50 到 End 加 HW50 之间<br/>松开基准取 TimeRelease
    按住 --> 已判: 帧钟跨过 End 加 HW50 仍未松手<br/>松开基准取该帧时刻
    按住 --> 已判: 松手晚于 End 加 HW50 且该帧未到<br/>松开基准镜像后早于 End 减 HW50<br/>结果 Miss

    已判 --> [*]
```

## 4. 事件与变量对照

| 事件 | stable 动作 | 关键变量 |
|---|---|---|
| 松 → 按 | 结算头一次，写按下时刻 | `TimePress` 每次按下都覆盖 |
| 持续按住 | 每 100ms 涨一格连击 | 节拍锚点每 100ms 前进一次 |
| 从未按下，帧钟跨过 `Start + HW50` | 立即断连，记一次 ComboBreak；尾不结算，仍可重抓 | `HasHoldBreak` 置位 |
| 按 → 松，早于 `End − HW50` | 断连，记一次 ComboBreak，LN 仍可重抓 | `HasHoldBreak` 置位 |
| 按 → 松，落在 `End − HW50` 到 `End + HW50` 之间 | 判整条 | 松开基准取 `TimeRelease` |
| 一直按住到 `End + HW50` | 帧钟判整条；之后松手不改结果 | 松开基准取该帧时刻 |
| 从未按下 | Miss | 按下基准取 `End − 1`，松开基准取 0 |

## 5. 两个容易误读的点

**头档只算一次，但 `TimePress` 会被反复覆盖。** 按下分支的进入条件是 `!Pressed`，
`TimePress = 帧钟` 没有任何「首次按下」守卫。头的重复结算由单独的「头已命中」标志挡住，
挡不住 `TimePress` 被改写。断连重抓之后，尾判的按下基准已经是重抓时刻。

**封顶是单向下压。** 断连标志只把优于 200 的档位拉下来。
重抓让按下误差变成「重抓 − 头」，先落进 `Meh50`，此时封顶条件不成立，结果停在 50。
