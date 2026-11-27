// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneSettingsPanel : OsuManualInputManagerTestScene
    {
        private TestSettingsOverlay settings;
        private DialogOverlay dialogOverlay;

        private readonly List<SettingsOpenMeasurement> settingsOpenMeasurements = new List<SettingsOpenMeasurement>();
        private bool measuringSettingsOpen;
        private bool settingsOpenMeasurementComplete;
        private string activeMeasurementName;
        private long measurementStartTimestamp;
        private long allocatedBytesAtMeasurementStart;
        private TimeSpan gcPauseAtMeasurementStart;
        private int gen0AtMeasurementStart;
        private int gen1AtMeasurementStart;
        private int gen2AtMeasurementStart;
        private int measurementFrameCount;
        private long lastUpdateTimestamp;
        private double maximumUpdateFrameMs;
        private double maximumUpdateGapAtMs;
        private int sectionsMountedAtMaximumUpdateGap;
        private int subsectionsLoadedAtMaximumUpdateGap;
        private int weatherItemsLoadedAtMaximumUpdateGap;
        private TimeSpan lastGcPauseDuration;
        private double maximumGcPauseBetweenUpdatesMs;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create settings", () =>
            {
                settings?.Expire();

                Add(settings = new TestSettingsOverlay
                {
                    State = { Value = Visibility.Hidden }
                });
            });
        }

        protected override void Update()
        {
            base.Update();

            if (!measuringSettingsOpen)
                return;

            long updateTimestamp = Stopwatch.GetTimestamp();

            if (lastUpdateTimestamp != 0)
            {
                double updateGap = Stopwatch.GetElapsedTime(lastUpdateTimestamp, updateTimestamp).TotalMilliseconds;

                if (updateGap > maximumUpdateFrameMs)
                {
                    maximumUpdateFrameMs = updateGap;
                    maximumUpdateGapAtMs = Stopwatch.GetElapsedTime(measurementStartTimestamp, updateTimestamp).TotalMilliseconds;
                    sectionsMountedAtMaximumUpdateGap = settings.SectionsContainer.Count;
                    EzGameSection ezGameSection = settings.SectionsContainer.ChildrenOfType<EzGameSection>().FirstOrDefault();
                    subsectionsLoadedAtMaximumUpdateGap = ezGameSection?.ChildrenOfType<SettingsSubsection>().Count(section => section.IsLoaded) ?? 0;
                    EzWeatherSettings weatherSettings = ezGameSection?.ChildrenOfType<EzWeatherSettings>().FirstOrDefault();
                    weatherItemsLoadedAtMaximumUpdateGap = weatherSettings?.ChildrenOfType<SettingsItemV2>().Count() ?? 0;
                }

                TimeSpan gcPauseDuration = GC.GetTotalPauseDuration();
                maximumGcPauseBetweenUpdatesMs = Math.Max(maximumGcPauseBetweenUpdatesMs, (gcPauseDuration - lastGcPauseDuration).TotalMilliseconds);
                lastGcPauseDuration = gcPauseDuration;
            }

            lastUpdateTimestamp = updateTimestamp;
            measurementFrameCount++;

            if (settings.State.Value != Visibility.Visible || !settings.AreSectionsReadyForDisplay)
                return;

            measuringSettingsOpen = false;
            EzStartupTuning.EnableSettingsSectionUpdateTrace = false;
            settingsOpenMeasurementComplete = true;

            settingsOpenMeasurements.Add(new SettingsOpenMeasurement(
                activeMeasurementName,
                Stopwatch.GetElapsedTime(measurementStartTimestamp).TotalMilliseconds,
                measurementFrameCount,
                GC.GetTotalAllocatedBytes(false) - allocatedBytesAtMeasurementStart,
                GC.CollectionCount(0) - gen0AtMeasurementStart,
                GC.CollectionCount(1) - gen1AtMeasurementStart,
                GC.CollectionCount(2) - gen2AtMeasurementStart,
                (GC.GetTotalPauseDuration() - gcPauseAtMeasurementStart).TotalMilliseconds,
                maximumGcPauseBetweenUpdatesMs,
                maximumUpdateFrameMs,
                maximumUpdateGapAtMs,
                sectionsMountedAtMaximumUpdateGap,
                subsectionsLoadedAtMaximumUpdateGap,
                weatherItemsLoadedAtMaximumUpdateGap));
        }

        [Test]
        public void TestBasic()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("do nothing", () => { });
            AddToggleStep("toggle visibility", visible => settings.State.Value = visible ? Visibility.Visible : Visibility.Hidden);
        }

        [Test]
        public void TestFiltering([Values] bool beforeLoad)
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            if (beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddUntilStep("wait for all settings sections to mount", () => settings.AreSectionsReadyForDisplay);

            if (!beforeLoad)
                AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");

            AddAssert("ensure all items match filter", () => settings.SectionsContainer
                                                                     .ChildrenOfType<SettingsSection>().Where(f => f.IsPresent)
                                                                     .All(section =>
                                                                         section.Children.Where(f => f.IsPresent)
                                                                                .OfType<ISettingsItem>()
                                                                                .OfType<IFilterable>()
                                                                                .All(f => f.FilterTerms.Any(t => t.ToString().Contains("scaling")))
                                                                     ));

            AddAssert("ensure section is current", () => settings.CurrentSection.Value is GraphicsSection);
            AddAssert("ensure section is placed first", () => settings.CurrentSection.Value.Y == 0);
        }

        [Test]
        public void TestFilterAfterLoad()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("wait for all settings sections to mount", () => settings.AreSectionsReadyForDisplay);

            AddStep("set filter", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "scaling");
        }

        [Test]
        public void ToggleVisibility()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddWaitStep("wait some", 5);
            AddToggleStep("toggle visibility", _ => settings.ToggleVisibility());
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelBackButton()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.AreSectionsReadyForDisplay);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Press back", () => settings
                                        .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                        .ChildrenOfType<SettingsSidebar.BackButton>().FirstOrDefault()?.TriggerClick());

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestTextboxFocusAfterNestedPanelEscape()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));

            AddUntilStep("sections loaded", () => settings.AreSectionsReadyForDisplay);
            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("open key binding subpanel", () =>
            {
                settings.SectionsContainer
                        .ChildrenOfType<InputSection>().FirstOrDefault()?
                        .ChildrenOfType<OsuButton>().FirstOrDefault()?
                        .TriggerClick();
            });

            AddUntilStep("binding panel textbox focused", () => settings
                                                                .ChildrenOfType<KeyBindingPanel>().FirstOrDefault()?
                                                                .ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);

            AddStep("Escape", () => InputManager.Key(Key.Escape));

            AddUntilStep("top-level textbox focused", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().FirstOrDefault()?.HasFocus == true);
        }

        [Test]
        public void TestSearchTextBoxSelectedOnShow()
        {
            SettingsSearchTextBox searchTextBox = null!;

            AddStep("show settings", () => settings.Show());
            AddUntilStep("search textbox loaded", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().Any());
            AddStep("set text", () => (searchTextBox = settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First()).Current.Value = "some text");
            AddAssert("no text selected", () => searchTextBox.SelectedText == string.Empty);
            AddRepeatStep("toggle visibility", () => settings.ToggleVisibility(), 2);
            AddAssert("search text selected", () => searchTextBox.SelectedText == searchTextBox.Current.Value);
        }

        [Test]
        public void TestColdAndRepeatedOpenPerformance() => addColdAndRepeatedOpenPerformance(1);

        [Test]
        public void TestColdAndRepeatedOpenPerformanceParallel() => addColdAndRepeatedOpenPerformance(4);

        [Test]
        public void TestFreshSettingsPanelAfterActivatorCacheWarmup()
        {
            long settleStarted = 0;
            int reflectionActivatorCountBeforeFirstPanel = 0;
            AddStep("set settings load batch count", () => settings.LoadBatchCount = 4);
            AddStep("start test host settle interval", () => settleStarted = Stopwatch.GetTimestamp());
            AddUntilStep("wait for idle test host", () => Stopwatch.GetElapsedTime(settleStarted).TotalSeconds >= 5);
            AddStep("record dependency activator baseline", () => reflectionActivatorCountBeforeFirstPanel = getReflectedDependencyActivatorCount());

            AddStep("start first settings panel cold open", () => startSettingsOpenMeasurement("first-panel"));
            AddUntilStep("first settings panel ready", () => settingsOpenMeasurementComplete);
            AddAssert("first settings panel is complete", settingsMenuIsComplete);
            AddStep("report first settings panel", () => reportSettingsOpenMeasurement(0));
            AddStep("report first panel dependency activators", () => TestContext.Progress.WriteLine(
                $"[SETTINGS-ACTIVATOR] stage=first-panel reflectedTypes={getReflectedDependencyActivatorCount() - reflectionActivatorCountBeforeFirstPanel}"));

            AddStep("close first settings panel", () => settings.Hide());
            AddUntilStep("finish first settings panel close", () => settings.Alpha <= 0.01f);
            AddStep("replace with a fresh settings panel", () =>
            {
                settings.Expire();
                Add(settings = new TestSettingsOverlay
                {
                    LoadBatchCount = 4,
                    State = { Value = Visibility.Hidden }
                });
            });
            AddUntilStep("fresh settings panel loaded", () => settings.IsLoaded);

            AddStep("start fresh settings panel open", () => startSettingsOpenMeasurement("fresh-panel"));
            AddUntilStep("fresh settings panel ready", () => settingsOpenMeasurementComplete);
            AddAssert("fresh settings panel is complete", settingsMenuIsComplete);
            AddStep("report fresh settings panel", () => reportSettingsOpenMeasurement(1));
            AddStep("report fresh panel dependency activators", () => TestContext.Progress.WriteLine(
                $"[SETTINGS-ACTIVATOR] stage=fresh-panel reflectedTypes={getReflectedDependencyActivatorCount() - reflectionActivatorCountBeforeFirstPanel}"));
        }

        private static int getReflectedDependencyActivatorCount()
            => GlobalStatistics.Get<int>("Dependencies", "Reflected [BackgroundDependencyLoader]s").Value;

        private void addColdAndRepeatedOpenPerformance(int loadBatchCount)
        {
            long settleStarted = 0;
            AddStep("set settings load batch count", () => settings.LoadBatchCount = loadBatchCount);
            AddStep("start test host settle interval", () => settleStarted = Stopwatch.GetTimestamp());
            AddUntilStep("wait for idle test host", () => Stopwatch.GetElapsedTime(settleStarted).TotalSeconds >= 5);

            AddStep($"start cold settings open with {loadBatchCount} load batches", () => startSettingsOpenMeasurement($"cold-{loadBatchCount}-batch"));
            AddUntilStep($"cold settings open with {loadBatchCount} batches ready", () => settingsOpenMeasurementComplete);
            AddAssert("all settings sections and sidebar buttons are complete", settingsMenuIsComplete);
            AddStep("report cold settings open", () => reportSettingsOpenMeasurement(0));

            for (int repeat = 0; repeat < 2; repeat++)
            {
                int measurementIndex = repeat + 1;

                AddStep($"close settings before repeat {measurementIndex}", () => settings.Hide());
                AddUntilStep($"finish close animation {measurementIndex}", () => settings.Alpha <= 0.01f);
                AddStep($"start repeated settings open {measurementIndex}", () => startSettingsOpenMeasurement($"repeat-{measurementIndex}"));
                AddUntilStep($"repeated settings open {measurementIndex} ready", () => settingsOpenMeasurementComplete);
                AddStep($"report repeated settings open {measurementIndex}", () => reportSettingsOpenMeasurement(measurementIndex));
            }

            AddAssert("reopening is faster than the cold load", () => settingsOpenMeasurements[0].ElapsedMilliseconds
                                                                      > settingsOpenMeasurements.Skip(1).Max(m => m.ElapsedMilliseconds));
        }

        private void startSettingsOpenMeasurement(string name)
        {
            activeMeasurementName = name;
            settingsOpenMeasurementComplete = false;
            EzStartupTuning.EnableSettingsSectionUpdateTrace = true;
            measurementStartTimestamp = Stopwatch.GetTimestamp();
            allocatedBytesAtMeasurementStart = GC.GetTotalAllocatedBytes(false);
            gcPauseAtMeasurementStart = GC.GetTotalPauseDuration();
            gen0AtMeasurementStart = GC.CollectionCount(0);
            gen1AtMeasurementStart = GC.CollectionCount(1);
            gen2AtMeasurementStart = GC.CollectionCount(2);
            measurementFrameCount = 0;
            lastUpdateTimestamp = 0;
            lastGcPauseDuration = gcPauseAtMeasurementStart;
            maximumGcPauseBetweenUpdatesMs = 0;
            maximumUpdateGapAtMs = 0;
            sectionsMountedAtMaximumUpdateGap = 0;
            subsectionsLoadedAtMaximumUpdateGap = 0;
            weatherItemsLoadedAtMaximumUpdateGap = 0;
            maximumUpdateFrameMs = 0;
            measuringSettingsOpen = true;
            settings.Show();
        }

        private bool settingsMenuIsComplete()
        {
            SettingsSection[] sections = settings.SectionsContainer.ChildrenOfType<SettingsSection>().ToArray();
            SidebarIconButton[] buttons = settings.ChildrenOfType<SidebarIconButton>().ToArray();
            SettingsSubsection[] subsections = sections.SelectMany(section => section.ChildrenOfType<SettingsSubsection>()).ToArray();
            int optionalSections = sections.Count(section => section is RulesetSection or AfToggleSection);
            int unloadedSections = sections.Count(section => !section.IsLoaded);
            int subsectionsWithNoItems = subsections.Count(subsection => !subsection.ChildrenOfType<IFilterable>().Any());
            Drawable[] settingsItems = subsections.SelectMany(subsection => subsection.ChildrenOfType<ISettingsItem>().OfType<Drawable>()).ToArray();
            int unreadyVisibleItems = settingsItems.Count(item => item.LoadState < LoadState.Ready && item.IsPresent);
            int unloadedSettingsItems = settingsItems.Count(item => !item.IsLoaded);
            int unloadedRowsInViewport = settingsItems.Count(item => !item.IsLoaded && item.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(settings.SectionsContainer.ScreenSpaceDrawQuad.AABBFloat));
            int ezSubsections = sections.OfType<EzGameSection>().Single().ChildrenOfType<SettingsSubsection>().Count();
            int weatherItems = sections.OfType<EzGameSection>().Single().ChildrenOfType<EzWeatherSettings>().Single().ChildrenOfType<SettingsItemV2>().Count();
            bool sectionOrderMatchesSidebar = sections.SequenceEqual(buttons.Select(button => button.Section));

            foreach (Drawable item in settingsItems.Where(item => !item.IsLoaded))
            {
                SettingsSection section = item.FindClosestParent<SettingsSection>();
                SettingsSubsection subsection = item.FindClosestParent<SettingsSubsection>();
                string filterTerms = item is IFilterable filterable ? string.Join(", ", filterable.FilterTerms.Select(term => term.ToString())) : string.Empty;
                bool intersectsViewport = item.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(settings.SectionsContainer.ScreenSpaceDrawQuad.AABBFloat);

                TestContext.Progress.WriteLine($"[SETTINGS-UNLOADED-ROW] type={item.GetType().Name} section=\"{section?.Header}\" subsection=\"{subsection?.Title}\" "
                                               + $"filterTerms=\"{filterTerms}\" loadState={item.LoadState} present={item.IsPresent} "
                                               + $"currentSection={section == settings.CurrentSection.Value} intersectsViewport={intersectsViewport}");
            }

            TestContext.Progress.WriteLine($"[SETTINGS-COMPLETE] sections={sections.Length}/expected:{11 + optionalSections} unloadedSections={unloadedSections} "
                                           + $"buttons={buttons.Length} subsections={subsections.Length} noItems={subsectionsWithNoItems} settingRows={settingsItems.Length} unreadyVisibleRows={unreadyVisibleItems} "
                                           + $"unloadedRows={unloadedSettingsItems} unloadedRowsInViewport={unloadedRowsInViewport} "
                                           + $"ezSubsections={ezSubsections}/11 weatherItems={weatherItems}/33 order={sectionOrderMatchesSidebar}");
            return sections.Length == 11 + optionalSections
                   && buttons.Length == sections.Length
                   && unloadedSections == 0
                   && sections.All(section => section.ChildrenOfType<IFilterable>().Any())
                   && subsections.All(subsection => subsection.LoadState >= LoadState.Ready || !subsection.IsPresent)
                   && subsectionsWithNoItems == 0
                   && settingsItems.Length > 0
                   && unreadyVisibleItems == 0
                   && unloadedRowsInViewport == 0
                   && ezSubsections == 11
                   && weatherItems == 33
                   && sections.OfType<EzGameSection>().Single().ChildrenOfType<EzWeatherSettings>().Single().ChildrenOfType<SettingsItemV2>()
                              .All(item => item.LoadState >= LoadState.Ready || !item.IsPresent)
                   && sectionOrderMatchesSidebar;
        }

        private void reportSettingsOpenMeasurement(int index)
        {
            SettingsOpenMeasurement measurement = settingsOpenMeasurements[index];

            TestContext.Progress.WriteLine(
                $"[SETTINGS-PERF] stage={measurement.Name} elapsed={measurement.ElapsedMilliseconds:F1}ms "
                + $"frames={measurement.Frames} allocated={measurement.AllocatedBytes / 1024.0:F1}KB "
                + $"gc={measurement.Gen0}/{measurement.Gen1}/{measurement.Gen2} gcPause={measurement.GcPauseMilliseconds:F1}ms maxGcPause={measurement.MaximumGcPauseBetweenUpdatesMs:F1}ms "
                + $"maxUpdate={measurement.MaximumUpdateFrameMs:F1}ms@{measurement.MaximumUpdateGapAtMs:F1}ms sections={measurement.SectionsMountedAtMaximumUpdateGap} "
                + $"ezSubsections={measurement.SubsectionsLoadedAtMaximumUpdateGap} weatherItems={measurement.WeatherItemsLoadedAtMaximumUpdateGap}");
        }

        private readonly record struct SettingsOpenMeasurement(
            string Name,
            double ElapsedMilliseconds,
            int Frames,
            long AllocatedBytes,
            int Gen0,
            int Gen1,
            int Gen2,
            double GcPauseMilliseconds,
            double MaximumGcPauseBetweenUpdatesMs,
            double MaximumUpdateFrameMs,
            double MaximumUpdateGapAtMs,
            int SectionsMountedAtMaximumUpdateGap,
            int SubsectionsLoadedAtMaximumUpdateGap,
            int WeatherItemsLoadedAtMaximumUpdateGap);

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(dialogOverlay = new DialogOverlay
            {
                Depth = -1
            });

            Dependencies.CacheAs<IDialogOverlay>(dialogOverlay);
        }

        private sealed partial class TestSettingsOverlay : SettingsOverlay
        {
            public int LoadBatchCount { get; set; } = 4;

            protected override int SettingsLoadBatchCount => LoadBatchCount;
        }
    }
}
