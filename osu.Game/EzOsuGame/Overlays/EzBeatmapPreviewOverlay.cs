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
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
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
        private const float min_panel_width = 360;
        private const float max_panel_width = 580;
        private const float min_panel_height = 180;
        private const float max_panel_height = 800;
        private const float bottom_controls_height = 56;
        private const float resize_handle_height = 10;
        private const float resize_handle_width = 10;
        private const float preview_mode_button_width = 90;
        private const float preview_mode_list_width = preview_mode_button_width;
        private const float preview_mode_button_height = 30;
        private const float preview_mode_button_spacing = 6;

        private const float dynamic_preview_duration = 10000;
        private const float dynamic_preview_repeat_delay = 500;
        private const int change_debounce = 50;

        // private static bool rememberedExpanded;

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
        private readonly Bindable<EzBeatmapPreviewMode> previewMode = new Bindable<EzBeatmapPreviewMode>();

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
        private Drawable? pendingPreviewRoot;
        private DrawableRuleset? drawableRuleset;

        [Resolved(CanBeNull = true)]
        private ISkin? skin { get; set; }

        private IBeatmap? playableBeatmap;
        private RulesetInfo? currentRuleset;

        private bool selectionLoadInProgress;
        private long selectionEventVersion;
        private int currentRulesetOnlineId = -1;
        private string currentBeatmapHash = string.Empty;

        // 指示在隐藏时是否应立即释放所有引用（默认 true：隐藏即释放）。
        private bool releaseOnHide = true;

        private bool dynamicMode => previewMode.Value == EzBeatmapPreviewMode.Dynamic;

        private bool expanded;

        public readonly Bindable<bool> ExpandedState = new Bindable<bool>();

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
                                Text = "Load Time: 0ms",
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
                                        Anchor = Anchor.BottomLeft,
                                        Origin = Anchor.BottomLeft,
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
                                                Anchor = Anchor.BottomLeft,
                                                Origin = Anchor.BottomLeft,
                                                Size = new Vector2(640, 480),
                                            },
                                            stateText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Font = OsuFont.Default.With(size: 20, weight: FontWeight.SemiBold),
                                                Colour = Color4.White,
                                                Text = "No Load"
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
            // 初始化对外可观察的展开状态
            ExpandedState.Value = expanded;
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
                collapse();
            else
                expand();
        }

        private void expand()
        {
            if (expanded)
                return;

            expanded = true;
            ExpandedState.Value = true;

            if (drawableRuleset == null && playableBeatmap != null && currentRuleset != null)
                selectionDirty = true;

            panelContainer.ClearTransforms();
            panelContainer.MoveTo(new Vector2(panel_left_margin, 14));
            panelContainer.FadeIn(160, Easing.OutQuint);
            panelContainer.MoveToY(0, 160, Easing.OutQuint);

            if (selectionDirty)
                scheduleSelectionLoad();
        }

        private void collapse()
        {
            if (!expanded)
                return;

            expanded = false;
            ExpandedState.Value = false;
            heightResizeActive = false;
            widthResizeActive = false;
            selectionLoadInProgress = false;
            nextDynamicLoopStartTime = 0;
            previewClock.Stop();
            cancelScheduledSelectionLoad();
            cancelPendingLoad();
            disposePreviewResources();

            if (releaseOnHide)
            {
                // 立即释放对谱面/规则集等的大对象引用，降低内存占用。
                playableBeatmap = null;
                currentRuleset = null;
                currentRulesetOnlineId = -1;
                currentBeatmapHash = string.Empty;
                selectionDirty = false;
            }
            else
            {
                selectionDirty = playableBeatmap != null && currentRuleset != null;
            }

            setStateText("No Load");

            panelContainer.ClearTransforms();
            panelContainer.FadeOut(160, Easing.OutQuint);
            panelContainer.MoveToY(14, 160, Easing.OutQuint);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="playableBeatmap">传入mod转换结果</param>
        /// <param name="ruleset">用于判断预览开始时间</param>
        /// <param name="forceReload">强制刷新，附注mod设置变化时的判断</param>
        public void UpdateSelection(IBeatmap? playableBeatmap, RulesetInfo ruleset, bool forceReload = false)
        {
            if (playableBeatmap == null)
                return;

            this.playableBeatmap = playableBeatmap;
            string beatmapHash = playableBeatmap.BeatmapInfo.Hash;
            bool beatmapSame = currentRulesetOnlineId == ruleset.OnlineID && currentBeatmapHash == beatmapHash;

            bool unchanged = !forceReload && beatmapSame;

            currentRuleset = ruleset;
            updatePreviewModeButtons();

            if (unchanged)
            {
                if (drawableRuleset != null) return;

                selectionDirty = true;

                if (expanded)
                    scheduleSelectionLoad();

                return;
            }

            currentRulesetOnlineId = ruleset.OnlineID;
            currentBeatmapHash = beatmapHash;
            lastSelectionEventTime = Time.Current;
            selectionEventVersion++;

            selectionDirty = true;

            if (!expanded)
                return;

            // 对于切换谱面（beatmap 改变）立即取消当前加载并立刻加载；仅 mods 更改仍使用去抖。
            if (!beatmapSame)
            {
                cancelScheduledSelectionLoad();
                cancelPendingLoad();
                loadPendingSelection(selectionEventVersion);
            }
            else
            {
                scheduleSelectionLoad();
            }
        }

        public void SuspendForScreenExit()
        {
            selectionLoadInProgress = false;
            cancelScheduledSelectionLoad();
            cancelPendingLoad();

            disposePreviewResources();
            // 立即释放所有引用，避免切换屏幕时占用内存。
            playableBeatmap = null;
            currentRuleset = null;
            selectionDirty = false;
            currentRulesetOnlineId = -1;
            currentBeatmapHash = string.Empty;
            updatePreviewModeButtons();

            // rememberedExpanded = expanded;
        }

        // public void RestoreRememberedState()
        // {
        //     if (rememberedExpanded)
        //         expand();
        //     else
        //         collapse();
        //
        //     updatePreviewModeButtons();
        // }

        private void beginLoadPendingSelectionIfRequired()
        {
            if (!expanded || selectionLoadInProgress || !selectionDirty)
                return;

            loadPendingSelection(selectionEventVersion);
        }

        private void scheduleSelectionLoad()
        {
            cancelScheduledSelectionLoad();

            if (!expanded)
                return;

            beginLoadPendingSelectionIfRequired();
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

            if (playableBeatmap == null || currentRuleset == null)
                return;

            selectionLoadInProgress = true;
            scheduledSelectionLoad = null;
            selectionDirty = false;
            lastLoadTimeMs = 0;

            // 立即释放旧的预览资源，防止在切换谱面时短暂显示旧画面或跳转。
            disposePreviewResources();
            previewClock.Stop();
            updateProgressDisplay(0);

            cancelPendingLoad();
            previewLoadCancellation = new CancellationTokenSource();
            var token = previewLoadCancellation.Token;

            var ruleset = currentRuleset;

            // local copy to avoid capturing mutable/large fields in the task closure
            var localPlayable = playableBeatmap;

            double loadStartTime = lastSelectionEventTime > 0 ? lastSelectionEventTime : Time.Current;

            Task.Run<LoadedPreviewData?>(() =>
            {
                token.ThrowIfCancellationRequested();

                if (localPlayable == null)
                    return null;

                double maxTime = localPlayable.BeatmapInfo.Length;
                double startTime = computeDefaultStartTime(localPlayable, ruleset!, 0);

                return new LoadedPreviewData(eventVersion, localPlayable, ruleset!, startTime, 0, maxTime);
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

                    setupDrawableRulesetAsync(result.Value.Version, result.Value.PlayableBeatmap, result.Value.RulesetInfo, token);

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

                // 清理所有引用
                playableBeatmap = null;
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

        private void setupDrawableRulesetAsync(long eventVersion, IBeatmap playableBeatmap, RulesetInfo rulesetInfo, CancellationToken cancellationToken)
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

            pendingPreviewRoot = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, skin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new NonInteractivePreviewContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = newDrawableRuleset,
                }
            };

            LoadComponentAsync(pendingPreviewRoot, loaded =>
            {
                if (ReferenceEquals(pendingPreviewRoot, loaded))
                    pendingPreviewRoot = null;

                if (cancellationToken.IsCancellationRequested || eventVersion != selectionEventVersion || !expanded)
                {
                    loaded.Dispose();
                    return;
                }

                stageScaleContainer.Child = loaded;
                drawableRuleset = newDrawableRuleset;
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
            // First, clear the visual container (this disposes children on the update thread).
            if (stageScaleContainer.Count > 0)
            {
                if (!IsLoaded)
                    stageScaleContainer.Clear(true);
                else
                    Schedule(() => stageScaleContainer.Clear(true));
            }

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

        private double computeDefaultStartTime(IBeatmap playableBeatmap, RulesetInfo ruleset, double fallback)
        {
            // Mania 模式使用谱面 Metadata.PreviewTime 作为预览起点（无效时回退到 fallback）。
            if (ruleset.OnlineID == 3)
            {
                int previewTime = playableBeatmap.Metadata.PreviewTime;
                if (previewTime <= 0)
                    return fallback;

                return previewTime;
            }

            // 非 mania 模式：使用谱面 Kiai 起点作为预览时间，若无 Kiai 则使用第一个 HitObject 的起始时间，仍无则回退到 fallback。
            double kiaiStart = getKiaiStartTime(playableBeatmap);
            if (!double.IsNaN(kiaiStart))
                return kiaiStart;

            return fallback;
        }

        private static double getKiaiStartTime(IBeatmap beatmap)
        {
            try
            {
                var cp = beatmap.ControlPointInfo;

                // EffectPoints typically ordered by time; find first with Kiai enabled.
                foreach (var e in cp.EffectPoints)
                {
                    if (e.KiaiMode)
                        return e.Time;
                }
            }
            catch
            {
                // If any API differs, fall back silently.
            }

            return double.NaN;
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
            IBeatmap PlayableBeatmap,
            RulesetInfo RulesetInfo,
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
