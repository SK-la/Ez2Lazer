// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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
using osu.Game.EzOsuGame.Startup;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osuTK;
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
        private readonly List<SidebarIconButton> loadedSidebarButtons = new List<SidebarIconButton>();

        private bool sectionsConstructionStarted;
        private bool sectionsConstructionInProgress;
        private int sectionsLoadingBatchesPending;
        private int sectionsLoadingBatchCount;
        private List<SettingsSection> loadedSections;
        private bool searchBindingAdded;
        private bool selectedSectionBindingAdded;
        private bool sectionsMountInProgress;
        private bool sectionsMountCompleted;
        private Stopwatch sectionsLoadStopwatch;
        private Stopwatch sectionsMountStopwatch;
        private long? sectionsLoadDurationMs;
        private bool loadHeartbeatActive;

        /// <summary>
        /// Whether settings sections have finished async construction.
        /// </summary>
        public bool AreSectionsLoaded { get; private set; }

        /// <summary>
        /// Whether all settings sections and sidebar buttons are mounted and have completed their first update.
        /// </summary>
        public bool AreSectionsReadyForDisplay { get; private set; }

        public IBindable<SettingsSection> CurrentSection = new Bindable<SettingsSection>();

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        private Scheduler preloadScheduler => gameHost.UpdateThread.Scheduler;

        protected virtual int SettingsLoadBatchCount => 4;

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
            loadableSections.Add(section);
        }

        protected virtual void PopulateSections()
        {
        }

        protected virtual Drawable CreateHeader() => new Container();

        protected virtual Drawable CreateFooter() => new Container();

        protected override void PopIn()
        {
            if (!AreSectionsReadyForDisplay)
            {
                SectionsContainer.SearchContainer.AlwaysPresent = true;
                Sidebar.AlwaysPresent = true;
                loading.Show();
                Sidebar.FadeTo(0);
                Scheduler.AddDelayed(loadSections, 1);
            }

            ContentContainer.MoveToX(ExpandedPosition, TRANSITION_LENGTH, Easing.OutQuint);

            SectionsContainer.FadeEdgeEffectTo(WaveContainer.SHADOW_OPACITY, WaveContainer.APPEAR_DURATION, Easing.Out);

            Sidebar?.MoveToX(0, TRANSITION_LENGTH, Easing.OutQuint);
            this.FadeTo(1, TRANSITION_LENGTH / 2, Easing.OutQuint);

            SearchTextBox.TakeFocus();
            SearchTextBox.HoldFocus = true;
        }

        protected virtual float ExpandedPosition => 0;

        protected override void PopOut()
        {
            base.PopOut();

            if (!AreSectionsReadyForDisplay)
            {
                SectionsContainer.SearchContainer.AlwaysPresent = false;
                Sidebar.AlwaysPresent = false;
            }

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

            updateSectionsDisplayReadiness();
        }

        private void updateSectionsDisplayReadiness()
        {
            if (AreSectionsReadyForDisplay || !sectionsMountCompleted)
                return;

            if (SectionsContainer.Count != loadableSections.Count
                || Sidebar.Children.OfType<SidebarIconButton>().Count() != loadableSections.Count
                || SectionsContainer.Any(section => !section.IsLoaded)
                || Sidebar.Children.OfType<SidebarIconButton>().Any(button => !button.IsLoaded))
                return;

            AreSectionsReadyForDisplay = true;
            loadHeartbeatActive = false;
            long mountDuration = sectionsMountStopwatch?.ElapsedMilliseconds ?? 0;

            SectionsContainer.SearchContainer.AlwaysPresent = false;
            Sidebar.AlwaysPresent = false;
            SectionsContainer.SearchContainer.FadeTo(1);
            Sidebar.FadeTo(1, 150, Easing.OutQuint);
            SectionsContainer.Footer.FadeInFromZero(fade_in_duration, Easing.OutQuint);
            loading.Hide();

            if (!searchBindingAdded)
            {
                searchBindingAdded = true;
                SearchTextBox.Current.BindValueChanged(term =>
                {
                    SectionsContainer.SearchTerm = term.NewValue;

                    searchActive = !string.IsNullOrEmpty(term.NewValue);
                    updateSubsectionNav();
                }, true);
            }

            if (!selectedSectionBindingAdded)
            {
                selectedSectionBindingAdded = true;
                SectionsContainer.SelectedSection.BindValueChanged(section => updateSelectedSidebarButton(section.NewValue), true);
            }

            sectionsLoadStopwatch?.Stop();
            sectionsMountStopwatch?.Stop();
            EzStartupTrace.Log($"Settings.ready frameReadyMs={sectionsLoadStopwatch?.ElapsedMilliseconds ?? 0} mountMs={mountDuration}");
        }

        private const double fade_in_duration = 500;

        private const int sections_add_batch_size = 1;

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
            EzStartupTrace.LogDiagnostic($"Settings.status context={context} loaded={AreSectionsLoaded} ready={AreSectionsReadyForDisplay} "
                                         + $"sections={loadableSections.Count} mounted={SectionsContainer?.Count ?? 0} "
                                         + $"loadMs={sectionsLoadDurationMs?.ToString() ?? "-"}");
        }

        private void loadSections()
        {
            if (AreSectionsReadyForDisplay)
                return;

            if (!sectionsConstructionStarted)
            {
                sectionsConstructionStarted = true;
                sectionsConstructionInProgress = true;
                sectionsLoadStopwatch = Stopwatch.StartNew();
                EzStartupTrace.Log("Settings.open construction begin");
                startLoadHeartbeat();

                Task.Run(PopulateSections).ContinueWith(task => preloadScheduler.Add(() =>
                {
                    sectionsConstructionInProgress = false;

                    if (IsDisposed)
                        return;

                    task.GetAwaiter().GetResult();
                    beginAsyncSectionLoad();
                }), TaskScheduler.Default);
            }

            if (AreSectionsLoaded)
                scheduleMountLoadedSections();
        }

        private void beginAsyncSectionLoad()
        {
            EzStartupTrace.LogDiagnostic($"Settings.open load begin sections={loadableSections.Count}");

            var sidebarButtons = createSidebarButtons(loadableSections).ToList();

            if (loadableSections.Count == 0)
            {
                AreSectionsLoaded = true;
                loadedSections = new List<SettingsSection>();
                sectionsLoadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
                scheduleMountLoadedSections();
                return;
            }

            int batchCount = Math.Clamp(SettingsLoadBatchCount, 1, loadableSections.Count);
            var sectionsByBatch = new List<SettingsSection>[batchCount];
            var sidebarButtonsByBatch = new List<SidebarIconButton>[batchCount];
            var loadablesByBatch = new List<Drawable>[batchCount];

            for (int i = 0; i < batchCount; i++)
            {
                sectionsByBatch[i] = new List<SettingsSection>();
                sidebarButtonsByBatch[i] = new List<SidebarIconButton>();
                loadablesByBatch[i] = new List<Drawable>();
            }

            for (int i = 0; i < loadableSections.Count; i++)
            {
                sectionsByBatch[i % batchCount].Add(loadableSections[i]);
                loadablesByBatch[i % batchCount].Add(loadableSections[i]);
            }

            for (int i = 0; i < sidebarButtons.Count; i++)
            {
                sidebarButtonsByBatch[i % batchCount].Add(sidebarButtons[i]);
                loadablesByBatch[i % batchCount].Add(sidebarButtons[i]);
            }

            loadedSections = new List<SettingsSection>(loadableSections.Count);
            sectionsLoadingBatchCount = batchCount;
            sectionsLoadingBatchesPending = batchCount;

            for (int i = 0; i < batchCount; i++)
            {
                int batchIndex = i;

                LoadComponentsAsync(loadablesByBatch[batchIndex], components => completeSectionLoadBatch(
                    components,
                    sectionsByBatch[batchIndex],
                    sidebarButtonsByBatch[batchIndex]), scheduler: preloadScheduler);
            }
        }

        private void completeSectionLoadBatch(
            IEnumerable<Drawable> components,
            IReadOnlyCollection<SettingsSection> sections,
            IReadOnlyCollection<SidebarIconButton> sidebarButtons)
        {
            var loadedComponents = components.ToList();
            loadedSections.AddRange(sections.Where(loadedComponents.Contains));
            loadedSidebarButtons.AddRange(sidebarButtons.Where(loadedComponents.Contains));

            if (--sectionsLoadingBatchesPending > 0)
                return;

            loadedSections = loadableSections.Where(loadedSections.Contains).ToList();
            AreSectionsLoaded = true;
            logSectionLoadSnapshot();
            sectionsLoadDurationMs = sectionsLoadStopwatch.ElapsedMilliseconds;
            EzStartupTrace.LogDiagnostic($"Settings.sections loaded batches={sectionsLoadingBatchCount} clickToLoadMs={sectionsLoadDurationMs}");
            scheduleMountLoadedSections();
        }

        /// <summary>
        /// Reports the per-section load cost after async construction, so the slowest sections are visible
        /// without re-reading individual <see cref="SettingsSection.LoadDurationMs"/> values by hand.
        /// </summary>
        private void logSectionLoadSnapshot()
        {
            if (loadedSections == null)
                return;

            foreach (var section in loadedSections.OrderByDescending(s => s.LoadDurationMs).Take(5))
                EzStartupTrace.LogDiagnostic($"Settings.snapshot top5 {section.LoadDurationMs}ms header=\"{section.Header}\"");
        }

        private void scheduleMountLoadedSections()
        {
            if (AreSectionsReadyForDisplay || loadedSections == null)
                return;

            int startIndex = SectionsContainer.Count;

            if (startIndex >= loadedSections.Count)
            {
                finishSectionsDisplay();
                return;
            }

            if (sectionsMountInProgress)
                return;

            sectionsMountInProgress = true;
            sectionsMountStopwatch ??= Stopwatch.StartNew();
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

                if (sectionsConstructionInProgress || sectionsLoadingBatchesPending > 0 || sectionsMountInProgress)
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
                EzStartupTrace.LogDiagnostic($"Settings.mount batches begin count={sections.Count}");

            int endIndex = Math.Min(startIndex + sections_add_batch_size, sections.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                SectionsContainer.Add(sections[i]);

                var sidebarButton = loadedSidebarButtons.FirstOrDefault(button => button.Section == sections[i]);

                if (sidebarButton != null && !Sidebar.Children.Contains(sidebarButton))
                    Sidebar.Add(sidebarButton);
            }

            if (endIndex < sections.Count)
            {
                Scheduler.Add(() => addSectionsInBatches(sections, endIndex));
                return;
            }

            sectionsMountInProgress = false;
            EzStartupTrace.LogDiagnostic("Settings.mount batches complete");
            finishSectionsDisplay();
        }

        private void finishSectionsDisplay()
        {
            sectionsMountCompleted = true;
            updateSectionsDisplayReadiness();
        }

        private IEnumerable<SidebarIconButton> createSidebarButtons(IEnumerable<SettingsSection> sections)
        {
            foreach (var section in sections)
                yield return createSidebarButton(section);
        }

        private SidebarIconButton createSidebarButton(SettingsSection section) => new SidebarIconButton
        {
            Section = section,
            Action = () =>
            {
                if (!SectionsContainer.IsLoaded || !SectionsContainer.Children.Contains(section))
                    return;

                SectionsContainer.ScrollTo(section);
            },
        };

        private void updateSelectedSidebarButton(SettingsSection section)
        {
            selectedSidebarButton?.Selected = false;
            selectedSidebarButton = Sidebar.Children.OfType<SidebarIconButton>().FirstOrDefault(button => button.Section == section);
            selectedSidebarButton?.Selected = true;
            updateSubsectionNav();
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

            // the second-level column is fixed: it is always reserved, and only its entries change with the section.
            bool shouldShow = ShowSubsectionNavColumn
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

            var current = SectionsContainer.GetActiveDrawableAtScrollPosition(indexedSection.IndexedSubsections) as SettingsSubsection;
            subsectionNavColumn.SetSelected(current);
        }

        private partial class NonMaskedContent : Container<Drawable>
        {
            // masking breaks the pan-out transform with nested sub-settings panels.
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
        }

        public partial class SettingsSectionsContainer : SectionsContainer<SettingsSection>
        {
            public SearchContainer<SettingsSection> SearchContainer;

            public override bool PropagatePositionalInputSubTree => SearchContainer.Alpha > 0 && base.PropagatePositionalInputSubTree;

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
