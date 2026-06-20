// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
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

        private const int time_bins = 50;

        private double binSize;
        private double maxTime;
        private double minTime;
        private double timeRange;

        // 用于绘制主图表的容器（位于左侧统计宽度 LeftMarginConst 的右侧）。
        private Container graphContainer = null!;

        // 边界线和标签的专用容器（整个生命周期保持不变，offset 拖动时不受影响）。
        private Container boundaryContainer = null!;

        // 左侧预留用于判定区间标签的容器。派生类可在 X = LeftMarginConst - labelAreaWidth 处创建并赋值给它以预留标签区域。
        protected Container? LeftLabelContainer;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        // 通过 EzReplaySessionRegistry 访问，Generator 静态初始化后自动可用
        // TODO(P3-Rest): Registry 注册后 Graph/Panel/Race 可用；P3-Rest 阶段各 Ruleset 应通过 DI 注入替代
        protected IEzReplaySession? ReplaySession => EzReplaySessionRegistry.Instance;

        // Graph-UX: 双轨刷新状态管理
        // TODO(P3-Rest): CommittedNowScore/committedEnvironment 应由 IEzReplaySession.RunRequestAsync(ForLiveAnalysis) 填充
        // displayOffset 由 RefreshDisplayOnly 管理（拖动时），RefreshFromService 成功后重置为 0
        protected Score? CommittedNowScore;
#pragma warning disable IDE0051 // committedEnvironment 为 P3-Rest 阶段预留，当前暂未使用
        private IGameplayEnvironment? committedEnvironment;
