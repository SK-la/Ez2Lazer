// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Models;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Overlays.SkinEditor
{
    /// <summary>
    /// 组件库预览借用的是目标界面的依赖。结算组件要求分数和覆盖层颜色，玩法界面和选歌界面没有分数。
    /// 缺了就在预览容器上补一份，不写进游戏本体，结果屏自己的缓存仍然优先。
    /// </summary>
    internal static class SkinEditorPreviewDependencies
    {
        public static void ApplyFallbacks(DependencyContainer dependencies, IReadOnlyDependencyContainer source)
        {
            if (!source.TryGet<IBindable<IScoreInfo>>(out _))
                dependencies.CacheAs<IBindable<IScoreInfo>>(createPreviewScore(source));

            if (!source.TryGet<OverlayColourProvider>(out _))
                dependencies.CacheAs(new OverlayColourProvider(OverlayColourScheme.Blue));
        }

        private static Bindable<IScoreInfo> createPreviewScore(IReadOnlyDependencyContainer dependencies)
        {
            RulesetInfo? ruleset = null;

            if (dependencies.TryGet<IBindable<RulesetInfo>>(out var bindable))
                ruleset = bindable.Value;

            if (ruleset == null && dependencies.TryGet<IRulesetStore>(out var store))
                ruleset = store.AvailableRulesets.OfType<RulesetInfo>().FirstOrDefault();

            RulesetInfo rulesetInfo;

            if (ruleset != null)
            {
                rulesetInfo = ruleset.Clone();
                rulesetInfo.Available = true;
            }
            else
            {
                rulesetInfo = new RulesetInfo
                {
                    Name = @"osu!",
                    ShortName = @"osu",
                    OnlineID = 0,
                    InstantiationInfo = @"osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu",
                    Available = true,
                };
            }

            var beatmap = new BeatmapInfo(rulesetInfo, metadata: new BeatmapMetadata(new RealmUser { Username = @"preview" })
            {
                Title = @"Preview",
                Artist = @"Preview",
            })
            {
                DifficultyName = @"Preview",
                StarRating = 2,
            };

            return new Bindable<IScoreInfo>(new ScoreInfo(beatmap, rulesetInfo, new RealmUser { Username = @"preview" })
            {
                Rank = ScoreRank.S,
                Accuracy = 0.95,
                MaxCombo = 100,
                TotalScore = 1_000_000,
                Date = DateTimeOffset.Now,
            });
        }

        /// <summary>
        /// 组件库会实例化每一种可序列化组件。目标界面没有对应依赖时，加载会抛错；这表示放不进去，不是编辑器故障。
        /// </summary>
        public static bool IsUnplaceable(Exception exception)
        {
            switch (exception)
            {
                case DependencyNotRegisteredException:
                case RulesetLoadException:
                    return true;

                case AggregateException aggregate:
                    return aggregate.InnerExceptions.All(IsUnplaceable);

                default:
                    return exception.InnerException != null && IsUnplaceable(exception.InnerException);
            }
        }
    }
}
