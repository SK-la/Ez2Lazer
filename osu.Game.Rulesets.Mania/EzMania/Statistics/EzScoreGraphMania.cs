// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
using osu.Game.Rulesets.Mania.EzMania.Helper;
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
        private readonly ManiaHitWindows maniaHitWindows = new ManiaHitWindows();

        private readonly CustomHitWindowsHelper hitWindowsV1;
        private readonly CustomHitWindowsHelper hitWindowsV2;

        private Bindable<EzEnumHitMode> hitModeBindable = null!;
        private Bindable<double> offsetPlusMania = new Bindable<double>(0);

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzScoreGraphMania(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            maniaHitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);

            // 在此初始化 helper（在基类构造完成并且静态 OD/HP 已设置之后）。
            hitWindowsV1 = new CustomHitWindowsHelper { OverallDifficulty = OD };
            hitWindowsV2 = new CustomHitWindowsHelper { OverallDifficulty = OD };
        }

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
        {
            var events = Score.HitEvents.Where(e => maniaHitWindows.IsHitResultAllowed(e.Result));

            // 如果未设置偏移，则直接返回原始事件以避免分配。
            if (offsetPlusMania.Value == 0)
                return events.ToList();

            // 否则返回一个新的列表，调整 TimeOffset 以使可视化（点云）反映校正结果。
            return events.Select(e => new HitEvent(e.TimeOffset + offsetPlusMania.Value, e.GameplayRate, e.Result, e.HitObject, e.LastHitObject, e.Position)).ToList();
        }

        protected override double UpdateBoundary(HitResult result)
        {
            return maniaHitWindows.WindowFor(result);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 绑定全局打击模式设置，以便切换模式时更新 helper 并重绘。
            hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            hitModeBindable.BindValueChanged(v =>
            {
                hitWindowsV1.HitMode = v.NewValue;
                hitWindowsV2.HitMode = v.NewValue;

                // 确保 mania 判定窗口根据全局配置和难度重新计算。
                maniaHitWindows.ResetRange();
                maniaHitWindows.SetDifficulty(Beatmap.Difficulty.OverallDifficulty);

                // 重新计算并重绘。
                Refresh();
            }, true);

            // 绑定 OffsetPlusMania，以便分析反映运行时校正并在更改时重绘。
            offsetPlusMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            offsetPlusMania.BindValueChanged(_ => Refresh(), true);
        }

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            return hitWindowsV1.ResultFor(hitEvent.TimeOffset);
        }

        protected override HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            return maniaHitWindows.ResultFor(hitEvent.TimeOffset);
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
                makeSimpleStat(scAcc.ToString("F1") + "%", "Acc Orig.", colours.Blue1),
                makeSimpleStat((V2Accuracy * 100).ToString("F1") + "%", "Acc Now", colours.Blue1),
                makeSimpleStat((V1Accuracy * 100).ToString("F1") + "%", "Acc v1", colours.Blue1),

                // Score 使用橙色
                makeSimpleStat((scScore / 1000.0).ToString("F0") + "k", "Scr Orig.", colours.Orange1),
                makeSimpleStat((V2Score / 1000.0).ToString("F0") + "k", "Scr Now", colours.Orange1),
                makeSimpleStat((V1Score / 1000.0).ToString("F0") + "k", "Scr v1", colours.Orange1),

                makeSimpleStat(Score.Pauses.Count.ToString(), "Pauses")
            };

            // 判定计数，使用 OsuColour 提供的判定色彩
            // var judgementOrder = new[] { HitResult.Perfect, HitResult.Great, HitResult.Good, HitResult.Ok, HitResult.Meh, HitResult.Miss };
            var rulesetInstance = Score.Ruleset.CreateInstance();
            List<HitResult> results = rulesetInstance.GetValidHitResults()
                                                     .Where(r => !r.ToString().Contains("Ignore"))
                                                     .OrderBy(r => r.GetIndexForOrderedDisplay())
                                                     .ToList();

            foreach (var r in results)
            {
                string name = r.ToString();
                string display = $"{V2Counts.GetValueOrDefault(r, 0)} | {V1Counts.GetValueOrDefault(r, 0)}";
                var c = colours.ForHitResult(r);
                items.Add(makeSimpleStat(display, name, c));
            }

            // 左侧固定宽度容器，保持纵向单列排列。为判定标签预留一部分右侧宽度以避免重叠。
            const float label_area_width = 48f;

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
