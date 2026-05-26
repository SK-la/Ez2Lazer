// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osuTK;

namespace osu.Game.EzOsuGame.ScriptedSkin
{
    /// <summary>
    /// 构建脚本皮肤 Roslyn 编译选项（程序集引用与全局 using）。
    /// </summary>
    internal static class ScriptedSkinCompilation
    {
        private static readonly string[] additional_assembly_names =
        {
            "osu.Game.Rulesets.Mania",
            "JetBrains.Annotations",
        };

        private static readonly string[] global_imports =
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "JetBrains.Annotations",
            "osu.Framework.Audio.Sample",
            "osu.Framework.Bindables",
            "osu.Framework.Extensions",
            "osu.Framework.Graphics",
            "osu.Framework.Graphics.Containers",
            "osu.Framework.Graphics.Shapes",
            "osu.Framework.Graphics.Textures",
            "osu.Framework.IO.Stores",
            "osu.Framework.Testing",
            "osu.Game.Audio",
            "osu.Game.Beatmaps",
            "osu.Game.Beatmaps.Formats",
            "osu.Game.Extensions",
            "osu.Game.IO",
            "osu.Game.EzOsuGame.Configuration",
            "osu.Game.EzOsuGame.HUD",
            "osu.Game.EzOsuGame.ScriptedSkin",
            "osu.Game.Rulesets.Mania.Beatmaps",
            "osu.Game.Rulesets.Mania.EzMania",
            "osu.Game.Rulesets.Mania.EzMania.HUD",
            "osu.Game.Rulesets.Mania.Skinning.Ez2",
            "osu.Game.Rulesets.Mania.Skinning.EzStylePro",
            "osu.Game.Rulesets.Mania",
            "osu.Game.Rulesets.Mania.Skinning",
            "osu.Game.Rulesets.Mania.Skinning.Scripted",
            "osu.Game.Rulesets.Scoring",
            "osu.Game.Screens.Play.HUD",
            "osu.Game.Screens.Play.HUD.HitErrorMeters",
            "osu.Game.Screens.Play.HUD.JudgementCounter",
            "osu.Game.Skinning",
            "osu.Game.Skinning.Components",
            "osuTK",
            "osuTK.Graphics",
        };

        private static readonly Lazy<ScriptOptions> script_options = new Lazy<ScriptOptions>(buildScriptOptions);

        public static ScriptOptions Options => script_options.Value;

        /// <summary>
        /// 为脚本源码启用可空引用类型上下文，避免覆盖带 <c>?</c> 签名的 API 时出现 CS8632。
        /// </summary>
        public static string PrepareScriptSource(string scriptCode)
        {
            if (scriptCode.Contains("#nullable", StringComparison.Ordinal))
                return scriptCode;

            return "#nullable enable\n" + scriptCode;
        }

        public static Script CreateScript(string scriptCode) => CSharpScript.Create(PrepareScriptSource(scriptCode), Options);

        public static Compilation ApplyCompilationDefaults(Compilation compilation)
        {
            if (compilation is not CSharpCompilation csharpCompilation)
                return compilation;

            var options = (CSharpCompilationOptions)csharpCompilation.Options;

            return csharpCompilation.WithOptions(options.WithNullableContextOptions(NullableContextOptions.Enable));
        }

        public static IReadOnlyCollection<string> AllowedAssemblyNames { get; } = buildAllowedAssemblyNames();

        public static IResourceStore<byte[]>? CreateDirectoryResourceStore(string scriptPath)
        {
            string? directory = Path.GetDirectoryName(scriptPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

            return new StorageBackedResourceStore(new NativeStorage(directory));
        }

        private static ScriptOptions buildScriptOptions()
        {
            var references = new HashSet<Assembly>
            {
                typeof(object).Assembly,
                typeof(Attribute).Assembly,
                typeof(Enumerable).Assembly,
                typeof(List<>).Assembly,
                typeof(Task).Assembly,
                typeof(IScriptedSkin).Assembly,
                typeof(Drawable).Assembly,
                typeof(Vector2).Assembly,
                typeof(IBeatmap).Assembly,
                typeof(Sample).Assembly,
            };

            foreach (string assemblyName in additional_assembly_names)
            {
                Assembly? assembly = resolveAssembly(assemblyName);

                if (assembly != null)
                    references.Add(assembly);
            }

            foreach (Assembly reference in references.ToArray())
            {
                foreach (AssemblyName dependency in reference.GetReferencedAssemblies())
                {
                    if (!isAllowedDependency(dependency.Name))
                        continue;

                    Assembly? dependencyAssembly = resolveAssembly(dependency.Name!);

                    if (dependencyAssembly != null)
                        references.Add(dependencyAssembly);
                }
            }

            return ScriptOptions.Default
                                .WithReferences(references)
                                .WithImports(global_imports);
        }

        private static IReadOnlyCollection<string> buildAllowedAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "osu.Game",
                "osu.Game.Rulesets.Mania",
                "osu.Framework",
                "osuTK",
                "System.Runtime",
                "System.Collections",
                "System.Linq",
                "System.Core",
                "mscorlib",
                "netstandard",
                "JetBrains.Annotations",
                "osu.Framework.Testing",
            };

            foreach (string assemblyName in additional_assembly_names)
            {
                Assembly? assembly = resolveAssembly(assemblyName);
                string? name = assembly?.GetName().Name;

                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }

            return names;
        }

        internal static Assembly? ResolveAssembly(string assemblyName) => resolveAssembly(assemblyName);

        private static bool isAllowedDependency(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (AllowedAssemblyNames.Contains(assemblyName))
                return true;

            return assemblyName.StartsWith("System.", StringComparison.Ordinal)
                   || assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal)
                   || assemblyName.StartsWith("osu.", StringComparison.Ordinal)
                   || assemblyName.StartsWith("netstandard", StringComparison.Ordinal)
                   || assemblyName.StartsWith("mscorlib", StringComparison.Ordinal);
        }

        private static Assembly? resolveAssembly(string assemblyName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    return assembly;
            }

            try
            {
                return Assembly.Load(assemblyName);
            }
            catch
            {
            }

            string gameAssemblyPath = typeof(ScriptedSkinCompilation).Assembly.Location;

            if (string.IsNullOrEmpty(gameAssemblyPath))
                return null;

            string candidate = Path.Combine(Path.GetDirectoryName(gameAssemblyPath)!, assemblyName + ".dll");

            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        }
    }
}
