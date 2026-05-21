# 脚本皮肤文档入口

`docs` 目录下脚本皮肤相关文档统一在这里索引。

## 文档清单

- `docs/SCRIPTED_SKIN_OVERVIEW.md`：系统说明与命名/加载规则
- `docs/SCRIPTED_SKIN_API.md`：接口与关键类 API
- `docs/SCRIPTED_SKIN_USAGE.md`：从零开始的使用步骤

## 示例文件

- `docs/TestScriptedSkin/TestScriptedSkin.csx`
- `docs/TestScriptedSkin/ManiaTestScriptedSkinTransformer.csx`

## 快速规则

- 根目录：`EzResources/ScriptedSkin/<FolderName>/`
- 主脚本推荐：`<FolderName>Skin.csx`
- Mania 脚本推荐：`Mania<FolderName>SkinTransformer.csx`
- 兼容旧命名：`Skin.csx`、`ManiaTransformer.csx`
- `.csx` 使用全局命名空间（不要写 `namespace`）
- 游戏显示名模板：`[Script] {Name} {Creator}`（`Creator` 为空时自动省略尾部空格）
