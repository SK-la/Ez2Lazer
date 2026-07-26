// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Mania 判定偏移分布图。Original 用 Realm 静态 <see cref="ScoreInfo"/>；
    /// Now 经 <see cref="ManiaReplaySession"/>（ForLive）；拖动中使用展示层 fake 平移，
    /// debounce 后的 Session 通过 replay frame 时间偏移得到最终值。
    /// </summary>
    public partial class EzScoreGraphMania : EzScoreGraphBase
    {
        // 帧量化检测后注入的精确 HitEvents（不写回 ScoreInfo）
        private readonly IReadOnlyList<HitEvent>? originalHitEventsOverride;

        private readonly ManiaHitWindows hitWindowsNow = new ManiaHitWindows();
        private readonly HitModeHelper hitWindowsV1 = new HitModeHelper(EzEnumHitMode.Classic);

        private Bindable<EzEnumHitMode> hitModeBindable = null!;
        private Bindable<EzEnumHealthMode> healthModeBindable = null!;
        private Bindable<double> offsetPlusMania = new Bindable<double>();

        private EzEnumHitMode currentHitMode;
        private EzEnumHealthMode currentHealthMode;
        private readonly double originalAccuracy;
        private readonly long originalTotalScore;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        public EzScoreGraphMania(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            hitWindowsNow.SetDifficulty(OD);
            hitWindowsV1.OverallDifficulty = OD;
            originalAccuracy = score.Accuracy;
            originalTotalScore = score.TotalScore;

            // 帧量化检测：若所有 HitEvent 的 TimeOffset 都是帧间隔（约 16.67ms）的整数倍，
            // 说明 HitEvents 已被帧量化污染，需要用 RunHitEventsAsync 重新生成精确版本。
            originalHitEventsOverride = tryDetectAndRegenerateFrameQuantizedEvents(score, beatmap);
        }

        /// <summary>
        /// 检测 HitEvents 是否被帧量化污染，若污染则通过 <see cref="EzReplaySession.RunHitEventsAsync"/>
        /// 重新生成精确 HitEvents（不写回 <c>ScoreInfo</c>）。
        /// </summary>
        private static IReadOnlyList<HitEvent>? tryDetectAndRegenerateFrameQuantizedEvents(ScoreInfo score, IBeatmap beatmap)
        {
            var hitEvents = score.HitEvents;
            if (hitEvents.Count == 0)
                return null;

            const double frame_interval_ms = 16.67;
            const double tolerance = 0.5; // ms

            // 检查是否所有 TimeOffset 都是帧间隔的整数倍
            bool allFrameQuantized = hitEvents.All(e => Math.Abs(e.TimeOffset % frame_interval_ms) < tolerance);

            if (!allFrameQuantized)
                return null;

            // 冗余分支清理：无 Replay 即无法重算
            try
            {
                if (score.Ruleset.OnlineID != 3)
                    return null;

                var scoreForBridge = new Score { ScoreInfo = score };

                if (scoreForBridge.Replay == null || !ManiaReplayFrameEdgeParser.IsManiaReplay(scoreForBridge.Replay))
                    return null;

                var precise = new ManiaReplaySessionService()
                    .RunHitEventsAsync(scoreForBridge, beatmap)
                    .GetAwaiter()
                    .GetResult();

                if (precise is { Count: > 0 })
                    return precise;
            }
            catch
            {
                // 生成失败时静默回退到原始（已量化）事件
            }

            return null;
        }

        /// <summary>
        /// 返回精确 HitEvents（帧量化检测后重新生成）或基类的 OriginalHitEvents。
        /// </summary>
        private IReadOnlyList<HitEvent> getEffectiveOriginalHitEvents() => originalHitEventsOverride ?? OriginalHitEvents;

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
        {
            if (CommittedNowScore?.ScoreInfo.HitEvents is { } sessionEvents)
                return filterForCurrentHitMode(sessionEvents);

            return Array.Empty<HitEvent>();
        }

        private IReadOnlyList<HitEvent> filterForCurrentHitMode(IEnumerable<HitEvent> events)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(currentHitMode).ToHashSet();
            var filtered = events.Where(e => isTimingScatterEvent(e)
                                             && (validResults.Contains(e.Result) || e.Result is HitResult.Miss or HitResult.Poor));

            return applyFakeOffsetToEvents(filtered);
        }

        /// <summary>
        /// 散点图只展示有真实按键时序的物件（Note/Head/Tail）；排除 LN auxiliary（Body/HoldNote）与 Ignore 类结果。
        /// </summary>
        private static bool isTimingScatterEvent(HitEvent hitEvent)
        {
            if (hitEvent.HitObject is HoldNoteBody or HoldNote)
                return false;

            return hitEvent.Result.AffectsAccuracy();
        }

        /// <summary>展示层预览：相对已提交 Session offset 的 delta。</summary>
        private IReadOnlyList<HitEvent> applyFakeOffsetToEvents(IEnumerable<HitEvent> events)
        {
            var list = events.ToList();
            double delta = offsetPlusMania.Value - CommittedSessionOffset;

            if (delta == 0)
                return list;

            return list.Select(e => new HitEvent(
                e.TimeOffset + delta,
                e.GameplayRate,
                e.Result,
                e.HitObject,
                e.LastHitObject,
                e.Position)).ToList();
        }

        protected override void CalculateNowAccuracy()
        {
            var info = CommittedNowScore?.ScoreInfo;

            // Debounce 前的拖动预览仍走 HitEvent 偏移重判；Session 提交后直接使用其完整重算结果。
            if (info == null || offsetPlusMania.Value != CommittedSessionOffset)
            {
                RecalculateNowFromDisplayEvents();
                return;
            }

            NowCounts = extractDisplayCounts(info.Statistics);
            NowAccuracy = info.Accuracy;
            NowScore = info.TotalScore;
            logDiffIfMismatch(info);
        }

        /// <summary>
        /// 按当前 HitMode 的基分权重计算 NowAccuracy 和 NowScore。
        /// 公式：NowAccuracy = Σ(countᵢ × baseScoreᵢ) / (totalCount × maxBaseScore)
        ///       NowScore = NowAccuracy × 1_000_000
        /// </summary>
        private (double accuracy, long score) computeNowAccuracyAndScore(Dictionary<HitResult, int> counts, EzEnumHitMode hitMode)
        {
            int total = 0;
            long totalBase = 0;

            foreach (var (result, count) in counts)
            {
                // 对齐 ScoreProcessor：只累计影响准确度的判定（排除 IgnoreHit/ComboBreak/IgnoreMiss 等）
                if (!result.AffectsAccuracy())
                    continue;

                total += count;
                totalBase += (long)HitModeHelper.GetBaseScoreForResult(hitMode, result) * count;
            }

            if (total == 0)
                return (0, 0);

            int maxBase = HitModeHelper.GetBaseScoreForResult(hitMode, HitResult.Perfect);
            long totalMax = (long)maxBase * total;

            double accuracy = totalMax > 0 ? (double)totalBase / totalMax : 0;
            long score = (long)(accuracy * 1_000_000);
            return (accuracy, score);
        }

        /// <summary>
        /// 诊断日志：对比 Original（现场游玩 Realm 快照）与 Now（Session Simulator 重放）的判定计数差异。
        /// </summary>
        private void logDiffIfMismatch(ScoreInfo sessionInfo)
        {
            // Original: 直接从精确 OriginalHitEvents 按 HitResult 分组统计
            var origCounts = getEffectiveOriginalHitEvents()
                             .GroupBy(e => e.Result)
                             .ToDictionary(g => g.Key, g => g.Count());

            // Now: 直接从 Session 产出的 Statistics 提取（与 NowCounts 同源）
            var nowCounts = extractDisplayCounts(sessionInfo.Statistics);

            // 收集所有出现的 HitResult
            var allResults = origCounts.Keys.Concat(nowCounts.Keys).Distinct().OrderBy(r => r).ToList();

            var diffs = new List<string>();

            foreach (var r in allResults)
            {
                int o = origCounts.GetValueOrDefault(r, 0);
                int n = nowCounts.GetValueOrDefault(r, 0);
                if (o != n)
                    diffs.Add($"{r}: Orig={o} Now={n} (diff={n - o})");
            }

            int origTotal = origCounts.Values.Sum();
            int nowTotal = nowCounts.Values.Sum();

            if (diffs.Count > 0 || origTotal != nowTotal)
            {
                Logger.Log(
                    $"[EzScoreGraphMania DIFF] HitMode={currentHitMode} | "
                    + $"Total: Orig={origTotal} Now={nowTotal} | "
                    + string.Join(" | ", diffs),
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            else
            {
                Logger.Log(
                    $"[EzScoreGraphMania MATCH] HitMode={currentHitMode} Total={origTotal} — all counts identical",
                    Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
        }

        protected override IReadOnlyList<HitEvent> GetV1HitEvents()
        {
            // Classic 路线固定使用原始基础事件，不受当前 HitMode 可见结果集合影响。
            return applyFakeOffsetToEvents(getEffectiveOriginalHitEvents().Where(e => e.Result.IsBasic()));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            O2HitModeExtension.SetControlPoints(Beatmap.ControlPointInfo);
            O2HitModeExtension.SetOriginalBPM(Beatmap.BeatmapInfo.BPM);

            // 初始加载时设置当前值，不触发 Session 重放。
            currentHitMode = ezConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            currentHealthMode = ezConfig.Get<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            hitWindowsNow.SetHitMode(currentHitMode);

            // 非 offset 配置变更：清除 Session 缓存并使用 hitWindowsNow 对
            // OriginalHitEvents.TimeOffset 重判（保留现场游玩的精确 timing）。
            // Session 仅在 offset 拖动时使用（OnOffsetChanged 路径）。
            offsetPlusMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            healthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitModeBindable.BindValueChanged(v =>
            {
                currentHitMode = v.NewValue;
                hitWindowsNow.SetHitMode(currentHitMode);
                // 切换到 O2Jam 时立即同步 BPM，确保后续判定窗口正确
                if (currentHitMode == EzEnumHitMode.O2Jam && Beatmap.HitObjects.Count > 0)
                    hitWindowsNow.UpdateO2JamBpmFromTime(Beatmap.HitObjects[0].StartTime);
                onReplayConfigChanged();
            });

            healthModeBindable.BindValueChanged(__ =>
            {
                currentHealthMode = healthModeBindable.Value;
                onReplayConfigChanged();
            });

            ezConfig.GetBindable<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence)
                    .BindValueChanged(__ => onReplayConfigChanged());

            ezConfig.GetBindable<bool>(Ez2Setting.BmsPoorHitResultEnable)
                    .BindValueChanged(__ => onReplayConfigChanged());

            offsetPlusMania.BindValueChanged(v => OnOffsetChanged(v.NewValue), true);

            Refresh();
        }

        protected override double UpdateBoundary(HitResult result, double? time = null)
        {
            if (currentHitMode == EzEnumHitMode.O2Jam && time.HasValue)
                hitWindowsNow.UpdateO2JamBpmFromTime(time.Value);

            return hitWindowsNow.WindowFor(result);
        }

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            HitResult result = hitWindowsV1.ResultFor(hitEvent.TimeOffset);
            return result == HitResult.None ? HitResult.Miss : result;
        }

        /// <summary>Session 基准判定已产出；fake offset 重判由 <see cref="GetDisplayResult"/> 负责。</summary>
        protected override HitResult RecalculateNowResult(HitEvent hitEvent) => hitEvent.Result;

        /// <summary>
        /// 基于当前 displayOffset 实时重算 Now 统计。
        /// 每次 offset 拖动时由 <see cref="EzScoreGraphBase.RefreshDisplayOnly"/> 调用，
        /// 通过 <see cref="GetDisplayResult"/> 重新判定每个事件的 HitResult。
        /// </summary>
        protected override void RecalculateNowFromDisplayEvents()
        {
            var displayEvents = GetDisplayHitEvents();
            var validResults = HitModeHelper.GetHitModeValidHitResults(currentHitMode).ToHashSet();
            var counts = new Dictionary<HitResult, int>();

            foreach (var e in displayEvents)
            {
                var result = GetDisplayResult(e);
                if (!validResults.Contains(result) && result != HitResult.Miss && result != HitResult.Poor)
                    continue;

                counts.TryAdd(result, 0);
                counts[result]++;
            }

            NowCounts = counts;
            (NowAccuracy, NowScore) = computeNowAccuracyAndScore(counts, currentHitMode);
        }

        /// <summary>
        /// 展示层判定结果：zero offset 直接使用 Session 结果；fake offset 时只对 Session HitEvents 重判。
        /// 不读取或拼贴原始 Score 的 Statistics / HitEvents。
        /// </summary>
        protected override HitResult GetDisplayResult(HitEvent hitEvent)
        {
            if (offsetPlusMania.Value == CommittedSessionOffset)
                return hitEvent.Result;

            if (currentHitMode == EzEnumHitMode.O2Jam)
                hitWindowsNow.UpdateO2JamBpmFromTime(hitEvent.HitObject.StartTime);

            var strategy = ManiaJudgementRegistry.GetHitModeJudgement(currentHitMode)
                           ?? (IManiaNoteJudgementStrategy)LazerNoteJudgementReplica.Instance;

            return strategy.RejudgeHitEvent(hitEvent, hitWindowsNow);
        }

        protected override ReplayRunRequest CreateReplayRunRequest(Score score)
            => new ReplayRunRequest(score, Beatmap, ReplayRunPurpose.ForLive)
            {
                IncludeGlobalManiaOffset = true
            };

        protected override Score? ResolveInputScore() => scoreManager.GetScore(Score);

        private Dictionary<HitResult, int> extractDisplayCounts(IReadOnlyDictionary<HitResult, int> statistics) => ExtractDisplayCounts(statistics, currentHitMode);

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

        /// <summary>
        /// 左侧统计表应展示的判定行（跟随当前 HitMode 有效集合，含 KPoor、ComboBreak）。
        /// </summary>
        internal static IReadOnlyList<HitResult> GetOrderedStatHitResults(EzEnumHitMode hitMode)
            => HitModeHelper.GetHitModeValidHitResults(hitMode)
                            .Where(r => r is not (HitResult.IgnoreHit or HitResult.IgnoreMiss))
                            .OrderBy(r => r.GetIndexForOrderedDisplay())
                            .ToList();

        /// <summary>
        /// HitMode / 重判相关配置变更：清空 Session 缓存，即时 rejudge 展示，并异步重跑 Session。
        /// </summary>
        private void onReplayConfigChanged()
        {
            CommittedNowScore = null;
            CommittedSessionOffset = 0;
            InvalidateTextUi();
            Refresh();
            _ = RefreshFromService();
        }

        /// <summary>Graph 展示层时间轴：Now 为空时回退 Original。</summary>
        internal static double ComputeTimeRangeForTesting(IReadOnlyList<HitEvent> displayEvents, IReadOnlyList<HitEvent> fallbackEvents)
        {
            var eventsForExtent = displayEvents.Count > 0 ? displayEvents : fallbackEvents;

            if (eventsForExtent.Count == 0)
                return 0;

            return eventsForExtent.Max(e => e.HitObject.StartTime) - eventsForExtent.Min(e => e.HitObject.StartTime);
        }

        // TODO: 缺少血量计算工具
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

        protected override void UpdateDisplay()
        {
            base.UpdateDisplay();

            bool show = offsetPlusMania.Value != 0;
            OffsetOverlayText.Text = show
                ? $"Fake Offset Fixing: {offsetPlusMania.Value:+0;-0;0} ms"
                : string.Empty;
            OffsetOverlayText.Alpha = show ? 1 : 0;
        }

        private SimpleStatisticItem<string>[]? statItems; // 缓存固定行（Acc/Score/Pauses）引用
        private readonly Dictionary<HitResult, SimpleStatisticItem<string>> judgementStatItems = new Dictionary<HitResult, SimpleStatisticItem<string>>();

        protected override void CreateTextUI()
        {
            judgementStatItems.Clear();

            // 创建带默认占位值的 item 列表
            var items = new List<SimpleStatisticItem<string>>
            {
                makeSimpleStat("—", "Acc Original", Colours.Blue1),
                makeSimpleStat("—", "Acc Now Setting", Colours.Blue1),
                makeSimpleStat("—", "Acc v1 Algorithm", Colours.Blue1),

                makeSimpleStat("—", "Score Original", Colours.Orange1),
                makeSimpleStat("—", "Score Now Setting", Colours.Orange1),
                makeSimpleStat("—", "Score v1 Algorithm", Colours.Orange1),

                makeSimpleStat(Score.Pauses.Count.ToString(), "Pauses"),
                makeSimpleStat("Now | V1", "↓", Colours.Gray8),
            };

            foreach (var r in GetOrderedStatHitResults(currentHitMode))
            {
                string name = r.GetHitModeDisplayName(currentHitMode).ToString();
                var c = Colours.ForHitResult(r);
                var item = makeSimpleStat("—", name, c);
                judgementStatItems[r] = item;
                items.Add(item);
            }

            statItems = items.ToArray();

            const float label_area_width = 35f;

            var statsContent = new SimpleStatisticTable(1, items)
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
                Child = contentHolder,
                Depth = float.MaxValue - 1
            };
            AddInternal(leftContainer);

            var labelArea = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(LeftMarginConst - label_area_width, 0),
                Size = new Vector2(label_area_width, DrawHeight),
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Depth = float.MaxValue
            };
            AddInternal(labelArea);

            LeftLabelContainer = labelArea;

            // 创建完 UI 后立即填入当前数值
            UpdateTextValues();
        }

        protected override void UpdateTextValues()
        {
            if (statItems == null || statItems.Length == 0)
                return;

            double scAcc = originalAccuracy * 100;
            long scScore = originalTotalScore;

            string nowAccText = (NowAccuracy * 100).ToString("F1") + "%";
            string nowScoreText = (NowScore / 1000.0).ToString("F0") + "k";

            statItems[0].Value = scAcc.ToString("F1") + "%";
            statItems[1].Value = nowAccText;
            statItems[2].Value = (V1Accuracy * 100).ToString("F1") + "%";
            statItems[3].Value = (scScore / 1000.0).ToString("F0") + "k";
            statItems[4].Value = nowScoreText;
            statItems[5].Value = (V1Score / 1000.0).ToString("F0") + "k";
            // statItems[6] = Pauses（pauses 不随 offset 变化，无需更新）
            // statItems[7] = "↓" 分隔线，不需更新

            foreach (var r in GetOrderedStatHitResults(currentHitMode))
            {
                if (!judgementStatItems.TryGetValue(r, out var item))
                    continue;

                int nowCount = NowCounts.GetValueOrDefault(r, 0);
                int v1Count = V1Counts.GetValueOrDefault(r, 0);
                item.Value = $"{nowCount} | {v1Count}";
            }
        }

        private SimpleStatisticItem<string> makeSimpleStat(string display, string name = "Count", ColourInfo? colour = null)
        {
            var item = new SimpleStatisticItem<string>(name)
            {
                Value = display,
                Colour = colour ?? Color4.White
            };
            return item;
        }
    }
}
