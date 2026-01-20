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
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// 基类分数图表，用于分析和可视化得分数据。
    /// 继承类应实现特定规则集的逻辑。
    /// </summary>
    public abstract partial class BaseEzScoreGraph : CompositeDrawable
    {
        protected readonly IReadOnlyList<HitEvent> HitEvents;
        protected readonly ScoreInfo Score;
        protected readonly IBeatmap Beatmap;

        protected ScoreProcessor ScoreProcessor { get; set; }

        protected HitWindows HitWindows { get; set; }

        protected virtual HitResult GetHitResult()
        {
            return HitResult.Perfect;
        }

        protected static double HP;
        protected static double OD;

        private const int current_offset = 0;
        private const int time_bins = 50;
        private const float circle_size = 2f;

        private double binSize;
        private double maxTime;
        private double minTime;
        private double timeRange;

        protected float LeftMarginConst { get; set; } = 158;
        protected float RightMarginConst { get; set; } = 7;

        protected double V1Accuracy { get; set; }
        protected long V1Score { get; set; }
        protected Dictionary<HitResult, int> V1Counts { get; set; } = new Dictionary<HitResult, int>();

        // V2 Accuracy calculation properties
        protected double V2Accuracy { get; set; }
        protected long V2Score { get; set; }
        protected Dictionary<HitResult, int> V2Counts { get; set; } = new Dictionary<HitResult, int>();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        protected BaseEzScoreGraph(ScoreInfo score, IBeatmap beatmap, HitWindows hitWindows)
        {
            Score = score;
            HitWindows = hitWindows;
            HitEvents = score.HitEvents;
            Beatmap = beatmap;

            HP = beatmap.Difficulty.DrainRate;
            // OD = beatmap.Difficulty.OverallDifficulty;

            ScoreProcessor = Score.Ruleset.CreateInstance().CreateScoreProcessor();
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

            Scheduler.AddOnce(updateDisplay);
        }

        /// <summary>
        /// Calculate V1 (Classic) accuracy. Subclasses should override CalculateV1ScoresManually instead of this method.
        /// Sets V1Accuracy, V1Score, and V1Counts properties instead of returning values.
        /// </summary>
        protected virtual void CalculateV1Accuracy()
        {
            ScoreProcessor.Mods.Value = Score.Mods;
            ScoreProcessor.ApplyBeatmap(Beatmap);

            var v1Counts = new Dictionary<HitResult, int>();

            foreach (var hitEvent in GetApplicableHitEvents())
            {
                var recalculated = RecalculateV1Result(hitEvent);

                // track recalculated counts for V1
                v1Counts[recalculated] = v1Counts.GetValueOrDefault(recalculated, 0) + 1;

                ScoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = recalculated,
                    TimeOffset = hitEvent.TimeOffset
                });
            }

            double accuracy = ScoreProcessor.AccuracyClassic.Value;
            long totalScore = ScoreProcessor.TotalScore.Value;

            Logger.Log($"[V1 ScoreProcessor]: {accuracy * 100:F2}%, Score: {totalScore / 10000}w");

            // Set properties instead of returning
            V1Accuracy = accuracy;
            V1Score = totalScore;
            V1Counts = v1Counts;
        }

        protected virtual IEnumerable<HitEvent> GetApplicableHitEvents()
        {
            return Score.HitEvents.Where(e => e.Result.IsBasic());
        }

        /// <summary>
        /// Recalculate the V1-style HitResult for a given <see cref="HitEvent"/>.
        /// Subclasses may override to provide ruleset-specific V1 judgement logic (e.g. Mania's CustomHitWindowsHelper).
        /// </summary>
        /// <param name="hitEvent">The hit event to recalculate for.</param>
        /// <returns>The recalculated <see cref="HitResult"/> for V1 accuracy.</returns>
        protected virtual HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            double offset = Math.Abs(hitEvent.TimeOffset);
            return HitWindows.ResultFor(offset);
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

            foreach (var hitEvent in GetApplicableHitEvents())
            {
                var judgement = hitEvent.HitObject.CreateJudgement();
                v2ScoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, judgement)
                {
                    Type = hitEvent.Result,
                    TimeOffset = hitEvent.TimeOffset
                });
            }

            Dictionary<HitResult, int> v2Counts = HitEvents.GroupBy(e => e.Result).ToDictionary(g => g.Key, g => g.Count());

            double accuracy = v2ScoreProcessor.Accuracy.Value;
            long totalScore = v2ScoreProcessor.TotalScore.Value;

            Logger.Log($"[V2 ScoreProcessor] Accuracy: {accuracy * 100:F2}%, Score: {totalScore / 10000}w");

            // Set properties instead of returning
            V2Accuracy = accuracy;
            V2Score = totalScore;
            V2Counts = v2Counts;
        }

        private void updateDisplay()
        {
            ClearInternal();

            double scAcc = Score.Accuracy * 100;
            long scScore = Score.TotalScore;

            CalculateV1Accuracy();
            CalculateV2Accuracy();

            AddInternal(new GridContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = Vector2.Zero,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc org",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {scAcc:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc v2",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Accuracy * 100:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc v1",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V1Accuracy * 100:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr org",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {scScore / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr v2",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Score / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr v1",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V1Score / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Pauses",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {Score.Pauses.Count}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "PERFECT",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Perfect, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Perfect, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "GREAT",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Great, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Great, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "GOOD",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Good, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Good, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "OK",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Ok, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Ok, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "MEH",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Meh, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Meh, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "MISS",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Miss, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Miss, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                }
            });

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>().Where(r => r <= HitResult.Perfect && r >= HitResult.Meh))
            {
                double boundary = UpdateBoundary(result);
                drawBoundaryLine(boundary, result);
                drawBoundaryLine(-boundary, result);
            }

            var sortedHitEvents = GetApplicableHitEvents().OrderBy(e => e.HitObject.StartTime).ToList();

            var pointList = new List<(Vector2 pos, Color4 colour)>();

            foreach (var e in sortedHitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float yPosition = (float)(e.TimeOffset + current_offset);

                var x = (xPosition * (DrawWidth - LeftMarginConst - RightMarginConst)) - (DrawWidth / 2) + LeftMarginConst;
                pointList.Add((new Vector2(x, yPosition), colours.ForHitResult(e.Result)));
            }

            if (pointList.Count > 0)
            {
                var scorePoints = new GirdPoints(circle_size)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                scorePoints.SetPoints(pointList);
                AddInternal(scorePoints);
            }

            drawHealthLine(sortedHitEvents);
        }

        protected virtual double UpdateBoundary(HitResult result)
        {
            return HitWindows.WindowFor(result);
        }

        private void drawHealthLine(IReadOnlyList<HitEvent> sortedEvents)
        {
            double currentHealth = 0.0;
            List<Vector2> healthPoints = new List<Vector2>();

            foreach (var e in sortedEvents)
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

        protected abstract HitResult RecalculateV2Result(HitEvent hitEvent);
    }
}

