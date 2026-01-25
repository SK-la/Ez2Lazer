// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Statistics
{
    /// <summary>
    /// 基类分数图表，用于分析和可视化得分数据。
    /// </summary>
    public abstract partial class BaseEzScoreGraph : CompositeDrawable
    {
        protected readonly ScoreInfo Score;
        protected readonly IBeatmap Beatmap;

        protected HitWindows HitWindows { get; set; }

        protected static double HP;
        protected static double OD;

        protected float LeftMarginConst { get; set; } = 158;
        protected float RightMarginConst { get; set; } = 7;

        private const int current_offset = 0;
        private const int time_bins = 50;

        private double binSize;
        private double maxTime;
        private double minTime;
        private double timeRange;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        protected double V1Accuracy { get; set; }
        protected long V1Score { get; set; }
        protected Dictionary<HitResult, int> V1Counts { get; set; } = new Dictionary<HitResult, int>();

        protected double V2Accuracy { get; set; }
        protected long V2Score { get; set; }
        protected Dictionary<HitResult, int> V2Counts { get; set; } = new Dictionary<HitResult, int>();

        private readonly IReadOnlyList<HitEvent> originalHitEvents;

        protected IReadOnlyList<HitEvent> HitEvents => FilterHitEvents();

        /// <summary>
        /// 继承类应 HitWindows.IsHitResultAllowed 等方式过滤出有效的 HitEvent。
        /// </summary>
        /// <returns>应当返回与当前规则集HitWindows匹配的 HitEvent</returns>
        protected virtual IReadOnlyList<HitEvent> FilterHitEvents()
        {
            return originalHitEvents.Where(e => e.Result.IsBasic()).ToList();
        }

        protected BaseEzScoreGraph(ScoreInfo score, IBeatmap beatmap, HitWindows hitWindows)
        {
            Score = score;
            HitWindows = hitWindows;
            originalHitEvents = score.HitEvents;
            Beatmap = beatmap;

            HP = beatmap.Difficulty.DrainRate;
            OD = beatmap.Difficulty.OverallDifficulty;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (HitEvents.Count == 0)
                return;

            binSize = Math.Ceiling(HitEvents.Max(e => e.HitObject.StartTime) / time_bins);
            binSize = Math.Max(1, binSize);

            maxTime = HitEvents.Count > 0 ? HitEvents.Max(e => e.HitObject.StartTime) : 1;
            minTime = HitEvents.Count > 0 ? HitEvents.Min(e => e.HitObject.StartTime) : 0;
            timeRange = maxTime - minTime;

            Scheduler.AddOnce(UpdateDisplay);
        }

        /// <summary>
        /// Calculate V1 (Classic) accuracy. Subclasses should override CalculateV1ScoresManually instead of this method.
        /// Sets V1Accuracy, V1Score, and V1Counts properties instead of returning values.
        /// </summary>
        protected virtual void CalculateV1Accuracy()
        {
            var v1ScoreProcessor = Score.Ruleset.CreateInstance().CreateScoreProcessor();
            v1ScoreProcessor.IsLegacyScore = true;
            v1ScoreProcessor.Mods.Value = Score.Mods;
            v1ScoreProcessor.ApplyBeatmap(Beatmap);

            var v1Counts = new Dictionary<HitResult, int>();

            foreach (var hitEvent in HitEvents)
            {
                var recalculated = RecalculateV1Result(hitEvent);
                v1Counts[recalculated] = v1Counts.GetValueOrDefault(recalculated, 0) + 1;
                v1ScoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = recalculated,
                    TimeOffset = hitEvent.TimeOffset
                });
            }

            double accuracy = v1ScoreProcessor.AccuracyClassic.Value;
            long totalScore = v1ScoreProcessor.TotalScore.Value;

            Logger.Log($"[V1 ScoreProcessor]: {accuracy * 100:F2}%, Score: {totalScore / 10000}w");

            // Set properties instead of returning
            V1Accuracy = accuracy;
            V1Score = totalScore;
            V1Counts = v1Counts;
        }

        /// <summary>
        /// Recalculate the V1-style HitResult for a given <see cref="HitEvent"/>.
        /// Subclasses may override to provide ruleset-specific V1 judgement logic (e.g. Mania's CustomHitWindowsHelper).
        /// </summary>
        /// <param name="hitEvent">The hit event to recalculate for.</param>
        /// <returns>The recalculated <see cref="HitResult"/> for V1 accuracy.</returns>
        protected virtual HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            return HitWindows.ResultFor(hitEvent.TimeOffset);
        }

        protected virtual HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            return HitWindows.ResultFor(hitEvent.TimeOffset);
        }

        /// <summary>
        /// Calculate V2 accuracy. Subclasses can override to customize calculation.
        /// Sets V2Accuracy, V2Score, and V2Counts properties instead of returning values.
        /// </summary>
        protected virtual void CalculateV2Accuracy()
        {
            // Create a fresh ScoreProcessor for V2 calculation (V1 already used one)
            var v2ScoreProcessor = Score.Ruleset.CreateInstance().CreateScoreProcessor();
            v2ScoreProcessor.Mods.Value = Score.Mods;
            v2ScoreProcessor.ApplyBeatmap(Beatmap);
            var v2Counts = new Dictionary<HitResult, int>();

            foreach (var hitEvent in HitEvents)
            {
                var recalculated = RecalculateV2Result(hitEvent);
                v2Counts[recalculated] = v2Counts.GetValueOrDefault(recalculated, 0) + 1;
                v2ScoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = recalculated,
                    TimeOffset = hitEvent.TimeOffset
                });
            }

            double accuracy = v2ScoreProcessor.Accuracy.Value;
            long totalScore = v2ScoreProcessor.TotalScore.Value;

            Logger.Log($"[V2 ScoreProcessor] Accuracy: {accuracy * 100:F2}%, Score: {totalScore / 10000}w");

            // Set properties instead of returning
            V2Accuracy = accuracy;
            V2Score = totalScore;
            V2Counts = v2Counts;
        }

        protected virtual void UpdateDisplay()
        {
            if (!IsAlive || IsDisposed)
                return;

            if (DrawWidth <= 0 || DrawHeight <= 0)
            {
                Scheduler.AddOnce(UpdateDisplay);
                return;
            }

            ClearInternal();

            CalculateV1Accuracy();
            CalculateV2Accuracy();
            UpdateText();

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>().Where(r => r <= HitResult.Perfect && r >= HitResult.Meh))
            {
                double boundary = UpdateBoundary(result);
                drawBoundaryLine(boundary, result);
                drawBoundaryLine(-boundary, result);
            }

            var sortedHitEvents = HitEvents.OrderBy(e => e.HitObject.StartTime).ToList();

            drawHealthLine(sortedHitEvents);
            drawPointsGraph(sortedHitEvents);
        }

        private void drawPointsGraph(List<HitEvent> sortedHitEvents)
        {
            var pointList = new List<(Vector2 pos, Color4 colour)>();

            foreach (var e in sortedHitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float yPosition = (float)(e.TimeOffset + current_offset);

                float x = (xPosition * (DrawWidth - LeftMarginConst - RightMarginConst)) - (DrawWidth / 2) + LeftMarginConst;
                pointList.Add((new Vector2(x, yPosition), colours.ForHitResult(e.Result)));
            }

            if (pointList.Count > 0)
            {
                var scorePoints = new GirdPoints
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                scorePoints.SetPoints(pointList);
                AddInternal(scorePoints);
            }
        }

        protected virtual void UpdateText()
        {
        }

        /// <summary>
        /// Request the graph to recalculate and redraw. Safe to call from other threads (will schedule on update thread).
        /// </summary>
        protected void Refresh()
        {
            Scheduler.AddOnce(UpdateDisplay);
        }

        protected virtual double UpdateBoundary(HitResult result)
        {
            return HitWindows.WindowFor(result);
        }

        private void drawHealthLine(List<HitEvent> sortedHitEvents)
        {
            double currentHealth = 0.0;
            List<Vector2> healthPoints = new List<Vector2>();

            foreach (var e in sortedHitEvents)
            {
                var judgement = e.HitObject.CreateJudgement();
                var judgementResult = new JudgementResult(e.HitObject, judgement) { Type = e.Result };
                double healthIncrease = judgement.HealthIncreaseFor(judgementResult);
                currentHealth = Math.Clamp(currentHealth + healthIncrease, 0, 1);

                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float x = (xPosition * (DrawWidth - LeftMarginConst - RightMarginConst)) - (DrawWidth / 2) + LeftMarginConst;
                float y = (float)((1 - currentHealth) * DrawHeight - DrawHeight / 2);

                healthPoints.Add(new Vector2(x, y));
            }

            if (healthPoints.Count > 1)
            {
                AddInternal(new Path
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    PathRadius = 1,
                    Colour = Color4.Red,
                    Alpha = 0.3f,
                    Vertices = healthPoints.ToArray()
                });
            }
        }

        private void drawBoundaryLine(double boundary, HitResult result)
        {
            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst + 20;
            float relativeWidth = availableWidth / DrawWidth;

            AddInternal(new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Width = relativeWidth,
                Alpha = 0.1f,
                Colour = Color4.Gray,
            });

            AddInternal(new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Width = relativeWidth,
                Alpha = 0.1f,
                Colour = colours.ForHitResult(result),
                Y = (float)(boundary + current_offset),
            });

            AddInternal(new OsuSpriteText
            {
                Text = $"{boundary:+0.##;-0.##}",
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                Font = OsuFont.GetFont(size: 12),
                Colour = Color4.White,
                X = LeftMarginConst - 25,
                Y = (float)(boundary + current_offset),
            });
        }
    }
}

