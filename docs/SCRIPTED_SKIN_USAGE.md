# 脚本皮肤使用文档

本页是当前版本的实际使用流程。

## 1. 快速开始

### 第一步：创建目录

```text
EzResources/
└── ScriptedSkin/
    └── TestScriptedSkin/
```

### 第二步：放入脚本文件

复制以下示例（含 `skin.ini`，可选 `MainHUDComponents.json` 等图层配置）：

- `docs/TestScriptedSkin/TestScriptedSkin.csx`
- `docs/TestScriptedSkin/ManiaTestScriptedSkinTransformer.csx`
- `docs/TestScriptedSkin/skin.ini`

仓库内也可直接参考：`EzResources/ScriptedSkin/TestScriptedSkin/`（需同步到游戏实际使用的 `EzResources` 目录）。

到：

```text
EzResources/ScriptedSkin/TestScriptedSkin/
        ├── TestScriptedSkin.csx
        ├── ManiaTestScriptedSkinTransformer.csx
        ├── skin.ini
        └── MainHUDComponents.json   # 可选
```

### 第三步：启动并选择皮肤

1. 启动游戏
2. 打开皮肤选择
3. 选择 `[Script] TestScriptedSkin`

## 2. 资源文件（skin.ini / json / 贴图）

脚本目录即皮肤根目录，可与普通皮肤一样放置：

- `skin.ini`：由 `Skin` 基类自动解析为 `Configuration`
- `{GlobalSkinnableContainers}.json`：例如 `MainHUDComponents.json`
- 贴图、音效等素材文件

主脚本构造函数需把目录资源传给基类：

```csharp
public MySkin(SkinInfo skin, IStorageResourceProvider resources, IResourceStore<byte[]>? fallbackStore = null)
    : base(skin, resources, fallbackStore)
{
}
```

加载器会在实例化时自动注入脚本目录的 fallback store。

## 3. 文件命名建议

推荐：

- 主脚本：`<SkinName>Skin.csx`
- Mania 扩展：`Mania<SkinName>SkinTransformer.csx`

兼容：

- 主脚本：`Skin.csx`
- Mania 扩展：`ManiaTransformer.csx`

## 4. 如何把现有 `Ez2` 方案改成脚本

目标：

- `Ez2Skin.cs` -> `TestScriptedSkin.csx`
- `ManiaEz2SkinTransformer.cs` -> `ManiaTestScriptedSkinTransformer.csx`

操作建议：

1. 先复制类代码到对应 `.csx`
2. 删除 `namespace`，脚本应使用全局命名空间
3. 用 `CreateInfo()` 设置你想要的 `Name`、`Creator`、`Protected`
4. 启动游戏验证显示与行为

## 5. 显示名与作者

脚本皮肤显示模板固定：

```text
[Script] {Name} {Creator}
```

说明：

- `Name`、`Creator` 来自脚本元数据（如 `CreateInfo()`）
- `Creator` 为空时不显示尾部空格

## 6. 保护模式（Protected）

若脚本元数据里 `Protected = true`：

- 编辑时会先创建副本
- 不会直接修改原脚本皮肤

这与内置受保护皮肤的编辑策略一致。

## 7. 热重载

- 修改 `.csx` 保存后会自动重载
- 监听目录是 `EzResources/ScriptedSkin/`
- 也可以走手动重载按钮（调用 `TriggerScriptReload`）

## 8. 常见问题

### 看不到皮肤

检查：

1. 路径是否在 `EzResources/ScriptedSkin/<目录>/`
2. 目录内是否有可识别主脚本（参考命名建议）
3. 脚本是否可编译（查看 runtime log）

### Mania 扩展不生效

检查文件名：

1. `Mania{MainScriptStem}Transformer.csx`
2. 或 `ManiaTransformer.csx`

### 选择后回退默认皮肤

通常是脚本加载异常：

- 语法错误
- 构造签名不匹配
- 引用受沙箱限制的 API

## 9. 推荐实践

- 主脚本里尽量提供 `CreateInfo()`
- `Name` 使用稳定值，避免频繁改名影响识别
- `Creator` 填真实作者标记，方便列表辨识
- 先跑主脚本，再加 Mania transformer

## 10. 参考

- 总入口：`docs/SCRIPTED_SKIN_README.md`
- 说明文档：`docs/SCRIPTED_SKIN_OVERVIEW.md`
- API 文档：`docs/SCRIPTED_SKIN_API.md`
- 示例：`docs/TestScriptedSkin/`
