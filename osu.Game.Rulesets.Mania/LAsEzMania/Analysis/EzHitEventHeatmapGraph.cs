using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Mods;
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
        private readonly ManiaHitWindows hitWindows;
        private readonly ScoreInfo score;

        private double binSize;
        private double drainRate;

        private int currentOffset = 0;
        // private Bindable<int>? offsetBindable;

        private const int time_bins = 50; // 时间分段数
        private const float circle_size = 5f; // 圆形大小
        private float leftMarginConst { get; set; } = 138; // 左侧预留空间
        private float rightMarginConst { get; set; } = 7; // 右侧预留空间

        private double maxTime;
        private double minTime;
        private double timeRange;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        // [Resolved]
        // private OsuConfigManager config { get; set; } = null!;

        public EzHitEventHeatmapGraph(ScoreInfo score, ManiaHitWindows hitWindows)
        {
            this.score = score;
            this.hitEvents = score.HitEvents;
            this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic()).ToList();
            this.hitWindows = hitWindows;

            drainRate = score.BeatmapInfo is not null
                ? score.BeatmapInfo.Difficulty.DrainRate
                : 10.0;
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

        public double TotalMultiplier => hitWindows.SpeedMultiplier / hitWindows.DifficultyMultiplier;

        // private void updateOffset(ValueChangedEvent<int> obj)
        // {
        //     currentOffset = obj.NewValue;
        //     Scheduler.AddOnce(updateDisplay);
        // }

        private (double accuracy, long score) calculateV1Accuracy()
        {
            hitWindows.ClassicModActive = true;
            hitWindows.IsConvert = false;
            hitWindows.ScoreV2Active = false;

            var ruleset = score.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = new[] { new ManiaModClassic() }; // 设置 Classic mod for V1

            // 创建一个简单的 beatmap
            var beatmap = new Beatmap { BeatmapInfo = score.BeatmapInfo };
            foreach (var hitEvent in hitEvents)
                beatmap.HitObjects.Add(hitEvent.HitObject);

            scoreProcessor.ApplyBeatmap(beatmap);

            // 应用所有结果
            foreach (var hitEvent in hitEvents)
            {
                double offset = Math.Abs(hitEvent.TimeOffset);
                HitResult result;
                if (offset <= hitWindows.WindowFor(HitResult.Perfect))
                    result = HitResult.Perfect;
                else if (offset <= hitWindows.WindowFor(HitResult.Great))
                    result = HitResult.Great;
                else if (offset <= hitWindows.WindowFor(HitResult.Good))
                    result = HitResult.Good;
                else if (offset <= hitWindows.WindowFor(HitResult.Ok))
                    result = HitResult.Ok;
                else if (offset <= hitWindows.WindowFor(HitResult.Meh))
                    result = HitResult.Meh;
                else
                    result = HitResult.Miss;

                var judgement = hitEvent.HitObject.CreateJudgement();
                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, judgement) { Type = result });
            }

            // V1 acc 手动计算，不受 mod 影响
            var processor = new ManiaScoreProcessor();
            long totalScoreForAcc = 0;
            double maxScore = hitEvents.Count * 300.0;

            foreach (var hitEvent in hitEvents)
            {
                double offset = Math.Abs(hitEvent.TimeOffset);
                HitResult result;
                if (offset <= hitWindows.WindowFor(HitResult.Perfect))
                    result = HitResult.Perfect;
                else if (offset <= hitWindows.WindowFor(HitResult.Great))
                    result = HitResult.Great;
                else if (offset <= hitWindows.WindowFor(HitResult.Good))
                    result = HitResult.Good;
                else if (offset <= hitWindows.WindowFor(HitResult.Ok))
                    result = HitResult.Ok;
                else if (offset <= hitWindows.WindowFor(HitResult.Meh))
                    result = HitResult.Meh;
                else
                    result = HitResult.Miss;

                totalScoreForAcc += processor.GetBaseScoreForResult(result);
            }

            double accuracy = maxScore > 0 ? totalScoreForAcc / maxScore : 0;

            long totalScore = scoreProcessor.TotalScore.Value;
            return (accuracy, totalScore);
        }

        private (double accuracy, long score) calculateV2Accuracy()
        {
            hitWindows.ClassicModActive = false;
            hitWindows.IsConvert = false;
            hitWindows.ScoreV2Active = true;

            var ruleset = score.Ruleset.CreateInstance();
            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.Mods.Value = score.Mods; // 使用原始 mods for V2

            // 创建一个简单的 beatmap
            var beatmap = new Beatmap { BeatmapInfo = score.BeatmapInfo };
            foreach (var hitEvent in hitEvents)
                beatmap.HitObjects.Add(hitEvent.HitObject);

            scoreProcessor.ApplyBeatmap(beatmap);

            // 应用所有结果
            foreach (var hitEvent in hitEvents)
            {
                double offset = Math.Abs(hitEvent.TimeOffset);
                HitResult result;
                if (offset <= hitWindows.WindowFor(HitResult.Perfect))
                    result = HitResult.Perfect;
                else if (offset <= hitWindows.WindowFor(HitResult.Great))
                    result = HitResult.Great;
                else if (offset <= hitWindows.WindowFor(HitResult.Good))
                    result = HitResult.Good;
                else if (offset <= hitWindows.WindowFor(HitResult.Ok))
                    result = HitResult.Ok;
                else if (offset <= hitWindows.WindowFor(HitResult.Meh))
                    result = HitResult.Meh;
                else
                    result = HitResult.Miss;

                var judgement = hitEvent.HitObject.CreateJudgement();
                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, judgement) { Type = result });
            }

            double accuracy = scoreProcessor.Accuracy.Value;
            long totalScore = scoreProcessor.TotalScore.Value;
            return (accuracy, totalScore);
        }

        private void updateDisplay()
        {
            ClearInternal();

            double scAcc = score.Accuracy * 100;
            var (v1Acc, v1Score) = calculateV1Accuracy();
            var (v2Acc, v2Score) = calculateV2Accuracy();
            long scScore = score.TotalScore;

            // Add overlay text box in top-left corner
            AddInternal(new GridContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = Vector2.Zero,
                Padding = new MarginPadding { Top = 5, Bottom = 5 },
                RowDimensions = new[]
                {
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
                            Text = $" : {scAcc:F2}%",
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
                            Text = $" : {v2Acc * 100:F2}%",
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
                            Text = $" : {v1Acc * 100:F2}%",
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
                float yPosition = (float)(e.TimeOffset + currentOffset);

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
                Y = (float)(boundary + currentOffset),
            });

            AddInternal(new OsuSpriteText
            {
                Text = $"{boundary:+0.##;-0.##}",
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                Font = OsuFont.GetFont(size: 12),
                Colour = Color4.White,
                X = leftMarginConst - 25,
                Y = (float)(boundary + currentOffset),
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
