# Live2D 桌宠使用指南

客户端已内置 Cubism 网格绘制（无 clipping mask；部分混合近似）。角色模型放在游戏数据目录的 `EzResources/Pets/`。Cubism Core 动态库按平台放在 `_cubism/<rid>/`（或直接放入 SDK 的 `Core/dll` 目录树）。

场景触发与 PNG **共用** `pet.json` 的 `rules` / `states` / `clips`。Live2D 额外把 clip 解成**同时叠加**的参数表情（游戏内置配方），不必为每个场景单独做 `motion3`。

**注意：** 模型子目录写在 `live2d.root`（默认 `"live2d"`），**不要**写成 `"clips": { "root": "live2d" }`——`clips` 的值必须是对象（如 `{ "loop": true }`），字符串会反序列化失败。

完整示例：[`pet.live2d.example.json`](pet.live2d.example.json)。

## 你需要准备

1. 从 [Cubism SDK for Native](https://www.live2d.com/download/cubism-sdk/download-native/) 下载 SDK（需同意 Live2D 协议）。
2. 从 SDK 的 **`Core/dll/...`** 取出当前系统动态库（不要用 `.lib` / `.a`）。也可把整个 `dll` 树拷到 `_cubism/`。
3. 一套 `.model3.json` + `.moc3` + 贴图。

## 目录

```
EzResources/Pets/
  _cubism/…                    ← Core（RID 或 SDK 布局）
  MyPet/
    pet.json                   ← "renderer": "live2d"
    live2d/
      xxx.model3.json
      xxx.moc3
      *.motion3.json           ← 可选加分
```

## 参数表情（同时叠加）

内置表情 ID（缺对应 `Param*` 则跳过该键）：

| ID | 作用（示意） |
| --- | --- |
| `smile` | 嘴型笑、眼笑、腮红等 |
| `wave` | 手臂挥动（需 `ParamArm*`） |
| `jump` | 身体微动 + 立绘弹跳 |
| `nod` / `shake` | 点头 / 摇头 |
| `lookDown` / `pout` / `kick` / `coverEyes` | 低头、噘嘴、踢、捂眼 |

结算 SS 等应写成 **同时**激活多个 ID，例如 `["smile","wave","jump"]`，不是排队三段。

`pet.json`：

```json
"live2d": {
  "clipExpressions": {
    "rankSS": ["smile", "wave", "jump"],
    "fail": ["shake"],
    "clear": ["nod"]
  },
  "lipSync": { "enabled": true, "minOpen": 0.25 }
}
```

未写 `clipExpressions` 时，客户端对 `fail`/`clear`/`rankA`… 有内置默认。可用 `live2d.expressions` **整份覆盖**某表情配方（改幅度、频率、Param ID）。

### 口型字段（`live2d.lipSync`）

| 字段 | 含义 |
| --- | --- |
| `enabled` | 是否按 BPM 四分音符开合嘴（默认 `false`） |
| `defaultOpen` | **平时**嘴张开程度 0–1（关闭联动时也生效；默认 `0.5`） |
| `minOpen` | 仅联动开启时的最低开口（拍点之间不会低于此值） |

例：平时半开、不要跟音乐：

```json
"lipSync": { "enabled": false, "defaultOpen": 0.5 }
```

例：平时略闭、跟音乐时最低 0.3：

```json
"lipSync": { "enabled": true, "defaultOpen": 0.35, "minOpen": 0.3 }
```

### 改内置动作幅度（`live2d.expressions`）

内置 ID：`smile` / `wave` / `jump` / `nod` / `shake` / `lookDown` / `pout` / `kick` / `coverEyes`。覆盖时写完整 `params`（会替换该 ID 的整份配方，不是合并单个参数）。

例：更轻更慢的摇头/点头：

```json
"expressions": {
  "shake": {
    "holdSeconds": 0,
    "params": [
      { "id": "ParamAngleZ", "value": 2.5, "oscillate": true, "frequency": 0.6 },
      { "id": "ParamBodyAngleZ", "value": 2.5, "oscillate": true, "frequency": 0.6 }
    ]
  },
  "nod": {
    "holdSeconds": 1.1,
    "params": [
      { "id": "ParamAngleY", "value": 8, "oscillate": true, "frequency": 2.0 },
      { "id": "ParamBodyAngleY", "value": 3, "oscillate": true, "frequency": 2.0 }
    ]
  }
}
```

`value`：固定偏移；`oscillate: true` 时 `value` 为振幅、`frequency` 为 Hz。缺模型里没有的 `Param*` 会自动跳过。

改「哪个场景播哪些表情」用 `clipExpressions`（上面已有）；改「表情本身长什么样」用 `expressions`。

可选 `motion3`：与表情叠层一起播；`live2d.clipMotions` 可把 clip 映射到不同 motion 文件名键。

## 规则事件（与 PNG 相同 + Live2D 常用）

| `when` | 说明 |
| --- | --- |
| `fail` | 本局失败 |
| `clear` | 本局通关 |
| `resultsRank` | 进入结算；可选 `"rank": "A"` / `S` / `SH` / `X` / `XH` / `B` / `C`… |

设置 → 桌宠 → **Live2D 音乐关联**：只控制按 BPM **晃头**（二分音符）。  
**口型联动**用 `lipSync.enabled`；**平时开口**用 `lipSync.defaultOpen`（0–1，与编辑器一致）。

## 半身模（如当前 Miku）边界

有头身/嘴型参 → 笑、点头、摇头、口型跟音乐、立绘弹跳可用。  
无手臂参 → `wave` / `coverEyes` 几乎无效，除非换带 `ParamArm*` 的模型或改配方。

## 验证

1. 设置选 Live2D 包；状态行显示 Core 就绪。
2. 点击应点头/表情；失败/通关/结算档位按 `rules` 切换。
3. 开户口型开关后，有音乐播放时嘴会动。

更多 PNG 规则见 [`docs/桌宠使用说明.md`](../桌宠使用说明.md)。
