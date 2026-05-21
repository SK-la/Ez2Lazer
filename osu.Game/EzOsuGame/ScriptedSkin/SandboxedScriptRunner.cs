// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osuTK;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 沙箱化的脚本执行器，负责加载、编译和执行用户编写的皮肤脚本。
    /// </summary>
    /// <remarks>
    /// 此执行器通过限制可访问的 API 和文件系统操作来确保安全性，
    /// 防止恶意脚本对系统造成损害或在游戏中作弊。
    /// </remarks>
    public class SandboxedScriptRunner
    {
        internal static readonly string LOGGER_PREFIX = "ScriptedSkin";

        private static readonly ScriptOptions safe_script_options;

        private readonly ScriptCompilationCache cache;

        /// <summary>
        /// 创建沙箱脚本执行器实例。
        /// </summary>
        public SandboxedScriptRunner()
        {
            cache = new ScriptCompilationCache();
        }

        static SandboxedScriptRunner()
        {
            // 定义允许引用的程序集白名单
            var allowedAssemblies = new[]
            {
                typeof(IScriptedSkin).Assembly, // osu.Game
                typeof(Drawable).Assembly, // osu.Framework
                typeof(Vector2).Assembly, // osuTK
                typeof(Console).Assembly, // System.Runtime
                typeof(List<>).Assembly, // System.Collections
                typeof(Enumerable).Assembly, // System.Linq
            };

            // 构建安全的脚本选项
            safe_script_options = ScriptOptions.Default
                                               .WithReferences(allowedAssemblies)
                                               .WithImports(
                                                   "System",
                                                   "System.Collections.Generic",
                                                   "System.Linq",
                                                   "osu.Framework.Graphics",
                                                   "osu.Framework.Graphics.Containers",
                                                   "osu.Framework.Graphics.Shapes",
                                                   "osu.Framework.Bindables",
                                                   "osuTK",
                                                   "osuTK.Graphics",
                                                   "osu.Game.Skinning",
                                                   "osu.Game.Skinning.ScriptedSkin"
                                               )
                                               .WithMetadataResolver(new SafeMetadataResolver());

            Logger.Log("SandboxedScriptRunner initialized with security restrictions.", LoggingTarget.Information);
        }

        /// <summary>
        /// 异步加载并编译脚本文件。
        /// </summary>
        /// <param name="scriptPath">脚本文件的完整路径。</param>
        /// <returns>编译后的 IScriptedSkin 实例。</returns>
        /// <exception cref="ScriptCompilationException">编译失败时抛出。</exception>
        /// <exception cref="ScriptSecurityException">安全验证失败时抛出。</exception>
        /// <exception cref="ScriptExecutionException">执行失败时抛出。</exception>
        public async Task<IScriptedSkin> LoadScriptAsync(string scriptPath)
        {
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Script file not found: {scriptPath}");

            // 尝试从缓存中获取
            if (cache.TryGet(scriptPath, out var cachedSkin))
            {
                if (cachedSkin != null)
                {
                    Logger.Log($"{LOGGER_PREFIX} Using cached skin: {cachedSkin.Name}", LoggingTarget.Information);
                    return cachedSkin;
                }
            }

            Logger.Log($"{LOGGER_PREFIX} Loading script: {Path.GetFileName(scriptPath)}", LoggingTarget.Information);

            // 读取脚本内容
            string scriptCode = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(false);

            // 验证脚本安全性
            validateScriptSafety(scriptCode, scriptPath);

            try
            {
                // 创建脚本对象
                var script = CSharpScript.Create<IScriptedSkin>(scriptCode, safe_script_options);

                // 获取编译结果并检查错误
                var compilation = script.GetCompilation();
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

                // 执行脚本（创建实例）
                Logger.Log($"{LOGGER_PREFIX} Executing script...", LoggingTarget.Information);
                var state = await script.RunAsync().ConfigureAwait(false);
                var skin = state.ReturnValue;

                if (skin == null)
                    throw new ScriptExecutionException("Script did not return an IScriptedSkin instance.");

                // 添加到缓存
                cache.Add(scriptPath, skin);

                Logger.Log($"{LOGGER_PREFIX} Successfully loaded skin: {skin.Name} v{skin.Version} by {skin.Author}", LoggingTarget.Information);

                return skin;
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

        /// <summary>
        /// 验证脚本的安全性，阻止危险操作。
        /// </summary>
        /// <param name="scriptCode">脚本源代码。</param>
        /// <param name="scriptPath">脚本文件路径（用于错误报告）。</param>
        /// <exception cref="ScriptSecurityException">发现不安全代码时抛出。</exception>
        private void validateScriptSafety(string scriptCode, string scriptPath)
        {
            // 定义禁止的 API 模式
            var forbiddenPatterns = new Dictionary<string, string>
            {
                { "System.IO.File", "File system access is forbidden" },
                { "System.IO.Directory", "Directory operations are forbidden" },
                { "System.Net", "Network access is forbidden" },
                { "System.Diagnostics", "Process operations are forbidden" },
                { "System.Reflection", "Reflection is forbidden for security reasons" },
                { "unsafe", "Unsafe code is not allowed" },
                { "DllImport", "P/Invoke is forbidden" },
                { "Marshal", "Memory manipulation is forbidden" },
            };

            foreach (var kvp in forbiddenPatterns)
            {
                if (scriptCode.Contains(kvp.Key))
                {
                    string message = $"Security violation in {Path.GetFileName(scriptPath)}: {kvp.Value}. " +
                                     $"Forbidden API: '{kvp.Key}'";
                    Logger.Log(message, LoggingTarget.Runtime);
                    throw new ScriptSecurityException(message);
                }
            }

            // 检查是否使用了命名空间声明（应该使用全局命名空间）
            if (scriptCode.Contains("namespace "))
            {
                Logger.Log($"{LOGGER_PREFIX} Warning: Script {Path.GetFileName(scriptPath)} contains namespace declaration. " +
                           "Scripts should use global namespace for simplicity.", LoggingTarget.Information);
            }
        }
    }

    /// <summary>
    /// 安全的元数据解析器，限制脚本可引用的程序集。
    /// </summary>
    internal class SafeMetadataResolver : MetadataReferenceResolver
    {
        private static readonly HashSet<string> allowed_assemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "osu.Game",
            "osu.Framework",
            "osuTK",
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Core",
            "mscorlib",
            "netstandard",
        };

        public override bool ResolveMissingAssemblies => false;

        public override ImmutableArray<PortableExecutableReference> ResolveReference(
            string reference,
            string? baseFilePath,
            MetadataReferenceProperties properties)
        {
            string assemblyName = Path.GetFileNameWithoutExtension(reference);

            if (!allowed_assemblies.Contains(assemblyName))
            {
                string message = $"Assembly '{assemblyName}' is not allowed in scripted skins. " +
                                 $"Allowed assemblies: {string.Join(", ", allowed_assemblies)}";
                Logger.Log(message, LoggingTarget.Runtime, LogLevel.Important);
                throw new ScriptSecurityException(message);
            }

            return ImmutableArray.Create(MetadataReference.CreateFromFile(reference));
        }

        public override bool Equals(object? other) => other is SafeMetadataResolver;

        public override int GetHashCode() => nameof(SafeMetadataResolver).GetHashCode();
    }

    /// <summary>
    /// 受限的文件解析器，禁止访问脚本目录外的文件。
    /// </summary>
    /// <remarks>
    /// 由于 Roslyn 的 SourceFileResolver API 限制，此类的实际文件访问限制
    /// 主要通过 SafeMetadataResolver 和静态代码分析来实现。
    /// </remarks>
    internal class RestrictedFileResolver : SourceFileResolver
    {
        public RestrictedFileResolver()
            : base(Array.Empty<string>(), Directory.GetCurrentDirectory())
        {
        }
    }
}
