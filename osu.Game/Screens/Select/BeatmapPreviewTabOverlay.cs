// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapPreviewTabOverlay : CompositeDrawable
    {
        private const float panel_left_margin = 12;
        private const float panel_width_ratio = 0.54f;
        private const float default_panel_height = 340;
        private const float default_panel_min_width = 520;
        private const float min_panel_width = 360;
        private const float max_panel_width = 1200;
        private const float min_panel_height = 180;
        private const float max_panel_height = 800;
        private const float bottom_controls_height = 56;
        private const float resize_handle_height = 10;
        private const float resize_handle_width = 10;

        private const float dynamic_preview_duration = 10000;
        private const float dynamic_preview_repeat_delay = 500;

        private static bool rememberedExpanded;
        private static bool rememberedDynamicMode;

        private readonly StopwatchClock previewClock = new StopwatchClock();
        private readonly FramedClock framedPreviewClock;

        private readonly Container panelContainer;
        private readonly Container stageViewport;
        private readonly Container stageScaleContainer;
        private readonly ProgressBar timeline;
        private readonly OsuSpriteText progressText;
        private readonly OsuSpriteText stateText;
        private readonly OsuSpriteText loadTimeText;

        private bool expanded;
        private bool dynamicMode;
        private bool heightResizeActive;
        private bool widthResizeActive;
        private bool selectionDirty;

        private float panelWidth;
        private float panelHeight = default_panel_height;

        private double playbackStartTime;
        private double beatmapMinTime;
        private double beatmapMaxTime;
        private double nextDynamicLoopStartTime;
        private double lastLoadTimeMs;
        private double lastSelectionEventTime;
        private double lastDisplayedLoadTimeMs = -1;
        private double lastProgressDisplayTime = double.MinValue;

        private float lastAppliedPanelWidth = -1;
        private float lastAppliedPanelHeight = -1;
        private float lastAppliedPanelY = float.NaN;
        private float lastViewportWidth = -1;
        private float lastViewportHeight = -1;
        private float lastAppliedStageScale = -1;

        private CancellationTokenSource? previewLoadCancellation;
        private DrawableRuleset? drawableRuleset;
        private IWorkingBeatmap? pendingWorkingBeatmap;
        private RulesetInfo? pendingRuleset;
        private bool selectionLoadInProgress;

        private long selectionEventVersion;
        private int currentRulesetOnlineId = -1;
        private string currentBeatmapHash = string.Empty;
        private int previewLoadedCount;
        private int previewReleasedCount;

        public BeatmapPreviewTabOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;

            framedPreviewClock = new FramedClock(previewClock);

            InternalChildren = new Drawable[]
            {
                panelContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Masking = true,
                    CornerRadius = 10,
                    Alpha = 0,
                    AlwaysPresent = true,
                    RelativeSizeAxes = Axes.None,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black.Opacity(0.78f)
                            },
                            loadTimeText = new OsuSpriteText
                            {
                                Text = "加载: 0ms",
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                Colour = Color4.CornflowerBlue,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Margin = new MarginPadding { Top = 8, Right = 8 }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding
                                {
                                    Top = resize_handle_height,
                                    Bottom = bottom_controls_height,
                                    Left = 8,
                                    Right = 8
                                },
                                Children = new Drawable[]
                                {
                                    stageViewport = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Masking = true,
                                        CornerRadius = 6,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = Color4.Black.Opacity(0.4f)
                                            },
                                            stageScaleContainer = new Container
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Size = new Vector2(1366, 768),
                                            },
                                            stateText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Font = OsuFont.Default.With(size: 20, weight: FontWeight.SemiBold),
                                                Colour = Color4.White,
                                                Text = "预览未加载"
                                            }
                                        }
                                    }
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = bottom_controls_height,
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Padding = new MarginPadding(10),
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 18,
                                        Children = new Drawable[]
                                        {
                                            timeline = new ProgressBar(true)
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                BackgroundColour = Color4.Black.Opacity(0.45f),
                                                FillColour = Color4.CornflowerBlue,
                                            },
                                            progressText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                X = -6,
                                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                                Colour = Color4.White,
                                                Text = "00:00.000"
                                            }
                                        }
                                    }
                                }
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = resize_handle_height,
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Colour = Color4.White.Opacity(0.22f)
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = resize_handle_width,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Colour = Color4.White.Opacity(0.15f)
                            }
                        }
                    }
                }
            };

            timeline.OnSeek = seekTo;
            timeline.OnCommit = seekTo;
        }

        public void Toggle()
        {
            if (expanded)
                Collapse();
            else
                Expand();
        }

        public void Expand()
        {
            if (expanded)
                return;

            expanded = true;

            panelContainer.ClearTransforms();
            panelContainer.MoveTo(new Vector2(panel_left_margin, 14));
            panelContainer.FadeIn(160, Easing.OutQuint);
            panelContainer.MoveToY(0, 160, Easing.OutQuint);

            if (selectionDirty)
                beginLoadPendingSelectionIfRequired();
        }

        public void Collapse()
        {
            if (!expanded)
                return;

            expanded = false;
            heightResizeActive = false;
            widthResizeActive = false;
            selectionLoadInProgress = false;
            nextDynamicLoopStartTime = 0;
            previewClock.Stop();
            cancelPendingLoad();

            // Clean up resources
            disposePreviewResources();
            pendingWorkingBeatmap = null;
            pendingRuleset = null;

            panelContainer.ClearTransforms();
            panelContainer.FadeOut(160, Easing.OutQuint);
            panelContainer.MoveToY(14, 160, Easing.OutQuint);
        }

        public void UpdateSelection(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset)
        {
            if (workingBeatmap.BeatmapInfo == null)
                return;

            string beatmapHash = workingBeatmap.BeatmapInfo.Hash;

            bool unchanged = currentRulesetOnlineId == ruleset.OnlineID
                             && currentBeatmapHash == beatmapHash;

            if (unchanged)
                return;

            currentRulesetOnlineId = ruleset.OnlineID;
            currentBeatmapHash = beatmapHash;
            lastSelectionEventTime = Time.Current;
            selectionEventVersion++;

            pendingWorkingBeatmap = workingBeatmap;
            pendingRuleset = ruleset;
            selectionDirty = true;

            if (!expanded)
                return;

            beginLoadPendingSelectionIfRequired();
        }

        public void SuspendForScreenExit()
        {
            selectionLoadInProgress = false;
            cancelPendingLoad();
            rememberedExpanded = expanded;
            rememberedDynamicMode = dynamicMode;
        }

        public void RestoreRememberedState()
        {
            if (rememberedExpanded)
                Expand();
            else
                Collapse();

            dynamicMode = rememberedDynamicMode;
        }

        private void beginLoadPendingSelectionIfRequired()
        {
            if (!expanded || selectionLoadInProgress || !selectionDirty)
                return;

            loadPendingSelection(selectionEventVersion);
        }

        private void loadPendingSelection(long eventVersion)
        {
            if (eventVersion != selectionEventVersion)
                return;

            if (pendingWorkingBeatmap == null || pendingRuleset == null || pendingWorkingBeatmap.BeatmapInfo == null)
                return;

            selectionLoadInProgress = true;
            selectionDirty = false;
            lastLoadTimeMs = 0;

            setStateText("谱面预览加载中...");

            cancelPendingLoad();
            previewLoadCancellation = new CancellationTokenSource();
            var token = previewLoadCancellation.Token;

            var workingBeatmap = pendingWorkingBeatmap;
            var ruleset = pendingRuleset;

            double loadStartTime = lastSelectionEventTime > 0 ? lastSelectionEventTime : Time.Current;

            Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset);

                token.ThrowIfCancellationRequested();

                var objects = playableBeatmap.HitObjects;

                if (objects.Count == 0)
                    return new LoadedPreviewData(eventVersion, workingBeatmap, ruleset, playableBeatmap, 0, 0, 0);

                double minTime = objects.First().StartTime;
                double maxTime = objects.Max(o => o.GetEndTime());
                double startTime = computeDefaultStartTime(playableBeatmap, ruleset, minTime);

                return new LoadedPreviewData(eventVersion, workingBeatmap, ruleset, playableBeatmap, startTime, minTime, maxTime);
            }, token).ContinueWith(task =>
            {
                Schedule(() =>
                {
                    if (token.IsCancellationRequested || task.IsCanceled)
                    {
                        onSelectionLoadFinished();
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        if (eventVersion == selectionEventVersion)
                            setStateText("预览加载失败");

                        onSelectionLoadFinished();
                        return;
                    }

                    var result = task.GetResultSafely();

                    if (result.Version != selectionEventVersion)
                    {
                        onSelectionLoadFinished();
                        return;
                    }

                    lastLoadTimeMs = Time.Current - loadStartTime;
                    lastDisplayedLoadTimeMs = -1;

                    beatmapMinTime = result.MinTime;
                    beatmapMaxTime = Math.Max(result.MaxTime, beatmapMinTime + 1);
                    playbackStartTime = result.StartTime;

                    setupDrawableRuleset(result.WorkingBeatmap, result.RulesetInfo, result.PlayableBeatmap);

                    // Clear pending references to avoid memory leak
                    pendingWorkingBeatmap = null;
                    pendingRuleset = null;

                    previewClock.Stop();
                    previewClock.Seek(playbackStartTime);

                    if (dynamicMode)
                    {
                        nextDynamicLoopStartTime = Time.Current;
                        previewClock.Start();
                    }

                    updateProgressDisplay(previewClock.CurrentTime);
                    setStateText(string.Empty);

                    onSelectionLoadFinished();
                });
            }, CancellationToken.None);
        }

        private void onSelectionLoadFinished()
        {
            selectionLoadInProgress = false;
            beginLoadPendingSelectionIfRequired();
        }

        protected override void Update()
        {
            base.Update();

            if (lastLoadTimeMs > 0)
            {
                double displayed = Math.Round(lastLoadTimeMs);

                if (displayed != lastDisplayedLoadTimeMs)
                {
                    loadTimeText.Text = $"加载: {displayed:F0}ms";
                    lastDisplayedLoadTimeMs = displayed;
                }
            }

            if (panelWidth <= 0)
                panelWidth = getDefaultPanelWidth();

            float targetPanelY = expanded ? 0 : 14;

            if (panelWidth != lastAppliedPanelWidth)
            {
                panelContainer.Width = panelWidth;
                lastAppliedPanelWidth = panelWidth;
            }

            if (panelHeight != lastAppliedPanelHeight)
            {
                panelContainer.Height = panelHeight;
                lastAppliedPanelHeight = panelHeight;
            }

            panelContainer.X = panel_left_margin;

            if (targetPanelY != lastAppliedPanelY)
            {
                panelContainer.Y = targetPanelY;
                lastAppliedPanelY = targetPanelY;
            }

            float viewportWidth = stageViewport.DrawWidth;
            float viewportHeight = stageViewport.DrawHeight;

            if (viewportWidth != lastViewportWidth || viewportHeight != lastViewportHeight)
            {
                float scale = Math.Min(viewportWidth / stageScaleContainer.Width, viewportHeight / stageScaleContainer.Height);
                float clampedScale = Math.Max(0.05f, scale);

                if (clampedScale != lastAppliedStageScale)
                {
                    stageScaleContainer.Scale = new Vector2(clampedScale);
                    lastAppliedStageScale = clampedScale;
                }

                lastViewportWidth = viewportWidth;
                lastViewportHeight = viewportHeight;
            }

            if (!expanded)
                return;

            if (dynamicMode && drawableRuleset != null)
            {
                if (previewClock.IsRunning)
                {
                    double elapsed = previewClock.CurrentTime - playbackStartTime;

                    if (elapsed >= dynamic_preview_duration)
                    {
                        previewClock.Stop();
                        previewClock.Seek(playbackStartTime);
                        nextDynamicLoopStartTime = Time.Current + dynamic_preview_repeat_delay;
                    }
                }
                else if (Time.Current >= nextDynamicLoopStartTime)
                {
                    previewClock.Seek(playbackStartTime);
                    previewClock.Start();
                }
            }

            if (!timeline.Seeking && dynamicMode && previewClock.IsRunning)
            {
                if (previewClock.CurrentTime - lastProgressDisplayTime >= 16)
                {
                    updateProgressDisplay(previewClock.CurrentTime);
                    lastProgressDisplayTime = previewClock.CurrentTime;
                }
            }
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!expanded)
                return false;

            Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
            float panelRight = panel_left_margin + panelWidth;
            float panelTop = DrawHeight - panelHeight;
            float panelBottom = DrawHeight;

            bool inWidthHandle = local.X >= panelRight - resize_handle_width && local.X <= panelRight + resize_handle_width
                                                                             && local.Y >= panelTop && local.Y <= panelBottom;

            bool inHeightHandle = local.X >= panel_left_margin && local.X <= panelRight
                                                               && local.Y >= panelTop - resize_handle_height && local.Y <= panelTop + resize_handle_height;

            if (!inWidthHandle && !inHeightHandle)
                return base.OnDragStart(e);

            if (inWidthHandle)
                widthResizeActive = true;

            if (inHeightHandle)
                heightResizeActive = true;

            return true;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (!expanded || drawableRuleset == null)
                return base.OnScroll(e);

            Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
            float panelRight = panel_left_margin + panelWidth;
            float panelTop = DrawHeight - panelHeight;
            float panelBottom = DrawHeight;

            // Check if scroll is within panel bounds
            if (local.X < panel_left_margin || local.X > panelRight || local.Y < panelTop || local.Y > panelBottom)
                return base.OnScroll(e);

            // In dynamic mode: fast-forward 3 seconds per scroll, keep playback running
            if (dynamicMode)
            {
                double newTime = previewClock.CurrentTime + e.ScrollDelta.Y * 3000;
                previewClock.Seek(Math.Clamp(newTime, beatmapMinTime, beatmapMaxTime));
                updateProgressDisplay(previewClock.CurrentTime);
                return true;
            }

            // In static mode: seek to position relative to scroll
            if (beatmapMaxTime <= beatmapMinTime)
                return true;

            double totalDuration = beatmapMaxTime - beatmapMinTime;
            double timePerScroll = totalDuration * 0.005;
            double seekTime = previewClock.CurrentTime + e.ScrollDelta.Y * timePerScroll;

            seekTo(Math.Clamp(seekTime, beatmapMinTime, beatmapMaxTime));
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            bool handled = false;

            if (heightResizeActive)
            {
                setPanelHeight(panelHeight - e.Delta.Y);
                handled = true;
            }

            if (widthResizeActive)
            {
                setPanelWidth(panelWidth + e.Delta.X);
                handled = true;
            }

            if (handled)
                return;

            base.OnDrag(e);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            heightResizeActive = false;
            widthResizeActive = false;
            base.OnDragEnd(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            selectionLoadInProgress = false;
            cancelPendingLoad();
            drawableRuleset = null;
            base.Dispose(isDisposing);
        }

        private void cancelPendingLoad()
        {
            previewLoadCancellation?.Cancel();
            previewLoadCancellation?.Dispose();
            previewLoadCancellation = null;
        }

        public void TogglePreviewMode()
        {
            dynamicMode = !dynamicMode;

            previewClock.Stop();

            if (!dynamicMode)
                return;

            if (!expanded || drawableRuleset == null)
                return;

            previewClock.Seek(playbackStartTime);
            nextDynamicLoopStartTime = Time.Current;
            previewClock.Start();
        }

        private void setupDrawableRuleset(IWorkingBeatmap workingBeatmap, RulesetInfo rulesetInfo, IBeatmap playableBeatmap)
        {
            // Clean up previous resources first
            disposePreviewResources();

            var ruleset = rulesetInfo.CreateInstance();

            drawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap);
            drawableRuleset.Clock = framedPreviewClock;
            drawableRuleset.FrameStablePlayback = false;
            drawableRuleset.Playfield.DisplayJudgements.Value = false;

            stageScaleContainer.Child = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, workingBeatmap.Skin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new NonInteractivePreviewContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = drawableRuleset,
                }
            };

            previewLoadedCount++;
            logPreviewLoadReleaseStats("loaded");
        }

        private float getDefaultPanelWidth()
        {
            float preferred = DrawWidth * panel_width_ratio;
            return Math.Clamp(preferred, default_panel_min_width, Math.Min(max_panel_width, DrawWidth - panel_left_margin - 20));
        }

        private void setPanelWidth(float width)
        {
            float maxWidth = Math.Min(max_panel_width, DrawWidth - panel_left_margin - 20);
            panelWidth = Math.Clamp(width, min_panel_width, Math.Max(min_panel_width, maxWidth));
        }

        private void setPanelHeight(float height)
        {
            float maxHeight = Math.Min(max_panel_height, DrawHeight - 30);
            panelHeight = Math.Clamp(height, min_panel_height, Math.Max(min_panel_height, maxHeight));
        }

        private void setStateText(string text)
        {
            stateText.Text = text;
            stateText.FadeTo(string.IsNullOrEmpty(text) ? 0 : 1, 120, Easing.OutQuint);
        }

        private void seekTo(double time)
        {
            dynamicMode = false;
            nextDynamicLoopStartTime = 0;
            previewClock.Stop();
            previewClock.Seek(time);
            updateProgressDisplay(time);
        }

        private void updateProgressDisplay(double time)
        {
            if (beatmapMaxTime <= beatmapMinTime)
            {
                timeline.EndTime = 1;
                timeline.CurrentTime = 0;
                progressText.Text = "00:00.000";
                return;
            }

            double clamped = Math.Clamp(time, beatmapMinTime, beatmapMaxTime);
            timeline.EndTime = beatmapMaxTime;
            timeline.CurrentTime = clamped;
            progressText.Text = formatTime(clamped);
        }

        private void disposePreviewResources()
        {
            // Don't manually dispose - let the container handle cleanup
            // Manual disposal causes issues with audio sample management in RulesetSkinProvidingContainer
            if (drawableRuleset != null)
            {
                previewReleasedCount++;
                logPreviewLoadReleaseStats("released");
            }

            stageScaleContainer.Clear();
            drawableRuleset = null;
        }

        private void logPreviewLoadReleaseStats(string action)
        {
            Logger.Log($"[BeatmapPreview] action={action}, loaded={previewLoadedCount}, released={previewReleasedCount}", LoggingTarget.Runtime, LogLevel.Debug);
        }

        private static double computeDefaultStartTime(IBeatmap beatmap, RulesetInfo ruleset, double fallback)
        {
            var objects = beatmap.HitObjects;

            if (objects.Count == 0)
                return 0;

            double anchorTime;

            if (string.Equals(ruleset.ShortName, "mania", StringComparison.OrdinalIgnoreCase))
                anchorTime = getMaxKpsAnchor(objects);
            else
                anchorTime = getKiaiAnchor(beatmap.ControlPointInfo, objects[0].StartTime);

            return Math.Max(fallback, anchorTime - 1000);
        }

        private static double getKiaiAnchor(ControlPointInfo controlPoints, double fallback)
        {
            var firstKiai = controlPoints.EffectPoints.FirstOrDefault(p => p.KiaiMode);
            return firstKiai?.Time ?? fallback;
        }

        private static double getMaxKpsAnchor(IReadOnlyList<HitObject> objects)
        {
            int left = 0;
            int bestCount = 0;
            double bestTime = objects[0].StartTime;

            for (int right = 0; right < objects.Count; right++)
            {
                double rightTime = objects[right].StartTime;

                while (left <= right && objects[left].StartTime < rightTime - 1000)
                    left++;

                int count = right - left + 1;

                if (count > bestCount)
                {
                    bestCount = count;
                    bestTime = rightTime;
                }
            }

            return bestTime;
        }

        private static string formatTime(double time)
        {
            TimeSpan span = TimeSpan.FromMilliseconds(Math.Max(0, time));
            return $"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
        }

        private readonly record struct LoadedPreviewData(
            long Version,
            IWorkingBeatmap WorkingBeatmap,
            RulesetInfo RulesetInfo,
            IBeatmap PlayableBeatmap,
            double StartTime,
            double MinTime,
            double MaxTime);

        private partial class NonInteractivePreviewContainer : Container
        {
            public override bool HandlePositionalInput => false;

            public override bool HandleNonPositionalInput => false;

            public override bool PropagateNonPositionalInputSubTree => false;
        }
    }
}
