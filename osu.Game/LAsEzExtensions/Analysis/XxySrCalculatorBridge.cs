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

        private static readonly Lazy<(MethodInfo? withRate, MethodInfo? withoutRate)> calculate_methods = new Lazy<(MethodInfo?, MethodInfo?)>(resolveCalculateMethods, LazyThreadSafetyMode.ExecutionAndPublication);

        private static int resolveFailLogged;
        private static int invokeFailCount;

        public static bool TryCalculate(IBeatmap beatmap, out double sr)
        {
            return TryCalculate(beatmap, 1.0, out sr);
        }

        public static bool TryCalculate(IBeatmap beatmap, double clockRate, out double sr)
        {
            sr = 0;

            var (methodWithRate, methodWithoutRate) = calculate_methods.Value;

            // Try method with clockRate first (newer signature)
            if (methodWithRate != null)
            {
                try
                {
                    object? result = methodWithRate.Invoke(null, new object?[] { beatmap, clockRate });

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
                        Logger.Error(ex, $"xxy_SR bridge invoke exception with clockRate. beatmapType={beatmap.GetType().FullName}, clockRate={clockRate}", logger_name);

                    return false;
                }
            }

            // Fallback to method without clockRate (older signature)
            var method = methodWithoutRate;

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

        private static (MethodInfo? withRate, MethodInfo? withoutRate) resolveCalculateMethods()
        {
            try
            {
                var type = findType(calculator_type_name);

                if (type == null)
                    return (null, null);

                // Try to find method with clockRate parameter (newer signature)
                var methodWithRate = type.GetMethod(calculator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(IBeatmap), typeof(double) }, modifiers: null);

                // Try to find method without clockRate parameter (older signature)
                var methodWithoutRate = type.GetMethod(calculator_method_name, BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(IBeatmap) }, modifiers: null);

                return (methodWithRate, methodWithoutRate);
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref resolveFailLogged, 1) == 0)
                    Logger.Error(ex, $"xxy_SR bridge resolve exception for {calculator_type_name}.{calculator_method_name}.", logger_name);

                return (null, null);
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
