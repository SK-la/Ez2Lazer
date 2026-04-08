// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzBeatmapPreviewOverlay : CompositeDrawable
    {
        private const float panel_left_margin = 12;
        private const float panel_width_ratio = 0.54f;
        private const float panel_right_margin = 20;
        private const float default_panel_height = 340;
        private const float default_panel_min_width = 520;
        private const float min_panel_width = 360;
        private const float max_panel_width = 800;
        private const float min_panel_height = 180;
        private const float max_panel_height = 800;
        private const float bottom_controls_height = 56;
        private const float resize_handle_height = 10;
        private const float resize_handle_width = 10;
        private const float preview_mode_button_width = 120;
        private const float preview_mode_list_width = preview_mode_button_width;
        private const float preview_mode_button_height = 30;
        private const float preview_mode_button_spacing = 6;

        private const float dynamic_preview_duration = 10000;
        private const float dynamic_preview_repeat_delay = 500;
        private const int selection_load_debounce = 180;

        private static bool rememberedExpanded;

        private static readonly EzBeatmapPreviewMode[] shared_preview_modes =
        {
            EzBeatmapPreviewMode.Dynamic,
            EzBeatmapPreviewMode.Static,
        };

        private static readonly EzBeatmapPreviewMode[] mania_preview_modes =
        {
            EzBeatmapPreviewMode.Dynamic,
            EzBeatmapPreviewMode.Static,
            EzBeatmapPreviewMode.StaticFullMap,
            EzBeatmapPreviewMode.StaticScroll,
        };

        private readonly StopwatchClock previewClock = new StopwatchClock();
        private readonly FramedClock framedPreviewClock;
        private readonly BindableBool expandedBindable = new BindableBool();
        private readonly Bindable<EzBeatmapPreviewMode> previewMode = new Bindable<EzBeatmapPreviewMode>(EzBeatmapPreviewMode.Static);

        private readonly Container panelContainer;
        private readonly Container stageViewport;
        private readonly Container stageScaleContainer;
        private readonly ProgressBar timeline;
        private readonly OsuSpriteText progressText;
        private readonly OsuSpriteText stateText;
        private readonly OsuSpriteText loadTimeText;
        private readonly FillFlowContainer previewModeButtonList;
        private readonly Dictionary<EzBeatmapPreviewMode, PreviewModeButton> previewModeButtons = new Dictionary<EzBeatmapPreviewMode, PreviewModeButton>();
        private readonly Box topResizeHandle;
        private readonly Box rightResizeHandle;

        private bool expanded;
        private bool heightResizeActive;
        private bool widthResizeActive;
        private bool panelWidthManuallyAdjusted;
        private bool selectionDirty;
        private float dragStartPanelWidth;
        private float dragStartPanelHeight;

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
        private ScheduledDelegate? scheduledSelectionLoad;
        private Drawable? currentPreviewRoot;
        private Drawable? pendingPreviewRoot;
        private DrawableRuleset? drawableRuleset;

        [Resolved]
        private EzAnalysisCache? ezAnalysisCache { get; set; }

        private IWorkingBeatmap? currentWorkingBeatmap;
        private RulesetInfo? currentRuleset;
        private IReadOnlyList<Mod> currentMods = Array.Empty<Mod>();
        private bool selectionLoadInProgress;

        private IBeatmap? injectedPlayableBeatmap;

        private long selectionEventVersion;
        private int currentRulesetOnlineId = -1;
        private string currentBeatmapHash = string.Empty;
        private int previewLoadedCount;
        private int previewReleasedCount;

        private bool dynamicMode => previewMode.Value == EzBeatmapPreviewMode.Dynamic;

        public IBindable<bool> ExpandedState => expandedBindable;

        public Func<float>? DefaultPanelRightEdgeInScreenSpace { get; set; }

        public EzBeatmapPreviewOverlay()
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
                            previewModeButtonList = new FillFlowContainer
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Depth = float.MinValue,
                                Position = new Vector2(8, resize_handle_height + 8),
                                Width = preview_mode_list_width,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, preview_mode_button_spacing),
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding
                                {
                                    Top = resize_handle_height,
                                    Bottom = bottom_controls_height,
                                    Left = preview_mode_list_width + 16,
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
                                Padding = new MarginPadding
                                {
                                    Top = 10,
                                    Bottom = 10,
                                    Left = preview_mode_list_width + 16,
                                    Right = 10
                                },
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
                            topResizeHandle = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = resize_handle_height,
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Colour = Color4.White.Opacity(0.22f)
                            },
                            rightResizeHandle = new Box
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

            timeline.OnSeek = time => seekTo(time, dynamicMode);
            timeline.OnCommit = time => seekTo(time, dynamicMode);

            createPreviewModeButtons();
            updatePreviewModeButtons();
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            previewMode.BindTo(ezConfig.GetBindable<EzBeatmapPreviewMode>(Ez2Setting.BeatmapPreviewMode));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            previewMode.BindValueChanged(_ => onPreviewModeChanged(), true);
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
            expandedBindable.Value = true;

            if (drawableRuleset == null && currentWorkingBeatmap != null && currentRuleset != null)
                selectionDirty = true;

            panelContainer.ClearTransforms();
            panelContainer.MoveTo(new Vector2(panel_left_margin, 14));
            panelContainer.FadeIn(160, Easing.OutQuint);
            panelContainer.MoveToY(0, 160, Easing.OutQuint);

            if (selectionDirty)
                scheduleSelectionLoad();
        }

        public void Collapse()
        {
            if (!expanded)
                return;

            expanded = false;
            expandedBindable.Value = false;
            heightResizeActive = false;
            widthResizeActive = false;
            selectionLoadInProgress = false;
            nextDynamicLoopStartTime = 0;
            previewClock.Stop();
            cancelScheduledSelectionLoad();
            cancelPendingLoad();

            // Clean up resources
            disposePreviewResources();
            selectionDirty = currentWorkingBeatmap != null && currentRuleset != null;
            setStateText("预览未加载");

            panelContainer.ClearTransforms();
            panelContainer.FadeOut(160, Easing.OutQuint);
            panelContainer.MoveToY(14, 160, Easing.OutQuint);
        }

        public void UpdateSelection(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, IReadOnlyList<Mod> mods, bool forceReload = false)
        {
            if (workingBeatmap.BeatmapInfo == null)
                return;

            string beatmapHash = workingBeatmap.BeatmapInfo.Hash;
            bool unchanged = !forceReload
                             && currentRulesetOnlineId == ruleset.OnlineID
                             && currentBeatmapHash == beatmapHash
                             && currentMods.SequenceEqual(mods);

            currentWorkingBeatmap = workingBeatmap;
            currentRuleset = ruleset;
            currentMods = mods.ToArray();
            updatePreviewModeButtons();

            if (unchanged)
            {
                if (drawableRuleset == null)
                {
                    selectionDirty = true;

                    if (expanded)
                        scheduleSelectionLoad();
                }

                return;
            }

            currentRulesetOnlineId = ruleset.OnlineID;
            currentBeatmapHash = beatmapHash;
            lastSelectionEventTime = Time.Current;
            selectionEventVersion++;

            selectionDirty = true;

            if (!expanded)
                return;

            scheduleSelectionLoad();
        }

        /// <summary>
        /// Update selection with a precomputed playable beatmap. The overlay will prefer using
        /// <paramref name="playableBeatmap"/> when loading the preview to avoid doing conversion on the overlay side.
        /// </summary>
        public void UpdateSelection(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, IReadOnlyList<Mod> mods, IBeatmap? playableBeatmap, bool forceReload = false)
        {
            injectedPlayableBeatmap = playableBeatmap;
            UpdateSelection(workingBeatmap, ruleset, mods, forceReload);
        }

        public void SuspendForScreenExit()
        {
            selectionLoadInProgress = false;
            cancelScheduledSelectionLoad();
            cancelPendingLoad();

            disposePreviewResources();
            currentWorkingBeatmap = null;
            currentRuleset = null;
            currentMods = Array.Empty<Mod>();
            selectionDirty = false;
            currentRulesetOnlineId = -1;
            currentBeatmapHash = string.Empty;
            updatePreviewModeButtons();

            rememberedExpanded = expanded;
        }

        public void RestoreRememberedState()
        {
            if (rememberedExpanded)
                Expand();
            else
                Collapse();

            updatePreviewModeButtons();
        }

        private void beginLoadPendingSelectionIfRequired()
        {
            if (!expanded || selectionLoadInProgress || !selectionDirty)
                return;

            if (Time.Current - lastSelectionEventTime < selection_load_debounce)
            {
                scheduleSelectionLoad();
                return;
            }

            loadPendingSelection(selectionEventVersion);
        }

        private void scheduleSelectionLoad()
        {
            cancelScheduledSelectionLoad();

            if (!expanded)
                return;

            scheduledSelectionLoad = Scheduler.AddDelayed(beginLoadPendingSelectionIfRequired, selection_load_debounce);
        }

        private void cancelScheduledSelectionLoad()
        {
            scheduledSelectionLoad?.Cancel();
            scheduledSelectionLoad = null;
        }

        private void loadPendingSelection(long eventVersion)
        {
            if (eventVersion != selectionEventVersion)
                return;

            if (currentWorkingBeatmap == null || currentRuleset == null || currentWorkingBeatmap.BeatmapInfo == null)
                return;

            selectionLoadInProgress = true;
            scheduledSelectionLoad = null;
            selectionDirty = false;
            lastLoadTimeMs = 0;

            setStateText("loading...");

            cancelPendingLoad();
            previewLoadCancellation = new CancellationTokenSource();
            var token = previewLoadCancellation.Token;

            var workingBeatmap = currentWorkingBeatmap;
            var ruleset = currentRuleset;
            var mods = currentMods;

            double loadStartTime = lastSelectionEventTime > 0 ? lastSelectionEventTime : Time.Current;

            Task.Run<LoadedPreviewData?>(() =>
            {
                token.ThrowIfCancellationRequested();

                // Prefer an externally-injected playable beatmap if available to avoid performing
                // conversion (which may be thread-affine) on the overlay side.
                IBeatmap? playableBeatmap = injectedPlayableBeatmap;

                if (playableBeatmap != null)
                {
                    // Clear the injection so it isn't reused accidentally.
                    injectedPlayableBeatmap = null;
                }
                else
                {
                    playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods, token);
                }

                token.ThrowIfCancellationRequested();

                if (playableBeatmap == null)
                    return null;

                var objects = playableBeatmap.HitObjects;

                if (objects.Count == 0)
                    return new LoadedPreviewData(eventVersion, workingBeatmap, ruleset, playableBeatmap, 0, 0, 0);

                double minTime = objects.First().StartTime;
                double maxTime = objects.Max(o => o.GetEndTime());
                double startTime = computeDefaultStartTime(workingBeatmap, ruleset, mods, playableBeatmap, minTime, token);

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
                            setStateText("load fail");

                        onSelectionLoadFinished();
                        return;
                    }

                    var result = task.GetResultSafely();

                    if (result == null)
                    {
                        onSelectionLoadFinished();
                        return;
                    }

                    if (result.Value.Version != selectionEventVersion)
                    {
                        onSelectionLoadFinished();
                        return;
                    }

                    lastLoadTimeMs = Time.Current - loadStartTime;
                    lastDisplayedLoadTimeMs = -1;

                    beatmapMinTime = result.Value.MinTime;
                    beatmapMaxTime = Math.Max(result.Value.MaxTime, beatmapMinTime + 1);
                    playbackStartTime = result.Value.StartTime;

                    setupDrawableRulesetAsync(result.Value.Version, result.Value.WorkingBeatmap, result.Value.RulesetInfo, result.Value.PlayableBeatmap, token);

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

            if (selectionDirty)
                scheduleSelectionLoad();
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

            panelWidth = panelWidthManuallyAdjusted
                ? clampPanelWidth(panelWidth <= 0 ? getDefaultPanelWidth() : panelWidth)
                : getDefaultPanelWidth();

            panelHeight = clampPanelHeight(panelHeight);

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

            bool inWidthHandle = isWithinWidthResizeHandle(e.ScreenSpaceMousePosition);
            bool inHeightHandle = isWithinHeightResizeHandle(e.ScreenSpaceMousePosition);

            if (!inWidthHandle && !inHeightHandle)
                return base.OnDragStart(e);

            dragStartPanelWidth = panelWidth <= 0 ? getDefaultPanelWidth() : panelWidth;
            dragStartPanelHeight = panelHeight;

            if (inWidthHandle)
                widthResizeActive = true;

            if (inHeightHandle)
                heightResizeActive = true;

            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!expanded)
                return base.OnMouseDown(e);

            if (base.OnMouseDown(e))
                return true;

            return isWithinPanel(e.ScreenSpaceMousePosition);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (!expanded || drawableRuleset == null)
                return base.OnScroll(e);

            var panelQuad = panelContainer.ScreenSpaceDrawQuad;
            Vector2 mouse = e.ScreenSpaceMousePosition;

            // Check if scroll is within panel bounds
            if (mouse.X < panelQuad.TopLeft.X || mouse.X > panelQuad.TopRight.X || mouse.Y < panelQuad.TopLeft.Y || mouse.Y > panelQuad.BottomLeft.Y)
                return base.OnScroll(e);

            // In dynamic mode: fast-forward 3 seconds per scroll, keep playback running
            if (dynamicMode)
            {
                double newTime = previewClock.CurrentTime - e.ScrollDelta.Y * 3000;
                seekTo(Math.Clamp(newTime, beatmapMinTime, beatmapMaxTime), true);
                return true;
            }

            // In static mode: seek to position relative to scroll
            if (beatmapMaxTime <= beatmapMinTime)
                return true;

            double totalDuration = beatmapMaxTime - beatmapMinTime;
            double timePerScroll = totalDuration * 0.005;
            double seekTime = previewClock.CurrentTime - e.ScrollDelta.Y * timePerScroll;

            seekTo(Math.Clamp(seekTime, beatmapMinTime, beatmapMaxTime));
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            bool handled = false;
            Vector2 localDelta = ToLocalSpace(e.ScreenSpaceMousePosition) - ToLocalSpace(e.ScreenSpaceMouseDownPosition);

            if (heightResizeActive)
            {
                setPanelHeight(dragStartPanelHeight - localDelta.Y);
                handled = true;
            }

            if (widthResizeActive)
            {
                setPanelWidth(dragStartPanelWidth + localDelta.X, true);
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
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                selectionLoadInProgress = false;
                cancelScheduledSelectionLoad();
                cancelPendingLoad();
                disposePreviewResources();
                currentWorkingBeatmap = null;
                currentRuleset = null;
                previewMode.UnbindAll();
            }
        }

        private void cancelPendingLoad()
        {
            previewLoadCancellation?.Cancel();
            previewLoadCancellation?.Dispose();
            previewLoadCancellation = null;
        }

        private void setupDrawableRulesetAsync(long eventVersion, IWorkingBeatmap workingBeatmap, RulesetInfo rulesetInfo, IBeatmap playableBeatmap, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested || eventVersion != selectionEventVersion || !expanded)
                return;

            if (cancellationToken.IsCancellationRequested || eventVersion != selectionEventVersion || !expanded)
                return;

            var ruleset = rulesetInfo.CreateInstance();

            var newDrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap);
            newDrawableRuleset.Clock = framedPreviewClock;
            newDrawableRuleset.FrameStablePlayback = false;
            newDrawableRuleset.Playfield.DisplayJudgements.Value = false;

            var previewContainer = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, workingBeatmap.Skin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new NonInteractivePreviewContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = newDrawableRuleset,
                }
            };

            pendingPreviewRoot = previewContainer;

            cancellationToken.Register(() => Schedule(() =>
            {
                if (!ReferenceEquals(pendingPreviewRoot, previewContainer))
                    return;

                pendingPreviewRoot = null;
                previewContainer.Dispose();
            }));

            LoadComponentAsync(previewContainer, loaded =>
            {
                if (ReferenceEquals(pendingPreviewRoot, loaded))
                    pendingPreviewRoot = null;

                if (cancellationToken.IsCancellationRequested || eventVersion != selectionEventVersion || !expanded)
                {
                    loaded.Dispose();
                    return;
                }

                var previousPreviewRoot = currentPreviewRoot;

                stageScaleContainer.Child = loaded;
                currentPreviewRoot = loaded;
                drawableRuleset = newDrawableRuleset;

                if (previousPreviewRoot != null)
                {
                    previewReleasedCount++;
                    logPreviewLoadReleaseStats("released");
                }

                previewLoadedCount++;
                logPreviewLoadReleaseStats("loaded");
            }, cancellationToken);
        }

        private float getDefaultPanelWidth()
        {
            float preferred = DrawWidth * panel_width_ratio;

            if (DefaultPanelRightEdgeInScreenSpace != null)
            {
                float targetRightEdge = ToLocalSpace(new Vector2(DefaultPanelRightEdgeInScreenSpace(), 0)).X;

                if (!float.IsNaN(targetRightEdge) && !float.IsInfinity(targetRightEdge))
                    preferred = targetRightEdge - panel_left_margin;
            }

            return clampPanelWidth(preferred);
        }

        private void setPanelWidth(float width, bool adjustedByUser = false)
        {
            panelWidth = clampPanelWidth(width);

            if (adjustedByUser)
                panelWidthManuallyAdjusted = true;
        }

        private void setPanelHeight(float height)
        {
            panelHeight = clampPanelHeight(height);
        }

        private void setStateText(string text)
        {
            stateText.Text = text;
            stateText.FadeTo(string.IsNullOrEmpty(text) ? 0 : 1, 120, Easing.OutQuint);
        }

        private void seekTo(double time, bool preserveDynamicPlayback = false)
        {
            double clamped = beatmapMaxTime <= beatmapMinTime
                ? Math.Max(0, time)
                : Math.Clamp(time, beatmapMinTime, beatmapMaxTime);

            if (preserveDynamicPlayback && dynamicMode && drawableRuleset != null)
            {
                playbackStartTime = clamped;
                nextDynamicLoopStartTime = 0;
                previewClock.Seek(clamped);
                previewClock.Start();
            }
            else
            {
                nextDynamicLoopStartTime = 0;
                previewClock.Stop();
                previewClock.Seek(clamped);
            }

            updateProgressDisplay(clamped);
        }

        private void updateProgressDisplay(double time)
        {
            if (beatmapMaxTime <= beatmapMinTime)
            {
                timeline.EndTime = 1;
                timeline.CurrentTime = 0;
                progressText.Text = "00:00.000";
                lastProgressDisplayTime = 0;
                return;
            }

            double clamped = Math.Clamp(time, beatmapMinTime, beatmapMaxTime);
            timeline.EndTime = beatmapMaxTime;
            timeline.CurrentTime = clamped;
            progressText.Text = formatTime(clamped);
            lastProgressDisplayTime = clamped;
        }

        private void disposePreviewResources()
        {
            if (currentPreviewRoot != null)
            {
                previewReleasedCount++;
                logPreviewLoadReleaseStats("released");
            }

            if (stageScaleContainer.Count > 0)
            {
                if (!IsLoaded)
                    stageScaleContainer.Clear(true);
                else
                    Schedule(() => stageScaleContainer.Clear(true));
            }

            currentPreviewRoot = null;
            drawableRuleset = null;
        }

        private float clampPanelWidth(float width)
        {
            float maxWidth = Math.Min(max_panel_width, DrawWidth - panel_left_margin - panel_right_margin);
            return Math.Clamp(width, min_panel_width, Math.Max(min_panel_width, maxWidth));
        }

        private float clampPanelHeight(float height)
        {
            float maxHeight = Math.Min(max_panel_height, DrawHeight - 30);
            return Math.Clamp(height, min_panel_height, Math.Max(min_panel_height, maxHeight));
        }

        private bool isWithinPanel(Vector2 screenSpacePosition)
            => panelContainer.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

        private bool isWithinWidthResizeHandle(Vector2 screenSpacePosition)
            => rightResizeHandle.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

        private bool isWithinHeightResizeHandle(Vector2 screenSpacePosition)
            => topResizeHandle.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

        private void logPreviewLoadReleaseStats(string action)
        {
            int activePreviewCount = previewLoadedCount - previewReleasedCount;
            Logger.Log($"[BeatmapPreview] action={action}, loaded={previewLoadedCount}, released={previewReleasedCount}, active={activePreviewCount}, expanded={expanded}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        private double computeDefaultStartTime(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, IReadOnlyList<Mod> mods, IBeatmap playableBeatmap, double fallback,
                                               CancellationToken cancellationToken)
        {
            var kpsData = getPreviewKpsData(workingBeatmap, ruleset, mods, playableBeatmap, cancellationToken);

            if (kpsData.Values.Count == 0)
                return fallback;

            double anchorTime = getPreviewKpsAnchorTime(playableBeatmap, kpsData);
            return Math.Max(fallback, anchorTime - 1000);
        }

        private PreviewKpsData getPreviewKpsData(IWorkingBeatmap workingBeatmap, RulesetInfo ruleset, IReadOnlyList<Mod> mods, IBeatmap playableBeatmap,
                                                 CancellationToken cancellationToken)
        {
            if (ezAnalysisCache != null && workingBeatmap.BeatmapInfo != null)
            {
                try
                {
                    var analysis = ezAnalysisCache.GetAnalysisAsync(workingBeatmap.BeatmapInfo, ruleset, mods, cancellationToken).GetAwaiter().GetResult();

                    if (analysis?.KpsList is { Count: > 0 } storedKpsList)
                        return new PreviewKpsData(storedKpsList, PreviewKpsSource.Analysis);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // 回退到本地快速计算。
                }
            }

            var (_, _, coarseKpsList) = OptimizedBeatmapCalculator.GetKpsCoarse(playableBeatmap, buckets: 64);
            return new PreviewKpsData(coarseKpsList, PreviewKpsSource.Coarse);
        }

        private static double getPreviewKpsAnchorTime(IBeatmap beatmap, PreviewKpsData kpsData)
        {
            if (kpsData.Values.Count == 0 || beatmap.HitObjects.Count == 0)
                return 0;

            int maxIndex = 0;
            double maxValue = kpsData.Values[0];

            for (int i = 1; i < kpsData.Values.Count; i++)
            {
                if (kpsData.Values[i] > maxValue)
                {
                    maxValue = kpsData.Values[i];
                    maxIndex = i;
                }
            }

            double songStart = beatmap.HitObjects[0].StartTime;
            double songEnd = beatmap.HitObjects[^1].StartTime;

            if (kpsData.Source == PreviewKpsSource.Analysis)
            {
                double bpm = beatmap.BeatmapInfo.BPM;
                double interval = 240000.0 / bpm;
                double estimatedIntervals = (songEnd / interval) + 1;

                if (estimatedIntervals > OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS)
                {
                    int lastIndex = (int)estimatedIntervals - 1;
                    int sampledIndex = (int)((long)maxIndex * lastIndex / (OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS - 1));
                    return sampledIndex * interval;
                }

                return maxIndex * interval;
            }

            double duration = Math.Max(1, songEnd - songStart);
            double bucketDuration = duration / kpsData.Values.Count;
            return songStart + (maxIndex + 1) * bucketDuration;
        }

        private readonly record struct PreviewKpsData(IReadOnlyList<double> Values, PreviewKpsSource Source);

        private enum PreviewKpsSource
        {
            Analysis,
            Coarse,
        }

        private static string formatTime(double time)
        {
            TimeSpan span = TimeSpan.FromMilliseconds(Math.Max(0, time));
            return $"{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
        }

        private void createPreviewModeButtons()
        {
            foreach (EzBeatmapPreviewMode mode in mania_preview_modes)
            {
                previewModeButtons[mode] = new PreviewModeButton
                {
                    Width = preview_mode_button_width,
                    Height = preview_mode_button_height,
                    Text = mode.GetLocalisableDescription(),
                    Action = () => setPreviewMode(mode)
                };
            }
        }

        private void setPreviewMode(EzBeatmapPreviewMode mode)
        {
            if (previewMode.Value == mode)
                return;

            previewMode.Value = mode;
        }

        private void onPreviewModeChanged()
        {
            updatePreviewModeButtons();

            nextDynamicLoopStartTime = 0;
            previewClock.Stop();

            if (!dynamicMode || !expanded || drawableRuleset == null)
            {
                updateProgressDisplay(previewClock.CurrentTime);
                return;
            }

            playbackStartTime = Math.Clamp(previewClock.CurrentTime, beatmapMinTime, beatmapMaxTime);
            previewClock.Seek(playbackStartTime);
            previewClock.Start();
            updateProgressDisplay(playbackStartTime);
        }

        private void updatePreviewModeButtons()
        {
            previewModeButtonList.Clear(false);

            foreach (EzBeatmapPreviewMode mode in getAvailablePreviewModes())
                previewModeButtonList.Add(previewModeButtons[mode]);

            EzBeatmapPreviewMode highlightedMode = getHighlightedPreviewMode();

            foreach (var pair in previewModeButtons)
                pair.Value.Selected = pair.Key == highlightedMode;
        }

        private IReadOnlyList<EzBeatmapPreviewMode> getAvailablePreviewModes() => isManiaRuleset(currentRuleset) ? mania_preview_modes : shared_preview_modes;

        private EzBeatmapPreviewMode getHighlightedPreviewMode()
        {
            IReadOnlyList<EzBeatmapPreviewMode> availableModes = getAvailablePreviewModes();

            if (availableModes.Contains(previewMode.Value))
                return previewMode.Value;

            return dynamicMode ? EzBeatmapPreviewMode.Dynamic : EzBeatmapPreviewMode.Static;
        }

        private static bool isManiaRuleset(RulesetInfo? ruleset) => ruleset?.OnlineID == 3;

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

        private partial class PreviewModeButton : OsuButton
        {
            private Color4 textColour = Color4.White;
            private bool selected;

            public bool Selected
            {
                set
                {
                    if (selected == value)
                        return;

                    selected = value;
                    updateVisualState();
                }
            }

            public Color4 TextColour
            {
                set
                {
                    textColour = value;
                    SpriteText.FadeColour(textColour, 120, Easing.OutQuint);
                }
            }

            public PreviewModeButton()
            {
                Size = new Vector2(108, 28);
                Content.CornerRadius = 6;
            }

            protected override float HoverLayerFinalAlpha => 0.06f;

            protected override void LoadComplete()
            {
                base.LoadComplete();
                SpriteText.Colour = textColour;
                updateVisualState();
            }

            private void updateVisualState()
            {
                BackgroundColour = selected ? Color4.CornflowerBlue.Opacity(0.85f) : Color4.Black.Opacity(0.5f);
                TextColour = selected ? Color4.White : Color4.White.Opacity(0.9f);
            }

            protected override SpriteText CreateText() => new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold)
            };
        }
    }
}
