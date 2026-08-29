// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileScoreTrendPanel : CompositeDrawable
    {
        private const string space_graph_name = "Space Graph";

        private readonly Bindable<EzLocalProfileDrillScoreRow?> currentScore;
        private FillFlowContainer contentFlow = null!;
        private CancellationTokenSource? loadCancellation;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private IEzReplaySession replaySession { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public EzLocalProfileScoreTrendPanel(Bindable<EzLocalProfileDrillScoreRow?> currentScore)
        {
            this.currentScore = currentScore;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = contentFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
            };

            currentScore.BindValueChanged(_ => Schedule(reloadTrends), true);
        }

        private void reloadTrends()
        {
            loadCancellation?.Cancel();
            loadCancellation = null;
            contentFlow.Clear();

            var row = currentScore.Value;

            if (row == null)
            {
                contentFlow.Add(createEmptyHint());
                return;
            }

            contentFlow.Add(new LoadingSpinner { RelativeSizeAxes = Axes.X, Height = 80 });

            var localCancellation = loadCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                var detached = realm.Run(r => r.Find<ScoreInfo>(row.ScoreId)?.DeepClone());
                if (detached == null)
                    return null;

                if (detached.HitEvents.Count == 0)
                {
                    var databasedScore = scoreManager.GetScore(detached);

                    if (databasedScore != null)
                    {
                        var workingBeatmap = beatmapManager.GetWorkingBeatmap(detached.BeatmapInfo);
                        var playable = workingBeatmap.GetPlayableBeatmap(detached.Ruleset, detached.Mods);
                        var generated = await replaySession.RunHitEventsAsync(databasedScore, playable, ReplayRunPurpose.ForStored, localCancellation.Token).ConfigureAwait(false);

                        if (generated != null)
                            detached.HitEvents = generated;
                    }
                }

                return detached;
            }, localCancellation.Token).ContinueWith(task => Schedule(() =>
            {
                if (task.IsCanceled)
                    return;

                contentFlow.Clear();

                if (task.IsFaulted)
                {
                    contentFlow.Add(createEmptyHint());
                    return;
                }

                var score = task.GetResultSafely();

                if (score == null || score.HitEvents.Count == 0)
                {
                    contentFlow.Add(createEmptyHint());
                    return;
                }

                var workingBeatmap = beatmapManager.GetWorkingBeatmap(score.BeatmapInfo);
                var playable = workingBeatmap.GetPlayableBeatmap(score.Ruleset, score.Mods);
                var ruleset = score.Ruleset.CreateInstance();

                var spaceGraph = ruleset.CreateStatisticsForScore(score, playable)
                                        .FirstOrDefault(item => item.Name.ToString() == space_graph_name);

                if (spaceGraph != null)
                {
                    contentFlow.Add(spaceGraph.CreateContent());
                    return;
                }

                var timedHitEvents = score.HitEvents.Where(e => e.Result.IsBasic()).ToList();
                contentFlow.Add(new HitEventTimingDistributionGraph(timedHitEvents)
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 150,
                });
            }), localCancellation.Token);
        }

        private static OsuSpriteText createEmptyHint() => new OsuSpriteText
        {
            Text = EzSettingsStrings.LOCAL_PROFILE_TREND_EMPTY,
            Font = OsuFont.GetFont(size: 14),
        };
    }
}
