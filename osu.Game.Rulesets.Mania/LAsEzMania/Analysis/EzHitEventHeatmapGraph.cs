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
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Analysis
{
    /// <summary>
    /// 结算成绩分析。显示命中时间偏移的热力图。带有血量折线和命中结果边界线。
    /// </summary>
    public partial class EzHitEventHeatmapGraph : CompositeDrawable
    {
        private readonly IReadOnlyList<HitEvent> hitEvents;

        // 注意：这是从外部传入的，会跟随 HitMode 切换而变化
        private readonly ManiaHitWindows hitWindows = new ManiaHitWindows();
        private readonly ScoreInfo score;
        private readonly IBeatmap playableBeatmap;

        private readonly double drainRate;
        private readonly double overallDifficulty;

        private const int current_offset = 0;
        private const int time_bins = 50; // 时间分段数
        private const float circle_size = 5f; // 圆形大小
        private float leftMarginConst { get; set; } = 158; // 左侧预留空间
        private float rightMarginConst { get; set; } = 7; // 右侧预留空间

        private double binSize;
        private double maxTime;
        private double minTime;
        private double timeRange;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        // [Resolved]
        // private OsuConfigManager config { get; set; } = null!;

        public EzHitEventHeatmapGraph(ScoreInfo score, IBeatmap playableBeatmap)
        {
            this.score = score;
            hitEvents = score.HitEvents;
            // this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic()).ToList();
            // this.hitWindows = hitWindows;
            this.playableBeatmap = playableBeatmap;

            drainRate = playableBeatmap.Difficulty.DrainRate;
            overallDifficulty = playableBeatmap.Difficulty.OverallDifficulty;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEvents.Count == 0)
                return;

            binSize = Math.Ceiling(hitEvents.Max(e => e.HitObject.StartTime) / time_bins);
            binSize = Math.Max(1, binSize);

            maxTime = hitEvents.Count > 0 ? hitEvents.Max(e => e.HitObject.StartTime) : 1;
            minTime = hitEvents.Count > 0 ? hitEvents.Min(e => e.HitObject.StartTime) : 0;
            timeRange = maxTime - minTime;

            Scheduler.AddOnce(updateDisplay);

            // TODO: 音频偏移功能待实现, 目前下面的代码无法实现，会报错
            // offsetBindable = config.GetBindable<int>(OsuSetting.AudioOffset);
            // currentOffset = offsetBindable.Value;
            //
            // offsetBindable.BindValueChanged(updateOffset);
        }

        private (double accuracy, long score, Dictionary<HitResult, int> counts) calculateV1Accuracy()
        {
            var maniaHitWindows = new ManiaHitWindows
            {
                CustomHitWindows = false,
                ClassicModActive = true,
                IsConvert = false,
                ScoreV2Active = false,
                SpeedMultiplier = hitWindows.SpeedMultiplier,
                DifficultyMultiplier = hitWindows.DifficultyMultiplier
            };

            maniaHitWindows.ResetRange();
            maniaHitWindows.SetDifficulty(overallDifficulty);

            double PerfectRange = maniaHitWindows.WindowFor(HitResult.Perfect);
            double GreatRange = maniaHitWindows.WindowFor(HitResult.Great);
            double GoodRange = maniaHitWindows.WindowFor(HitResult.Good);
            double OkRange = maniaHitWindows.WindowFor(HitResult.Ok);
            double MehRange = maniaHitWindows.WindowFor(HitResult.Meh);
            double MissRange = maniaHitWindows.WindowFor(HitResult.Miss);

            Logger.Log($"[EzHitEventHeatmapGraph] V1 HitWindows: P{PerfectRange} G{GreatRange} Go{GoodRange} O{OkRange} M{MehRange} Mi{MissRange}");

            double[] HeadOffsets = new double[18];
            double MaxPoints = 0;
            double TotalPoints = 0;

            // double TotalMultiplier = hitWindows.SpeedMultiplier / hitWindows.DifficultyMultiplier;
            //
            // double invertedOd = 10 - overallDifficulty;
            //
            // double PerfectRange = Math.Floor(16 * TotalMultiplier) + 0;
            // double GreatRange = Math.Floor((34 + 3 * invertedOd)) * TotalMultiplier + 0;
            // double GoodRange = Math.Floor((67 + 3 * invertedOd)) * TotalMultiplier + 0;
            // double OkRange = Math.Floor((97 + 3 * invertedOd)) * TotalMultiplier + 0;
            // double MehRange = Math.Floor((121 + 3 * invertedOd)) * TotalMultiplier + 0;
            // double MissRange = Math.Floor((158 + 3 * invertedOd)) * TotalMultiplier + 0;

            HitResult getResultByOffset(double offset) =>
                offset < PerfectRange ? HitResult.Perfect :
                offset < GreatRange ? HitResult.Great :
                offset < GoodRange ? HitResult.Good :
                offset < OkRange ? HitResult.Ok :
                offset < MehRange ? HitResult.Meh :
                offset < MissRange ? HitResult.Miss :
                HitResult.None;

            double getLNScore(double head, double tail)
            {
                double combined = head + tail;

                (double range, double headFactor, double combinedFactor, double score)[] rules = new[]
                {
                    (PerfectRange, 1.2, 2.4, 300.0),
                    (GreatRange, 1.1, 2.2, 300),
                    (GoodRange, 1.0, 2.0, 200),
                    (OkRange, 1.0, 2.0, 100),
                    (MehRange, 1.0, 2.0, 50),
                };

                foreach (var (range, headFactor, combinedFactor, lnScore) in rules)
                {
                    if (head < range * headFactor && combined < range * combinedFactor)
                    {
                        return lnScore;
                    }
                }

                return 0;
            }

            Dictionary<HitResult, int> v1Counts = new Dictionary<HitResult, int>();

            foreach (var hit in score.HitEvents.Where(e => e.Result.IsBasic()))
            {
                double offset = Math.Abs(hit.TimeOffset);
                var result = getResultByOffset(offset);

                HitResult hitResult = maniaHitWindows.ResultFor(offset);

                v1Counts[result] = v1Counts.GetValueOrDefault(result, 0) + 1;
                var hitObject = (ManiaHitObject)hit.HitObject;

                if (hitObject is HeadNote)
                {
                    HeadOffsets[hitObject.Column] = offset;
                }
                else if (hitObject is TailNote)
                {
                    MaxPoints += 300;
                    TotalPoints += getLNScore(HeadOffsets[hitObject.Column], offset);
                    HeadOffsets[hitObject.Column] = 0;
                }
                else if (hitObject is Note)
                {
                    MaxPoints += 300;
                    TotalPoints += result switch
                    {
                        HitResult.Perfect => 300,
                        HitResult.Great => 300,
                        HitResult.Good => 200,
                        HitResult.Ok => 100,
                        HitResult.Meh => 50,
                        _ => 0
                    };
                }
            }

            double accuracy = TotalPoints / MaxPoints;

            return (accuracy, (long)TotalPoints, v1Counts);
        }

        private (double accuracy, long score, Dictionary<HitResult, int> counts) calculateV2Accuracy()
        {
            hitWindows.ClassicModActive = false;
            hitWindows.IsConvert = false;
            hitWindows.ScoreV2Active = true;

            var ruleset = score.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = score.Mods;

            // 创建一个简单的 beatmap
            var beatmap = new Beatmap
            {
                BeatmapInfo = score.BeatmapInfo ?? new BeatmapInfo(),
                ControlPointInfo = new ControlPointInfo()
            };
            foreach (var hitEvent in hitEvents)
                beatmap.HitObjects.Add(hitEvent.HitObject);

            scoreProcessor.ApplyBeatmap(beatmap);

            // 应用所有结果
            foreach (var hitEvent in hitEvents)
            {
                var judgement = hitEvent.HitObject.CreateJudgement();
                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, judgement) { Type = hitEvent.Result });
            }

            Dictionary<HitResult, int> v2Counts = hitEvents.GroupBy(e => e.Result).ToDictionary(g => g.Key, g => g.Count());

            double accuracy = scoreProcessor.Accuracy.Value;
            long totalScore = scoreProcessor.TotalScore.Value;
            return (accuracy, totalScore, v2Counts);
        }

        private void updateDisplay()
        {
            ClearInternal();

            double scAcc = score.Accuracy * 100;
            var (v1Acc, v1Score, v1Counts) = calculateV1Accuracy();
            var (v2Acc, v2Score, v2Counts) = calculateV2Accuracy();
            long scScore = score.TotalScore;

            // Add overlay text box in top-left corner
            AddInternal(new GridContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = Vector2.Zero,
                // Padding = new MarginPadding { Top = 5, Bottom = 5 },
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
                            Text = $" : {v2Acc * 100:F1}%",
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
                            Text = $" : {v1Acc * 100:F1}%",
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
                            Text = $" : {v2Score / 1000.0:F0}k",
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
                            Text = $" : {v1Score / 1000.0:F0}k",
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
                            Text = $" : {score.Pauses.Count}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Perfect, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Perfect, 0)}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Great, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Great, 0)}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Good, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Good, 0)}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Ok, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Ok, 0)}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Meh, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Meh, 0)}",
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
                            Text = $" : {v2Counts.GetValueOrDefault(HitResult.Miss, 0)}\\{v1Counts.GetValueOrDefault(HitResult.Miss, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                }
            });

            // 遍历所有有效的 HitResult，绘制边界线
            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>().Where(r => r <= HitResult.Perfect && r >= HitResult.Meh))
            {
                double boundary = hitWindows.WindowFor(result);

                drawBoundaryLine(boundary, result);
                drawBoundaryLine(-boundary, result);
            }

            // 绘制每个 HitEvent 的圆点
            var sortedHitEvents = hitEvents.OrderBy(e => e.HitObject.StartTime).ToList();

            foreach (var e in sortedHitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0; // 计算 x 轴位置
                float yPosition = (float)(e.TimeOffset + current_offset);

                AddInternal(new Circle
                {
                    Size = new Vector2(circle_size),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    X = (xPosition * (DrawWidth - leftMarginConst - rightMarginConst)) - (DrawWidth / 2) + leftMarginConst,
                    Y = yPosition,
                    Alpha = 0.8f,
                    Colour = colours.ForHitResult(e.Result),
                });
            }

            // 计算并绘制血量折线
            drawHealthLine();
        }

        private void drawHealthLine()
        {
            var sortedEvents = hitEvents.OrderBy(e => e.HitObject.StartTime).ToList();
            double currentHealth = 0.0; // 初始血量
            List<Vector2> healthPoints = new List<Vector2>();

            foreach (var e in sortedEvents)
            {
                var judgement = e.HitObject.CreateJudgement();
                var judgementResult = new JudgementResult(e.HitObject, judgement) { Type = e.Result };
                double healthIncrease = judgement.HealthIncreaseFor(judgementResult);
                currentHealth = Math.Clamp(currentHealth + healthIncrease, 0, 1);

                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;
                float x = (xPosition * (DrawWidth - leftMarginConst - rightMarginConst)) - (DrawWidth / 2) + leftMarginConst;
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
            // // 计算当前区间内的 note 数量占比
            // int notesInBoundary = hitEvents.Count(e => e.TimeOffset <= boundary);
            // float noteRatio = (float)notesInBoundary / hitEvents.Count;
            //
            // // 根据 noteRatio 动态调整透明度，noteRatio 越大透明度越低
            // float adjustedAlpha = 0.1f + (1 - noteRatio) * 0.3f; // 最低透明度为 0.2f，最高为 0.5f
            float availableWidth = DrawWidth - leftMarginConst - rightMarginConst + 20;
            float relativeWidth = availableWidth / DrawWidth;
            // 绘制中心轴 (0ms)
            AddInternal(new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Width = relativeWidth,
                // X = centerOffset,
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
                X = leftMarginConst - 25,
                Y = (float)(boundary + current_offset),
            });
        }
    }

    public partial class CreateRotatedColumnGraphs : CompositeDrawable
    {
        private const float horizontal_spacing_ratio = 0.015f;
        private const float top_margin = 20;
        private const float bottom_margin = 10;
        private const float horizontal_margin = 10;

        private readonly List<IGrouping<int, HitEvent>> hitEventsByColumn;

        public CreateRotatedColumnGraphs(List<IGrouping<int, HitEvent>> hitEventsByColumn)
        {
            this.hitEventsByColumn = hitEventsByColumn;
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEventsByColumn.Count == 0)
                return;

            // 创建所有UI元素
            for (int i = 0; i < hitEventsByColumn.Count; i++)
            {
                var column = hitEventsByColumn[i];

                // 添加标题
                AddInternal(new OsuSpriteText
                {
                    Text = $"Column {column.Key + 1}",
                    Font = OsuFont.GetFont(size: 14),
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopCentre
                });

                // 添加图表
                AddInternal(new HitEventTimingDistributionGraph(column.ToList())
                {
                    RelativeSizeAxes = Axes.None,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.BottomLeft,
                    Rotation = 90
                });
            }

            updateLayout();
        }

        protected override void Update()
        {
            base.Update();

            if (InternalChildren.Count == 0 || DrawWidth <= 0)
                return;

            updateLayout();
        }

        private void updateLayout()
        {
            int columnCount = hitEventsByColumn.Count;
            if (columnCount == 0) return;

            float effectiveWidth = DrawWidth - (2 * horizontal_margin);
            float totalSpacingWidth = horizontal_spacing_ratio * (columnCount - 1);
            float columnWidthRatio = (1f - totalSpacingWidth) / columnCount;
            float xPosition = horizontal_margin;

            for (int i = 0; i < columnCount; i++)
            {
                float columnWidth = columnWidthRatio * effectiveWidth;
                float spacingWidth = horizontal_spacing_ratio * effectiveWidth;

                int titleIndex = i * 2;
                int graphIndex = i * 2 + 1;

                // 更新标题位置
                if (titleIndex < InternalChildren.Count && InternalChildren[titleIndex] is OsuSpriteText titleText)
                {
                    titleText.X = xPosition + columnWidth / 2;
                    titleText.Y = 0;
                }

                // 更新图表位置和尺寸
                if (graphIndex < InternalChildren.Count && InternalChildren[graphIndex] is HitEventTimingDistributionGraph graph)
                {
                    graph.Width = DrawHeight - top_margin - bottom_margin;
                    graph.Height = columnWidth;
                    graph.X = xPosition;
                    graph.Y = top_margin;
                }

                xPosition += columnWidth + spacingWidth;
            }
        }
    }
}
