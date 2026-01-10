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
        private const string logger_name = "xxy_sr";

        private const string calculator_type_name = "osu.Game.Rulesets.Mania.LAsEZMania.Analysis.SRCalculator";
        private const string calculator_method_name = "CalculateSR";
        private const string mania_assembly_name = "osu.Game.Rulesets.Mania";

        private static readonly Lazy<MethodInfo?> calculate_method = new Lazy<MethodInfo?>(resolveCalculateMethod, LazyThreadSafetyMode.ExecutionAndPublication);

        private static int resolveFailLogged;
        private static int invokeFailCount;

        public static bool TryCalculate(IBeatmap beatmap, out double sr)
        {
            sr = 0;

            var method = calculate_method.Value;

            if (method == null)
            {
                if (Interlocked.Exchange(ref resolveFailLogged, 1) == 0)
                    Logger.Log($"xxy_SR bridge failed to resolve {calculator_type_name}.{calculator_method_name}(IBeatmap).", logger_name, LogLevel.Error);

                return false;
            }

            try
            {
                object? result = method.Invoke(null, new object?[] { beatmap });

                if (result is double d)
                {
                    sr = d;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                // Avoid spamming logs if something is systematically broken.
                if (Interlocked.Increment(ref invokeFailCount) <= 10)
                    Logger.Error(ex, $"xxy_SR bridge invoke exception. beatmapType={beatmap.GetType().FullName}", logger_name);

                return false;
            }
        }

        private static MethodInfo? resolveCalculateMethod()
        {
            try
            {
                var type = findType(calculator_type_name);

                return type?.GetMethod(calculator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(IBeatmap) }, modifiers: null);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref resolveFailLogged, 1) == 0)
                    Logger.Error(ex, $"xxy_SR bridge resolve exception for {calculator_type_name}.{calculator_method_name}(IBeatmap).", logger_name);

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
