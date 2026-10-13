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
        /// The width of the subsection navigation column shown to the left of the content.
        /// </summary>
        public const float SUBSECTION_NAV_WIDTH = 150;

        /// <summary>
        /// The full width of the settings panel, including the sidebar and subsection navigation column.
        /// </summary>
        public const float WIDTH = sidebar_width + SUBSECTION_NAV_WIDTH + PANEL_WIDTH;

        protected Container<Drawable> ContentContainer;

        protected override Container<Drawable> Content => ContentContainer;

        protected SettingsSidebar Sidebar;
        private SidebarIconButton selectedSidebarButton;

        private SettingsSubsectionNavColumn subsectionNavColumn;

        /// <summary>
        /// Whether the subsection navigation column is currently shown.
        /// </summary>
        private bool subsectionNavVisible;

        /// <summary>
        /// Suppresses the subsection navigation column, e.g. while a sub-panel is open.
        /// </summary>
        private bool subsectionNavSuppressed;

        private bool searchActive;

        private SettingsSection indexedSection;

        /// <summary>
        /// The section whose subsections are currently navigable. Only set when the user explicitly
        /// jumps to a category, so that normal scrolling never brings up the subsection column.
        /// </summary>
        private SettingsSection subsectionNavSection;

        private ScheduledDelegate subsectionHighlightDelegate;

        private double subsectionHighlightSuppressUntil;

        private const double subsection_highlight_suppress_duration = 700;

        private const double subsection_nav_transition_duration = 500;

        public SettingsSectionsContainer SectionsContainer { get; private set; }

        protected SeekLimitedSearchTextBox SearchTextBox { get; private set; }

        protected override string PopInSampleName => "UI/settings-pop-in";
        protected override double PopInOutSampleBalance => -OsuGameBase.SFX_STEREO_STRENGTH;

        private readonly bool showBackButton;

        private LoadingLayer loading;

        private readonly List<SettingsSection> loadableSections = new List<SettingsSection>();

        private Task sectionsLoadingTask;
        private List<SettingsSection> loadedSections;
        private bool sectionsMountInProgress;
        private Stopwatch sectionsLoadStopwatch;
        private long? sectionsPreloadDurationMs;
        private bool loadHeartbeatActive;
        private double lastPopInTime;

        private const double pop_in_section_load_delay = 150;

        /// <summary>
        /// Whether settings sections have finished async construction (tree mount is deferred until PopIn).
        /// </summary>
        public bool AreSectionsLoaded { get; private set; }

        /// <summary>
        /// Whether settings sections and sidebar buttons are mounted and ready to show without further loading.
        /// </summary>
        public bool AreSectionsReadyForDisplay { get; private set; }

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

            AddInternal(subsectionNavColumn = new SettingsSubsectionNavColumn
            {
                EntryClicked = subsection =>
                {
                    // keep the clicked entry selected while the scroll animation runs; the
                    // scroll-position tracker would otherwise snap the highlight to the top item.
                    subsectionHighlightSuppressUntil = Time.Current + subsection_highlight_suppress_duration;
                    SectionsContainer?.ScrollTo(subsection);
                },
                Alpha = 0,
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
            // the subsection column is opt-in per category click, and shouldn't linger from a previous open.
            subsectionNavSection = null;
            resetSubsectionNav();

            if (!AreSectionsReadyForDisplay)
            {
                loading.Show();
                lastPopInTime = Time.Current;
                Scheduler.AddDelayed(loadSections, pop_in_section_load_delay);
            }
            else
                Scheduler.Add(loadSections);

            ContentContainer.MoveToX(ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            SectionsContainer.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);

            Sidebar?.MoveToX(0, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(1, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.TakeFocus();
            SearchTextBox.HoldFocus = true;
        }

        private void scheduleAfterPopInLoadDelay(Action action)
        {
            double delay = Math.Max(0, pop_in_section_load_delay - (Time.Current - lastPopInTime));

            if (delay > 0)
                Scheduler.AddDelayed(action, delay);
            else
                Scheduler.Add(action);
        }

        protected virtual float ExpandedPosition => 0;

        protected override void PopOut()
        {
            base.PopOut();

            stopSubsectionHighlight();

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

            float sidebarWidth = Sidebar?.DrawWidth ?? 0;
            float subsectionNavWidth = subsectionNavColumn?.DrawWidth ?? 0;

            // the subsection column only reserves space while it is expanded, so the panel keeps its
            // original geometry when hidden and grows smoothly (as the column's width animates) when shown.
            ContentContainer.Margin = new MarginPadding { Left = sidebarWidth + subsectionNavWidth };

            subsectionNavColumn?.X = ContentContainer.X + sidebarWidth;
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
            loadSections();
        }

        /// <summary>
        /// Emit a snapshot of section preload progress to the startup trace log.
        /// </summary>
        public void LogPreloadStatus(string context)
        {
        }

        private void loadSections()
        {
            if (AreSectionsReadyForDisplay)
                return;

            if (!AreSectionsLoaded)
            {
                if (sectionsLoadingTask != null)
                    return;

                beginAsyncSectionLoad();
                return;
            }

            scheduleMountLoadedSections();
        }

        private void beginAsyncSectionLoad()
        {
            EzStartupTrace.Log($"Settings.asyncPreload begin sections={loadableSections.Count}");
            sectionsLoadStopwatch = Stopwatch.StartNew();
            startLoadHeartbeat();

            sectionsLoadingTask = LoadComponentsAsync(loadableSections, sections =>
            {
                loadedSections = sections.ToList();
                AreSectionsLoaded = true;

                if (State.Value != Visibility.Visible)
                {
                    sectionsPreloadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
                    sectionsLoadStopwatch.Stop();
                    loadHeartbeatActive = false;
                    EzStartupTrace.Log($"Settings.asyncPreload complete {sectionsPreloadDurationMs}ms (mount deferred)");
                    return;
                }

                scheduleAfterPopInLoadDelay(scheduleMountLoadedSections);
            }, scheduler: preloadScheduler);
        }

        private void scheduleMountLoadedSections()
        {
            if (AreSectionsReadyForDisplay || loadedSections == null)
                return;

            int startIndex = SectionsContainer.Count;

            if (startIndex >= loadedSections.Count)
                return;

            if (sectionsMountInProgress)
                return;

            sectionsMountInProgress = true;
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
                if (AreSectionsReadyForDisplay || (AreSectionsLoaded && State.Value != Visibility.Visible))
                {
                    loadHeartbeatActive = false;
                    return;
                }

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

            if (startIndex == 0)
                EzStartupTrace.Log($"Settings.mount batches begin count={sections.Count}");

            int endIndex = Math.Min(startIndex + sections_add_batch_size, sections.Count);

            for (int i = startIndex; i < endIndex; i++)
                SectionsContainer.Add(sections[i]);

            if (endIndex < sections.Count)
            {
                Scheduler.Add(() => addSectionsInBatches(sections, endIndex));
                return;
            }

            sectionsMountInProgress = false;
            EzStartupTrace.Log("Settings.mount batches complete");
            finishSectionsDisplay();
        }

        private void finishSectionsDisplay()
        {
            SectionsContainer.Footer.FadeInFromZero(fade_in_duration, Easing.OutQuint);
            SectionsContainer.SearchContainer.FadeInFromZero(fade_in_duration, Easing.OutQuint);

            loading.Hide();

            SearchTextBox.Current.BindValueChanged(term =>
            {
                SectionsContainer.SearchTerm = term.NewValue;

                searchActive = !string.IsNullOrEmpty(term.NewValue);
                updateSubsectionNav();
            }, true);

            mountSidebarButtons();
        }

        private void mountSidebarButtons()
        {
            if (Sidebar == null)
                return;

            EzStartupTrace.Log("Settings.sidebar mount begin (sync)");

            float delay = 0;

            foreach (var button in createSidebarButtons())
            {
                Sidebar.Add(button);

                button.FadeOut()
                      .Delay(delay)
                      .FadeInFromZero(fade_in_duration, Easing.OutQuint);

                delay += 40;
            }

            SectionsContainer.SelectedSection.BindValueChanged(section =>
            {
                selectedSidebarButton?.Selected = false;

                selectedSidebarButton = Sidebar.Children.OfType<SidebarIconButton>().FirstOrDefault(b => b.Section == section.NewValue);

                selectedSidebarButton?.Selected = true;

                updateSubsectionNav();
            }, true);

            AreSectionsReadyForDisplay = true;
            loadHeartbeatActive = false;
            sectionsPreloadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
            sectionsLoadStopwatch.Stop();
            EzStartupTrace.Log($"Settings.ready mount+sidebar {sectionsPreloadDurationMs}ms");
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

                        // an explicit category jump is the only thing that enables the subsection column.
                        subsectionNavSection = section;
                        SectionsContainer.ScrollTo(section);
                        updateSubsectionNav();
                    },
                };
            }
        }

        /// <summary>
        /// Suppresses or restores the subsection navigation column, e.g. while a sub-panel is shown.
        /// </summary>
        public void SetSubsectionNavSuppressed(bool suppressed)
        {
            if (subsectionNavSuppressed == suppressed)
                return;

            subsectionNavSuppressed = suppressed;
            updateSubsectionNav();
        }

        /// <summary>
        /// Whether the subsection navigation column should ever be shown for this panel.
        /// Sub-panels such as the key binding panel provide their own navigation and opt out.
        /// </summary>
        protected virtual bool ShowSubsectionNavColumn => true;

        private void updateSubsectionNav()
        {
            var section = CurrentSection.Value;
            var subsections = section?.IndexedSubsections;

            // only shown for the category the user explicitly jumped to, and only when there is
            // enough of a subsection list to be worth navigating.
            bool shouldShow = ShowSubsectionNavColumn
                              && subsectionNavSection != null
                              && ReferenceEquals(section, subsectionNavSection)
                              && subsections != null
                              && subsections.Count > 3
                              && !subsectionNavSuppressed
                              && !searchActive;

            if (shouldShow && indexedSection != section)
            {
                indexedSection = section;
                subsectionNavColumn.SetSubsections(subsections);
            }
            else if (!shouldShow)
            {
                indexedSection = null;
            }

            bool wasVisible = subsectionNavVisible;
            subsectionNavVisible = shouldShow;

            if (wasVisible != shouldShow)
                subsectionNavColumn.SetExpanded(shouldShow, subsection_nav_transition_duration);

            if (shouldShow)
                startSubsectionHighlight();
            else
                stopSubsectionHighlight();
        }

        /// <summary>
        /// Immediately collapses the subsection column without animating, e.g. while the panel is off-screen.
        /// </summary>
        private void resetSubsectionNav()
        {
            subsectionNavVisible = false;
            subsectionNavColumn?.SetExpanded(false, 0);
            stopSubsectionHighlight();
        }

        private void startSubsectionHighlight()
        {
            if (subsectionHighlightDelegate != null)
                return;

            subsectionHighlightDelegate = Scheduler.AddDelayed(updateSubsectionHighlight, 100, true);
            updateSubsectionHighlight();
        }

        private void stopSubsectionHighlight()
        {
            subsectionHighlightDelegate?.Cancel();
            subsectionHighlightDelegate = null;
        }

        private void updateSubsectionHighlight()
        {
            if (indexedSection == null)
                return;

            // a click sets the selection optimistically; don't fight it while the scroll animation settles.
            if (Time.Current < subsectionHighlightSuppressUntil)
                return;

            float threshold = (SectionsContainer.FixedHeader?.ScreenSpaceDrawQuad.BottomLeft.Y ?? SectionsContainer.ScreenSpaceDrawQuad.TopLeft.Y) + 8;

            SettingsSubsection current = null;
            SettingsSubsection firstPresent = null;

            foreach (var subsection in indexedSection.IndexedSubsections)
            {
                if (!subsection.IsPresent)
                    continue;

                firstPresent ??= subsection;

                if (subsection.ScreenSpaceDrawQuad.TopLeft.Y <= threshold)
                    current = subsection;
                else
                    break;
            }

            subsectionNavColumn.SetSelected(current ?? firstPresent);
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
