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
  "lipSync": { "minOpen": 0.15 }
}
```

未写 `clipExpressions` 时，客户端对 `fail`/`clear`/`rankA`… 有内置默认。可用 `live2d.expressions` 覆盖某表情的参数配方（自定义 Param ID 时用）。

可选 `motion3`：与表情叠层一起播；`live2d.clipMotions` 可把 clip 映射到不同 motion 文件名键。

## 规则事件（与 PNG 相同 + Live2D 常用）

| `when` | 说明 |
| --- | --- |
| `fail` | 本局失败 |
| `clear` | 本局通关 |
| `resultsRank` | 进入结算；可选 `"rank": "A"` / `S` / `SH` / `X` / `XH` / `B` / `C`… |

设置 → 桌宠 → **Live2D 口型关联音乐**：按曲目振幅开合 `ParamMouthOpenY`（最低 `minOpen`，变化幅度约为满量程一半；未跟随时默认约 `0.5`）。

## 半身模（如当前 Miku）边界

有头身/嘴型参 → 笑、点头、摇头、口型跟音乐、立绘弹跳可用。  
无手臂参 → `wave` / `coverEyes` 几乎无效，除非换带 `ParamArm*` 的模型或改配方。

## 验证

1. 设置选 Live2D 包；状态行显示 Core 就绪。
2. 点击应点头/表情；失败/通关/结算档位按 `rules` 切换。
3. 开户口型开关后，有音乐播放时嘴会动。

更多 PNG 规则见 [`docs/桌宠使用说明.md`](../桌宠使用说明.md)。
