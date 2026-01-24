// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Reflection;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;

namespace osu.Game.LAsEzExtensions.Analysis
{
    internal static class XxySrCalculatorBridge
    {
        private const string calculator_type_name = "osu.Game.Rulesets.Mania.LAsEZMania.Analysis.SRCalculator";
        private const string calculator_method_name = "CalculateSR";
        private const string mania_assembly_name = "osu.Game.Rulesets.Mania";

        private static readonly Lazy<MethodInfo?> calculate_method = new Lazy<MethodInfo?>(resolveCalculateMethod, LazyThreadSafetyMode.ExecutionAndPublication);

        private static int resolveFailLogged;
        private static int invokeFailCount;

        public static bool TryCalculate(IBeatmap beatmap, out double sr)
        {
            return TryCalculate(beatmap, 1.0, out sr);
        }

        public static bool TryCalculate(IBeatmap beatmap, double clockRate, out double sr)
        {
            sr = 0;

            double cs = beatmap.BeatmapInfo.Difficulty.CircleSize;
            int keyCount = Math.Max(1, (int)Math.Round(cs));

            if (keyCount >= 11 && (keyCount % 2 == 1))
            {
                return false;
            }

            var method = calculate_method.Value;

            if (method != null)
            {
                try
                {
                    object? result = method.Invoke(null, new object?[] { beatmap, clockRate });

                    if (result is double d)
                    {
                        sr = d;
                        return true;
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    if (Interlocked.Increment(ref invokeFailCount) <= 10)
                        Logger.Error(ex, $"xxy_SR bridge invoke exception with clockRate. beatmapType={beatmap.GetType().FullName}, clockRate={clockRate}", EzAnalysisPersistentStore.LOGGER_NAME);
                }
            }

            return false;
        }

        private static MethodInfo? resolveCalculateMethod()
        {
            try
            {
                var type = findType(calculator_type_name);

                return type?.GetMethod(calculator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(IBeatmap), typeof(double) }, modifiers: null);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref resolveFailLogged, 1) == 0)
                    Logger.Error(ex, $"xxy_SR bridge resolve exception for {calculator_type_name}.{calculator_method_name}.", EzAnalysisPersistentStore.LOGGER_NAME);

                return null;
            }
        }

        private static Type? findType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }

            try
            {
                // 尝试显式加载 mania 程序集。
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
