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
using osu.Framework.Graphics.Primitives;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Audio;
using osu.Game.Overlays.Settings.Sections.Input;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Settings
{
    [TestFixture]
    public partial class TestSceneSettingsPanel : OsuManualInputManagerTestScene
    {
        private SettingsOverlay settings;
        private DialogOverlay dialogOverlay;

        private readonly List<SettingsOpenMeasurement> settingsOpenMeasurements = new List<SettingsOpenMeasurement>();
        private bool measuringSettingsOpen;
        private bool measuringSettingsPreload;
        private bool settingsOpenMeasurementComplete;
        private bool navigatingSettings;
        private string activeMeasurementName;
        private long measurementStartTimestamp;
        private int navigationUpdateFrameCount;
        private long settingsReadyTimestamp;
        private long allocatedBytesAtMeasurementStart;
        private TimeSpan gcPauseAtMeasurementStart;
        private int gen0AtMeasurementStart;
        private int gen1AtMeasurementStart;
        private int gen2AtMeasurementStart;
        private int measurementFrameCount;
        private long lastUpdateTimestamp;
        private double showCallMilliseconds;
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

                Add(settings = new SettingsOverlay
                {
                    State = { Value = Visibility.Hidden }
                });
            });
        }

        protected override void Update()
        {
            base.Update();

            if (navigatingSettings)
                navigationUpdateFrameCount++;

            if (!measuringSettingsOpen && !measuringSettingsPreload)
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

            if (measuringSettingsPreload)
            {
                if (!settings.AreSectionsReadyForDisplay)
                    return;

                settingsReadyTimestamp = updateTimestamp;
            }
            else
            {
                if (settings.State.Value != Visibility.Visible)
                    return;

                if (settingsReadyTimestamp == 0 && settings.AreSectionsReadyForDisplay)
                    settingsReadyTimestamp = updateTimestamp;
            }

            if (settingsReadyTimestamp == 0)
                return;

            measuringSettingsOpen = false;
            measuringSettingsPreload = false;
            EzStartupTuning.EnableSettingsSectionUpdateTrace = false;
            settingsOpenMeasurementComplete = true;

            settingsOpenMeasurements.Add(new SettingsOpenMeasurement(
                activeMeasurementName,
                Stopwatch.GetElapsedTime(measurementStartTimestamp, settingsReadyTimestamp).TotalMilliseconds,
                showCallMilliseconds,
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

            AddUntilStep("all settings are mounted for search", settingsMenuIsComplete);
            AddUntilStep("search result section becomes current", () => settings.CurrentSection.Value?.MatchingFilter == true);

            AddAssert("ensure all items match filter", () => settings.SectionsContainer
                                                                     .ChildrenOfType<SettingsSection>().Where(f => f.IsPresent)
                                                                     .All(section =>
                                                                         section.Children.Where(f => f.IsPresent)
                                                                                .OfType<ISettingsItem>()
                                                                                .OfType<IFilterable>()
                                                                                .All(f => f.FilterTerms.Any(t => t.ToString().Contains("scaling")))
                                                                     ));

            AddAssert("ensure matching section is current", () => settings.CurrentSection.Value?.MatchingFilter == true);
            AddAssert("ensure section is placed first", () => settings.CurrentSection.Value.Y == 0);
        }

        [Test]
        public void TestFilterMatchesWeatherItemsMountedAfterOpen()
        {
            AddStep("show settings", () => settings.Show());
            AddStep("reset mouse", () => InputManager.MoveMouseTo(settings));
            AddUntilStep("settings shell ready", () => settings.AreSectionsReadyForDisplay);
            AddStep("search deferred weather controls", () => settings.SectionsContainer.ChildrenOfType<SettingsSearchTextBox>().First().Current.Value = "playfield");
            AddUntilStep("weather control is mounted and matched", () => settings.SectionsContainer
                                                                              .ChildrenOfType<EzWeatherSettings>()
                                                                              .SelectMany(weather => weather.ChildrenOfType<SettingsItemV2>())
                                                                              .Any(item => item.IsPresent
                                                                                            && item.FilterTerms.Any(term => term.ToString().Contains("playfield", StringComparison.OrdinalIgnoreCase))));
        }

        [Test]
        public void TestSidebarNavigatesToDeferredSection()
        {
            GraphicsSection target = null!;

            AddStep("show settings", () => settings.Show());
            AddUntilStep("initial settings section ready", () => settings.AreSectionsReadyForDisplay);
            AddUntilStep("graphics sidebar button mounted", () => settings.ChildrenOfType<SidebarIconButton>().Any(button => button.Section is GraphicsSection));
            AddStep("click graphics sidebar button", () =>
            {
                target = settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section is GraphicsSection).Section as GraphicsSection ?? throw new InvalidOperationException();
                settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section == target).TriggerClick();
            });
            AddUntilStep("deferred graphics section selected and loaded", () => target != null && target.IsLoaded && settings.CurrentSection.Value == target);
        }

        [Test]
        public void TestSidebarNavigatesToAudioDevices()
        {
            AudioSection target = null!;

            AddStep("show settings", () => settings.Show());
            AddUntilStep("complete settings menu ready", () => settings.AreSectionsReadyForDisplay);
            AddUntilStep("audio sidebar button mounted", () => settings.ChildrenOfType<SidebarIconButton>().Any(button => button.Section is AudioSection));
            AddStep("navigate to audio settings", () =>
            {
                target = (AudioSection)settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section is AudioSection).Section;
                settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section == target).TriggerClick();
            });
            AddUntilStep("audio device setting loaded", () => target.ChildrenOfType<AudioDevicesSettings>()
                                                                  .SelectMany(section => section.ChildrenOfType<SettingsItemV2>())
                                                                  .Any(item => item.IsLoaded));
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
            AddUntilStep("input sidebar button mounted", () => settings.ChildrenOfType<SidebarIconButton>().Any(button => button.Section is InputSection));
            AddStep("navigate to input settings", () => settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section is InputSection).TriggerClick());
            AddUntilStep("input settings loaded", () => settings.SectionsContainer.ChildrenOfType<InputSection>().Any(section => section.IsLoaded));

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
            AddUntilStep("input sidebar button mounted", () => settings.ChildrenOfType<SidebarIconButton>().Any(button => button.Section is InputSection));
            AddStep("navigate to input settings", () => settings.ChildrenOfType<SidebarIconButton>().Single(button => button.Section is InputSection).TriggerClick());
            AddUntilStep("input settings loaded", () => settings.SectionsContainer.ChildrenOfType<InputSection>().Any(section => section.IsLoaded));

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

        [TestCase(false, TestName = "TestColdAndRepeatedOpenPerformance_Cold")]
        [TestCase(true, TestName = "TestColdAndRepeatedOpenPerformance_Preloaded")]
        public void TestColdAndRepeatedOpenPerformance(bool preloadWhileHidden) => addColdAndRepeatedOpenPerformance(preloadWhileHidden);

        [Test]
        public void TestShowAndHideDuringSettingsPreload()
        {
            AddUntilStep("settings panel is loaded before preloading", () => settings.IsLoaded);
            AddStep("begin settings preload while hidden", () => settings.BeginLoadingSections());
            AddStep("show settings during preload", () => settings.Show());
            AddStep("hide settings during preload", () => settings.Hide());
            AddUntilStep("hidden settings preload completes", () => settings.AreSectionsReadyForDisplay);
            AddAssert("preloaded settings remain hidden and unfocused", settingsAreHiddenAndUnfocused);
            AddAssert("preload mounted one complete settings tree", settingsMenuIsComplete);
        }

        [Test]
        public void TestAllSettingsSectionsAreReachableAndVisible()
        {
            AddStep("show settings", () => settings.Show());
            AddUntilStep("complete settings menu ready", () => settings.AreSectionsReadyForDisplay);
            AddAssert("complete settings tree is mounted", settingsMenuIsComplete);
            AddStep("start section navigation checks", () =>
            {
                navigationUpdateFrameCount = 0;
                navigatingSettings = true;
            });

            addSectionNavigationStep("General", section => section is GeneralSection);
            addSectionNavigationStep("Skin", section => section is SkinSection);
            addSectionNavigationStep("Input", section => section is InputSection);
            addSectionNavigationStep("User Interface", section => section is UserInterfaceSection);
            addSectionNavigationStep("Gameplay", section => section is GameplaySection);
            addSectionNavigationStep("Ez 游玩", section => section is EzGameSection);
            addSectionNavigationStep("Rulesets / seasonal section", section => section is RulesetSection or AfToggleSection, optional: true);
            addSectionNavigationStep("Audio", section => section is AudioSection);
            addSectionNavigationStep("Graphics", section => section is GraphicsSection);
            addSectionNavigationStep("Online", section => section is OnlineSection);
            addSectionNavigationStep("Maintenance", section => section is MaintenanceSection);
            addSectionNavigationStep("Debug", section => section is DebugSection);

            AddStep("finish section navigation checks", () => navigatingSettings = false);
            AddAssert("all settings remain mounted after navigation", settingsMenuIsComplete);
        }

        private void addSectionNavigationStep(string name, Func<SettingsSection, bool> matches, bool optional = false)
        {
            SettingsSection targetSection = null;
            int frameBeforeNavigation = 0;

            AddStep($"navigate to {name}", () =>
            {
                SidebarIconButton button = settings.ChildrenOfType<SidebarIconButton>().FirstOrDefault(candidate => matches(candidate.Section));

                if (button == null)
                {
                    if (optional)
                        return;

                    throw new InvalidOperationException($"No settings sidebar entry was found for {name}.");
                }

                targetSection = button.Section;
                frameBeforeNavigation = navigationUpdateFrameCount;
                button.TriggerClick();
            });

            AddUntilStep($"{name} is visible and updated", () => targetSection == null
                                                                   || navigationUpdateFrameCount >= frameBeforeNavigation + 2
                                                                   && sectionIsVisible(targetSection));
        }

        [Test]
        public void TestFreshSettingsPanelAfterActivatorCacheWarmup()
        {
            long settleStarted = 0;
            int reflectionActivatorCountBeforeFirstPanel = 0;
            AddStep("start test host settle interval", () => settleStarted = Stopwatch.GetTimestamp());
            AddUntilStep("wait for idle test host", () => Stopwatch.GetElapsedTime(settleStarted).TotalSeconds >= 5);
            AddStep("record dependency activator baseline", () => reflectionActivatorCountBeforeFirstPanel = getReflectedDependencyActivatorCount());

            AddStep("start first settings panel cold open", () => startSettingsOpenMeasurement("first-panel"));
            AddUntilStep("first settings menu fully ready", () => settingsOpenMeasurementComplete);
            AddAssert("first settings menu is fully mounted", settingsMenuIsComplete);
            AddStep("report first settings panel", () => reportSettingsOpenMeasurement(0));
            AddStep("report first panel dependency activators", () => TestContext.Progress.WriteLine(
                $"[SETTINGS-ACTIVATOR] stage=first-panel reflectedTypes={getReflectedDependencyActivatorCount() - reflectionActivatorCountBeforeFirstPanel}"));

            AddStep("close first settings panel", () => settings.Hide());
            AddUntilStep("finish first settings panel close", () => settings.Alpha <= 0.01f);
            AddStep("replace with a fresh settings panel", () =>
            {
                settings.Expire();
                Add(settings = new SettingsOverlay
                {
                    State = { Value = Visibility.Hidden }
                });
            });
            AddUntilStep("fresh settings panel loaded", () => settings.IsLoaded);
            AddStep("record warmed dependency activator baseline", () => reflectionActivatorCountBeforeFirstPanel = getReflectedDependencyActivatorCount());

            AddStep("start fresh settings panel open", () => startSettingsOpenMeasurement("fresh-panel"));
            AddUntilStep("fresh settings menu fully ready", () => settingsOpenMeasurementComplete);
            AddAssert("fresh settings menu is fully mounted", settingsMenuIsComplete);
            AddStep("report fresh settings panel", () => reportSettingsOpenMeasurement(1));
            AddStep("report fresh panel dependency activators", () => TestContext.Progress.WriteLine(
                $"[SETTINGS-ACTIVATOR] stage=fresh-panel reflectedTypes={getReflectedDependencyActivatorCount() - reflectionActivatorCountBeforeFirstPanel}"));
        }

        private static int getReflectedDependencyActivatorCount()
            => GlobalStatistics.Get<int>("Dependencies", "Reflected [BackgroundDependencyLoader]s").Value;

        private void addColdAndRepeatedOpenPerformance(bool preloadWhileHidden)
        {
            long settleStarted = 0;
            AddStep("start test host settle interval", () => settleStarted = Stopwatch.GetTimestamp());
            AddUntilStep("wait for idle test host", () => Stopwatch.GetElapsedTime(settleStarted).TotalSeconds >= 5);

            if (preloadWhileHidden)
            {
                AddStep("start settings preload while hidden", () => startSettingsPreloadMeasurement("hidden-preload"));
                AddUntilStep("full settings menu preloaded", () => settingsOpenMeasurementComplete);
                AddAssert("preload leaves settings hidden and unfocused", settingsAreHiddenAndUnfocused);
                AddAssert("all settings are mounted before first open", settingsMenuIsComplete);
                AddStep("report hidden preload cost", () => reportSettingsOpenMeasurement(0));

                AddStep("start first click after preload", () => startSettingsOpenMeasurement("preloaded-first-open"));
                AddUntilStep("preloaded menu is visible and ready", () => settingsOpenMeasurementComplete);
                AddAssert("preloaded settings tree is complete", settingsMenuIsComplete);
                AddStep("report first open after preload", () => reportSettingsOpenMeasurement(1));
            }
            else
            {
                AddStep("start cold settings open", () => startSettingsOpenMeasurement("cold"));
                AddUntilStep("cold settings menu fully ready", () => settingsOpenMeasurementComplete);
                AddAssert("all settings are mounted after cold open", settingsMenuIsComplete);
                AddStep("report cold settings open", () => reportSettingsOpenMeasurement(0));
            }

            for (int repeat = 0; repeat < 2; repeat++)
            {
                int measurementIndex = repeat + (preloadWhileHidden ? 2 : 1);

                AddStep($"close settings before repeat {measurementIndex}", () => settings.Hide());
                AddUntilStep($"finish close animation {measurementIndex}", () => settings.Alpha <= 0.01f);
                AddStep($"start repeated settings open {measurementIndex}", () => startSettingsOpenMeasurement($"repeat-{measurementIndex}"));
                AddUntilStep($"repeated settings menu {measurementIndex} fully ready", () => settingsOpenMeasurementComplete);
                AddStep($"report repeated settings open {measurementIndex}", () => reportSettingsOpenMeasurement(measurementIndex));
            }

            int firstOpenMeasurement = preloadWhileHidden ? 1 : 0;

            if (preloadWhileHidden)
            {
                AddAssert("preloaded show calls remain under 5ms", () => settingsOpenMeasurements.Skip(firstOpenMeasurement).All(m => m.ShowCallMilliseconds <= 5));
            }
            else
            {
                AddAssert("reopening is faster than the cold load", () => settingsOpenMeasurements[firstOpenMeasurement].ElapsedMilliseconds
                                                                          > settingsOpenMeasurements.Skip(firstOpenMeasurement + 1).Max(m => m.ElapsedMilliseconds));
            }
        }

        private void startSettingsOpenMeasurement(string name)
        {
            resetSettingsOpenMeasurement(name);
            measuringSettingsOpen = true;
            long showStarted = Stopwatch.GetTimestamp();
            settings.Show();
            showCallMilliseconds = Stopwatch.GetElapsedTime(showStarted).TotalMilliseconds;
        }

        private void startSettingsPreloadMeasurement(string name)
        {
            resetSettingsOpenMeasurement(name);
            measuringSettingsPreload = true;
            settings.BeginLoadingSections();
        }

        private void resetSettingsOpenMeasurement(string name)
        {
            activeMeasurementName = name;
            settingsOpenMeasurementComplete = false;
            measuringSettingsOpen = false;
            measuringSettingsPreload = false;
            EzStartupTuning.EnableSettingsSectionUpdateTrace = true;
            measurementStartTimestamp = Stopwatch.GetTimestamp();
            allocatedBytesAtMeasurementStart = GC.GetTotalAllocatedBytes(false);
            gcPauseAtMeasurementStart = GC.GetTotalPauseDuration();
            gen0AtMeasurementStart = GC.CollectionCount(0);
            gen1AtMeasurementStart = GC.CollectionCount(1);
            gen2AtMeasurementStart = GC.CollectionCount(2);
            measurementFrameCount = 0;
            lastUpdateTimestamp = 0;
            showCallMilliseconds = 0;
            settingsReadyTimestamp = 0;
            lastGcPauseDuration = gcPauseAtMeasurementStart;
            maximumGcPauseBetweenUpdatesMs = 0;
            maximumUpdateGapAtMs = 0;
            sectionsMountedAtMaximumUpdateGap = 0;
            subsectionsLoadedAtMaximumUpdateGap = 0;
            weatherItemsLoadedAtMaximumUpdateGap = 0;
            maximumUpdateFrameMs = 0;
        }

        private bool settingsAreHiddenAndUnfocused()
            => settings.State.Value == Visibility.Hidden
               && settings.Alpha <= 0.01f
               && !settings.ChildrenOfType<SettingsSearchTextBox>().Any(textBox => textBox.HasFocus);

        private bool settingsMenuIsComplete()
        {
            SettingsSection[] sections = settings.SectionsContainer.ChildrenOfType<SettingsSection>().ToArray();
            SidebarIconButton[] buttons = settings.ChildrenOfType<SidebarIconButton>().ToArray();
            EzGameSection ezGameSection = sections.OfType<EzGameSection>().SingleOrDefault();

            if (ezGameSection == null || !settings.AreSectionsReadyForDisplay || !settings.AreAllSectionsMounted)
                return false;

            SettingsSubsection[] subsections = sections.SelectMany(section => section.ChildrenOfType<SettingsSubsection>()).ToArray();
            int optionalSections = sections.Count(section => section is RulesetSection or AfToggleSection);
            int unloadedSections = sections.Count(section => !section.IsLoaded);
            int subsectionsWithNoItems = subsections.Count(subsection => !subsection.ChildrenOfType<IFilterable>().Any());
            Drawable[] settingsItems = subsections.SelectMany(subsection => subsection.ChildrenOfType<ISettingsItem>().OfType<Drawable>()).ToArray();
            int unreadySettingsItems = settingsItems.Count(item => item.LoadState < LoadState.Ready);
            int unloadedSettingsItems = settingsItems.Count(item => !item.IsLoaded);
            int unloadedDrawables = sections.SelectMany(section => section.ChildrenOfType<Drawable>()).Count(drawable => !drawable.IsLoaded);
            int ezSubsections = ezGameSection.ChildrenOfType<SettingsSubsection>().Count();
            EzWeatherSettings weatherSettings = ezGameSection.ChildrenOfType<EzWeatherSettings>().SingleOrDefault();
            int weatherItems = weatherSettings?.ChildrenOfType<SettingsItemV2>().Count() ?? 0;
            bool sectionOrderMatchesSidebar = sections.SequenceEqual(buttons.Select(button => button.Section));

            if (weatherItems != 33)
                return false;

            TestContext.Progress.WriteLine($"[SETTINGS-COMPLETE] sections={sections.Length}/expected:{11 + optionalSections} unloadedSections={unloadedSections} "
                                           + $"buttons={buttons.Length} subsections={subsections.Length} noItems={subsectionsWithNoItems} settingRows={settingsItems.Length} unreadyRows={unreadySettingsItems} "
                                           + $"unloadedRows={unloadedSettingsItems} unloadedDrawables={unloadedDrawables} "
                                           + $"ezSubsections={ezSubsections}/11 weatherItems={weatherItems}/33 order={sectionOrderMatchesSidebar}");
            return sections.Length == 11 + optionalSections
                   && buttons.Length == sections.Length
                   && sections.All(section => section.ChildrenOfType<IFilterable>().Any())
                   && subsections.All(subsection => subsection.LoadState >= LoadState.Ready || !subsection.IsPresent)
                   && subsections.Length >= 46
                   && subsectionsWithNoItems == 0
                   && settingsItems.Length >= 240
                   && unreadySettingsItems == 0
                   && ezSubsections == 11
                   && weatherItems == 33
                   && sectionOrderMatchesSidebar;
        }

        private bool sectionIsVisible(SettingsSection section)
        {
            RectangleF viewport = settings.SectionsContainer.ScreenSpaceDrawQuad.AABBFloat;
            return section.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(viewport);
        }

        private void reportSettingsOpenMeasurement(int index)
        {
            SettingsOpenMeasurement measurement = settingsOpenMeasurements[index];

            TestContext.Progress.WriteLine(
                $"[SETTINGS-PERF] stage={measurement.Name} showCall={measurement.ShowCallMilliseconds:F2}ms fullMenuReady={measurement.ElapsedMilliseconds:F1}ms "
                + $"frames={measurement.Frames} allocated={measurement.AllocatedBytes / 1024.0:F1}KB "
                + $"gc={measurement.Gen0}/{measurement.Gen1}/{measurement.Gen2} gcPause={measurement.GcPauseMilliseconds:F1}ms maxGcPause={measurement.MaximumGcPauseBetweenUpdatesMs:F1}ms "
                + $"maxUpdate={measurement.MaximumUpdateFrameMs:F1}ms@{measurement.MaximumUpdateGapAtMs:F1}ms sections={measurement.SectionsMountedAtMaximumUpdateGap} "
                + $"ezSubsections={measurement.SubsectionsLoadedAtMaximumUpdateGap} weatherItems={measurement.WeatherItemsLoadedAtMaximumUpdateGap}");
        }

        private readonly record struct SettingsOpenMeasurement(
            string Name,
            double ElapsedMilliseconds,
            double ShowCallMilliseconds,
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
    }
}
