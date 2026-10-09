// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzBeatmapPreviewOverlay : CompositeDrawable
    {
        private const float max_panel_width_ratio = 1f;
        private const float max_panel_height_ratio = 1f;

        private const float min_panel_width = 360;
        private const float min_panel_height = 160;

        private const float panel_width_ratio = 0.6f;
        private const float panel_background_focus_opacity = 0.92f;

        private const float button_height = 30;
        private const float button_width = 90;

        // 命中范围，需大于框架 ClickDragDistance（10）。不参与边框外观。
        private const float resize_grip_extent = 16f;

        private const float resize_border_thickness = 2.5f;
        private static readonly Color4 resize_border_idle_colour = new Color4(255, 255, 255, 64);
        private static readonly Color4 resize_border_hover_colour = new Color4(255, 255, 255, 128);

        private const float dynamic_preview_duration = 10000;
        private const float dynamic_preview_repeat_delay = 500;
        private const int change_debounce = 50;
        private const int panel_transition_duration = 160;

        private readonly StopwatchClock previewClock = new StopwatchClock();
        private readonly FramedClock framedPreviewClock;
        private readonly Bindable<EzBeatmapPreviewMode> previewMode = new Bindable<EzBeatmapPreviewMode>();

        private readonly EzAcrylicPanelBackground panelBackground;
        private readonly Container panelContainer;
        private readonly Container stageViewport;
        private readonly Container stageScaleContainer;
        private readonly Container stageAreaContainer;
        private readonly Container bottomControlsContainer;
        private readonly ProgressBar timeline;
        private readonly OsuSpriteText progressText;
        private readonly OsuSpriteText stateText;
        private readonly OsuSpriteText loadTimeText;
        private readonly FillFlowContainer previewModeButtonList;
        private readonly Dictionary<EzBeatmapPreviewMode, PreviewModeButton> previewModeButtons = new Dictionary<EzBeatmapPreviewMode, PreviewModeButton>();
        private readonly Container topResizeGrip;
        private readonly Container rightResizeGrip;
        private readonly Box topResizeBorder;
        private readonly Box rightResizeBorder;

        private bool heightResizeActive;
        private bool widthResizeActive;
        private bool topGripHovered;
        private bool rightGripHovered;
        private bool panelWidthManuallyAdjusted;
        private bool selectionDirty;
        private float dragStartPanelWidth;
        private float dragStartPanelHeight;

        private float panelWidth;
        private float panelHeight;

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
        private IManiaStaticPreviewRenderer? maniaStaticRenderer;
        private PreviewDensityController? densityController;
        private Bindable<double> previewDensity = null!;
        private Bindable<EzBeatmapPreviewMode> sharedPreviewModeConfig = null!;
        private Bindable<EzBeatmapPreviewMode> maniaPreviewModeConfig = null!;

        [Resolved(CanBeNull = true)]
        private ISkin? skin { get; set; }

        private IBeatmap? playableBeatmap;
        private RulesetInfo? currentRuleset;

        private bool selectionLoadInProgress;
        private long selectionEventVersion;
        private int currentRulesetOnlineId = -1;
        private string currentBeatmapHash = string.Empty;

        private IReadOnlyList<EzBeatmapPreviewMode>? lastPreviewModeList;

        private bool panelTransitionActive;

        private bool dynamicMode => previewMode.Value == EzBeatmapPreviewMode.Dynamic;

        private bool customManiaStaticMode => EzBeatmapPreviewModes.IsManiaRuleset(currentRuleset)
                                              && (previewMode.Value == EzBeatmapPreviewMode.StaticFullMap || previewMode.Value == EzBeatmapPreviewMode.StaticScroll);

        private bool fullMapMode => previewMode.Value == EzBeatmapPreviewMode.StaticFullMap;
        private bool scrollMode => previewMode.Value == EzBeatmapPreviewMode.StaticScroll;

        private bool expanded;
        private bool fullMapFocusActive;
        private bool songSelectBackgroundRevealed;
        private float focusSavedPanelWidth;
        private float focusSavedPanelHeight;

        public readonly Bindable<bool> ExpandedState = new Bindable<bool>();
        public readonly Bindable<bool> FullMapFocusState = new Bindable<bool>();

        public Func<float>? DefaultPanelRightEdgeInScreenSpace { get; set; }

        public EzBeatmapPreviewOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Padding = new MarginPadding { Bottom = ScreenFooter.HEIGHT };

            framedPreviewClock = new FramedClock(previewClock);

            InternalChildren = new Drawable[]
            {
                panelContainer = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Masking = true,
                    CornerRadius = EzAcrylicStyle.CORNER_RADIUS_DISPLAY,
                    Alpha = 0,
                    RelativeSizeAxes = Axes.None,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            panelBackground = new EzAcrylicPanelBackground(Color4.Black.Opacity(panel_background_focus_opacity)),
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
                                Position = new Vector2(8, resize_grip_extent + 8),
                                Width = button_width,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 6),
                            },
                            stageAreaContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding
                                {
                                    Top = resize_grip_extent,
                                    Bottom = button_height,
                                    Left = button_width + 16,
                                    Right = resize_grip_extent
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
                                                Alpha = 0,
                                                // Colour = Color4.Black.Opacity(0.4f)
                                            },
                                            stageScaleContainer = new Container
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
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
                            bottomControlsContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = button_height,
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Padding = new MarginPadding
                                {
                                    Top = 10,
                                    Bottom = 10,
                                    Left = button_width + 16,
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
                            topResizeGrip = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = resize_grip_extent,
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Child = topResizeBorder = new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = resize_border_thickness,
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Colour = resize_border_idle_colour
                                }
                            },
                            rightResizeGrip = new Container
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = resize_grip_extent,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Child = rightResizeBorder = new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = resize_border_thickness,
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Colour = resize_border_idle_colour
                                }
                            }
                        }
                    }
                }
            };

            timeline.OnSeek = onTimelineSeek;
            timeline.OnCommit = onTimelineSeek;

            createPreviewModeButtons();
            // 初始化对外可观察的状态
            ExpandedState.Value = expanded;
            FullMapFocusState.Value = fullMapFocusActive;
            updatePreviewModeButtons();
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            sharedPreviewModeConfig = ezConfig.GetBindable<EzBeatmapPreviewMode>(Ez2Setting.BeatmapPreviewMode);
            maniaPreviewModeConfig = ezConfig.GetBindable<EzBeatmapPreviewMode>(Ez2Setting.BeatmapPreviewModeMania);
            previewDensity = ezConfig.GetBindable<double>(Ez2Setting.BeatmapPreviewDensity);
            densityController = new PreviewDensityController(previewDensity);
            applyPreviewModeForCurrentRuleset();
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
            panelBackground.AcrylicCaptureVisible = true;
            panelBackground.SyncAcrylicCaptureState();

            if (drawableRuleset == null && playableBeatmap != null && currentRuleset != null)
                selectionDirty = true;

            panelContainer.ClearTransforms();
            beginPanelTransition();
            panelContainer.MoveTo(new Vector2(0, 14));
            panelContainer.FadeIn(panel_transition_duration, Easing.OutQuint).Finally(_ => finishPanelTransition());
            panelContainer.MoveToY(0, panel_transition_duration, Easing.OutQuint);

            updatePreviewControlsLayout();

            if (selectionDirty)
                scheduleSelectionLoad();
        }

        private void collapse()
        {
            if (!expanded)
                return;

            expanded = false;
            ExpandedState.Value = false;
            panelBackground.AcrylicCaptureVisible = false;
            panelBackground.SyncAcrylicCaptureState();
            heightResizeActive = false;
            widthResizeActive = false;
            selectionLoadInProgress = false;
            nextDynamicLoopStartTime = 0;
            previewClock.Stop();
            cancelScheduledSelectionLoad();
            cancelPendingLoad();
            disposePreviewResources();

            playableBeatmap = null;
            currentRuleset = null;
            currentRulesetOnlineId = -1;
            currentBeatmapHash = string.Empty;
            selectionDirty = false;

            setStateText("No Load");

            panelContainer.ClearTransforms();
            beginPanelTransition();
            panelContainer.FadeOut(panel_transition_duration, Easing.OutQuint).Finally(_ => finishPanelTransition());
            panelContainer.MoveToY(14, panel_transition_duration, Easing.OutQuint);
        }

        private void beginPanelTransition()
        {
            panelTransitionActive = true;
            panelContainer.AlwaysPresent = true;
        }

        private void finishPanelTransition()
        {
            panelTransitionActive = false;
            panelContainer.AlwaysPresent = false;
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <param name="playableBeatmap">传入mod转换结果</param>
        /// <param name="ruleset">用于判断预览开始时间</param>
        /// <param name="forceReload">强制刷新，附注mod设置变化时的判断</param>
        public void UpdateSelection(IBeatmap? playableBeatmap, RulesetInfo ruleset, bool forceReload = false)
        {
            if (playableBeatmap == null || !expanded)
                return;

            this.playableBeatmap = playableBeatmap;
            string beatmapHash = playableBeatmap.BeatmapInfo.Hash;
            bool rulesetChanged = currentRulesetOnlineId != ruleset.OnlineID;
            bool beatmapSame = currentRulesetOnlineId == ruleset.OnlineID && currentBeatmapHash == beatmapHash;

            bool unchanged = !forceReload && beatmapSame;

            currentRuleset = ruleset;

            if (rulesetChanged)
                applyPreviewModeForCurrentRuleset();

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

            // 切歌 / mod 变化统一走去抖。快速切歌时中间歌曲的 scheduledSelectionLoad 被取消，
            // 避免为每首过渡歌曲都启动昂贵的 DrawableRuleset 创建 + replay 模拟。
            if (!beatmapSame)
            {
                cancelScheduledSelectionLoad();
                cancelPendingLoad();
                // 立即释放旧预览资源，避免去抖期间显示旧谱面画面。
                disposePreviewResources();
                previewClock.Stop();
                updateProgressDisplay(0);
            }

            scheduleSelectionLoad();
        }

        /// <summary>
        /// Match song select background-hold reveal: hide all preview UI while only the beatmap background remains visible.
        /// </summary>
        public void SetSongSelectBackgroundRevealed(bool revealed)
        {
            if (songSelectBackgroundRevealed == revealed)
                return;

            songSelectBackgroundRevealed = revealed;

            if (revealed)
            {
                if (fullMapFocusActive)
                    setFullMapFocusState(false);

                ClearTransforms();
                this.FadeOut(200, Easing.OutQuint);
                this.ScaleTo(1.2f, 600, Easing.OutQuint);
                return;
            }

            ClearTransforms();
            this.FadeIn(500, Easing.OutQuint);
            this.ScaleTo(1f, 500, Easing.OutQuint);
        }

        public void SuspendForScreenExit()
        {
            SetSongSelectBackgroundRevealed(false);

            if (expanded)
            {
                expanded = false;
                ExpandedState.Value = false;
                panelBackground.AcrylicCaptureVisible = false;
                panelBackground.SyncAcrylicCaptureState();
            }

            selectionLoadInProgress = false;
            cancelScheduledSelectionLoad();
            cancelPendingLoad();

            disposePreviewResources();
            playableBeatmap = null;
            currentRuleset = null;
            selectionDirty = false;
            currentRulesetOnlineId = -1;
            currentBeatmapHash = string.Empty;
            updatePreviewModeButtons();

            panelContainer.ClearTransforms();
            panelContainer.AlwaysPresent = false;
            panelTransitionActive = false;
            panelContainer.Alpha = 0;
            panelContainer.Y = 14;
            lastAppliedPanelY = 14;
        }

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

            // 去抖：延迟 change_debounce 毫秒后再启动加载。
            // 快速切歌时，中间歌曲的 ScheduledDelegate 被后续 cancelScheduledSelectionLoad 取消，
            // 不会启动昂贵的 DrawableRuleset 创建 + replay 模拟。
            scheduledSelectionLoad = Scheduler.AddDelayed(() =>
            {
                scheduledSelectionLoad = null;
                beginLoadPendingSelectionIfRequired();
            }, change_debounce);
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

            disposePreviewResources();
            previewClock.Stop();
            updateProgressDisplay(0);

            cancelPendingLoad();
            previewLoadCancellation = new CancellationTokenSource();
            var token = previewLoadCancellation.Token;

            double loadStartTime = lastSelectionEventTime > 0 ? lastSelectionEventTime : Time.Current;

            beatmapMinTime = 0;
            beatmapMaxTime = Math.Max(playableBeatmap.BeatmapInfo.Length, beatmapMinTime + 1);
            playbackStartTime = computeDefaultStartTime(playableBeatmap, currentRuleset, 0);

            lastLoadTimeMs = Time.Current - loadStartTime;
            lastDisplayedLoadTimeMs = -1;

            setupDrawableRulesetAsync(eventVersion, playableBeatmap, currentRuleset, token);

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

            if (!expanded && !panelTransitionActive)
                return;

            updateResizeGripHover();

            if (lastLoadTimeMs > 0)
            {
                double displayed = Math.Round(lastLoadTimeMs);

                if (displayed != lastDisplayedLoadTimeMs)
                {
                    loadTimeText.Text = $"加载: {displayed:F0}ms";
                    lastDisplayedLoadTimeMs = displayed;
                }
            }

            float targetPanelY = expanded ? 0 : 14;
            float displayPanelWidth;
            float displayPanelHeight;

            if (!fullMapFocusActive)
            {
                panelWidth = panelWidthManuallyAdjusted
                    ? clampPanelWidth(panelWidth <= 0 ? getDefaultPanelWidth() : panelWidth)
                    : getDefaultPanelWidth();

                if (panelHeight <= 0)
                    panelHeight = getMaxPanelHeight();

                panelHeight = clampPanelHeight(panelHeight);
                displayPanelWidth = panelWidth;
                displayPanelHeight = panelHeight;
            }
            else
            {
                // 按住全屏铺满 footer 以上的区域，不受面板最大宽高比例限制。
                displayPanelWidth = ChildSize.X;
                displayPanelHeight = ChildSize.Y;
                targetPanelY = 0;
            }

            if (displayPanelWidth != lastAppliedPanelWidth)
            {
                panelContainer.Width = displayPanelWidth;
                lastAppliedPanelWidth = displayPanelWidth;
            }

            if (displayPanelHeight != lastAppliedPanelHeight)
            {
                panelContainer.Height = displayPanelHeight;
                lastAppliedPanelHeight = displayPanelHeight;
            }

            panelContainer.X = 0;

            if (targetPanelY != lastAppliedPanelY)
            {
                panelContainer.Y = targetPanelY;
                lastAppliedPanelY = targetPanelY;
            }

            float viewportWidth = stageViewport.DrawWidth;
            float viewportHeight = stageViewport.DrawHeight;

            if (!customManiaStaticMode && (viewportWidth != lastViewportWidth || viewportHeight != lastViewportHeight))
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

            if (customManiaStaticMode && maniaStaticRenderer != null)
            {
                maniaStaticRenderer.SetDensity((float)previewDensity.Value);
                return;
            }

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
            if (!expanded || fullMapFocusActive)
                return false;

            // 拖拽在移动超过阈值后才触发。用按下位置判定，避免先离开细边才开始检测。
            bool inWidthHandle = isWithinWidthResizeHandle(e.ScreenSpaceMouseDownPosition);
            bool inHeightHandle = isWithinHeightResizeHandle(e.ScreenSpaceMouseDownPosition);

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
            if (!expanded || fullMapFocusActive)
                return base.OnMouseDown(e);

            if (isWithinHeightResizeHandle(e.ScreenSpaceMousePosition) || isWithinWidthResizeHandle(e.ScreenSpaceMousePosition))
                return true;

            if (base.OnMouseDown(e))
                return true;

            if (stageViewport.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                setFullMapFocusState(true);
                return true;
            }

            return isWithinPanel(e.ScreenSpaceMousePosition);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (fullMapFocusActive)
                setFullMapFocusState(false);

            base.OnMouseUp(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (!expanded)
                return base.OnScroll(e);

            var panelQuad = panelContainer.ScreenSpaceDrawQuad;
            Vector2 mouse = e.ScreenSpaceMousePosition;

            // Check if scroll is within panel bounds
            if (mouse.X < panelQuad.TopLeft.X || mouse.X > panelQuad.TopRight.X || mouse.Y < panelQuad.TopLeft.Y || mouse.Y > panelQuad.BottomLeft.Y)
                return base.OnScroll(e);

            // Alt + Scroll: 调整滚动规则集的时间跨度（note 疏密）
            if (e.CurrentState.Keyboard.AltPressed && customManiaStaticMode)
            {
                float before = (float)previewDensity.Value;
                float after = Math.Clamp(before + Math.Sign(e.ScrollDelta.Y) * 0.05f, 0.1f, 5f);

                if (Math.Abs(after - before) > 0.001f)
                {
                    previewDensity.Value = after;
                    maniaStaticRenderer?.SetDensity(after);
                    string densityText = $"{after:F2}x";
                    setStateText(densityText);

                    Scheduler.AddDelayed(() =>
                    {
                        if (stateText.Text == densityText)
                            setStateText(string.Empty);
                    }, 1000);

                    return true;
                }
            }

            if (previewMode.Value == EzBeatmapPreviewMode.StaticScroll
                && maniaStaticRenderer is StaticScrollPreviewRenderer scrollRenderer)
            {
                scrollRenderer.AdjustScroll(-e.ScrollDelta.Y * 48f);
                syncTimelineFromScroll(scrollRenderer.GetScrollProgress());
                return true;
            }

            if (drawableRuleset == null)
                return base.OnScroll(e);

            if (e.CurrentState.Keyboard.AltPressed
                && drawableRuleset is IDrawableScrollingRuleset scrolling
                && densityController != null
                && densityController.TryAdjust(scrolling, Math.Sign(e.ScrollDelta.Y), out float displayDensity))
            {
                string densityText = $"{displayDensity:F2}x";
                setStateText(densityText);

                Scheduler.AddDelayed(() =>
                {
                    if (stateText.Text == densityText)
                        setStateText(string.Empty);
                }, 1000);

                return true;
            }

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
                sharedPreviewModeConfig.UnbindAll();
                maniaPreviewModeConfig.UnbindAll();
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

            if (EzBeatmapPreviewModes.IsManiaRuleset(rulesetInfo)
                && (previewMode.Value == EzBeatmapPreviewMode.StaticFullMap || previewMode.Value == EzBeatmapPreviewMode.StaticScroll))
            {
                setupManiaStaticPreview(playableBeatmap);
                return;
            }

            var ruleset = rulesetInfo.CreateInstance();
            var newDrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap);

            stageScaleContainer.RelativeSizeAxes = Axes.None;
            stageScaleContainer.Size = new Vector2(640, 480);

            newDrawableRuleset.Clock = framedPreviewClock;
            newDrawableRuleset.FrameStablePlayback = false;
            newDrawableRuleset.Playfield.DisplayJudgements.Value = false;

            pendingPreviewRoot = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, skin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new NonHandleContainer
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
                maniaStaticRenderer = null;

                // Capture after the ruleset has finished loading so TargetTimeRange / TimeRange are initialised.
                Schedule(() =>
                {
                    if (drawableRuleset is IDrawableScrollingRuleset scrollingRuleset)
                        densityController?.CaptureBaseline(scrollingRuleset);
                });
            }, cancellationToken);
        }

        private float getDefaultPanelWidth()
        {
            float preferred = DrawWidth * panel_width_ratio;

            if (DefaultPanelRightEdgeInScreenSpace != null)
            {
                float targetRightEdge = ToLocalSpace(new Vector2(DefaultPanelRightEdgeInScreenSpace(), 0)).X;

                if (!float.IsNaN(targetRightEdge) && !float.IsInfinity(targetRightEdge))
                    preferred = targetRightEdge;
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

            if (scrollMode)
                syncScrollRendererFromTime(clamped);

            updateProgressDisplay(clamped);
        }

        private void onTimelineSeek(double time)
        {
            if (scrollMode)
            {
                seekTo(time);
                return;
            }

            seekTo(time, dynamicMode);
        }

        private void syncScrollRendererFromTime(double time)
        {
            if (maniaStaticRenderer is not StaticScrollPreviewRenderer scroll)
                return;

            float progress = beatmapMaxTime > beatmapMinTime
                ? (float)((time - beatmapMinTime) / (beatmapMaxTime - beatmapMinTime))
                : 0;

            scroll.SetScrollProgress(progress);
        }

        private void syncTimelineFromScroll(float progress)
        {
            double time = beatmapMaxTime <= beatmapMinTime
                ? 0
                : beatmapMinTime + progress * (beatmapMaxTime - beatmapMinTime);

            previewClock.Stop();
            previewClock.Seek(time);
            updateProgressDisplay(time);
        }

        private void updatePreviewControlsLayout()
        {
            bool showTimeline = expanded && !fullMapFocusActive && !fullMapMode;

            bottomControlsContainer.Height = showTimeline ? button_height : 0;
            bottomControlsContainer.Alpha = showTimeline ? 1 : 0;

            stageAreaContainer.Padding = new MarginPadding
            {
                Top = resize_grip_extent,
                Bottom = showTimeline ? button_height : 8,
                Left = button_width + 16,
                Right = resize_grip_extent
            };
        }

        private void updateProgressDisplay(double time)
        {
            if (fullMapMode)
                return;

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
            setFullMapFocusState(false);

            drawableRuleset = null;
            maniaStaticRenderer = null;

            // densityController 由依赖加载器创建；预览宿主可能在加载前就被拆除，此时它仍为 null。
            densityController?.DisposeSession();

            // Clear synchronously: a scheduled Clear can run after a newer preview is mounted (e.g. switching key count).
            if (stageScaleContainer.Count > 0)
                stageScaleContainer.Clear(true);
        }

        private float getMaxPanelWidth() => DrawWidth * max_panel_width_ratio;

        private float getMaxPanelHeight()
        {
            float bodyHeight = DrawHeight - ScreenFooter.HEIGHT - FilterControl.HEIGHT_FROM_SCREEN_TOP;
            return Math.Max(0, bodyHeight) * max_panel_height_ratio;
        }

        private float clampPanelWidth(float width)
        {
            float maxWidth = getMaxPanelWidth();
            return Math.Clamp(width, min_panel_width, Math.Max(min_panel_width, maxWidth));
        }

        private float clampPanelHeight(float height)
        {
            float maxHeight = getMaxPanelHeight();
            return Math.Clamp(height, min_panel_height, Math.Max(min_panel_height, maxHeight));
        }

        private bool isWithinPanel(Vector2 screenSpacePosition) => panelContainer.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

        private void updateResizeGripHover()
        {
            bool allowHover = expanded && !fullMapFocusActive;
            setResizeBorderColour(topResizeBorder, allowHover && topResizeGrip.IsHovered, ref topGripHovered);
            setResizeBorderColour(rightResizeBorder, allowHover && rightResizeGrip.IsHovered, ref rightGripHovered);
        }

        private static void setResizeBorderColour(Box border, bool hovered, ref bool storedHovered)
        {
            if (storedHovered == hovered)
                return;

            storedHovered = hovered;
            border.FadeColour(hovered ? resize_border_hover_colour : resize_border_idle_colour, 80, Easing.OutQuint);
        }

        private bool isWithinWidthResizeHandle(Vector2 screenSpacePosition)
            => rightResizeGrip.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

        private bool isWithinHeightResizeHandle(Vector2 screenSpacePosition)
            => topResizeGrip.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition);

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
            foreach (EzBeatmapPreviewMode mode in EzBeatmapPreviewModes.AllUiModes)
            {
                previewModeButtons[mode] = new PreviewModeButton
                {
                    Width = button_width,
                    Height = button_height,
                    Text = mode.GetLocalisableDescription(),
                    Action = () => setPreviewMode(mode)
                };
            }
        }

        private void setPreviewMode(EzBeatmapPreviewMode mode)
        {
            EzBeatmapPreviewMode validated = EzBeatmapPreviewModes.ValidateMode(mode, currentRuleset);

            if (previewMode.Value == validated)
                return;

            previewMode.Value = validated;
            persistPreviewMode(validated);
        }

        private void applyPreviewModeForCurrentRuleset()
        {
            EzBeatmapPreviewMode target = EzBeatmapPreviewModes.ValidateMode(getStoredPreviewMode(), currentRuleset);

            if (previewMode.Value == target)
                return;

            previewMode.Value = target;
        }

        private EzBeatmapPreviewMode getStoredPreviewMode()
        {
            try
            {
                return EzBeatmapPreviewModes.IsManiaRuleset(currentRuleset) ? maniaPreviewModeConfig.Value : sharedPreviewModeConfig.Value;
            }
            catch
            {
                return EzBeatmapPreviewModes.GetDefaultMode(currentRuleset);
            }
        }

        private void persistPreviewMode(EzBeatmapPreviewMode mode)
        {
            try
            {
                if (EzBeatmapPreviewModes.IsManiaRuleset(currentRuleset))
                    maniaPreviewModeConfig.Value = mode;
                else
                    sharedPreviewModeConfig.Value = mode;
            }
            catch
            {
                // Keep the in-session choice even if persistence fails.
            }
        }

        private void onPreviewModeChanged()
        {
            updatePreviewModeButtons();
            setFullMapFocusState(false);
            updatePreviewControlsLayout();

            nextDynamicLoopStartTime = 0;
            previewClock.Stop();

            if (customManiaStaticMode && expanded && playableBeatmap != null)
            {
                setupManiaStaticPreview(playableBeatmap);
                previewClock.Seek(Math.Clamp(previewClock.CurrentTime, beatmapMinTime, beatmapMaxTime));

                if (scrollMode)
                    syncScrollRendererFromTime(previewClock.CurrentTime);
                else
                    updateProgressDisplay(previewClock.CurrentTime);

                return;
            }

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
            var modes = EzBeatmapPreviewModes.GetAvailableModes(currentRuleset);
            EzBeatmapPreviewMode highlightedMode = EzBeatmapPreviewModes.ValidateMode(previewMode.Value, currentRuleset);

            if (lastPreviewModeList == null || !modes.SequenceEqual(lastPreviewModeList))
            {
                lastPreviewModeList = modes;
                previewModeButtonList.Clear(false);

                foreach (EzBeatmapPreviewMode mode in modes)
                    previewModeButtonList.Add(previewModeButtons[mode]);
            }

            foreach (var pair in previewModeButtons)
                pair.Value.Selected = pair.Key == highlightedMode;
        }

        private void setupManiaStaticPreview(IBeatmap beatmap)
        {
            ManiaPreviewData data = ManiaPreviewGeometryBuilder.Build(beatmap);

            var renderer = previewMode.Value switch
            {
                EzBeatmapPreviewMode.StaticFullMap => (IManiaStaticPreviewRenderer)new StaticFullMapPreviewRenderer(),
                EzBeatmapPreviewMode.StaticScroll => new StaticScrollPreviewRenderer(),
                _ => null
            };

            if (renderer == null)
                return;

            renderer.SetData(data);
            renderer.SetDensity((float)previewDensity.Value);
            renderer.SetCurrentTime(previewClock.CurrentTime);

            stageScaleContainer.RelativeSizeAxes = Axes.Both;
            stageScaleContainer.Scale = Vector2.One;
            stageScaleContainer.Size = Vector2.One;
            stageScaleContainer.Child = (Drawable)renderer;

            maniaStaticRenderer = renderer;
            drawableRuleset = null;

            if (scrollMode)
                syncScrollRendererFromTime(previewClock.CurrentTime);

            updatePreviewControlsLayout();
        }

        private void setFullMapFocusState(bool focused)
        {
            if (fullMapFocusActive == focused)
                return;

            if (focused)
            {
                focusSavedPanelWidth = lastAppliedPanelWidth > 0 ? lastAppliedPanelWidth : panelWidth;
                focusSavedPanelHeight = lastAppliedPanelHeight > 0 ? lastAppliedPanelHeight : panelHeight;
            }

            fullMapFocusActive = focused;
            FullMapFocusState.Value = focused;

            panelBackground.TintBox.FadeColour(Color4.Black.Opacity(panel_background_focus_opacity), 100, Easing.OutQuint);

            previewModeButtonList.FadeTo(focused ? 0 : 1, 100, Easing.OutQuint);
            loadTimeText.FadeTo(focused ? 0 : 1, 100, Easing.OutQuint);
            topResizeGrip.FadeTo(focused ? 0 : 1, 100, Easing.OutQuint);
            rightResizeGrip.FadeTo(focused ? 0 : 1, 100, Easing.OutQuint);
            stateText.FadeTo(focused ? 0 : stateText.Alpha, 100, Easing.OutQuint);

            updatePreviewControlsLayout();

            if (!focused)
            {
                panelWidth = focusSavedPanelWidth;
                panelHeight = focusSavedPanelHeight;
                lastAppliedPanelWidth = -1;
                lastAppliedPanelHeight = -1;
            }

            if (maniaStaticRenderer is StaticFullMapPreviewRenderer fullMapRenderer)
                fullMapRenderer.SetZoom(focused);
        }
    }
}
