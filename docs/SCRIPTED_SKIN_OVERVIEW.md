# 脚本皮肤说明文档

本文描述当前分支里脚本皮肤功能的实际行为（以代码为准）。

## 1. 目标

脚本皮肤用于把 `.csx` 放在 `EzResources/ScriptedSkin/` 下，游戏内自动发现并加载。同一目录可同时放置与传统皮肤相同的 `skin.ini`、图层 `*.json` 与素材文件。

## 2. 目录与命名

推荐结构：

```text
EzResources/
└── ScriptedSkin/
    └── TestScriptedSkin/
        ├── TestScriptedSkin.csx
        ├── ManiaTestScriptedSkinTransformer.csx
        ├── skin.ini
        └── MainHUDComponents.json   # 可选，与传统皮肤一致
```

命名规则：

- **主脚本（推荐）**：`<SkinName>Skin.csx`
- **Mania 扩展（推荐）**：`Mania<SkinName>SkinTransformer.csx`
- **兼容旧命名**：`Skin.csx`、`ManiaTransformer.csx`

脚本约定：

- `.csx` 请使用全局命名空间（不要写 `namespace`）

## 3. 发现与加载流程

对应实现：`osu.Game/Skinning/SkinManager.cs`

1. 扫描 `EzResources/ScriptedSkin/*` 子目录。
2. 每个子目录通过以下优先级寻找主脚本：
   - `*Skin.csx`（按文件名排序取第一个）
   - `Skin.csx`
   - 其他 `.csx`（排除 `*Transformer.csx`）
3. 读取脚本元数据（优先 `CreateInfo()`，其次类型反射推断）。
4. 生成虚拟 `SkinInfo`：
   - `ID`：按目录名稳定生成
   - `Hash`：保存目录名用于路径反查
   - `InstantiationInfo`：`ScriptedSkinWrapper`
5. 在皮肤列表显示；选择时加载脚本并包装为 `ScriptedSkinWrapper`。
6. 加载时自动把脚本目录挂载为 `Skin` 的 fallback 资源存储，从而读取同目录的 `skin.ini` 与 `GlobalSkinnableContainers` 对应的 `*.json`。

## 4. 游戏内显示名规则

对应实现：`osu.Game/Skinning/SkinInfo.cs`

脚本皮肤显示名固定模板：

```text
[Script] {Name} {Creator}
```

其中：

- 仅脚本皮肤使用该模板
- `Creator` 为空时自动 `TrimEnd()`，不会残留尾部空格

## 5. Protected 行为

脚本皮肤可设置 `Protected`，行为与内置受保护皮肤一致。

来源：

- 主脚本 `CreateInfo()` 返回 `SkinInfo` 时的 `Protected`
- 或脚本公开 `bool Protected` 属性（反射读取）

效果：

- `Protected = true` 时，编辑器触发修改会先走 `EnsureMutableSkin()` 生成副本
- 不会直接改原皮肤

## 6. Mania Transformer 行为

对应实现：`osu.Game.Rulesets.Mania/Skinning/Scripted/ManiaScriptedSkinTransformer.cs`

进入 Mania 后，会在主脚本同目录按以下顺序找 Mania 脚本：

1. `Mania{MainScriptStem}Transformer.csx`
2. `ManiaTransformer.csx`

如果找到则先委托给该 transformer；未提供时回退到脚本默认行为和 Mania 默认配置。

## 7. 热重载

对应实现：

- `osu.Game/EzOsuGame/ScriptedSkin/HotReloadManager.cs`
- `osu.Game/EzOsuGame/ScriptedSkin/ScriptFileWatcher.cs`
- `osu.Game/Skinning/SkinManager.cs`

行为：

- 监控 `EzResources/ScriptedSkin/` 下 `.csx` 变化
- 自动防抖重载（500ms）
- 可手动触发 `SkinManager.TriggerScriptReload(scriptPath)`

## 8. 当前示例

- `docs/TestScriptedSkin/TestScriptedSkin.csx`
- `docs/TestScriptedSkin/ManiaTestScriptedSkinTransformer.csx`
