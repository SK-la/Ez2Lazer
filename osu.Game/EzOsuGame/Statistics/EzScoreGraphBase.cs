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
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 基类分数图表，用于分析和可视化得分数据。
    /// </summary>
    public abstract partial class EzScoreGraphBase : CompositeDrawable
    {
        protected readonly ScoreInfo Score;
        protected readonly IBeatmap Beatmap;

        protected HitWindows HitWindows { get; set; }

        protected static double HP;
        protected static double OD;

        protected float LeftMarginConst { get; set; } = 165;
        protected float RightMarginConst { get; set; } = 10;

        private const int current_offset = 0;
        private const int time_bins = 50;

        private double binSize;
        private double maxTime;
        private double minTime;
        private double timeRange;

        // 用于绘制主图表的容器（位于左侧统计宽度 LeftMarginConst 的右侧）。
        private Container graphContainer = null!;

        // 左侧预留用于判定区间标签的容器。派生类可在 X = LeftMarginConst - labelAreaWidth 处创建并赋值给它以预留标签区域。
        protected Container? LeftLabelContainer;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        protected double V1Accuracy { get; set; }
        protected long V1Score { get; set; }
        protected Dictionary<HitResult, int> V1Counts { get; set; } = new Dictionary<HitResult, int>();

        protected double V2Accuracy { get; set; }
        protected long V2Score { get; set; }
        protected Dictionary<HitResult, int> V2Counts { get; set; } = new Dictionary<HitResult, int>();

        private readonly IReadOnlyList<HitEvent> originalHitEvents;

        protected IReadOnlyList<HitEvent> HitEvents => GetDisplayHitEvents();
        protected IReadOnlyList<HitEvent> OriginalHitEvents => originalHitEvents;

        /// <summary>
        /// 继承类应 HitWindows.IsHitResultAllowed 等方式过滤出有效的 HitEvent。
        /// </summary>
        /// <returns>应当返回与当前规则集HitWindows匹配的 HitEvent</returns>
        protected virtual IReadOnlyList<HitEvent> FilterHitEvents()
        {
            return originalHitEvents.Where(e => e.Result.IsBasic()).ToList();
        }

        /// <summary>
        /// 当前图表展示使用的事件集合（通常随当前设置变化）。
        /// </summary>
        protected virtual IReadOnlyList<HitEvent> GetDisplayHitEvents() => FilterHitEvents();

        /// <summary>
        /// V1(Classic) 重算使用的事件集合。默认与展示集合一致，派生类可覆写为固定路线。
        /// </summary>
        protected virtual IReadOnlyList<HitEvent> GetV1HitEvents() => GetDisplayHitEvents();

        /// <summary>
        /// V2(Now) 重算使用的事件集合。默认与展示集合一致。
        /// </summary>
        protected virtual IReadOnlyList<HitEvent> GetV2HitEvents() => GetDisplayHitEvents();

        protected EzScoreGraphBase(ScoreInfo score, IBeatmap beatmap, HitWindows hitWindows)
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
            var displayEvents = GetDisplayHitEvents();

            if (displayEvents.Count == 0)
                return;

            binSize = Math.Ceiling(displayEvents.Max(e => e.HitObject.StartTime) / time_bins);
            binSize = Math.Max(1, binSize);

            maxTime = displayEvents.Count > 0 ? displayEvents.Max(e => e.HitObject.StartTime) : 1;
            minTime = displayEvents.Count > 0 ? displayEvents.Min(e => e.HitObject.StartTime) : 0;
            timeRange = maxTime - minTime;

            Scheduler.AddOnce(UpdateDisplay);
        }

        /// <summary>
        /// 计算 V1（Classic）准确率。子类应覆盖 CalculateV1ScoresManually 而不是此方法。
        /// 将结果设置到 V1Accuracy、V1Score 和 V1Counts 属性，而不是通过返回值提供。
        /// </summary>
        protected virtual void CalculateV1Accuracy()
        {
            var v1ScoreProcessor = Score.Ruleset.CreateInstance().CreateScoreProcessor();

            // 必须在 ApplyBeatmap() 前设置 Legacy 标记：
            // ManiaScoreProcessor 会在 ApplyBeatmap() 内缓存 hitMode，
            // 若顺序反了就会把当前 hitmode 当成 V1 路线，导致 classic 结果随 hitmode 改变。
            v1ScoreProcessor.IsLegacyScore = true;
            v1ScoreProcessor.ApplyBeatmap(Beatmap);
            v1ScoreProcessor.Mods.Value = Score.Mods;

            var v1Counts = new Dictionary<HitResult, int>();

            foreach (var hitEvent in GetV1HitEvents())
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
            long totalScore = v1ScoreProcessor.TotalScoreWithoutMods.Value;

            // Logger.Log($"[V1 ScoreProcessor] {accuracy * 100:F2}%, Score: {totalScore / 10000}w", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

            V1Accuracy = accuracy;
            V1Score = totalScore;
            V1Counts = v1Counts;
        }

        /// <summary>
        /// 为给定的 <see cref="HitEvent"/> 重新计算 V1 风格的 HitResult。
        /// 子类可以覆写以提供规则集特定的 V1 判定逻辑（例如 Mania 的 CustomHitWindowsHelper）。
        /// </summary>
        /// <param name="hitEvent">要重新计算的命中事件。</param>
        /// <returns>用于 V1 准确率的重新计算后的 <see cref="HitResult"/>。</returns>
        protected virtual HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            return HitWindows.ResultFor(hitEvent.TimeOffset);
        }

        protected virtual HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            return HitWindows.ResultFor(hitEvent.TimeOffset);
        }

        protected virtual double UpdateBoundary(HitResult result)
        {
            return HitWindows.WindowFor(result);
        }

        /// <summary>
        /// 图表展示阶段用于着色和血量推演的判定结果。
        /// 默认使用当前 V2 重算结果，保证图形与当前设置一致。
        /// </summary>
        protected virtual HitResult GetDisplayResult(HitEvent hitEvent) => RecalculateV2Result(hitEvent);

        /// <summary>
        /// 计算 V2 准确率。子类可覆写以定制计算逻辑。
        /// 将结果设置到 V2Accuracy、V2Score 和 V2Counts 属性，而不是通过返回值提供。
        /// </summary>
        protected virtual void CalculateV2Accuracy()
        {
            var v2ScoreProcessor = Score.Ruleset.CreateInstance().CreateScoreProcessor();
            v2ScoreProcessor.ApplyBeatmap(Beatmap);
            v2ScoreProcessor.Mods.Value = Score.Mods;

            var v2Counts = new Dictionary<HitResult, int>();

            foreach (var hitEvent in GetV2HitEvents())
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

            // Logger.Log($"[V2 ScoreProcessor] {accuracy * 100:F2}%, Score: {totalScore / 10000}w", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);

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

            // 创建主图表专用容器，用于将绘图区域可靠地放在左侧固定宽度统计面板（LeftMarginConst）右侧。
            graphContainer = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(LeftMarginConst, 0),
                Size = new Vector2(DrawWidth - LeftMarginConst - RightMarginConst, DrawHeight),
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Masking = false
            };

            AddInternal(graphContainer);

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>().Where(r => r <= HitResult.Perfect && r >= HitResult.Meh))
            {
                double boundary = UpdateBoundary(result);
                drawBoundaryLine(boundary, result);
                drawBoundaryLine(-boundary, result);
            }

            var sortedHitEvents = GetDisplayHitEvents().OrderBy(e => e.HitObject.StartTime).ToList();

            drawHealthLine(sortedHitEvents);
            drawPointsGraph(sortedHitEvents);
        }

        private void drawPointsGraph(List<HitEvent> sortedHitEvents)
        {
            var pointList = new List<(Vector2 pos, Color4 colour)>();

            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;
            float centerY = DrawHeight / 2f;

            foreach (var e in sortedHitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float yPosition = (float)(e.TimeOffset + current_offset);
                var displayResult = GetDisplayResult(e);

                float x = xPosition * availableWidth;
                float y = centerY + yPosition;

                pointList.Add((new Vector2(x, y), colours.ForHitResult(displayResult)));
            }

            if (pointList.Count > 0)
            {
                var scorePoints = new GirdPoints
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                };

                scorePoints.SetPoints(pointList);
                graphContainer.Add(scorePoints);
            }
        }

        protected virtual void UpdateText()
        {
        }

        /// <summary>
        /// 请求图表重新计算并重绘。可安全从其他线程调用（会安排到更新线程）。
        /// </summary>
        protected void Refresh()
        {
            Scheduler.AddOnce(UpdateDisplay);
        }

        private void drawHealthLine(List<HitEvent> sortedHitEvents)
        {
            double currentHealth = 0.0;
            List<Vector2> healthPoints = new List<Vector2>();

            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;

            foreach (var e in sortedHitEvents)
            {
                var judgement = e.HitObject.CreateJudgement();
                var judgementResult = new JudgementResult(e.HitObject, judgement) { Type = GetDisplayResult(e) };
                double healthIncrease = judgement.HealthIncreaseFor(judgementResult);
                currentHealth = Math.Clamp(currentHealth + healthIncrease, 0, 1);

                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float x = xPosition * availableWidth;
                float y = (float)((1 - currentHealth) * DrawHeight);

                healthPoints.Add(new Vector2(x, y));
            }

            if (healthPoints.Count > 1)
            {
                graphContainer.Add(new Path
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    PathRadius = 1,
                    Colour = Color4.Red,
                    Alpha = 0.3f,
                    Vertices = healthPoints.ToArray()
                });
            }
        }

        private void drawBoundaryLine(double boundary, HitResult result)
        {
            float centerY = DrawHeight / 2f;

            // 背景中心线（表示 0 ms）
            graphContainer.Add(new Box
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Width = 1,
                Alpha = 0.1f,
                Colour = Color4.Gray,
                Y = centerY
            });

            // 有色判定边界线
            graphContainer.Add(new Box
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Width = 1,
                Alpha = 0.1f,
                Colour = colours.ForHitResult(result),
                Y = centerY + (float)(boundary + current_offset)
            });

            // 在左侧标签区域绘制判定标签（优先使用 LeftLabelContainer）
            var label = new OsuSpriteText
            {
                Text = $"{boundary:+0.##;-0.##}",
                Font = OsuFont.GetFont(size: 12),
                Colour = Color4.White,
                Y = centerY + (float)(boundary + current_offset),
            };

            if (LeftLabelContainer != null)
            {
                // 在预留标签区域右侧对齐，并使文字垂直中心与线中心对齐
                label.Anchor = Anchor.TopRight;
                label.Origin = Anchor.CentreRight;
                label.X = -4;
                LeftLabelContainer.Add(label);
            }
            else
            {
                // 没有预留标签区域则回退：在左侧统计区附近绘制，文字垂直居中对齐
                label.Anchor = Anchor.TopLeft;
                label.Origin = Anchor.CentreLeft;
                label.X = LeftMarginConst - 25;
                AddInternal(label);
            }
        }
    }
}
