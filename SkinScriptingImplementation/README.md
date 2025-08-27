# 皮肤脚本系统 (Skin Scripting System)

这个实现添加了对外部Lua脚本的支持，允许皮肤制作者通过脚本定制皮肤的行为和外观。

## 实现概述

这个系统使用MoonSharp作为Lua脚本引擎，并通过以下关键组件实现：

1. **脚本接口** - 为皮肤脚本提供与游戏交互的API
2. **脚本管理器** - 负责加载、执行和管理皮肤脚本
3. **对现有代码的修改** - 在关键点调用脚本回调函数

## 安装和使用

### 安装

1. 安装MoonSharp NuGet包:
   ```
   dotnet add package MoonSharp.Interpreter --version 2.0.0
   ```

2. 将`SkinScriptingImplementation`文件夹中的所有文件复制到对应的项目文件夹中，保持相同的目录结构。

### 创建皮肤脚本

1. 创建一个`.lua`扩展名的文件
2. 将该文件放入你的皮肤文件夹中
3. 当皮肤加载时，脚本会自动被加载和执行

### 管理脚本

皮肤脚本系统提供了用户界面来管理皮肤脚本：

1. 转到`设置 -> 皮肤 -> 皮肤脚本`部分
2. 使用`启用皮肤脚本`选项来全局启用或禁用脚本功能
3. 使用`从文件导入脚本`按钮将新脚本添加到当前皮肤
4. 在`可用脚本`列表中，可以单独启用或禁用每个脚本

### 脚本元数据

脚本可以包含以下元数据变量：

```lua
-- 脚本描述信息，将显示在设置中
SCRIPT_DESCRIPTION = "这个脚本的功能描述"
SCRIPT_VERSION = "1.0" 
SCRIPT_AUTHOR = "作者名称"
```

## Lua脚本API

脚本可以实现以下回调函数：

```lua
-- 脚本加载时调用
function onLoad()
    -- 初始化工作，订阅事件等
end

-- 当皮肤组件被加载时调用
function onComponentLoaded(component)
    -- 你可以修改组件或对其创建做出反应
end

-- 当游戏事件发生时调用
function onGameEvent(eventName, data)
    -- 处理游戏事件
end

-- 当判定结果产生时调用
function onJudgement(result)
    -- 根据判定结果创建效果
end

-- 当输入事件发生时调用
function onInputEvent(event)
    -- 对输入事件做出反应
end

-- 每帧调用，用于连续动画或效果
function update()
    -- 创建连续动画或效果
end
```

### 全局API

所有脚本都可以访问通过`osu`对象的以下功能：

```lua
-- 获取当前谱面标题
osu.GetBeatmapTitle()

-- 获取当前谱面艺术家
osu.GetBeatmapArtist()

-- 获取当前规则集名称
osu.GetRulesetName()

-- 创建新组件
osu.CreateComponent(componentType)

-- 获取纹理
osu.GetTexture(name)

-- 获取音频样本
osu.GetSample(name)

-- 播放音频样本
osu.PlaySample(name)

-- 订阅游戏事件
osu.SubscribeToEvent(eventName)

-- 记录日志
osu.Log(message, level) -- level可以是"debug", "info", "warning", "error"
```

### Mania模式特定API

在Mania模式下，脚本还可以通过`mania`对象访问以下功能：

```lua
-- 获取列数
mania.GetColumnCount()

-- 获取音符所在的列
mania.GetNoteColumn(note)

-- 获取列绑定
mania.GetColumnBinding(column)

-- 获取列宽度
mania.GetColumnWidth(column)
```

## 示例脚本

请参考提供的示例脚本：
- [ExampleSkinScript.lua](ExampleSkinScript.lua) - 通用皮肤脚本示例
- [ExampleManiaSkinScript.lua](ExampleManiaSkinScript.lua) - Mania模式特定皮肤脚本示例

## 修改说明

以下文件已被修改以支持皮肤脚本系统：

1. `osu.Game/Skinning/Skin.cs` - 添加了脚本加载和管理功能
2. `osu.Game/Skinning/LegacySkin.cs` - 实现了脚本文件查找
3. `osu.Game/Skinning/SkinManager.cs` - 初始化脚本管理器
4. `osu.Game/Skinning/SkinnableDrawable.cs` - 添加了组件加载通知

新增的文件：

1. `osu.Game/Skinning/Scripting/*.cs` - 脚本系统核心类
2. `osu.Game/Rulesets/Mania/Skinning/Scripting/*.cs` - Mania模式特定脚本扩展

## 限制和注意事项

1. 脚本在沙箱环境中运行，访问权限有限
2. 过于复杂的脚本可能会影响性能
3. 脚本API可能会随着游戏更新而变化

## 故障排除

如果脚本无法正常工作：

1. 检查游戏日志中的脚本错误信息
2. 确保脚本文件格式正确（UTF-8编码，无BOM）
3. 确保脚本没有语法错误
