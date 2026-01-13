// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// 本地解析成绩结算的桥接
    /// 通过反射mania规则集和特定组件实现
    /// </summary>
    internal static class ScoreHitEventGeneratorBridge
    {
        private const string logger_name = "hit_events";

        private const string mania_assembly_name = "osu.Game.Rulesets.Mania";
        private const string generator_type_name = "osu.Game.Rulesets.Mania.Analysis.ManiaScoreHitEventGenerator";
        private const string generator_method_name = "Generate";

        private static readonly Lazy<MethodInfo?> generate_method = new Lazy<MethodInfo?>(resolve_method, LazyThreadSafetyMode.ExecutionAndPublication);

        private static int resolve_fail_logged;
        private static int invoke_fail_count;

        public static List<HitEvent>? TryGenerate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            var method = generate_method.Value;

            if (method == null)
            {
                if (Interlocked.Exchange(ref resolve_fail_logged, 1) == 0)
                    Logger.Log($"HitEvent generation bridge failed to resolve {generator_type_name}.{generator_method_name}(Score, IBeatmap, CancellationToken).", logger_name, LogLevel.Error);

                return null;
            }

            try
            {
                object? result = method.Invoke(null, new object?[] { score, playableBeatmap, cancellationToken });
                return result as List<HitEvent>;
            }
            catch (Exception ex)
            {
                // Avoid spamming logs if something is systematically broken.
                if (Interlocked.Increment(ref invoke_fail_count) <= 10)
                    Logger.Error(ex, $"HitEvent generation bridge invoke exception. ruleset={score.ScoreInfo?.Ruleset.ShortName}", logger_name);

                return null;
            }
        }

        private static MethodInfo? resolve_method()
        {
            try
            {
                var type = find_type(generator_type_name);
                if (type == null)
                    return null;

                return type.GetMethod(generator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null,
                    types: new[] { typeof(Score), typeof(IBeatmap), typeof(CancellationToken) }, modifiers: null);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref resolve_fail_logged, 1) == 0)
                    Logger.Error(ex, $"HitEvent generation bridge resolve exception for {generator_type_name}.{generator_method_name}.", logger_name);

                return null;
            }
        }

        private static Type? find_type(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }

            try
            {
                var asm = Assembly.Load(mania_assembly_name);
                return asm.GetType(fullName, throwOnError: false);
            }
            catch
            {
                return null;
            }
        }
    }
}
