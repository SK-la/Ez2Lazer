// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Ranking.Statistics;
using osu.Framework.Graphics.Colour;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania判定偏移分布图的特定实现，扩展了BaseEzScoreGraph。
    /// 按Mania的判定方式重新过滤、计算了每个HitEvent的结果，并将其与原始结果进行比较，以分析偏移分布和准确性。
    /// 覆写判定区间计算以适应Mania的判定方式，并添加了对Classic模式下LN（长按键）判定的支持。
    /// </summary>
    public partial class EzScoreGraphMania : EzScoreGraphBase
    {
        private readonly ManiaHitWindows hitWindowsV2 = new ManiaHitWindows();
        private readonly HitModeHelper hitWindowsV1 = new HitModeHelper(EzEnumHitMode.Classic);

        private Bindable<EzEnumHitMode> hitModeBindable = null!;
        private EzEnumHitMode currentHitMode;
        private Bindable<EzEnumHealthMode> healthModeBindable = null!;
        private EzEnumHealthMode currentHealthMode;
        private Bindable<double> offsetPlusMania = new Bindable<double>(0);

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzScoreGraphMania(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            hitWindowsV2.SetDifficulty(OD);
            hitWindowsV1.OverallDifficulty = OD;
        }

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(currentHitMode).ToHashSet();
            var filtered = Score.HitEvents.Where(e => validResults.Contains(e.Result));
            return applyFakeOffset(filtered);
        }

        protected override IReadOnlyList<HitEvent> GetV1HitEvents()
        {
            // Classic 路线固定使用原始基础事件，不受当前 hitmode 可见结果集合影响。
            return applyFakeOffset(OriginalHitEvents.Where(e => e.Result.IsBasic()));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            O2HitModeExtension.SetControlPoints(Beatmap.ControlPointInfo);
            O2HitModeExtension.SetOriginalBPM(Beatmap.BeatmapInfo.BPM);

            hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            hitModeBindable.BindValueChanged(v =>
            {
                currentHitMode = v.NewValue;
                // 重新计算并重绘。
                Refresh();
            }, true);

            healthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            healthModeBindable.BindValueChanged(v =>
            {
                currentHealthMode = v.NewValue;
                Refresh();
            }, true);

            // 绑定 OffsetPlusMania，以便分析反映运行时校正并在更改时重绘。
            offsetPlusMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            offsetPlusMania.BindValueChanged(_ => Refresh(), true);
        }

        protected override double UpdateBoundary(HitResult result, double? time = null)
        {
            if (currentHitMode == EzEnumHitMode.O2Jam && time.HasValue)
                hitWindowsV2.UpdateO2JamBpmFromTime(time.Value);

            return hitWindowsV2.WindowFor(result);
        }

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            // Classic 路线应独立于当前 hitmode 的结果标签，仅依据 offset 重算。
            // 对于超窗返回 None 的情况，回落为 Miss（该事件本身已经是一个可判对象）。
            HitResult result = hitWindowsV1.ResultFor(hitEvent.TimeOffset);
            return result == HitResult.None ? HitResult.Miss : result;
        }

        protected override HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            if (currentHitMode == EzEnumHitMode.O2Jam)
                hitWindowsV2.UpdateO2JamBpmFromTime(hitEvent.HitObject.StartTime);

            if (hitEvent.Result is HitResult.Miss or HitResult.Poor)
                return hitEvent.Result;

            return hitWindowsV2.ResultFor(hitEvent.TimeOffset);
        }

        protected override double GetDisplayHealthIncrease(HitEvent hitEvent, HitResult displayResult, double currentHealth)
        {
            if (currentHealthMode == EzEnumHealthMode.Lazer)
                return base.GetDisplayHealthIncrease(hitEvent, displayResult, currentHealth);

            if (currentHealthMode is EzEnumHealthMode.O2JamEasy or EzEnumHealthMode.O2JamNormal or EzEnumHealthMode.O2JamHard)
            {
                if (hitEvent.HitObject is HoldNoteBody)
                    return 0;
            }

            int row = (int)currentHealthMode;
            row = Math.Clamp(row, 0, HealthModeHelper.HEALTH_MODE_MAP.GetLength(0) - 1);

            double increase = displayResult switch
            {
                HitResult.Perfect => HealthModeHelper.HEALTH_MODE_MAP[row, 0],
                HitResult.Great => HealthModeHelper.HEALTH_MODE_MAP[row, 1],
                HitResult.Good => HealthModeHelper.HEALTH_MODE_MAP[row, 2],
                HitResult.Ok => HealthModeHelper.HEALTH_MODE_MAP[row, 3],
                HitResult.Meh => HealthModeHelper.HEALTH_MODE_MAP[row, 4],
                HitResult.Miss => HealthModeHelper.HEALTH_MODE_MAP[row, 5],
                HitResult.Poor => HealthModeHelper.HEALTH_MODE_MAP[row, 6],
                _ => 0
            };

            if (increase < 0 && currentHealth <= 0.5)
            {
                if (currentHealthMode == EzEnumHealthMode.IIDX_HD)
                {
                    if (currentHealth <= 0.3)
                        increase *= 0.5;
                }
                else if (currentHealthMode == EzEnumHealthMode.LR2_HD)
                {
                    if (currentHealth <= 0.3)
                        increase *= 0.6;
                }
                else if (currentHealthMode == EzEnumHealthMode.Raja_HD)
                {
                    if (currentHealth <= 0.3)
                    {
                        increase *= 0.6;
                    }
                    else if (currentHealth < 0.5)
                    {
                        double t = (currentHealth - 0.3) / 0.2;
                        double discount = 0.6 + t * 0.4;
                        increase *= discount;
                    }
                }
            }

            double scaled = Math.Clamp(increase, -0.2, 0.2);
            return Math.Abs(scaled) < 1e-6 ? 0 : scaled;
        }

        private IReadOnlyList<HitEvent> applyFakeOffset(IEnumerable<HitEvent> events)
        {
            var list = events.ToList();

            if (offsetPlusMania.Value == 0)
                return list;

            return list.Select(e => new HitEvent(
                e.TimeOffset + offsetPlusMania.Value,
                e.GameplayRate,
                e.Result,
                e.HitObject,
                e.LastHitObject,
                e.Position)).ToList();
        }

        protected override void UpdateDisplay()
        {
            base.UpdateDisplay();

            if (offsetPlusMania.Value != 0)
            {
                AddInternal(new OsuSpriteText
                {
                    Text = $"Fake Offset Fixing: {offsetPlusMania.Value:+0;-0;0} ms",
                    Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    Colour = Color4.OrangeRed,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                });
            }
        }

        protected override void UpdateText()
        {
            double scAcc = Score.Accuracy * 100;
            long scScore = Score.TotalScore;

            var items = new List<SimpleStatisticItem>
            {
                // Accuracy 使用蓝色，保持统一视觉
                makeSimpleStat(scAcc.ToString("F1") + "%", "Acc Original", colours.Blue1),
                makeSimpleStat((V2Accuracy * 100).ToString("F1") + "%", "Acc Now Setting", colours.Blue1),
                makeSimpleStat((V1Accuracy * 100).ToString("F1") + "%", "Acc v1 Algorithm", colours.Blue1),

                // Score 使用橙色
                makeSimpleStat((scScore / 1000.0).ToString("F0") + "k", "Score Original", colours.Orange1),
                makeSimpleStat((V2Score / 1000.0).ToString("F0") + "k", "Score Now Setting", colours.Orange1),
                makeSimpleStat((V1Score / 1000.0).ToString("F0") + "k", "Score v1 Algorithm", colours.Orange1),

                makeSimpleStat(Score.Pauses.Count.ToString(), "Pauses"),
                makeSimpleStat("Now | V1", "↓", colours.Gray8),
            };

            // 判定计数行使用 V1/V2 的并集，避免当前 hitmode 的可见判定集合把 classic 行“隐藏掉”。
            List<HitResult> results = V2Counts.Keys
                                              .Concat(V1Counts.Keys)
                                              .Distinct()
                                              .Where(r => r.IsBasic() || r == HitResult.Poor)
                                              .OrderBy(r => r.GetIndexForOrderedDisplay())
                                              .ToList();

            foreach (var r in results)
            {
                int v2Count = V2Counts.GetValueOrDefault(r, 0);
                int v1Count = V1Counts.GetValueOrDefault(r, 0);

                // 如果两个值都为0，跳过这个判定的显示
                if (v2Count == 0 && v1Count == 0)
                    continue;

                string name = r.GetHitModeDisplayName().ToString();
                string display = $"{v2Count} | {v1Count}";
                var c = colours.ForHitResult(r);
                items.Add(makeSimpleStat(display, name, c));
            }

            // 左侧固定宽度容器，保持纵向单列排列。为判定标签预留一部分右侧宽度以避免重叠。
            const float label_area_width = 35f;

            var statsContent = new SimpleStatisticTable(1, items.ToArray())
            {
                RelativeSizeAxes = Axes.X,
                Scale = new Vector2(0.96f)
            };

            var contentHolder = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Right = label_area_width },
                Child = statsContent
            };

            var leftContainer = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = Vector2.Zero,
                Width = LeftMarginConst,
                AutoSizeAxes = Axes.Y,
                Child = contentHolder
            };

            // 统计面板稍微低于顶端，以便标签的叠放顺序更可预测。
            leftContainer.Depth = float.MaxValue - 1;
            AddInternal(leftContainer);

            // 在左侧边距右侧创建专用标签区域，用于显示判定边界标签
            var labelArea = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(LeftMarginConst - label_area_width, 0),
                Size = new Vector2(label_area_width, DrawHeight),
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None
            };

            labelArea.Depth = float.MaxValue; // 在统计面板之上
            AddInternal(labelArea);

            // 将该标签区域暴露给基类绘制逻辑使用
            LeftLabelContainer = labelArea;
        }

        private SimpleStatisticItem<string> makeSimpleStat(string display, string name = "Count", ColourInfo? colour = null)
        {
            var item = new SimpleStatisticItem<string>(name) { Value = display };
            item.Colour = colour ?? Color4.White;
            return item;
        }
    }
}
