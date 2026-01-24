// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Statistics
{
    /// <summary>
    /// 桥接类，提供对规则集特定的 <see cref="IHitEventGenerator"/> 实现的访问。
    /// 允许每个规则集注册自己的生成器，并为未注册的规则集提供回退机制。
    /// </summary>
    public static class ScoreHitEventGeneratorBridge
    {
        private static readonly ConcurrentDictionary<string, IHitEventGenerator> generators = new ConcurrentDictionary<string, IHitEventGenerator>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册规则集特定的命中事件生成器。
        /// </summary>
        /// <param name="key">规则集标识符（通常是短名称，如"osu"、"mania"等）</param>
        /// <param name="generator">规则集的命中事件生成器</param>
        public static void Register(string key, IHitEventGenerator generator)
        {
            if (string.IsNullOrEmpty(key))
                return;

            generators[key] = generator;
        }

        /// <summary>
        /// 注销规则集特定的命中事件生成器。
        /// </summary>
        /// <param name="key">要注销的规则集标识符</param>
        public static void Unregister(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            generators.TryRemove(key, out _);
        }

        /// <summary>
        /// 通过规则集键获取已注册的命中事件生成器。
        /// </summary>
        /// <param name="key">规则集标识符</param>
        /// <returns>已注册的生成器，如果未找到则为null</returns>
        public static IHitEventGenerator? Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            return generators.GetValueOrDefault(key);
        }

        /// <summary>
        /// 尝试使用适当的规则集特定生成器为分数生成命中事件。
        /// 自动根据分数的规则集确定正确的生成器。
        /// </summary>
        /// <param name="score">要为其生成命中事件的分数</param>
        /// <param name="playableBeatmap">与分数关联的谱面</param>
        /// <param name="cancellationToken">用于停止生成的取消令牌</param>
        /// <returns>命中事件列表，如果没有可用的生成器则为null</returns>
        public static List<HitEvent>? TryGenerate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            try
            {
                string key = score.ScoreInfo.Ruleset.ShortName;

                if (string.IsNullOrEmpty(key))
                    key = score.ScoreInfo.Ruleset.OnlineID.ToString();

                // 快速路径：如果可用，使用已注册的生成器
                var gen = Get(key);
                if (gen != null)
                    return gen.Generate(score, playableBeatmap, cancellationToken);

                // 回退：通过反射发现实现了IHitEventGenerator接口的类型
                var discoveredGen = discoverAndRegisterGenerator(key, score, playableBeatmap, cancellationToken);
                return discoveredGen;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"HitEvent generation via bridge failed. ruleset={score.ScoreInfo.Ruleset.ShortName}", EzAnalysisPersistentStore.LOGGER_NAME);
                return null;
            }
        }

        /// <summary>
        /// 通过反射发现实现了IHitEventGenerator接口的类型并注册以供将来使用。
        /// 专门查找实现了IHitEventGenerator接口的类型。
        /// </summary>
        /// <param name="key">用于为发现的生成器注册的规则集键</param>
        /// <param name="score">要为其生成命中事件的分数</param>
        /// <param name="playableBeatmap">与分数关联的谱面</param>
        /// <param name="cancellationToken">用于停止生成的取消令牌</param>
        /// <returns>发现的生成器的结果，如果没有找到则为null</returns>
        private static List<HitEvent>? discoverAndRegisterGenerator(string key, Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            // 遍历所有程序集，查找实现了IHitEventGenerator接口的类型
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                // 获取程序集中的所有类型，如果无法访问则跳过此程序集
                // 某些程序集可能因安全限制等原因无法获取其类型列表
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t.IsInterface || t.IsAbstract)
                        continue;

                    if (!typeof(IHitEventGenerator).IsAssignableFrom(t))
                        continue;

                    IHitEventGenerator? candidate = null;

                    var instProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);

                    if (instProp != null && typeof(IHitEventGenerator).IsAssignableFrom(instProp.PropertyType))
                    {
                        candidate = instProp.GetValue(null) as IHitEventGenerator;
                    }

                    // 回退到无参构造函数
                    candidate ??= Activator.CreateInstance(t) as IHitEventGenerator;

                    // 测试此候选项是否适用于提供的分数
                    var result = candidate?.Generate(score, playableBeatmap, cancellationToken);

                    if (result != null && candidate != null)
                    {
                        Register(key, candidate);
                        return result;
                    }
                }
            }

            Logger.Log($"No HitEvent generator found for ruleset={key}. Skipping local generation.", EzAnalysisPersistentStore.LOGGER_NAME, LogLevel.Debug);
            return null;
        }
    }
}
