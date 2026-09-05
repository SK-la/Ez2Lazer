# Live2D 桌宠使用指南

客户端已内置 Cubism 网格绘制（无 clipping mask；部分混合近似）。角色模型放在游戏数据目录的 `EzResources/Pets/`。Cubism Core 动态库按平台放在 `_cubism/<rid>/`（发行版可随包附带当前平台；公开源码仓库不收录该二进制）。

## 你需要准备

1. 从 [Cubism SDK for Native](https://www.live2d.com/download/cubism-sdk/download-native/) 下载 SDK（需同意 Live2D 协议）。
2. 从 SDK 的 **`Core/dll/...`** 取出**当前系统**的动态库（不要用 `Core/lib` 下的 `.lib` / `.a`）：
   - Windows x64：`Live2DCubismCore.dll` → `_cubism/win-x64/`
   - Linux x64：`libLive2DCubismCore.so` → `_cubism/linux-x64/`
   - macOS Apple Silicon：`libLive2DCubismCore.dylib` → `_cubism/osx-arm64/`
   - macOS Intel：`libLive2DCubismCore.dylib` → `_cubism/osx-x64/`
3. 准备一套 `.model3.json` + `.moc3` + 贴图（自制或有授权的模型）。

## 放到游戏数据目录

```
EzResources/Pets/
  _cubism/
    win-x64/Live2DCubismCore.dll      ← 按你的平台选一个子目录
    linux-x64/libLive2DCubismCore.so
    osx-arm64/libLive2DCubismCore.dylib
  MyPet/
    pet.json                          ← "renderer": "live2d"
    live2d/
      xxx.model3.json
      xxx.moc3
      ...
```

Windows 仍兼容旧路径：`_cubism/Live2DCubismCore.dll`（平铺）。

也可以直接把 SDK 的 `Core/dll` 目录内容拷进 `_cubism/`（保持 `windows/x86_64/`、`linux/x86_64/`、`macos/` 结构），客户端同样能找到。

`pet.json` 最小示例：

```json
{
  "renderer": "live2d",
  "defaultState": "idle",
  "clips": {
    "idle": { "fps": 12, "loop": true },
    "poke": { "fps": 12, "loop": false },
    "grabbed": { "fps": 12, "loop": true }
  },
  "states": {
    "idle": { "clip": "idle" },
    "poke": { "clip": "poke", "next": "idle" },
    "grabbed": { "clip": "grabbed" }
  },
  "rules": [
    { "when": "click", "goto": "poke", "interrupt": true },
    { "when": "drag", "goto": "grabbed", "interrupt": true }
  ]
}
```

可选：在 `pet.json` 里写 `"live2d": { "root": "live2d", "model": "xxx.model3.json" }` 指定入口；省略则自动取 `live2d/` 下第一个 `*.model3.json`，否则第一个 `*.moc3`。

## 游戏内验证

1. 设置 → Ez 游玩设置 → 桌宠 → 选包。
2. 包下拉下方状态行：应显示「Live2D 模型与 Cubism Core 均已就绪」。
3. **成功**：看到立绘（呼吸 / 眨眼）；点一下可切 `poke`。日志含 `Core OK` 与 `loaded texture`。
4. **缺贴图**：日志 `missing texture`；确认 model3 内路径与文件夹一致。
5. **缺 Core**：紫色占位；状态行会写出期望的 `_cubism/<rid>/...` 路径。若同包还有 PNG 帧会回退帧动画。
6. **缺模型入口**：状态行提示 live2d/ 下没有 model3/moc3；不会走 Cubism。

## 日志关键字

- `Live2D authorised; cubism=ready`
- `Ez pet Cubism: Core OK`
- `Ez pet Cubism: loaded texture`
- `Ez pet Cubism: missing texture` / `UpdateMotion failed` 等排查用

## 首次用户 vs 自制模型

| 场景 | 做什么 |
| --- | --- |
| 首次只要桌宠（PNG） | 放 PNG 包即可，不必装 Core |
| 首次 / 自制 Live2D | 放当前平台 Core + `renderer:live2d` 包 |
| 分发模型包给别人 | 见 [Release资源包规范.md](Release资源包规范.md) |

更多 PNG 规则见 [`docs/桌宠使用说明.md`](../桌宠使用说明.md)。
