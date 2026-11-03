# 桌宠 Release 资源包规范（可选附件）

桌宠运行时随客户端发布。角色模型用可选 zip；Cubism Core 可由**游戏发行版**按平台附带，或由用户从 SDK 自行放入 `_cubism/<rid>/`。

## 原则

- 模型 zip **不含**版权不明或无权再分发的角色资源。
- 模型 zip **一般不含** Cubism Core（避免每个模型包重复附带专有二进制）；Core 随游戏发行或用户自备。
- 解压目标：游戏 Storage 下的 `EzResources/Pets/`（与 `Default/` 同级）。
- 使用 SDK 的 **`Core/dll`** 动态库（`.dll` / `.so` / `.dylib`），不要用 `Core/lib` 静态库。

## PNG 包 zip 示例

```
YourPack/
  pet.json
  idle/
    xxx_000.png
  poke/
    …
```

## Live2D 模型 zip 示例

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

用户或游戏发行版另需提供（按平台二选一即可）：

```
EzResources/Pets/_cubism/win-x64/Live2DCubismCore.dll
EzResources/Pets/_cubism/linux-x64/libLive2DCubismCore.so
EzResources/Pets/_cubism/osx-arm64/libLive2DCubismCore.dylib
EzResources/Pets/_cubism/osx-x64/libLive2DCubismCore.dylib
```

## Release 说明建议文案（摘录）

1. 桌宠开关：设置 → Ez 游玩设置 → 桌宠。
2. PNG：下载附件解压到 `EzResources/Pets/`，设置里选包名。
3. Live2D：解压模型包；确认 `_cubism/<平台>/` 下已有 Core（见 `docs/Pets/Live2D测试指南.md`）。
4. 默认 `Default` 包无帧时仅占位，属预期。

## 命名建议

- `Pets-PNG-<PackName>-<version>.zip`
- `Pets-Live2D-<PackName>-<version>.zip`
