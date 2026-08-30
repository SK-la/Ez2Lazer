// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Overlays.Settings;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    [Cached]
    public abstract partial class SettingsPanel : OsuFocusedOverlayContainer
    {
        public const float CONTENT_MARGINS = 20;

        // extra right padding to give room to the revert-to-default button in settings controls.
        public static readonly MarginPadding CONTENT_PADDING = new MarginPadding { Left = 12, Right = 22 };

        public const float TRANSITION_LENGTH = 600;

        private const float sidebar_width = SettingsSidebar.EXPANDED_WIDTH;

        /// <summary>
        /// The width of the settings panel content, excluding the sidebar.
        /// </summary>
        public const float PANEL_WIDTH = 400;

        /// <summary>
        /// The full width of the settings panel, including the sidebar.
        /// </summary>
        public const float WIDTH = sidebar_width + PANEL_WIDTH;

        protected Container<Drawable> ContentContainer;

        protected override Container<Drawable> Content => ContentContainer;

        protected SettingsSidebar Sidebar;
        private SidebarIconButton selectedSidebarButton;

        public SettingsSectionsContainer SectionsContainer { get; private set; }

        protected SeekLimitedSearchTextBox SearchTextBox { get; private set; }

        protected override string PopInSampleName => "UI/settings-pop-in";
        protected override double PopInOutSampleBalance => -OsuGameBase.SFX_STEREO_STRENGTH;

        private readonly bool showBackButton;

        private LoadingLayer loading;

        private readonly List<SettingsSection> loadableSections = new List<SettingsSection>();

        private Task sectionsLoadingTask;
        private List<SettingsSection> loadedSections;
        private bool sectionsLoaded;
        private bool sectionsDisplayReady;
        private bool sectionsAsyncCallbackInvoked;
        private bool sectionsMountInProgress;
        private int sectionsMountedCount;
        private Stopwatch sectionsLoadStopwatch;
        private long? sectionsPreloadDurationMs;
        private bool loadHeartbeatActive;
        private double lastPopInTime;

        /// <summary>
        /// Whether settings sections have finished async construction (tree mount is deferred until PopIn).
        /// </summary>
        public bool AreSectionsLoaded => sectionsLoaded;

        /// <summary>
        /// Whether settings sections and sidebar buttons are mounted and ready to show without further loading.
        /// </summary>
        public bool AreSectionsReadyForDisplay => sectionsDisplayReady;

        public IBindable<SettingsSection> CurrentSection = new Bindable<SettingsSection>();

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        private Scheduler preloadScheduler => gameHost.UpdateThread.Scheduler;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        protected SettingsPanel(bool showBackButton)
        {
            this.showBackButton = showBackButton;
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = ContentContainer = new NonMaskedContent
            {
                X = -WIDTH + ExpandedPosition,
                Width = PANEL_WIDTH,
                RelativeSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Scale = new Vector2(2, 1), // over-extend to the left for transitions
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                        Alpha = 1,
                    },
                    loading = new LoadingLayer()
                }
            };

            Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = SectionsContainer = new SettingsSectionsContainer
                {
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Colour = Color4.Black.Opacity(0),
                        Type = EdgeEffectType.Shadow,
                        Hollow = true,
                        Radius = 10
                    },
                    MaskingSmoothness = 0,
                    RelativeSizeAxes = Axes.Both,
                    ExpandableHeader = CreateHeader(),
                    SelectedSection = { BindTarget = CurrentSection },
                    FixedHeader = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding
                        {
                            Vertical = 6,
                            Left = CONTENT_PADDING.Left,
                            Right = CONTENT_PADDING.Right,
                        },
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Child = SearchTextBox = new SettingsSearchTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Origin = Anchor.TopCentre,
                            Anchor = Anchor.TopCentre,
                        }
                    },
                    Footer = CreateFooter().With(f => f.Alpha = 0)
                }
            });

            AddInternal(Sidebar = new SettingsSidebar(showBackButton)
            {
                BackButtonAction = Hide,
                Width = sidebar_width
            });
        }

        protected void AddSection(SettingsSection section)
        {
            if (IsLoaded)
                // just to keep things simple. can be accommodated for if we ever need it.
                throw new InvalidOperationException("All sections must be added before the panel is loaded.");

            loadableSections.Add(section);
        }

        protected virtual Drawable CreateHeader() => new Container();

        protected virtual Drawable CreateFooter() => new Container();

        protected override void PopIn()
        {
            logPreloadStatus("PopIn");

            lastPopInTime = Time.Current;

            if (!sectionsDisplayReady)
                loading.Show();

            ContentContainer.MoveToX(ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            SectionsContainer.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);

            // delay load enough to ensure it doesn't overlap with the initial animation.
            // this is done as there is still a brief stutter during load completion which is more visible if the transition is in progress.
            // the eventual goal would be to remove the need for this by splitting up load into smaller work pieces, or fixing the remaining
            // load complete overheads.
            // Preloaded sections wait for the full PopIn transition; cold load uses the official 200ms offset.
            Scheduler.AddDelayed(loadSections, getPopInLoadSectionsDelay());

            Sidebar?.MoveToX(0, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(1, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.TakeFocus();
            SearchTextBox.HoldFocus = true;
        }

        private double getPopInLoadSectionsDelay()
        {
            if (sectionsDisplayReady)
                return 0;

            // Async CPU preload is done; defer tree mount until the slide/fade finishes.
            if (sectionsLoaded)
                return TRANSITION_LENGTH;

            return TRANSITION_LENGTH / 3;
        }

        private double getRemainingPopInAnimationDelay()
        {
            if (sectionsDisplayReady)
                return 0;

            return Math.Max(0, TRANSITION_LENGTH - (Time.Current - lastPopInTime));
        }

        private void scheduleAfterPopInAnimation(Action action)
        {
            double delay = getRemainingPopInAnimationDelay();

            if (delay > 0)
                Scheduler.AddDelayed(action, delay);
            else
                Scheduler.Add(action);
        }

        protected virtual float ExpandedPosition => 0;

        protected override void PopOut()
        {
            base.PopOut();

            SectionsContainer.FadeEdgeEffectTo(0, WaveContainer.DISAPPEAR_DURATION, Easing.In);
            ContentContainer.MoveToX(-WIDTH + ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            Sidebar?.MoveToX(-sidebar_width, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(0, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.HoldFocus = false;
            if (SearchTextBox.HasFocus)
                GetContainingFocusManager()!.ChangeFocus(null);
        }

        public override bool AcceptsFocus => true;

        protected override void OnFocus(FocusEvent e)
        {
            SearchTextBox.TakeFocus();
            base.OnFocus(e);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            ContentContainer.Margin = new MarginPadding { Left = Sidebar?.DrawWidth ?? 0 };
        }

        private const double fade_in_duration = 500;

        private const int sections_add_batch_size = 3;

        private const double preload_heartbeat_interval_ms = 3000;

        /// <summary>
        /// Starts loading settings sections before the panel is shown.
        /// Safe to call multiple times; only the first call has an effect.
        /// </summary>
        public void BeginLoadingSections()
        {
            EzStartupTrace.Log("Settings.BeginLoadingSections called");
            loadSections();
        }

        /// <summary>
        /// Emit a snapshot of section preload progress to the startup trace log.
        /// </summary>
        public void LogPreloadStatus(string context) => logPreloadStatus(context);

        private void logPreloadStatus(string context)
        {
            string timing = sectionsPreloadDurationMs.HasValue
                ? $"preloadDuration={sectionsPreloadDurationMs}ms"
                : $"preloadElapsed={sectionsLoadStopwatch?.ElapsedMilliseconds ?? 0}ms";
            string taskStatus = sectionsLoadingTask?.Status.ToString() ?? "none";
            bool workerDone = sectionsLoadingTask?.IsCompleted ?? false;

            EzStartupTrace.Log(
                $"Settings[{context}] loaded={sectionsLoaded} displayReady={sectionsDisplayReady} " +
                $"workerDone={workerDone} callbackInvoked={sectionsAsyncCallbackInvoked} " +
                $"mounted={SectionsContainer.Count}/{loadableSections.Count} taskStatus={taskStatus} {timing} visible={State.Value == Visibility.Visible}");
        }

        private void loadSections()
        {
            if (sectionsDisplayReady)
            {
                EzStartupTrace.Log("Settings.loadSections skipped (already complete)");
                return;
            }

            if (!sectionsLoaded)
            {
                if (sectionsLoadingTask != null)
                {
                    EzStartupTrace.Log("Settings.loadSections skipped (async in progress)");
                    return;
                }

                beginAsyncSectionLoad();
                return;
            }

            scheduleMountLoadedSections();
        }

        private void beginAsyncSectionLoad()
        {
            sectionsLoadStopwatch = Stopwatch.StartNew();
            EzStartupTrace.Log($"Settings.loadSections started ({loadableSections.Count} sections)");
            startLoadHeartbeat();

            sectionsLoadingTask = LoadComponentsAsync(loadableSections, sections =>
            {
                sectionsAsyncCallbackInvoked = true;
                loadedSections = sections.ToList();
                sectionsLoaded = true;

                if (State.Value != Visibility.Visible)
                {
                    sectionsPreloadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
                    sectionsLoadStopwatch.Stop();
                    loadHeartbeatActive = false;
                    EzStartupTrace.Log($"Settings async preload complete after {sectionsPreloadDurationMs}ms (mount deferred until PopIn)");
                    return;
                }

                EzStartupTrace.Log($"Settings sections worker finished after {sectionsLoadStopwatch.ElapsedMilliseconds}ms, scheduling mount after PopIn animation");
                scheduleAfterPopInAnimation(scheduleMountLoadedSections);
            }, scheduler: preloadScheduler);
        }

        private void scheduleMountLoadedSections()
        {
            if (sectionsDisplayReady || loadedSections == null)
                return;

            int startIndex = SectionsContainer.Count;

            if (startIndex >= loadedSections.Count)
                return;

            if (sectionsMountInProgress)
            {
                EzStartupTrace.Log("Settings.mount skipped (already in progress)");
                return;
            }

            sectionsMountInProgress = true;
            EzStartupTrace.Log($"Settings mounting preloaded sections from index {startIndex}");
            Scheduler.Add(() => addSectionsInBatches(loadedSections, startIndex));
        }

        private void startLoadHeartbeat()
        {
            if (loadHeartbeatActive)
                return;

            loadHeartbeatActive = true;
            scheduleLoadHeartbeat();
        }

        private void scheduleLoadHeartbeat()
        {
            preloadScheduler.AddDelayed(() =>
            {
                if (sectionsDisplayReady || (sectionsLoaded && State.Value != Visibility.Visible))
                {
                    loadHeartbeatActive = false;
                    return;
                }

                logPreloadStatus("heartbeat");

                if (sectionsLoadingTask != null)
                    scheduleLoadHeartbeat();
                else
                    loadHeartbeatActive = false;
            }, preload_heartbeat_interval_ms);
        }

        private void addSectionsInBatches(IReadOnlyList<SettingsSection> sections, int startIndex)
        {
            if (State.Value != Visibility.Visible)
            {
                sectionsMountInProgress = false;
                return;
            }

            int endIndex = Math.Min(startIndex + sections_add_batch_size, sections.Count);

            for (int i = startIndex; i < endIndex; i++)
                SectionsContainer.Add(sections[i]);

            sectionsMountedCount = endIndex;

            if (endIndex < sections.Count)
            {
                Scheduler.Add(() => addSectionsInBatches(sections, endIndex));
                return;
            }

            sectionsMountInProgress = false;
            EzStartupTrace.Log($"Settings sections mount complete after {sectionsLoadStopwatch.ElapsedMilliseconds}ms");
            finishSectionsDisplay();
        }

        private void finishSectionsDisplay()
        {
            SectionsContainer.Footer.FadeInFromZero(fade_in_duration, Easing.OutQuint);
            SectionsContainer.SearchContainer.FadeInFromZero(fade_in_duration, Easing.OutQuint);

            loading.Hide();

            SearchTextBox.Current.BindValueChanged(term => SectionsContainer.SearchTerm = term.NewValue, true);

            loadSidebarButtons();
        }

        private void loadSidebarButtons()
        {
            if (Sidebar == null)
                return;

            LoadComponentsAsync(createSidebarButtons(), buttons =>
            {
                float delay = 0;

                foreach (var button in buttons)
                {
                    Sidebar.Add(button);

                    button.FadeOut()
                          .Delay(delay)
                          .FadeInFromZero(fade_in_duration, Easing.OutQuint);

                    delay += 40;
                }

                SectionsContainer.SelectedSection.BindValueChanged(section =>
                {
                    if (selectedSidebarButton != null)
                        selectedSidebarButton.Selected = false;

                    selectedSidebarButton = Sidebar.Children.OfType<SidebarIconButton>().FirstOrDefault(b => b.Section == section.NewValue);

                    if (selectedSidebarButton != null)
                        selectedSidebarButton.Selected = true;
                }, true);

                sectionsDisplayReady = true;
                loadHeartbeatActive = false;
                sectionsPreloadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
                sectionsLoadStopwatch.Stop();
                EzStartupTrace.Log($"Settings display ready after {sectionsPreloadDurationMs}ms (sidebar mounted)");
            }, scheduler: preloadScheduler);
        }

        private IEnumerable<SidebarIconButton> createSidebarButtons()
        {
            foreach (var section in SectionsContainer)
            {
                yield return new SidebarIconButton
                {
                    Section = section,
                    Action = () =>
                    {
                        if (!SectionsContainer.IsLoaded)
                            return;

                        SectionsContainer.ScrollTo(section);
                    },
                };
            }
        }

        private partial class NonMaskedContent : Container<Drawable>
        {
            // masking breaks the pan-out transform with nested sub-settings panels.
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }

        public partial class SettingsSectionsContainer : SectionsContainer<SettingsSection>
        {
            public SearchContainer<SettingsSection> SearchContainer;

            public string SearchTerm
            {
                get => SearchContainer.SearchTerm;
                set => SearchContainer.SearchTerm = value;
            }

            protected override FlowContainer<SettingsSection> CreateScrollContentContainer()
                => SearchContainer = new SearchContainer<SettingsSection>
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Vertical,
                    Alpha = 0,
                };

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                HeaderBackground = new Box
                {
                    Colour = colourProvider.Background5,
                    RelativeSizeAxes = Axes.Both
                };

                SearchContainer.FilterCompleted += InvalidateScrollPosition;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // no null check because the usage of this class is strict
                HeaderBackground!.Alpha = -ExpandableHeader!.Y / ExpandableHeader.LayoutSize.Y;
            }
        }
    }
}
