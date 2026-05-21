# 脚本皮肤 API 文档

本文只记录当前实现中可直接使用的接口与核心类。

## 1. `IScriptedSkin`

文件：`osu.Game/EzOsuGame/ScriptedSkin/IScriptedSkin.cs`

```csharp
public interface IScriptedSkin : IDisposable
{
    void Initialize(ISkinSource baseSkin, IStorageResourceProvider resources);
    Drawable? GetDrawableComponent(ISkinComponentLookup lookup);
    Texture? GetTexture(string componentName, WrapMode wrapModeS = default, WrapMode wrapModeT = default);
    ISample? GetSample(ISampleInfo sampleInfo);
    IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        where TLookup : notnull
        where TValue : notnull;

    string Name { get; }
    string Author { get; }
    Version Version { get; }
}
```

说明：

- `Initialize()` 仅在皮肤初始化时调用一次
- `Get*()` 返回 `null` 表示回退到默认链路

## 2. `IScriptedSkinMetadata`（可选）

文件：`osu.Game/EzOsuGame/ScriptedSkin/IScriptedSkin.cs`

```csharp
public interface IScriptedSkinMetadata
{
    bool Protected { get; }
}
```

用途：

- 提供脚本皮肤的保护状态元数据
- 不实现也可正常使用脚本皮肤

## 3. `SandboxedScriptRunner`

文件：`osu.Game/EzOsuGame/ScriptedSkin/SandboxedScriptRunner.cs`

关键方法：

```csharp
Task<IScriptedSkin> LoadScriptAsync(string scriptPath)
Task<T> LoadScriptAsync<T>(string scriptPath, params object?[] ctorArgs) where T : class
Task<SkinInfo?> LoadScriptInfoAsync(string scriptPath)
void ClearCache(string scriptPath)
```

行为：

- 用 Roslyn 编译 `.csx`
- 支持直接返回对象、反射实例化类型、以及 `Skin -> IScriptedSkin` 适配
- 有程序集白名单与危险 API 关键字检查

## 4. `ScriptedSkinWrapper`

文件：`osu.Game/Skinning/ScriptedSkinWrapper.cs`

构造函数：

```csharp
public ScriptedSkinWrapper(
    ISkinSource skinSource,
    IStorageResourceProvider resources,
    IScriptedSkin scriptedSkin,
    SandboxedScriptRunner? scriptRunner = null,
    SkinInfo? skinInfo = null)
```

关键成员：

```csharp
public SandboxedScriptRunner ScriptRunner { get; }
public IScriptedSkin GetScriptedSkin()
public Task<bool> ReloadAsync(string scriptPath)
```

说明：

- 包装脚本皮肤到标准 `Skin` 体系
- 可以复用外部传入的 `SkinInfo`（保留 `Protected`）

## 5. `SkinManager` 脚本相关方法

文件：`osu.Game/Skinning/SkinManager.cs`

```csharp
public string GetScriptPath(SkinInfo skinInfo)
public static string GetScriptPathStatic(SkinInfo skinInfo)
public void StartScriptWatching()
public Task<bool> TriggerScriptReload(string scriptPath)
```

内部关键流程：

- `scanForScriptedSkins()`：扫描并注册脚本皮肤
- `GetSkin()`：按 `SkinInfo` 加载脚本并构建 `ScriptedSkinWrapper`

## 6. `ManiaScriptedSkinTransformer`

文件：`osu.Game.Rulesets.Mania/Skinning/Scripted/ManiaScriptedSkinTransformer.cs`

行为：

- 加载 Mania 专用脚本 transformer（若存在）
- 查找优先级：
  - `Mania{MainScriptStem}Transformer.csx`
  - `ManiaTransformer.csx`
- `GetConfig` / `GetDrawableComponent` 优先走 Mania transformer，再回退

## 7. 热重载 API

文件：`osu.Game/EzOsuGame/ScriptedSkin/HotReloadManager.cs`

```csharp
public event Action<string, bool>? SkinReloaded;
public void StartWatching(string directory)
public void StopWatching()
public Task<bool> TriggerReload(string scriptPath)
public ReloadStatus GetReloadStatus(string scriptPath)
```

`ReloadStatus`：

```csharp
public enum ReloadState { Idle, Reloading, Success, Failed }
public class ReloadStatus
{
    public ReloadState State { get; set; }
    public DateTime? LastReloadTime { get; set; }
    public string? ErrorMessage { get; set; }
}
```

