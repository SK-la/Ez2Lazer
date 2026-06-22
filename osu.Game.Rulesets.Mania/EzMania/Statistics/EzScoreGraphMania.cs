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
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.EzMania.Scoring;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Replays;
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
    /// Now 经 <see cref="ManiaReplaySession"/>（ForLiveAnalysis，含 live config offset）；
    /// offset 滑条同步控制展示层 fake 平移与 Session 真实 offset。
    /// </summary>
    public partial class EzScoreGraphMania : EzScoreGraphBase
    {
        // TODO(P3-Rest): 删除静态服务实例，改用基类 ReplaySession（由 DI 注入）
        // private static readonly ManiaReplaySessionService replay_session = new ManiaReplaySessionService();

        private readonly ManiaHitWindows hitWindowsNow = new ManiaHitWindows();
        private readonly HitModeHelper hitWindowsV1 = new HitModeHelper(EzEnumHitMode.Classic);

        private Bindable<EzEnumHitMode> hitModeBindable = null!;
        private EzEnumHitMode currentHitMode;
        private Bindable<EzEnumHealthMode> healthModeBindable = null!;
        private EzEnumHealthMode currentHealthMode;
        private Bindable<double> offsetPlusMania = new Bindable<double>(0);

        private readonly double originalAccuracy;
        private readonly long originalTotalScore;

        // 帧量化检测后注入的精确 HitEvents（不写回 ScoreInfo）
        private readonly IReadOnlyList<HitEvent>? originalHitEventsOverride;

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
            hitWindowsNow.SetDifficulty(OD);
            hitWindowsV1.OverallDifficulty = OD;
            originalAccuracy = score.Accuracy;
            originalTotalScore = score.TotalScore;

            // 帧量化检测：若所有 HitEvent 的 TimeOffset 都是帧间隔（约 16.67ms）的整数倍，
            // 说明 HitEvents 已被帧量化污染，需要用 EzScoreReloadBridge 重新生成精确版本。
            originalHitEventsOverride = tryDetectAndRegenerateFrameQuantizedEvents(score, beatmap);
        }

        /// <summary>
        /// 检测 HitEvents 是否被帧量化污染，若污染则通过 <see cref="EzScoreReloadBridge"/>
        /// 重新生成精确 HitEvents（不写回 <c>ScoreInfo</c>）。
        /// </summary>
        private static IReadOnlyList<HitEvent>? tryDetectAndRegenerateFrameQuantizedEvents(ScoreInfo score, IBeatmap beatmap)
        {
            var hitEvents = score.HitEvents;
            if (hitEvents == null || hitEvents.Count == 0)
                return null;

            const double frame_interval_ms = 16.67;
            const double tolerance = 0.5; // ms

            // 检查是否所有 TimeOffset 都是帧间隔的整数倍
            bool allFrameQuantized = hitEvents.All(e => Math.Abs(e.TimeOffset % frame_interval_ms) < tolerance);

            if (!allFrameQuantized)
                return null;

            // 帧量化版本 → 尝试用 EzScoreReloadBridge 重新生成精确事件
            try
            {
                var scoreForBridge = new Score { ScoreInfo = score };
                var precise = EzScoreReloadBridge.TryGenerate(scoreForBridge, beatmap);
                if (precise != null && precise.Count > 0)
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
        private IReadOnlyList<HitEvent> getEffectiveOriginalHitEvents()
            => originalHitEventsOverride ?? OriginalHitEvents;

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
        {
            // Session 产出时使用 Session HitEvents
            if (CommittedNowScore?.ScoreInfo.HitEvents is { } sessionEvents)
                return filterForCurrentHitMode(sessionEvents);

            // 无 Session 时使用全部 OriginalHitEvents，不做结果有效性过滤。
            // GetDisplayResult 会通过 hitWindowsNow 对每个事件重新判定，
            // 确保散点颜色和血量线反映当前 HitMode 的判定结果。
            return applyFakeOffsetToEvents(getEffectiveOriginalHitEvents());
        }

        protected override void CalculateNowAccuracy()
        {
            var info = CommittedNowScore?.ScoreInfo;

            if (info == null)
            {
                // CommittedNowScore 未就绪时：静默保持上一次有效 Now 数据，不走同步 fallback 分支。
                // 异步 RefreshFromService 完成后更新 CommittedNowScore，届时自然触发本方法重算。
                return;
            }

            // Session 重放路径：从 Statistics 提取判定计数（已按当前 HitMode 重新判定）
            NowCounts = extractDisplayCounts(info.Statistics);
            (NowAccuracy, NowScore) = computeNowAccuracyAndScore(NowCounts, currentHitMode);
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
            hitModeBindable = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            hitModeBindable.BindValueChanged(v =>
            {
                currentHitMode = v.NewValue;
                hitWindowsNow.SetHitMode(currentHitMode);
                // 切换到 O2Jam 时立即同步 BPM，确保后续判定窗口正确
                if (currentHitMode == EzEnumHitMode.O2Jam && Beatmap.HitObjects.Count > 0)
                    hitWindowsNow.UpdateO2JamBpmFromTime(Beatmap.HitObjects[0].StartTime);
                // 不清 CommittedNowScore，Now 数据静默保持上一次有效值。
                // 异步 RefreshFromService 完成后自然刷新 Now 数据。
                _ = RefreshFromService();
            });

            healthModeBindable = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            healthModeBindable.BindValueChanged(__ =>
            {
                currentHealthMode = healthModeBindable.Value;
            });

            ezConfig.GetBindable<EzEnumJudgePrecedence>(Ez2Setting.JudgePrecedence)
                    .BindValueChanged(__ =>
                    {
                        // CommittedNowScore 由 RefreshFromService 完成后刷新
                        _ = RefreshFromService();
                    });

            ezConfig.GetBindable<bool>(Ez2Setting.BmsPoorHitResultEnable)
                    .BindValueChanged(__ =>
                    {
                        _ = RefreshFromService();
                    });

            offsetPlusMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
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

        /// <summary>Session 成功时 Now 判定已由 Session 产出。</summary>
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
        /// 展示层判定结果。以下场景通过当前 <see cref="IManiaNoteJudgementStrategy.RejudgeHitEvent"/> 重新判定：
        ///   1. offset 拖动（DisplayOffset != 0）：实时预览散点颜色变化
        ///   2. 无 Session（CommittedNowScore == null）：HitMode 变更后重判
        /// 有 Session 且 offset 归零时直接返回 Session 产出结果。
        /// </summary>
        protected override HitResult GetDisplayResult(HitEvent hitEvent)
        {
            if (DisplayOffset != 0 || CommittedNowScore == null)
            {
                var strategy = ManiaJudgementRegistry.GetHitModeJudgement(currentHitMode)
                               ?? (IManiaNoteJudgementStrategy)LazerNoteJudgementReplica.Instance;

                var result = strategy.RejudgeHitEvent(hitEvent, hitWindowsNow);
                return result == HitResult.None ? HitResult.Miss : result;
            }

            return hitEvent.Result;
        }

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

        private SimpleStatisticItem<string>[]? statItems; // 缓存文本 item 引用，供 UpdateTextValues 只更新数值

        protected override void CreateTextUI()
        {
            double scAcc = originalAccuracy * 100;
            long scScore = originalTotalScore;

            // 创建带默认占位值的 item 列表
            var items = new List<SimpleStatisticItem<string>>
            {
                makeSimpleStat("—", "Acc Original", colours.Blue1),
                makeSimpleStat("—", "Acc Now Setting", colours.Blue1),
                makeSimpleStat("—", "Acc v1 Algorithm", colours.Blue1),

                makeSimpleStat("—", "Score Original", colours.Orange1),
                makeSimpleStat("—", "Score Now Setting", colours.Orange1),
                makeSimpleStat("—", "Score v1 Algorithm", colours.Orange1),

                makeSimpleStat(Score.Pauses.Count.ToString(), "Pauses"),
                makeSimpleStat("Now | V1", "↓", colours.Gray8),
            };

            // 从 Now+V1 判定集合构建每个判定行的 item
            List<HitResult> results = NowCounts.Keys
                                              .Concat(V1Counts.Keys)
                                              .Distinct()
                                              .Where(r => r.IsBasic() || r == HitResult.Poor)
                                              .OrderBy(r => r.GetIndexForOrderedDisplay())
                                              .ToList();

            foreach (var r in results)
            {
                string name = r.GetHitModeDisplayName().ToString();
                var c = colours.ForHitResult(r);
                items.Add(makeSimpleStat("—", name, c));
            }

            // 缓存所有文本 item 引用
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

            int idx = 8; // 静态行之后是动态判定行
            List<HitResult> results = NowCounts.Keys
                                              .Concat(V1Counts.Keys)
                                              .Distinct()
                                              .Where(r => r.IsBasic() || r == HitResult.Poor)
                                              .OrderBy(r => r.GetIndexForOrderedDisplay())
                                              .ToList();

            foreach (var r in results)
            {
                int v2Count = NowCounts.GetValueOrDefault(r, 0);
                int v1Count = V1Counts.GetValueOrDefault(r, 0);

                if (idx < statItems.Length)
                {
                    statItems[idx].Value = $"{v2Count} | {v1Count}";
                    idx++;
                }
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
