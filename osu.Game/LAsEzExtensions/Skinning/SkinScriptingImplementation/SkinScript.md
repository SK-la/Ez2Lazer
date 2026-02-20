# 脚本说明

本文档描述当前版本的皮肤 Lua 脚本能力（与代码现状一致）。

## 使用方式

1. 打开设置中的皮肤脚本入口。
2. 使用 `Import script` / `Update script` 按钮为当前皮肤导入 `.lua`。
3. 脚本按皮肤 ID 存储为 `skin-scripts/<SkinID>.lua`，不会写入 Realm。
4. 切换到对应皮肤后自动加载并激活脚本。

## 日志约定

- 皮肤脚本相关日志统一使用 `[SkinScript]` 前缀。
- 常见日志：
    - `Loaded script file`
    - `Script activated`
    - `Script activation failed`
    - `Callback error in <script>.<function>`

## 脚本元数据

```lua
SCRIPT_DESCRIPTION = "Your script description"
SCRIPT_VERSION = "1.0"
SCRIPT_AUTHOR = "YourName"
```

## 回调函数

```lua
function onLoad()
        -- 脚本激活时调用一次
end

function onComponentLoaded(component)
        -- 皮肤组件创建时调用
end

function onGameEvent(eventName, data)
        -- 订阅事件触发后调用
end

function onJudgement(result)
        -- 每次判定调用
end

function onInputEvent(event)
        -- 输入事件调用（需订阅 InputEvent）
end

function update()
        -- 每帧调用
end
```

## osu 全局 API

```lua
osu.GetBeatmapTitle()
osu.GetBeatmapArtist()
osu.GetRulesetName()
osu.GetCurrentTime()

osu.CreateComponent(componentType)
osu.GetTexture(name)
osu.GetSample(name)
osu.PlaySample(name)

osu.SubscribeToEvent(eventName)
osu.Log(message, level) -- debug/info/warning/error
```

### CreateComponent 当前支持

- `Container`
- `Sprite`
- `Box`
- `SpriteText`（实际返回 `OsuSpriteText`）

未知类型会回退为 `Container` 并打印 warning。

## mania 全局 API

仅在当前规则集为 mania 时注入 `mania`：

```lua
mania.GetColumnCount()
mania.GetNoteColumn(note)
mania.GetColumnBinding(column)
mania.GetColumnWidth(column)
```

说明：
- `GetColumnCount()` 基于当前谱面对象统计。
- `GetColumnBinding()` 返回 `Column1/Column2/...` 形式的占位绑定名。
- `GetColumnWidth()` 返回归一化宽度（`1 / 列数`）。

## 事件订阅与触发

脚本通过 `osu.SubscribeToEvent(name)` 订阅事件。

当前已触发事件：

- `HitEvent`
    - 在判定发生时触发。
    - `data` 常见字段：`Type`, `IsHit`, `TimeOffset`, `ColumnIndex`。
- `ManiaColumnHit`
    - mania 判定且可解析列号时触发。
    - 字段：`ColumnIndex`。
- `ManiaHoldActivated`
    - mania 下识别到 hold head 判定时触发。
    - 字段：`ColumnIndex`。
- `ManiaHoldReleased`
    - mania 下识别到 hold tail 判定时触发。
    - 字段：`ColumnIndex`。
- `InputEvent`
    - 键盘输入时触发（按下/抬起）。
    - 字段：`Key`, `State`（`Down`/`Up`）。

## 对象字段（Lua 侧可见）

### component（onComponentLoaded）

- `component.Type.Name`
- `component.Alpha`（可读写）
- `component.Colour = { R, G, B, A }`（可写）
- `component.Column`（若该组件存在此属性）
- `component.StartTime` / `component.EndTime`（若存在）

### result（onJudgement）

- `result.Type`（例如 `Perfect`, `Great`, `Good`, `Ok`, `Meh`, `Miss`）
- `result.HitObject.Column`（若存在）
- `result.HitObject.StartTime` / `result.HitObject.EndTime`（若存在）

## 示例脚本

- [ExampleSkinScript.lua](ExampleSkinScript.lua)
- [ExampleManiaSkinScript.lua](ExampleManiaSkinScript.lua)

## 常见问题

1. **日志显示加载成功但没效果**
     - 先确认回调是否匹配：有些效果依赖 `onJudgement` 或订阅事件。
     - 检查是否调用了 `osu.SubscribeToEvent()`。

2. **脚本描述乱码**
     - 请将 `.lua` 文件保存为 UTF-8（建议无 BOM）。

3. **回调报错无法转换 CLR 类型**
     - 当前已通过 Lua 代理对象传参；若仍报字段不存在，通常是目标对象本身无该属性。
