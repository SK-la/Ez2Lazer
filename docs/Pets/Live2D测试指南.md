# Live2D 桌宠本地测试指南（ez-pet-live2d-builtin）

当前分支可验证：**官方白名单 + Cubism Core + 贴图网格绘制（无 clipping mask）**。遮罩/完整混合色尚未做齐，部分部件可能缺遮罩效果，但应能看到立绘。

## 你需要准备

1. 从 [Cubism SDK for Native](https://www.live2d.com/download/cubism-sdk/download-native/) 下载 SDK（需同意 Live2D 协议）。
2. 取出 Windows x64 的 `Live2DCubismCore.dll`。
3. 准备一套 `.model3.json` + `.moc3` + 贴图（官方 Sample 的 Haru / Hiyori 即可）。

## 放到游戏数据目录

游戏 Storage 下（与 PNG 包同一层）：

```
EzResources/Pets/
  _cubism/
    Live2DCubismCore.dll          ← 不要提交到公开 git
  _official_live2d_presets.json   ← 白名单（脚本生成）
  MyOfficialPet/
    pet.json                      ← "renderer": "live2d"
    live2d/
      xxx.model3.json
      xxx.moc3
      ...
```

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

## 登记白名单（必做）

在仓库根目录执行（PowerShell）：

```powershell
pwsh -File docs/Pets/Register-Live2DPreset.ps1 `
  -PetsRoot "D:\path\to\your\game\EzResources\Pets" `
  -PackName "MyOfficialPet"
```

脚本会：

1. 找到 `live2d/*.model3.json`（或 `.moc3`）
2. 计算 SHA-256
3. 写入 / 更新 `_official_live2d_presets.json`

## 游戏内验证

1. 编译并启动（当前分支 `ez-pet-live2d-builtin`）。
2. 设置 → 桌宠 → 选 `MyOfficialPet`。
3. **成功**：看到 Miku 立绘（呼吸微动）；点一下状态切到 `poke`。日志含 `Core OK` 与 `loaded texture`。
4. **缺贴图**：日志 `missing texture`，立绘发白/破图；确认 `model3.json` 里路径与文件夹一致（如 `miku-edit.512/`）。
5. **缺 Core**：紫色占位 + 缺 DLL 提示；若同包还有 PNG 帧会回退帧动画。
6. **未登记哈希**：当作普通包，不会走 Live2D。

## 日志关键字

- `loaded N Live2D preset hash(es)`
- `Live2D authorised; cubism=ready`
- `Ez pet Cubism: Core OK`

## 下一步（尚未做）

Clipping mask、multiply 混合色、motion3 动作切换。当前 mesh 为 MVP。
