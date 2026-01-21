# 需要修改的文件列表

以下是实现皮肤脚本系统所需修改的所有文件列表，包括修改内容的简要说明。

## 修改的现有文件

### 1. `osu.Game/Skinning/Skin.cs`

**主要修改：**
- 添加了 `Scripts` 属性，用于存储加载的脚本
- 添加了 `LoadComplete()` 和 `LoadScripts()` 方法，用于加载皮肤脚本
- 添加了 `GetScriptFiles()` 和 `GetStream()` 虚拟方法，供子类实现
- 修改了 `Dispose()` 方法，确保脚本资源被正确释放

### 2. `osu.Game/Skinning/LegacySkin.cs`

**主要修改：**
- 重写了 `GetScriptFiles()` 方法，实现从皮肤文件中查找 .lua 脚本文件

### 3. `osu.Game/Skinning/SkinManager.cs`

**主要修改：**
- 添加了 `scriptManager` 字段，用于存储脚本管理器实例
- 在构造函数中初始化脚本管理器并注册到 RealmAccess
- 确保皮肤切换时脚本也得到更新

### 4. `osu.Game/Skinning/SkinnableDrawable.cs`

**主要修改：**
- 添加了 `[Resolved]` 依赖注入 `SkinScriptManager`
- 添加了 `LoadComplete()` 方法，在组件加载完成后通知脚本
- 添加了 `NotifyScriptsOfComponentLoad()` 方法，用于通知脚本管理器组件已加载

## 新增的文件

### 1. `osu.Game/Skinning/Scripting/ISkinScriptHost.cs`

提供脚本与游戏交互的主机接口，定义了脚本可以访问的资源和功能。

### 2. `osu.Game/Skinning/Scripting/SkinScript.cs`

表示单个Lua脚本，包含加载、执行脚本及处理各种事件的逻辑。

### 3. `osu.Game/Skinning/Scripting/SkinScriptInterface.cs`

为Lua脚本提供API，封装对游戏系统的调用，确保安全且受控的访问。

### 4. `osu.Game/Skinning/Scripting/SkinScriptManager.cs`

管理所有活跃的皮肤脚本，协调脚本加载和事件分发。

### 5. `osu.Game/Skinning/Scripting/SkinScriptingConfig.cs`

管理脚本配置设置，包括启用/禁用脚本和权限列表。

### 6. `osu.Game/Skinning/Scripting/Overlays/SkinScriptingSettingsSection.cs`

提供脚本设置用户界面，允许用户启用/禁用脚本并导入新脚本。

### 7. `osu.Game/Skinning/Scripting/SkinScriptingOverlayRegistration.cs`

在游戏启动时注册脚本设置到设置界面。

### 8. `osu.Game/Overlays/Dialog/FileImportFaultDialog.cs`

用于显示文件导入错误的对话框。

### 9. `osu.Game/Rulesets/Mania/Skinning/Scripting/ManiaSkinScriptExtensions.cs`

为Mania模式提供特定的脚本扩展，允许脚本访问和修改Mania特有的元素。

## 示例文件

### 1. `ExampleSkinScript.lua`

通用皮肤脚本示例，演示基本功能和API用法。

### 2. `ExampleManiaSkinScript.lua`

Mania模式特定皮肤脚本示例，演示如何使用Mania特有的API。

## 需要安装的依赖

- MoonSharp.Interpreter (2.0.0) - Lua脚本引擎

## 实施步骤

1. 安装必要的NuGet包
2. 添加新文件到项目中
3. 按照列表修改现有文件
4. 编译并测试基本功能
5. 测试示例脚本

## 对现有代码的影响

修改尽量保持最小化，主要通过添加方法和属性来扩展现有类，而不是修改核心逻辑。所有脚本执行都在try-catch块中，确保脚本错误不会影响游戏稳定性。
