# 桌宠 Release 资源包规范（可选附件）

安装包**只带**桌宠运行时（含 Live2D 代码路径），**不带**角色模型与 Cubism Core。若要在 GitHub Release / 网盘提供可选资源包，按下列约定打 zip。

## 原则

- **不含** `Live2DCubismCore.dll`（用户自行从 Live2D 官方 SDK 取得）。
- **不含** `_official_live2d_presets.json`（已废弃，客户端不再读取）。
- **不含** 版权不明或无权再分发的角色资源（例如未授权的 Miku 测试包）。
- 解压目标：游戏 Storage 下的 `EzResources/Pets/`（与 `Default/` 同级）。

## PNG 包 zip 示例

压缩根目录直接是包文件夹（解压后出现 `EzResources/Pets/YourPack/`）：

```
YourPack/
  pet.json
  idle/
    xxx_000.png
  poke/
    …
```

或 zip 内带 `Pets/` 前缀亦可，说明里写清「解压到 `EzResources/`」还是「解压到 `EzResources/Pets/`」。

## Live2D 包 zip 示例

```
YourLive2DPack/
  pet.json                 # "renderer": "live2d"
  live2d/
    model.model3.json
    model.moc3
    textures/…
    *.motion3.json         # 可选
    *.physics3.json        # 可选
```

用户另需自行放置：

```
EzResources/Pets/_cubism/Live2DCubismCore.dll
```

## Release 说明建议文案（摘录）

1. 桌宠开关：设置 → Ez 游玩设置 → 桌宠。
2. PNG：下载附件解压到 `EzResources/Pets/`，设置里选包名。
3. Live2D：同上，并安装 Cubism Core 到 `_cubism/`；详见仓库 `docs/Pets/Live2D测试指南.md`。
4. 默认 `Default` 包无帧时仅占位，属预期。

## 命名建议

- `Pets-PNG-<PackName>-<version>.zip`
- `Pets-Live2D-<PackName>-<version>.zip`（仍不含 Core）
