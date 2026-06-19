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
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Ranking.Statistics;
using osu.Framework.Graphics.Colour;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania 判定偏移分布图。Original 用 Realm 静态 <see cref="ScoreInfo"/>；
    /// Now 经 <see cref="ManiaReplaySession"/>（ForLiveAnalysis，含 live config offset）；
    /// offset 滑条同步控制展示层 fake 平移与 Session 真实 offset。
    /// </summary>
    public partial class EzScoreGraphMania : EzScoreGraphBase
    {
        // TODO(P3-Rest): 删除静态服务实例，改用基类 ReplaySession（由 DI 注入）
        // private static readonly ManiaReplaySessionService replay_session = new ManiaReplaySessionService();

        private readonly ManiaHitWindows hitWindowsV2 = new ManiaHitWindows();
        private readonly HitModeHelper hitWindowsV1 = new HitModeHelper(EzEnumHitMode.Classic);

        private Bindable<EzEnumHitMode> hitModeBindable = null!;
        private EzEnumHitMode currentHitMode;
        private Bindable<EzEnumHealthMode> healthModeBindable = null!;
        private EzEnumHealthMode currentHealthMode;
        private Bindable<double> offsetPlusMania = new Bindable<double>(0);

        private readonly double originalAccuracy;
        private readonly long originalTotalScore;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        // TODO(P3-Rest): resolveSessionInputScore() 应移除，改为通过 IEzReplaySession.RunRequestAsync(ForLiveAnalysis)
        // 当前临时方案：保留 scoreManager 用于获取 databased score
        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzScoreGraphMania(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            hitWindowsV2.SetDifficulty(OD);
            hitWindowsV1.OverallDifficulty = OD;
            originalAccuracy = score.Accuracy;
            originalTotalScore = score.TotalScore;
        }

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
            => filterForCurrentHitMode(
                CommittedNowScore?.ScoreInfo.HitEvents ?? OriginalHitEvents);

        protected override void CalculateV2Accuracy()
        {
            var info = CommittedNowScore?.ScoreInfo;

            if (info != null)
            {
                V2Accuracy = info.Accuracy;
                V2Score = info.TotalScore;
                V2Counts = extractDisplayCounts(info.Statistics);
                return;
            }

            V2Accuracy = 0;
            V2Score = 0;
            V2Counts = new Dictionary<HitResult, int>();
        }

        protected override IReadOnlyList<HitEvent> GetV1HitEvents()
        {
            // Classic 路线固定使用原始基础事件，不受当前 HitMode 可见结果集合影响。
            return applyFakeOffsetToEvents(OriginalHitEvents.Where(e => e.Result.IsBasic()));
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
                hitWindowsV2.SetHitMode(currentHitMode);
                Schedule(() => RefreshFromService().ConfigureAwait(false));
            }, true);

            healthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            healthModeBindable.BindValueChanged(__ =>
            {
                currentHealthMode = healthModeBindable.Value;
                Schedule(() => RefreshFromService().ConfigureAwait(false));
            }, true);

            ezConfig.GetBindable<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence)
                    .BindValueChanged(__ => Schedule(() => RefreshFromService().ConfigureAwait(false)), true);

            ezConfig.GetBindable<bool>(Ez2Setting.BmsPoorHitResultEnable)
                    .BindValueChanged(__ => Schedule(() => RefreshFromService().ConfigureAwait(false)), true);

            offsetPlusMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            offsetPlusMania.BindValueChanged(v => OnOffsetChanged(v.NewValue), true);

            Refresh();
        }

        protected override double UpdateBoundary(HitResult result, double? time = null)
        {
            if (currentHitMode == EzEnumHitMode.O2Jam && time.HasValue)
                hitWindowsV2.UpdateO2JamBpmFromTime(time.Value);

            return hitWindowsV2.WindowFor(result);
        }

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            HitResult result = hitWindowsV1.ResultFor(hitEvent.TimeOffset);
            return result == HitResult.None ? HitResult.Miss : result;
        }

        /// <summary>Session 成功时 Now 判定已由 Session 产出。</summary>
        protected override HitResult RecalculateV2Result(HitEvent hitEvent) => hitEvent.Result;

        /// <summary>
        /// 无 committed score 时，基于当前 displayOffset 实时重算 V2 统计。
        /// 用于 offset 拖动时左侧 Now 数据的实时预览。
        /// </summary>
        protected override void RecalculateV2FromDisplayEvents()
        {
            // 有 committed score 时，CalculateV2Accuracy 已填好，无需重复
            if (CommittedNowScore != null)
                return;

            // 基于 FilterHitEvents（含 displayOffset）重算 counts
            var displayEvents = GetDisplayHitEvents();
            V2Counts = extractDisplayCountsFromEvents(displayEvents);

            // 重算准确率（基于 counts）
            int total = V2Counts.Values.Sum();
            if (total > 0)
            {
                int goods = V2Counts.GetValueOrDefault(HitResult.Good, 0)
                           + V2Counts.GetValueOrDefault(HitResult.Great, 0)
                           + V2Counts.GetValueOrDefault(HitResult.Perfect, 0);
                int meh = V2Counts.GetValueOrDefault(HitResult.Meh, 0);
                V2Accuracy = (goods + meh * 0.5) / total;
                V2Score = (long)(originalTotalScore * V2Accuracy / originalAccuracy);
            }
            else
            {
                V2Accuracy = 0;
                V2Score = 0;
            }
        }

        /// <summary>
        /// 从 HitEvent 列表提取统计 counts（不受 committed score 影响）。
        /// </summary>
        private Dictionary<HitResult, int> extractDisplayCountsFromEvents(IEnumerable<HitEvent> events)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(currentHitMode).ToHashSet();
            var counts = new Dictionary<HitResult, int>();

            foreach (var e in events)
            {
                var result = e.Result;
                if (!validResults.Contains(result) && result != HitResult.Miss && result != HitResult.Poor)
                    continue;

                if (!counts.ContainsKey(result))
                    counts[result] = 0;
                counts[result]++;
            }

            return counts;
        }

        protected override HitResult GetDisplayResult(HitEvent hitEvent) => hitEvent.Result;

        // P3-Rest 前过渡：通过 scoreManager 获取 databased score
        // TODO(P3-Rest): resolveSessionInputScore() 移除，改为通过 IEzReplaySession.RunRequestAsync(ForLiveAnalysis)
        protected override Score? ResolveInputScore() => resolveSessionInputScore();

        // P3-Rest 前过渡：返回 ForLiveAnalysis 环境（读 live config offset）
        // TODO(P3-Rest): createSessionEnvironment() 移除，改为通过 IEzReplaySession.RunRequestAsync(ForLiveAnalysis)
        protected override IGameplayEnvironment? CreateLiveAnalysisEnvironment() => createSessionEnvironment();

        private ManiaGameplayEnvironment createSessionEnvironment()
            => ManiaRuleset.ResolveEnvironment(null, ezConfig, ReplayRunPurpose.ForLiveAnalysis);

        // TODO(P3-Rest): resolveSessionInputScore() 移除，改为通过 IEzReplaySession.RunRequestAsync(ForLiveAnalysis)
        private Score? resolveSessionInputScore()
        {
            var databased = scoreManager.GetScore(Score);
            return databased != null && hasValidReplay(databased) ? databased : null;
        }

        private static bool hasValidReplay(Score score)
        {
            var replay = score.Replay;

            return replay != null
                   && replay.Frames.Count > 0
                   && replay.Frames.All(f => f is ManiaReplayFrame);
        }

        private Dictionary<HitResult, int> extractDisplayCounts(IReadOnlyDictionary<HitResult, int> statistics)
            => ExtractDisplayCounts(statistics, currentHitMode);

        internal static Dictionary<HitResult, int> ExtractDisplayCounts(IReadOnlyDictionary<HitResult, int> statistics, EzEnumHitMode hitMode)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(hitMode).ToHashSet();
            var counts = new Dictionary<HitResult, int>();

            foreach (var (result, count) in statistics)
            {
                if (count == 0)
                    continue;

                if (!validResults.Contains(result) && result is not (HitResult.Miss or HitResult.Poor))
                    continue;

                counts[result] = count;
            }

            return counts;
        }

        /// <summary>Graph 展示层时间轴：Now 为空时回退 Original。</summary>
        internal static double ComputeTimeRangeForTesting(IReadOnlyList<HitEvent> displayEvents, IReadOnlyList<HitEvent> fallbackEvents)
        {
            var eventsForExtent = displayEvents.Count > 0 ? displayEvents : fallbackEvents;

            if (eventsForExtent.Count == 0)
                return 0;

            return eventsForExtent.Max(e => e.HitObject.StartTime) - eventsForExtent.Min(e => e.HitObject.StartTime);
        }

        private IReadOnlyList<HitEvent> filterForCurrentHitMode(IEnumerable<HitEvent> events)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(currentHitMode).ToHashSet();
            var filtered = events.Where(e =>
                validResults.Contains(e.Result) || e.Result is HitResult.Miss or HitResult.Poor);

            return applyFakeOffsetToEvents(filtered);
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

        /// <summary>展示层叠加 OffsetPlusMania；Session 统计用 offset=0 环境。</summary>
        private IReadOnlyList<HitEvent> applyFakeOffsetToEvents(IEnumerable<HitEvent> events)
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
            double scAcc = originalAccuracy * 100;
            long scScore = originalTotalScore;

            string nowAccText = CommittedNowScore != null ? (V2Accuracy * 100).ToString("F1") + "%" : "—";
            string nowScoreText = CommittedNowScore != null ? (V2Score / 1000.0).ToString("F0") + "k" : "—";

            var items = new List<SimpleStatisticItem>
            {
                makeSimpleStat(scAcc.ToString("F1") + "%", "Acc Original", colours.Blue1),
                makeSimpleStat(nowAccText, "Acc Now Setting", colours.Blue1),
                makeSimpleStat((V1Accuracy * 100).ToString("F1") + "%", "Acc v1 Algorithm", colours.Blue1),

                makeSimpleStat((scScore / 1000.0).ToString("F0") + "k", "Score Original", colours.Orange1),
                makeSimpleStat(nowScoreText, "Score Now Setting", colours.Orange1),
                makeSimpleStat((V1Score / 1000.0).ToString("F0") + "k", "Score v1 Algorithm", colours.Orange1),

                makeSimpleStat(Score.Pauses.Count.ToString(), "Pauses"),
                makeSimpleStat("Now | V1", "↓", colours.Gray8),
            };

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

                if (v2Count == 0 && v1Count == 0)
                    continue;

                string name = r.GetHitModeDisplayName().ToString();
                string display = $"{v2Count} | {v1Count}";
                var c = colours.ForHitResult(r);
                items.Add(makeSimpleStat(display, name, c));
            }

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

            leftContainer.Depth = float.MaxValue - 1;
            AddInternal(leftContainer);

            var labelArea = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(LeftMarginConst - label_area_width, 0),
                Size = new Vector2(label_area_width, DrawHeight),
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None
            };

            labelArea.Depth = float.MaxValue;
            AddInternal(labelArea);

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
