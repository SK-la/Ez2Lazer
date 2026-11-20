// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 加载、编译并执行用户编写的皮肤脚本。
    /// </summary>
    /// <remarks>
    /// 脚本与游戏同进程执行，权限与游戏进程相同，可使用已引用的 .NET 与游戏 API。
    /// 这里没有沙箱：调用方只能把脚本当成与可执行文件同等的本机可信代码。
    /// </remarks>
    public class SandboxedScriptRunner
    {
        internal static readonly string LOGGER_PREFIX = "ScriptedSkin";

        private readonly ScriptCompilationCache cache;

        /// <summary>
        /// 创建脚本执行器实例。
        /// </summary>
        public SandboxedScriptRunner()
        {
            cache = new ScriptCompilationCache();
            Logger.Log($"{LOGGER_PREFIX} scripted skins execute as full-trust code in this process.", LoggingTarget.Information, LogLevel.Important);
        }

        /// <summary>
        /// 异步加载并编译脚本文件。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <returns>编译后的 IScriptedSkin 实例。</returns>
        /// <exception cref="ScriptCompilationException">编译失败时抛出。</exception>
        /// <exception cref="ScriptExecutionException">执行失败时抛出。</exception>
        public async Task<IScriptedSkin> LoadScriptAsync(string scriptPath)
            => await LoadScriptAsync<IScriptedSkin>(scriptPath).ConfigureAwait(false);

        /// <summary>
        /// 异步加载并编译脚本文件，返回指定目标类型的实例。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <param name="ctorArgs">构造函数参数。</param>
        /// <returns>编译后的实例。</returns>
        /// <exception cref="ScriptCompilationException">编译失败时抛出。</exception>
        /// <exception cref="ScriptExecutionException">执行失败时抛出。</exception>
        public async Task<T> LoadScriptAsync<T>(string scriptPath, params object?[] ctorArgs)
            where T : class
        {
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Script file not found: {scriptPath}");

            if (typeof(T) == typeof(IScriptedSkin) && cache.TryGet(scriptPath, out var cachedSkin))
            {
                if (cachedSkin != null)
                {
                    Logger.Log($"{LOGGER_PREFIX} Using cached skin: {cachedSkin.Name}", LoggingTarget.Information);
                    return (T)cachedSkin;
                }
            }

            Logger.Log($"{LOGGER_PREFIX} Loading script: {Path.GetFileName(scriptPath)}", LoggingTarget.Information);

            // 读取脚本内容
            string scriptCode = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

            try
            {
                // 创建脚本对象（启用 nullable 上下文，与 Skin API 的可空签名一致）
                var script = ScriptedSkinCompilation.CreateScript(scriptCode);

                // 获取编译结果并检查错误
                var compilation = ScriptedSkinCompilation.ApplyCompilationDefaults(script.GetCompilation());
                var diagnostics = compilation.GetDiagnostics();

                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

                if (errors.Any())
                {
                    var exception = new ScriptCompilationException(errors);
                    Logger.Log($"{LOGGER_PREFIX} Compilation failed: {exception.GetFormattedErrors()}", LoggingTarget.Runtime);
                    throw exception;
                }

                // 记录警告信息
                var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

                if (warnings.Any())
                {
                    foreach (var warning in warnings)
                    {
                        Logger.Log($"{LOGGER_PREFIX} Warning: {warning.GetMessage()} (Line {warning.Location.GetLineSpan().StartLinePosition.Line + 1})", LoggingTarget.Information);
                    }
                }

                var state = await script.RunAsync().ConfigureAwait(false);
                var instance = instantiateFromReturnValue<T>(state.ReturnValue, scriptPath, ctorArgs);
                if (instance != null)
                    return cacheAndReturn(scriptPath, instance);

                var loadedAssembly = emitAssembly(compilation, scriptPath);
                var reflected = instantiateFromAssembly<T>(loadedAssembly, scriptPath, ctorArgs);

                if (reflected != null)
                    return cacheAndReturn(scriptPath, reflected);

                throw new ScriptExecutionException($"Script did not expose a usable {typeof(T).Name} implementation.");
            }
            catch (CompilationErrorException ex)
            {
                Logger.Log($"{LOGGER_PREFIX} Compilation error: {ex.Message}", LoggingTarget.Runtime);
                throw new ScriptCompilationException(ex.Diagnostics);
            }
            catch (ScriptExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"{LOGGER_PREFIX} Unexpected error during script execution: {ex}", LoggingTarget.Runtime);
                throw new ScriptExecutionException($"Failed to execute script: {ex.Message}", ex);
            }
        }

        private static Assembly emitAssembly(Compilation compilation, string scriptPath)
        {
            using var peStream = new MemoryStream();

            var emitResult = compilation.Emit(peStream);

            if (!emitResult.Success)
                throw new ScriptCompilationException(emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList());

            return Assembly.Load(peStream.ToArray());
        }

        private T? instantiateFromReturnValue<T>(object? returnValue, string scriptPath, object?[] ctorArgs)
            where T : class
        {
            if (returnValue == null)
                return null;

            if (returnValue is T typed)
                return typed;

            if (typeof(T) == typeof(IScriptedSkin) && returnValue is Skin skin)
                return (T)(object)new SkinScriptedSkinAdapter(skin);

            Logger.Log($"{LOGGER_PREFIX} Script returned {returnValue.GetType().Name}, which cannot be used as {typeof(T).Name} for {Path.GetFileName(scriptPath)}.", LoggingTarget.Information);
            return null;
        }

        private T? instantiateFromAssembly<T>(Assembly assembly, string scriptPath, object?[] ctorArgs)
            where T : class
        {
            string expectedTypeName = Path.GetFileNameWithoutExtension(scriptPath);
            var candidateTypes = assembly.GetTypes()
                                         .Where(type => !type.IsAbstract && !type.IsInterface)
                                         .Where(matchesTargetType<T>)
                                         .OrderByDescending(type => string.Equals(type.Name, expectedTypeName, StringComparison.Ordinal))
                                         .ThenBy(type => type.FullName, StringComparer.Ordinal)
                                         .ToArray();

            foreach (var type in candidateTypes)
            {
                foreach (object?[] args in expandCtorArgs(scriptPath, ctorArgs))
                {
                    var instance = adaptInstance<T>(tryCreateInstance(type, args));
                    if (instance != null)
                        return instance;
                }
            }

            return null;
        }

        private static IEnumerable<object?[]> expandCtorArgs(string scriptPath, object?[] ctorArgs)
        {
            yield return ctorArgs;

            var fallbackStore = ScriptedSkinCompilation.CreateDirectoryResourceStore(scriptPath);

            if (fallbackStore == null)
                yield break;

            switch (ctorArgs.Length)
            {
                case 0:
                    yield return new object?[] { fallbackStore };

                    break;

                case 1:
                    yield return new[] { ctorArgs[0], fallbackStore };

                    break;

                case 2:
                    yield return new[] { ctorArgs[0], ctorArgs[1], fallbackStore };

                    break;
            }
        }

        private static bool matchesTargetType<T>(Type type)
            where T : class
        {
            if (typeof(T).IsAssignableFrom(type))
                return true;

            return typeof(T) == typeof(IScriptedSkin) && typeof(Skin).IsAssignableFrom(type);
        }

        private static object? tryCreateInstance(Type type, object?[] ctorArgs)
        {
            if (ctorArgs.Length == 0)
            {
                if (Activator.CreateInstance(type) is object direct)
                    return direct;
            }

            foreach (var ctor in type.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != ctorArgs.Length)
                    continue;

                bool compatible = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    object? arg = ctorArgs[i];

                    if (arg == null)
                    {
                        if (parameters[i].ParameterType.IsValueType && Nullable.GetUnderlyingType(parameters[i].ParameterType) == null)
                        {
                            compatible = false;
                            break;
                        }

                        continue;
                    }

                    if (!parameters[i].ParameterType.IsInstanceOfType(arg))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                    continue;

                return ctor.Invoke(ctorArgs);
            }

            return null;
        }

        private static T? adaptInstance<T>(object? instance)
            where T : class
        {
            if (instance == null)
                return null;

            if (instance is T typed)
                return typed;

            if (typeof(T) == typeof(IScriptedSkin) && instance is Skin skin)
                return (T)(object)new SkinScriptedSkinAdapter(skin);

            return null;
        }

        private T cacheAndReturn<T>(string scriptPath, T instance)
            where T : class
        {
            if (typeof(T) == typeof(IScriptedSkin) && instance is IScriptedSkin scriptedSkin)
                cache.Add(scriptPath, scriptedSkin);

            return instance;
        }

        /// <summary>
        /// 尝试提取脚本中声明的 <see cref="SkinInfo"/> 元数据。
        /// </summary>
        public async Task<SkinInfo?> LoadScriptInfoAsync(string scriptPath)
        {
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Script file not found: {scriptPath}");

            string scriptCode = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

            try
            {
                var script = ScriptedSkinCompilation.CreateScript(scriptCode);
                var compilation = ScriptedSkinCompilation.ApplyCompilationDefaults(script.GetCompilation());
                var diagnostics = compilation.GetDiagnostics();

                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                if (errors.Any())
                    throw new ScriptCompilationException(errors);

                var assembly = emitAssembly(compilation, scriptPath);
                string expectedTypeName = Path.GetFileNameWithoutExtension(scriptPath);

                var candidateTypes = assembly.GetTypes()
                                             .Where(type => !type.IsAbstract && !type.IsInterface)
                                             .OrderByDescending(type => string.Equals(type.Name, expectedTypeName, StringComparison.Ordinal))
                                             .ThenBy(type => type.FullName, StringComparer.Ordinal)
                                             .ToArray();

                foreach (var type in candidateTypes)
                {
                    var createInfo = type.GetMethod("CreateInfo", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                    if (createInfo?.ReturnType == typeof(SkinInfo) && createInfo.GetParameters().Length == 0)
                    {
                        if (createInfo.Invoke(null, null) is SkinInfo skinInfo)
                            return skinInfo;
                    }
                }

                string fallbackName = Path.GetFileNameWithoutExtension(scriptPath);
                return new SkinInfo(fallbackName, string.Empty, typeof(ScriptedSkinWrapper).GetInvariantInstantiationInfo());
            }
            catch (CompilationErrorException ex)
            {
                throw new ScriptCompilationException(ex.Diagnostics);
            }
            catch (ScriptCompilationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScriptExecutionException($"Failed to read script metadata: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清除指定脚本的编译缓存，强制下次加载时重新编译。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        public void ClearCache(string scriptPath)
        {
            cache.RemoveByPath(scriptPath);
            Logger.Log($"{LOGGER_PREFIX} Cache cleared for: {Path.GetFileName(scriptPath)}", LoggingTarget.Information);
        }

        /// <summary>
        /// 清除所有脚本的编译缓存。
        /// </summary>
        public void ClearAllCache()
        {
            cache.Clear();
            Logger.Log($"{LOGGER_PREFIX} All script caches cleared.", LoggingTarget.Information);
        }
    }

    /// <summary>
    /// 将 <see cref="Skin"/> 适配为 <see cref="IScriptedSkin"/>。
    /// </summary>
    internal sealed class SkinScriptedSkinAdapter : IScriptedSkin
    {
        private readonly Skin skin;

        public SkinScriptedSkinAdapter(Skin skin)
        {
            this.skin = skin;
        }

        public void Initialize(ISkinSource baseSkin, IStorageResourceProvider resources)
        {
        }

        public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => skin.GetDrawableComponent(lookup);

        public Texture? GetTexture(string componentName, WrapMode wrapModeS = default, WrapMode wrapModeT = default) => skin.GetTexture(componentName, wrapModeS, wrapModeT);

        public ISample? GetSample(ISampleInfo sampleInfo) => skin.GetSample(sampleInfo);

        public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
            where TLookup : notnull
            where TValue : notnull
            => skin.GetConfig<TLookup, TValue>(lookup);

        public string Name => skin.Name;

        public string Author => skin.SkinInfo.Value.Creator;

        public Version Version => skin.GetType().GetProperty("Version", BindingFlags.Public | BindingFlags.Instance)?.GetValue(skin) as Version ?? new Version(1, 0, 0);

        public void Dispose() => skin.Dispose();
    }
}