#pragma warning restore IDE0051
        protected double DisplayOffset;

        // Debounce 控制 — offset 拖动时仅展示，落定后触发 Session
        private CancellationTokenSource? debounceCancellation;
        private const int debounce_ms = 300; // offset 落定延迟

        protected double V1Accuracy { get; set; }
        protected long V1Score { get; set; }
        protected Dictionary<HitResult, int> V1Counts { get; set; } = new Dictionary<HitResult, int>();

        protected double V2Accuracy { get; set; }
        protected long V2Score { get; set; }
        protected Dictionary<HitResult, int> V2Counts { get; set; } = new Dictionary<HitResult, int>();

        protected IReadOnlyList<HitEvent> HitEvents => GetDisplayHitEvents();
        protected IReadOnlyList<HitEvent> OriginalHitEvents { get; }

        /// <summary>
        /// 继承类应 HitWindows.IsHitResultAllowed 等方式过滤出有效的 HitEvent。
        /// </summary>
        /// <returns>应当返回与当前规则集HitWindows匹配的 HitEvent</returns>
        protected virtual IReadOnlyList<HitEvent> FilterHitEvents()
        {
            return OriginalHitEvents.Where(e => e.Result.IsBasic()).ToList();
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
            OriginalHitEvents = score.HitEvents.ToList();
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
            // 若顺序反了就会把当前 HitMode 当成 V1 路线，导致 classic 结果随 HitMode 改变。
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

        protected virtual double UpdateBoundary(HitResult result, double? time = null)
        {
            return HitWindows.WindowFor(result);
        }

        /// <summary>
        /// 图表展示阶段用于着色和血量推演的判定结果。
        /// 默认使用当前 V2 重算结果，保证图形与当前设置一致。
        /// </summary>
        protected virtual HitResult GetDisplayResult(HitEvent hitEvent) => RecalculateV2Result(hitEvent);

        /// <summary>
        /// 从当前 displayOffset 重新计算 V2 统计（用于 offset 拖动时的实时预览）。
        /// 子类可覆盖以提供规则集特定的统计逻辑。
        /// </summary>
        protected virtual void RecalculateV2FromDisplayEvents()
        {
            // 默认实现：子类（Mania）会 override
        }

        /// <summary>
        /// 图表血量起始值。默认按游戏内从满血开始。
        /// </summary>
        protected virtual double GetInitialHealth() => 1.0;

        /// <summary>
        /// 图表血量推演使用的单次血量变化。
        /// 默认使用 Judgement 的血量变化逻辑，规则集可覆写以对齐自定义血量模式。
        /// </summary>
        protected virtual double GetDisplayHealthIncrease(HitEvent hitEvent, HitResult displayResult, double currentHealth)
        {
            var judgement = hitEvent.HitObject.CreateJudgement();
            var judgementResult = new JudgementResult(hitEvent.HitObject, judgement) { Type = displayResult };
            return judgement.HealthIncreaseFor(judgementResult);
        }

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
            textInitialized = false; // 全量刷新后标记文本未初始化，下次 UpdateText 会重建 UI

            CalculateV1Accuracy();
            CalculateV2Accuracy();
            updateTimeExtentsFromDisplayEvents();
            UpdateText();

            // 创建边界线专用容器（仅含边界线和标签），在整个生命周期内保持不变
            boundaryContainer = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(LeftMarginConst, 0),
                Size = new Vector2(DrawWidth - LeftMarginConst - RightMarginConst, DrawHeight),
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Masking = false
            };
            AddInternal(boundaryContainer);

            // 背景中心线（表示 0 ms）
            float centerY = projectOffsetToY(0, minTime);
            boundaryContainer.Add(new Box
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

            foreach (HitResult result in Enum.GetValues(typeof(HitResult)).Cast<HitResult>()
                                             .Where(r => r <= HitResult.Perfect && r >= HitResult.Meh && HitWindows.IsHitResultAllowed(r)))
            {
                drawBoundaryLine(result, isNegative: false);
                drawBoundaryLine(result, isNegative: true);
            }

            // 创建散点/血量专用容器（每次 offset 拖动时重建内容）
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

            var sortedHitEvents = GetDisplayHitEvents().OrderBy(e => e.HitObject.StartTime).ToList();

            drawHealthLine(sortedHitEvents, applyOffset: false);
            drawPointsGraph(sortedHitEvents, applyOffset: false);
        }

        private void drawPointsGraph(List<HitEvent> sortedHitEvents, bool applyOffset)
        {
            var pointList = new List<(Vector2 pos, Color4 colour)>();

            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;

            foreach (var e in sortedHitEvents)
            {
                double time = e.HitObject.StartTime;
                float xPosition = timeRange > 0 ? (float)((time - minTime) / timeRange) : 0;

                // 与 drawHealthLine(applyOffset: true) 一致：创建临时事件以传递调整后的 TimeOffset
                HitEvent displayEvent = applyOffset
                    ? new HitEvent(
                        e.TimeOffset + DisplayOffset,
                        e.GameplayRate,
                        e.Result,
                        e.HitObject,
                        e.LastHitObject,
                        e.Position)
                    : e;
                var displayResult = GetDisplayResult(displayEvent);

                float x = xPosition * availableWidth;
                float y = projectOffsetToY(
                    applyOffset ? e.TimeOffset + DisplayOffset : e.TimeOffset,
                    time);

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

        private bool textInitialized;

        /// <summary>
        /// 更新文本显示。首次调用或 <see cref="UpdateDisplay"/> 全量刷新后调用 <see cref="CreateTextUI"/> 重建 UI，
        /// 后续调用（如 offset 拖动时）仅调用 <see cref="UpdateTextValues"/> 更新数值。
        /// </summary>
        protected void UpdateText()
        {
            if (!textInitialized)
            {
                CreateTextUI();
                textInitialized = true;
            }
            else
            {
                UpdateTextValues();
            }
        }

        /// <summary>
        /// 创建文本统计 UI 结构。由 <see cref="UpdateText"/> 在首次或全量刷新后调用。
        /// 子类应在此方法中创建控件并缓存引用，供 <see cref="UpdateTextValues"/> 后续只更新数值。
        /// </summary>
        protected virtual void CreateTextUI()
        {
        }

        /// <summary>
        /// 更新文本统计数值。由 <see cref="UpdateText"/> 在非首次（如 offset 拖动）时调用。
        /// 子类应在此方法中仅更新已缓存 <see cref="SimpleStatisticItem.Value"/>，不重建 UI。
        /// </summary>
        protected virtual void UpdateTextValues()
        {
        }

        /// <summary>
        /// 请求图表重新计算并重绘。可安全从其他线程调用（会安排到更新线程）。
        /// </summary>
        protected void Refresh()
        {
            Scheduler.AddOnce(UpdateDisplay);
        }

        // ==================== Graph-UX: 双轨刷新机制 ====================

        /// <summary>
        /// 轻量重绘：仅应用 fake offset 到展示层，不触发 Session
        /// Graph-UX: 用于拖动时的即时反馈，实时更新统计和 scatter/health，不清边界线
        /// </summary>
        /// <param name="fakeOffset">展示的偏移量（毫秒）</param>
        protected void RefreshDisplayOnly(double fakeOffset)
        {
            DisplayOffset = fakeOffset;

            // 实时重算 V2 统计（无 committed score 时预览，有 committed score 时跳过）
            RecalculateV2FromDisplayEvents();

            // 重建左侧统计（只改数值）
            UpdateText();

            // 重绘 scatter 和 health（不清边界线）
            redrawScatterAndHealthWithOffset(fakeOffset);
        }

        /// <summary>
        /// 真实重算：调用 Session，更新 committed Now
        /// Graph-UX: offset 落定后触发，使用 ForLiveAnalysis 目的
        /// </summary>
        protected async Task RefreshFromService()
        {
            if (ReplaySession == null || Beatmap == null)
                return;

            var environment = CreateLiveAnalysisEnvironment();
            var inputScore = ResolveInputScore();

            if (inputScore == null)
                return;

            try
            {
                // TODO(P3-Rest): 应使用 ReplayRunRequest(ForLiveAnalysis) 统一入口
                // 当前暂时直接调用 RunAsync，P3-Rest 阶段改为 RunRequestAsync
                if (environment != null)
                {
                    CommittedNowScore = await ReplaySession.RunAsync(
                        inputScore.DeepClone(),
                        Beatmap,
                        environment
                    ).ConfigureAwait(false);

                    committedEnvironment = environment;
                }

                DisplayOffset = 0; // 重置展示 offset

                // 全量刷新（现在基于新的 committedNowScore）
                Schedule(Refresh);
            }
            catch (OperationCanceledException)
            {
                // 取消的请求忽略
            }
            catch
            {
                // Session 失败时清空 committedNowScore
                CommittedNowScore = null;
                committedEnvironment = null;
                Schedule(Refresh);
            }
        }

        /// <summary>
        /// Offset 变化处理：debounce 逻辑
        /// Graph-UX: 拖动时立即 display-only，停止后 debounce (300ms) 触发 service
        /// </summary>
        /// <param name="newOffset">新的 offset 值</param>
        protected void OnOffsetChanged(double newOffset)
        {
            // 取消之前的 debounce
            debounceCancellation?.Cancel();
            debounceCancellation = new CancellationTokenSource();

            var token = debounceCancellation.Token;

            // 立即应用 display-only（轻量重绘）
            RefreshDisplayOnly(newOffset);

            // debounce 后触发 service（真实重算）
            // fire-and-forget：故意不等待，由 CancellationToken 控制生命周期
#pragma warning disable CS4014
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(debounce_ms, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                        RefreshFromService();
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
#pragma warning restore CS4014
        }

        /// <summary>
        /// 轻量重绘 scatter + health line（带 offset）
        /// Graph-UX: 使用 <see cref="GetDisplayHitEvents"/>（已按 HitMode 过滤，含 fake offset）
        /// 重建 scatter 和血量，不清空边界线，拖动时性能友好。
        /// 注意：事件 TimeOffset 已经由 <see cref="FilterHitEvents"/> 链路的
        /// applyFakeOffsetToEvents 叠加了 displayOffset，因此 drawPointsGraph 无需再 applyOffset。
        /// </summary>
        private void redrawScatterAndHealthWithOffset(double offset)
        {
            if (graphContainer == null)
                return;

            // 移除旧的 scatter 和血量 path（保留边界线）
            var toRemove = graphContainer.Children
                .Where(c => c is GirdPoints || c is Path)
                .ToList();

            foreach (var child in toRemove)
                graphContainer.Remove(child, true);

            // 使用 GetDisplayHitEvents（已按 HitMode 过滤，已含 displayOffset）
            var displayEvents = GetDisplayHitEvents().OrderBy(e => e.HitObject.StartTime).ToList();
            drawPointsGraph(displayEvents, applyOffset: false);

            drawHealthLine(displayEvents, applyOffset: false);
        }

        /// <summary>
        /// 获取用于展示的 HitEvents（应用 displayOffset）
        /// </summary>
        protected IReadOnlyList<HitEvent> GetDisplayEventsWithOffset()
        {
            var baseEvents = CommittedNowScore?.ScoreInfo.HitEvents ?? OriginalHitEvents;

#pragma warning disable IDE0046 // displayOffset == 0 的简化写法
            if (DisplayOffset == 0)
                return baseEvents;
#pragma warning restore IDE0046

            return baseEvents.Select(e => new HitEvent(
                e.TimeOffset + DisplayOffset,
                e.GameplayRate,
                e.Result,
                e.HitObject,
                e.LastHitObject,
                e.Position
            )).ToList();
        }

        /// <summary>
        /// 创建 ForLiveAnalysis 环境（offset=0）
        /// 子类应重写以提供规则集特定的环境解析
        /// </summary>
        protected virtual IGameplayEnvironment? CreateLiveAnalysisEnvironment()
        {
            // 默认返回 null，子类必须重写
            return null;
        }

        /// <summary>
        /// 解析输入 Score（用于 Session 运行）
        /// 子类应重写以提供规则集特定的 Score 获取逻辑
        /// </summary>
        protected virtual Score? ResolveInputScore()
        {
            // 默认返回 null，子类必须重写
            return null;
        }

        private void updateTimeExtentsFromDisplayEvents()
        {
            var displayEvents = GetDisplayHitEvents();
            var eventsForExtent = displayEvents.Count > 0 ? displayEvents : OriginalHitEvents;

            if (eventsForExtent.Count == 0)
                return;

            binSize = Math.Ceiling(eventsForExtent.Max(e => e.HitObject.StartTime) / time_bins);
            binSize = Math.Max(1, binSize);

            maxTime = eventsForExtent.Max(e => e.HitObject.StartTime);
            minTime = eventsForExtent.Min(e => e.HitObject.StartTime);
            timeRange = maxTime - minTime;
        }

        private void drawHealthLine(List<HitEvent> sortedHitEvents)
        {
            List<Vector2> healthPoints = new List<Vector2>();
            double currentHealth = GetInitialHealth();

            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;

            foreach (var e in sortedHitEvents)
            {
                var displayResult = GetDisplayResult(e);
                double healthIncrease = GetDisplayHealthIncrease(e, displayResult, currentHealth);
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

        private void drawHealthLine(List<HitEvent> sortedHitEvents, bool applyOffset)
        {
            List<Vector2> healthPoints = new List<Vector2>();
            double currentHealth = GetInitialHealth();

            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;

            foreach (var e in sortedHitEvents)
            {
                // applyOffset 时需要创建带 offset 的临时 HitEvent 来获取 displayResult
                HitEvent displayEvent = applyOffset
                    ? new HitEvent(
                        e.TimeOffset + DisplayOffset,
                        e.GameplayRate,
                        e.Result,
                        e.HitObject,
                        e.LastHitObject,
                        e.Position)
                    : e;

                var displayResult = GetDisplayResult(displayEvent);
                double healthIncrease = GetDisplayHealthIncrease(displayEvent, displayResult, currentHealth);
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

        private void drawBoundaryLine(HitResult result, bool isNegative)
        {
            float availableWidth = DrawWidth - LeftMarginConst - RightMarginConst;
            int sampleCount = Math.Max(2, Math.Min(240, (int)Math.Ceiling(availableWidth / 8f)));
            double sign = isNegative ? -1 : 1;
            var vertices = new List<Vector2>(sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                double ratio = sampleCount == 1 ? 0 : i / (double)(sampleCount - 1);
                double time = minTime + timeRange * ratio;
                double boundary = UpdateBoundary(result, time);
                float x = (float)(ratio * availableWidth);
                float y = projectOffsetToY(sign * boundary, time);
                vertices.Add(new Vector2(x, y));
            }

            boundaryContainer.Add(new Path
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                PathRadius = 1,
                Colour = colours.ForHitResult(result),
                Alpha = 0.1f,
                Vertices = vertices.ToArray()
            });

            // 在左侧标签区域绘制判定标签（优先使用 LeftLabelContainer）
            double firstBoundary = UpdateBoundary(result, minTime) * sign;
            var label = new OsuSpriteText
            {
                Text = $"{firstBoundary:+0.##;-0.##}",
                Font = OsuFont.GetFont(size: 12),
                Colour = Color4.White,
                Y = projectOffsetToY(firstBoundary, minTime),
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

        private float projectOffsetToY(double offset, double? time)
        {
            double miss = Math.Max(1, UpdateBoundary(HitResult.Miss, time));
            // 使用 tanh 渐进映射替代线性+硬截断：
            //   offset=0        → y = 0.5*H(中心)
            //   offset=±miss    → y ≈ 0.88*H 或 0.12*H
            //   offset=±2*miss  → y ≈ 0.98*H 或 0.02*H
            //   offset→±∞       → y → H 或 0(渐进，不硬截断)
            // 这样超过 miss 窗口的事件仍可区分纵坐标，而不会全部归一化到边缘。
            float y = (float)((Math.Tanh(offset / miss) + 1) * 0.5 * DrawHeight);
            return Math.Clamp(y, 0, DrawHeight);
        }
    }
}
