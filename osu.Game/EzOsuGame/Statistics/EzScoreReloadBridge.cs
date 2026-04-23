// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 规则集特定的分数命中事件生成器的静态工厂。
    /// 允许每个规则集注册自己的生成器实现，并根据分数的规则集自动选择合适的实现。
    /// </summary>
    /// 在没有明确允许的情况下，禁止调用此方法
    public static class EzScoreReloadBridge
    {
        private static readonly ConcurrentDictionary<string, IScoreHitEventGenerator> registered_generators
            = new ConcurrentDictionary<string, IScoreHitEventGenerator>();

        /// <summary>
        /// 初始化所有已知的规则集生成器。
        /// 这会触发所有生成器的静态构造函数执行，从而完成注册。
        /// </summary>
        public static void InitializeAllGenerators()
        {
            // 强制加载各个规则集的生成器
            // 这会触发它们的静态构造函数，进而注册到工厂
            try
            {
                // Mania 生成器
                var maniaType = Type.GetType("osu.Game.Rulesets.Mania.EzMania.Statistics.ManiaScoreHitEventGenerator, osu.Game.Rulesets.Mania");

                if (maniaType != null)
                {
                    var instanceProp = maniaType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _ = instanceProp?.GetValue(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize ManiaScoreHitEventGenerator", Ez2ConfigManager.LOGGER_NAME);
            }

            try
            {
                // Osu 生成器
                var osuType = Type.GetType("osu.Game.Rulesets.Osu.EzOsu.Statistics.OsuScoreHitEventGenerator, osu.Game.Rulesets.Osu");

                if (osuType != null)
                {
                    var instanceProp = osuType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _ = instanceProp?.GetValue(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize OsuScoreHitEventGenerator", Ez2ConfigManager.LOGGER_NAME);
            }

            // foreach (var gen in registered_generators)
            // {
            //     Logger.Log($"  • {gen.Key}: {gen.Value.GetType().Name}",
            //         level: LogLevel.Debug, name: Ez2ConfigManager.LOGGER_NAME);
            // }
        }

        /// <summary>
        /// 为指定的规则集注册一个命中事件生成器实现。
        /// 通常在生成器初始化时由静态构造函数调用。
        /// </summary>
        /// <param name="rulesetShortName">规则集的短名称（例如 "osu"、"mania"、"taiko"、"catch"）</param>
        /// <param name="generator">实现 <see cref="IScoreHitEventGenerator"/> 的生成器实例</param>
        public static void RegisterImplementation(string rulesetShortName, IScoreHitEventGenerator generator)
        {
            ArgumentNullException.ThrowIfNull(generator);

            registered_generators.TryAdd(rulesetShortName, generator);
        }

        /// <summary>
        /// 为分数生成命中事件列表。
        /// 根据分数的规则集自动选择合适的生成器实现。
        /// </summary>
        /// <param name="databasedScore">要处理的分数</param>
        /// <param name="playableBeatmap">与分数关联的可玩谱面</param>
        /// <param name="cancellationToken">用于停止生成的取消令牌</param>
        /// <returns>生成的命中事件列表，若无法生成则返回 null</returns>
        public static List<HitEvent>? TryGenerate(Score databasedScore, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            string rulesetShortName = databasedScore.ScoreInfo.Ruleset.ShortName;

            if (!registered_generators.TryGetValue(rulesetShortName, out var generator))
            {
                Logger.Log(
                    $"No registered hit event generator for ruleset: {rulesetShortName}",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
                return null;
            }

            try
            {
                // First validate the score's replay data
                if (!generator.Validate(databasedScore))
                {
                    Logger.Log(
                        $"Score validation failed for ruleset: {rulesetShortName}",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);
                    return null;
                }

                // Then generate hit events
                return generator.Generate(databasedScore, playableBeatmap, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.Error(
                    ex,
                    $"Hit event generation failed for ruleset: {rulesetShortName}",
                    Ez2ConfigManager.LOGGER_NAME);
                return null;
            }
        }
    }
}
