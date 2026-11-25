// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Visual.SongSelect;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzModVerticalLayoutToggle : SongSelectTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Resolved]
        private EzConfigStore store { get; set; } = null!;

        private EzUserModSelectOverlay overlay => this.ChildrenOfType<EzUserModSelectOverlay>().Single();
        private EzModGroupColumn[] groups => overlay.ChildrenOfType<EzModGroupColumn>().ToArray();
        private EzModLayoutPanel[] panels => overlay.ChildrenOfType<EzModLayoutPanel>().ToArray();

        [TestCase(EzModSelectLayoutMode.Horizontal)]
        [TestCase(EzModSelectLayoutMode.Vertical)]
        public void TestAddPresetButtonTracksLayoutShear(EzModSelectLayoutMode mode)
        {
            open(mode: mode);
            AddStep("select a mod", () => SelectedMods.Value = new Mod[] { new OsuModHidden() });
            enterEditing();
            AddUntilStep("preset button loaded", () => addPresetButton.IsLoaded && addPresetLabel.DrawHeight > 0);
            AddStep("start with default shear", () => overlay.ChildrenOfType<EzLayoutShearControl>().Single().Current.Value = 0.2);

            foreach (float shear in new[] { 0f, 0.1f, 0.2f, 0f })
            {
                AddStep($"set shear to {shear}", () => overlay.ChildrenOfType<EzLayoutShearControl>().Single().Current.Value = shear);
                AddWaitStep("settle preset layout", 3);
                AddAssert("plus remains upright", () => screenShear(addPresetLabel), () => Is.EqualTo(0).Within(0.001f));
                AddAssert("button follows layout shear", () => screenShear(addPresetButton), () => Is.EqualTo(shear).Within(0.001f));
            }

            switchTo(mode == EzModSelectLayoutMode.Horizontal ? EzModSelectLayoutMode.Vertical : EzModSelectLayoutMode.Horizontal);
            AddAssert("plus upright after switching", () => screenShear(addPresetLabel), () => Is.EqualTo(0).Within(0.001f));
            switchTo(mode);
            AddAssert("plus upright after switching back", () => screenShear(addPresetLabel), () => Is.EqualTo(0).Within(0.001f));
            AddStep("add a preset", () => Realm.Write(realm => realm.Add(new ModPreset
            {
                Name = "Shear regression preset",
                Ruleset = realm.Find<RulesetInfo>("osu")!,
                Mods = new Mod[] { new OsuModHidden() },
            })));
            AddUntilStep("preset panel loaded", () => overlay.ChildrenOfType<ModPresetPanel>().Any(panel => panel.IsLoaded));
            AddAssert("plus upright after preset reload", () => screenShear(addPresetLabel), () => Is.EqualTo(0).Within(0.001f));
            AddStep("open add preset", () => click(addPresetButton));
            AddUntilStep("preset popover opens", () => this.ChildrenOfType<AddPresetPopover>().Any(popover => popover.IsPresent));
        }

        [TestCase(1024, 768, EzModSelectLayoutMode.Horizontal)]
        [TestCase(1024, 768, EzModSelectLayoutMode.Vertical)]
        [TestCase(1920, 1080, EzModSelectLayoutMode.Horizontal)]
        [TestCase(1920, 1080, EzModSelectLayoutMode.Vertical)]
        public void TestHeaderControlsStayOnOneRow(int width, int height, EzModSelectLayoutMode mode)
        {
            open(mode: mode);
            resizeViewport(width, height);
            AddWaitStep("settle header", 5);
            AddAssert("normal controls share one row", headerLayoutProblems, () => Is.Empty);
            enterEditing();
            AddWaitStep("settle edit controls", 5);
            AddAssert("all editing controls share one row", headerLayoutProblems, () => Is.Empty);
            AddStep("switch layout", () => click(overlay.LayoutModeButton));
            AddUntilStep("opposite layout active", () => overlay.LayoutMode != mode);
            waitForContent();
            AddAssert("header remains on one row after switch", headerLayoutProblems, () => Is.Empty);
            AddStep("finish editing", clickEditToggle);
            AddWaitStep("settle normal controls", 3);
            AddAssert("normal row restored", headerLayoutProblems, () => Is.Empty);
        }

        [TestCase(1024, 768, EzModSelectLayoutMode.Horizontal)]
        [TestCase(1024, 768, EzModSelectLayoutMode.Vertical)]
        [TestCase(1920, 1080, EzModSelectLayoutMode.Horizontal)]
        [TestCase(1920, 1080, EzModSelectLayoutMode.Vertical)]
        public void TestHiddenDrawerStaysVisibleAboveFooter(int width, int height, EzModSelectLayoutMode mode)
        {
            open(mode: mode);
            resizeViewport(width, height);
            enterEditing();
            AddWaitStep("settle drawer", 5);
            AddAssert("drawer fully visible above footer", drawerLayoutProblems, () => Is.Empty);
            AddStep("scroll content to end", () => overlay.ChildrenOfType<ModSelectContent>().Single().RestoreScroll(double.MaxValue));
            AddWaitStep("settle content scroll", 5);
            AddAssert("drawer remains outside scrolling viewport", drawerLayoutProblems, () => Is.Empty);
            AddStep("hover drawer right edge", () => InputManager.MoveMouseTo(hiddenPool.ToScreenSpace(new Vector2(hiddenPool.DrawWidth - 20, hiddenPool.DrawHeight / 2))));
            AddUntilStep("drawer receives input", () => hiddenPool.IsHovered);
            AddStep("click drawer", () => InputManager.Click(MouseButton.Left));
            AddAssert("drawer click keeps editor open", () => overlay.State.Value == Visibility.Visible && overlay.LayoutModeButton.IsPresent);
            AddStep("switch layout", () => click(overlay.LayoutModeButton));
            AddUntilStep("opposite layout active", () => overlay.LayoutMode != mode);
            waitForContent();
            AddAssert("drawer remains visible after switch", drawerLayoutProblems, () => Is.Empty);
        }

        [TestCase(EzModSelectLayoutMode.Horizontal)]
        [TestCase(EzModSelectLayoutMode.Vertical)]
        public void TestLayoutSwitchOnlyVisibleWhileEditing(EzModSelectLayoutMode mode)
        {
            open(mode: mode);
            AddAssert("one layout switch", () => overlay.ChildrenOfType<EzModLayoutModeButton>().Count() == 1);
            AddAssert("switch hidden outside editing", () => !overlay.LayoutModeButton.IsPresent && !overlay.LayoutModeButton.Enabled.Value);
            AddAssert("saved mode reflected", () => overlay.LayoutMode == mode && overlay.LayoutModeButton.Active.Value == (mode == EzModSelectLayoutMode.Vertical));
            enterEditing();
            AddAssert("switch visible and enabled", () => overlay.LayoutModeButton.IsPresent && overlay.LayoutModeButton.Enabled.Value);
            AddStep("toggle layout", () => click(overlay.LayoutModeButton));
            AddUntilStep("opposite layout active", () => overlay.LayoutMode != mode);
            waitForContent();
            AddAssert("layout matches switch", () => groups.All(group => group.WrapItems == overlay.LayoutModeButton.Active.Value));
            AddAssert("mode saved", () => store.ModSelectLayoutMode == overlay.LayoutMode);
            AddStep("toggle back", () => click(overlay.LayoutModeButton));
            AddUntilStep("original layout active", () => overlay.LayoutMode == mode);
            waitForContent();
            AddStep("finish editing", clickEditToggle);
            AddAssert("switch hidden and disabled again", () => !overlay.LayoutModeButton.IsPresent && !overlay.LayoutModeButton.Enabled.Value);
            AddAssert("mode retained after editing", () => overlay.LayoutMode == mode && store.ModSelectLayoutMode == mode);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TestSwitchBothDirectionsForRuleset(int ruleset)
        {
            open(ruleset);
            ModState[] states = null!;
            string[] acronyms = null!;
            AddStep("capture content", () =>
            {
                states = overlay.AllAvailableMods.ToArray();
                acronyms = panels.Select(panel => panel.Mod.Acronym).Order().ToArray();
            });

            switchTo(EzModSelectLayoutMode.Vertical);
            AddAssert("vertical group order", () => groups.Where(group => group.IsPresent).Zip(groups.Where(group => group.IsPresent).Skip(1))
                .All(pair => pair.First.ScreenSpaceDrawQuad.Centre.Y < pair.Second.ScreenSpaceDrawQuad.Centre.Y));
            AddAssert("same states and content", () => states.SequenceEqual(overlay.AllAvailableMods)
                && acronyms.SequenceEqual(panels.Select(panel => panel.Mod.Acronym).Order()));
            AddAssert("mode saved globally", () => store.ModSelectLayoutMode == EzModSelectLayoutMode.Vertical);

            switchTo(EzModSelectLayoutMode.Horizontal);
            AddAssert("horizontal group order", () => groups.Where(group => group.IsPresent).Zip(groups.Where(group => group.IsPresent).Skip(1))
                .All(pair => pair.First.ScreenSpaceDrawQuad.Centre.X < pair.Second.ScreenSpaceDrawQuad.Centre.X));
            AddAssert("content preserved", () => states.SequenceEqual(overlay.AllAvailableMods)
                && acronyms.SequenceEqual(panels.Select(panel => panel.Mod.Acronym).Order()));
        }

        [TestCase(1024, 768, 0f)]
        [TestCase(1280, 720, 0.2f)]
        [TestCase(1920, 1080, 0f)]
        [TestCase(2560, 1080, 0.2f)]
        public void TestWrappingAndBounds(int width, int height, float shear)
        {
            open();
            AddStep("resize viewport", () =>
            {
                overlay.RelativeSizeAxes = Axes.None;
                overlay.Size = new Vector2(width, height);
                // Keep the simulated viewport inside the render surface so masking does not suspend visible layout.
                overlay.Scale = new Vector2(Math.Min(1, Math.Min(DrawWidth / width, DrawHeight / height)));
            });
            switchTo(EzModSelectLayoutMode.Vertical);
            enterEditing();
            AddStep("set shear", () => overlay.ChildrenOfType<EzLayoutShearControl>().Single().Current.Value = shear);
            AddWaitStep("settle layout", 5);
            AddAssert("visible cards remain inside sections", visibleBounds, () => Is.Empty);
            AddAssert("visible cards do not overlap", visibleOverlap, () => Is.Empty);

            for (int i = 0; i < EzModLayoutResolver.DEFAULT_TYPE_ORDER.Length; i++)
            {
                int index = i;
                AddStep($"reveal section {index}", () => verticalContent.RevealColumn(groups[index]));
                AddWaitStep("settle visible layout", 3);
                AddAssert("visible cards remain inside sections", visibleBounds, () => Is.Empty);
                AddAssert("visible cards do not overlap", visibleOverlap, () => Is.Empty);
            }

            AddStep("reveal largest section", () => verticalContent.RevealColumn(groups.MaxBy(group => group.AvailableMods.Count)!));
            AddWaitStep("settle wrapped layout", 3);
            AddAssert("cards actually wrap", () => groups.MaxBy(group => group.AvailableMods.Count)!
                .ChildrenOfType<EzModLayoutPanel>().Where(panel => panel.IsPresent).Select(panel => panel.Y).Distinct().Count() > 1);

            AddStep("scroll to final section", () => verticalContent.RestoreScroll(double.MaxValue));
            AddWaitStep("settle scroll", 3);
            AddAssert("last section bottom reachable", () => verticalContent.ToLocalSpace(groups.Last().ScreenSpaceDrawQuad.BottomRight - new Vector2(15)).Y, () => Is.InRange(0, verticalContent.DrawHeight));
            AddAssert("last section right edge reachable", () => verticalContent.ToLocalSpace(groups.Last().ScreenSpaceDrawQuad.BottomRight - new Vector2(15)).X, () => Is.InRange(0, verticalContent.DrawWidth));
            AddAssert("toolbar remains above viewport", () => overlay.LayoutModeButton.ScreenSpaceDrawQuad.Centre.Y < verticalContent.ScreenSpaceDrawQuad.Centre.Y);
        }

        [Test]
        public void TestSearchSelectionAndSettingsSurviveSwitch()
        {
            open();
            AddStep("select configured DT", () => SelectedMods.Value = new Mod[] { new OsuModDoubleTime { SpeedChange = { Value = 1.2 } } });
            AddStep("search HD", () => overlay.SearchTerm = "HD");
            switchTo(EzModSelectLayoutMode.Vertical);
            AddAssert("search retained", () => overlay.SearchTerm == "HD");
            AddUntilStep("HD is filtered result", () => panels.Single(panel => panel.Mod is OsuModHidden).Visible);
            AddStep("focus search", () => InputManager.Key(Key.Tab));
            AddStep("select search result", () => InputManager.Key(Key.Enter));
            AddUntilStep("HD selected", () => SelectedMods.Value.Any(mod => mod is OsuModHidden));
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddAssert("DT settings preserved", () => SelectedMods.Value.OfType<OsuModDoubleTime>().Single().SpeedChange.Value == 1.2);
            AddAssert("HD selection preserved", () => SelectedMods.Value.Any(mod => mod is OsuModHidden));

            AddStep("clear search and focus", () =>
            {
                overlay.SearchTerm = string.Empty;
                overlay.SearchTextBox.KillFocus();
            });
            AddStep("clear via shortcut", clearRestore);
            AddUntilStep("selection cleared", () => SelectedMods.Value.Count == 0);
            switchTo(EzModSelectLayoutMode.Vertical);
            AddStep("restore via shortcut", clearRestore);
            AddUntilStep("selection restored", () => SelectedMods.Value.Count == 2);
            AddAssert("restored settings intact", () => SelectedMods.Value.OfType<OsuModDoubleTime>().Single().SpeedChange.Value == 1.2);
        }

        [Test]
        public void TestEditingDraftSurvivesSwitchWithoutSaving()
        {
            open();
            enterEditing();
            int count = 0;
            AddStep("capture group count", () => count = groups.Length);
            AddStep("add group", () => click(overlay.ChildrenOfType<ShearedButton>().Single(button => button.Text.ToString() == EzModLayoutStrings.NEW_GROUP.ToString())));
            AddUntilStep("group added", () => groups.Length == count + 1);
            switchTo(EzModSelectLayoutMode.Vertical);
            AddAssert("still editing", () => overlay.ChildrenOfType<EzLayoutShearControl>().Single().Alpha > 0);
            AddAssert("draft preserved", () => groups.Length == count + 1);
            AddAssert("draft not saved by switch", () => (store.GetModLayout(Ruleset.Value.ShortName)?.Groups?.Count ?? count) == count);
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddAssert("draft still preserved", () => groups.Length == count + 1);
            AddStep("finish editing", clickEditToggle);
            AddUntilStep("draft saved", () => store.GetModLayout(Ruleset.Value.ShortName)!.Groups!.Count == count + 1);
        }

        [Test]
        public void TestRenameAndEmptySearchAcrossSwitch()
        {
            const string name = "A deliberately long custom group name that should be truncated without moving controls";
            open();
            enterEditing();
            AddStep("rename first group", () =>
            {
                InputManager.MoveMouseTo(groups[0].ToScreenSpace(new Vector2(65, 20)));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("rename focused", () => groups[0].ChildrenOfType<OsuTextBox>().Single().HasFocus);
            AddStep("enter new name", () => groups[0].ChildrenOfType<OsuTextBox>().Single().Text = name);
            switchTo(EzModSelectLayoutMode.Vertical);
            AddAssert("pending name committed to draft", () => groups[0].GroupName == name);
            AddStep("finish editing", clickEditToggle);
            AddAssert("name saved", () => store.GetModLayout("osu")!.Groups![0].Name == name);
            AddStep("search no results", () => overlay.SearchTerm = "no-such-mod-layout-test");
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddAssert("no matching cards", () => panels.All(panel => !panel.Visible));
            AddStep("clear search", () => overlay.SearchTerm = string.Empty);
            AddUntilStep("content returns", () => panels.Any(panel => panel.Visible));
            AddAssert("name retained", () => groups[0].GroupName == name);
        }

        [TestCase(EzModSelectLayoutMode.Horizontal)]
        [TestCase(EzModSelectLayoutMode.Vertical)]
        public void TestGroupDragOrderPersistsAcrossSwitch(EzModSelectLayoutMode mode)
        {
            open();
            switchTo(mode);
            enterEditing();
            int count = 0;
            AddStep("capture group count", () => count = groups.Length);

            for (int i = 0; i < 2; i++)
            {
                int expected = i + 1;
                AddStep("add empty group", () => click(overlay.ChildrenOfType<ShearedButton>().Single(button => button.Text.ToString() == EzModLayoutStrings.NEW_GROUP.ToString())));
                AddUntilStep("empty group loaded", () => groups.Length == count + expected && groups.All(group => group.IsLoaded && group.ItemsLoaded));
            }

            string movedId = string.Empty;
            AddStep("scroll to empty groups", () => overlay.ChildrenOfType<ModSelectContent>().Single().RestoreScroll(double.MaxValue));
            AddWaitStep("settle scroll", 4);
            AddStep("start group drag", () =>
            {
                var source = groups[^2];
                movedId = source.GroupId;
                var point = source.ToScreenSpace(new Vector2(70, 20));
                InputManager.MoveMouseTo(point);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(point + new Vector2(15));
            });
            AddStep("drop after final group", () =>
            {
                var target = groups[^1];
                InputManager.MoveMouseTo(target.ToScreenSpace(mode == EzModSelectLayoutMode.Vertical
                    ? new Vector2(70, target.DrawHeight - 3)
                    : new Vector2(target.DrawWidth - 3, 20)));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("group order changed", () => groups.Last().GroupId == movedId);
            AddStep("scroll to toolbar", () => InputManager.MoveMouseTo(overlay.LayoutModeButton));
            switchTo(mode == EzModSelectLayoutMode.Horizontal ? EzModSelectLayoutMode.Vertical : EzModSelectLayoutMode.Horizontal);
            AddAssert("order survives switch", () => groups.Last().GroupId == movedId);
            AddStep("finish editing", clickEditToggle);
            AddAssert("order saved", () => store.GetModLayout("osu")!.Groups!.Last().Id == movedId);
        }

        [Test]
        public void TestRepeatedSwitchAndReopen()
        {
            open();

            for (int i = 0; i < 4; i++)
            {
                switchTo(EzModSelectLayoutMode.Vertical);
                switchTo(EzModSelectLayoutMode.Horizontal);
            }

            AddStep("switch while loading", () =>
            {
                overlay.SetLayoutMode(EzModSelectLayoutMode.Vertical);
                overlay.SetLayoutMode(EzModSelectLayoutMode.Horizontal);
                overlay.SetLayoutMode(EzModSelectLayoutMode.Vertical);
            });
            waitForContent();
            AddAssert("only one card per mod", () => panels.GroupBy(panel => panel.Mod.GetType()).All(group => group.Count() == 1));
            AddStep("close overlay", () => overlay.Hide());
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
            AddStep("reopen overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);
            AddAssert("mode retained", () => overlay.LayoutMode == EzModSelectLayoutMode.Vertical);
            AddStep("change ruleset", () => Ruleset.Value = Rulesets.GetRuleset(3)!);
            waitForContent();
            AddAssert("global mode retained across ruleset", () => overlay.LayoutMode == EzModSelectLayoutMode.Vertical);
        }

        [Test]
        public void TestScrollPositionsRestoreIndependently()
        {
            open();
            switchTo(EzModSelectLayoutMode.Vertical);
            double vertical = 0;
            double horizontal = 0;
            AddStep("scroll vertical to end", () => verticalContent.RestoreScroll(double.MaxValue));
            AddWaitStep("settle vertical scroll", 5);
            AddStep("capture vertical position", () => vertical = verticalContent.ScrollPosition);
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddWaitStep("settle horizontal layout", 5);
            AddStep("scroll horizontal to end", () => overlay.ChildrenOfType<ModSelectContent>().Single().RestoreScroll(double.MaxValue));
            AddWaitStep("settle horizontal scroll", 5);
            AddStep("capture horizontal position", () => horizontal = overlay.ChildrenOfType<ModSelectContent>().Single().ScrollPosition);
            switchTo(EzModSelectLayoutMode.Vertical);
            AddWaitStep("restore vertical position", 5);
            AddAssert("vertical position retained", () => verticalContent.ScrollPosition, () => Is.EqualTo(vertical).Within(1));
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddWaitStep("restore horizontal position", 5);
            AddAssert("horizontal position retained", () => overlay.ChildrenOfType<ModSelectContent>().Single().ScrollPosition, () => Is.EqualTo(horizontal).Within(1));
        }

        [Test]
        public void TestRulesetChangeKeepsDraftWithOriginalRuleset()
        {
            open();
            switchTo(EzModSelectLayoutMode.Vertical);
            enterEditing();
            int count = 0;
            string[] acronyms = null!;
            AddStep("capture original layout", () =>
            {
                count = groups.Length;
                acronyms = panels.Select(panel => panel.Mod.Acronym).Order().ToArray();
            });
            AddStep("add draft group", () => click(overlay.ChildrenOfType<ShearedButton>().Single(button => button.Text.ToString() == EzModLayoutStrings.NEW_GROUP.ToString())));
            AddUntilStep("group added", () => groups.Length == count + 1);
            AddStep("change ruleset while editing", () => Ruleset.Value = Rulesets.GetRuleset(3)!);
            waitForContent();
            AddAssert("original ruleset draft saved", () => store.GetModLayout("osu")!.Groups!.Count == count + 1);
            AddAssert("original mod membership retained", () => store.GetModLayout("osu")!.Groups!.SelectMany(group => group.Acronyms!).Order().ToArray(), () => Is.EqualTo(acronyms));
            AddAssert("new ruleset not editing", () => overlay.ChildrenOfType<EzLayoutShearControl>().Single().Alpha == 0);
            AddAssert("global mode retained", () => overlay.LayoutMode == EzModSelectLayoutMode.Vertical);
        }

        [Test]
        public void TestPresetAndIncompatibleSelection()
        {
            open();
            AddStep("create preset", () => Realm.Write(realm => realm.Add(new ModPreset
            {
                Name = "Layout preset",
                Ruleset = realm.Find<RulesetInfo>("osu")!,
                Mods = new Mod[] { new OsuModHidden() },
            })));
            AddStep("search without matching preset", () => overlay.SearchTerm = "no-such-layout-preset");
            switchTo(EzModSelectLayoutMode.Vertical);
            AddUntilStep("preset loaded", () => overlay.ChildrenOfType<ModPresetPanel>().Any(panel => panel.IsLoaded));
            AddAssert("preset search retained", () => overlay.ChildrenOfType<ModPresetPanel>().All(panel => !panel.MatchingFilter));
            AddStep("search preset by name", () => overlay.SearchTerm = "Layout preset");
            AddUntilStep("preset matches", () => overlay.ChildrenOfType<ModPresetPanel>().First().MatchingFilter);
            AddAssert("matching preset prevents empty results", () => overlay.ChildrenOfType<OsuSpriteText>()
                .Single(text => text.Text.ToString() == EzModLayoutStrings.NO_RESULTS.ToString()).Alpha == 0);
            AddStep("apply preset", () => click(overlay.ChildrenOfType<ModPresetPanel>().First()));
            AddUntilStep("preset applied", () => SelectedMods.Value.OfType<OsuModHidden>().Any());

            selectSearchResult("DT");
            AddUntilStep("DT selected", () => SelectedMods.Value.OfType<OsuModDoubleTime>().Any());
            switchTo(EzModSelectLayoutMode.Horizontal);
            selectSearchResult("HT");
            AddUntilStep("HT replaces incompatible DT", () => SelectedMods.Value.OfType<OsuModHalfTime>().Any()
                && !SelectedMods.Value.OfType<OsuModDoubleTime>().Any());
        }

        [Test]
        public void TestKeyboardInputRunsOnceAfterRepeatedSwitches()
        {
            open();
            switchTo(EzModSelectLayoutMode.Vertical);
            switchTo(EzModSelectLayoutMode.Horizontal);
            switchTo(EzModSelectLayoutMode.Vertical);
            ModState target = null!;
            Key key = Key.Unknown;
            int changes = 0;
            AddStep("prepare keyboard selection", () =>
            {
                overlay.SearchTextBox.KillFocus();
                int groupIndex = Array.FindIndex(groups, group => group.AvailableMods.Count > 0);
                target = groups[groupIndex].AvailableMods[0];
                key = new[] { Key.Q, Key.A, Key.Z }[groupIndex];
                target.Active.ValueChanged += _ => changes++;
            });
            AddStep("press mod hotkey", () => InputManager.Key(key));
            AddUntilStep("mod selected", () => target.Active.Value);
            AddAssert("one activation", () => changes, () => Is.EqualTo(1));
            AddAssert("only target selected", () => SelectedMods.Value.Count == 1 && SelectedMods.Value[0].GetType() == target.Mod.GetType());
        }

        private void selectSearchResult(string term)
        {
            AddStep("collapse settings if open", () =>
            {
                if (overlay.ChildrenOfType<ModCustomisationPanel>().Single().ExpandedState.Value != ModCustomisationPanel.ModCustomisationPanelState.Collapsed)
                    InputManager.Key(Key.Escape);
            });
            AddStep("focus search", () => click(overlay.SearchTextBox));
            AddUntilStep("search focused", () => overlay.SearchTextBox.HasFocus);
            AddStep($"search {term}", () => overlay.SearchTerm = term);
            AddUntilStep("search result preselected", () => string.Join("; ", overlay.AllAvailableMods.Where(state => state.Preselected.Value).Select(state => state.Mod.Acronym)), () => Is.EqualTo(term));
            AddStep("select result", () => InputManager.Key(Key.Enter));
        }

        [Test]
        public void TestDragToHiddenPoolAndBack()
        {
            open();
            switchTo(EzModSelectLayoutMode.Vertical);
            enterEditing();
            EzModLayoutPanel source = null!;
            string acronym = string.Empty;
            AddStep("start dragging mod", () =>
            {
                source = panels.First(panel => panel.IsPresent);
                acronym = source.Mod.Acronym;
                source.Active.Value = true;
                InputManager.MoveMouseTo(source);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(source.ScreenSpaceDrawQuad.Centre + new Vector2(15));
            });
            AddStep("switch ignored during drag", () => overlay.SetLayoutMode(EzModSelectLayoutMode.Horizontal));
            AddAssert("switch disabled during drag", () => !overlay.LayoutModeButton.Enabled.Value);
            AddAssert("drag keeps current mode", () => overlay.LayoutMode == EzModSelectLayoutMode.Vertical);
            AddStep("drop in hidden pool", () =>
            {
                InputManager.MoveMouseTo(overlay.ChildrenOfType<EzModHiddenPool>().Single());
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("mod removed from groups", () => panels.All(panel => panel.Mod.Acronym != acronym));
            AddAssert("hidden mod remains selected", () => SelectedMods.Value.Any(mod => mod.Acronym == acronym));
            switchTo(EzModSelectLayoutMode.Horizontal);
            AddAssert("hidden state retained", () => panels.All(panel => panel.Mod.Acronym != acronym));
            switchTo(EzModSelectLayoutMode.Vertical);
            AddStep("scroll to first group", () => verticalContent.RestoreScroll(0));
            AddWaitStep("settle scroll", 3);
            AddStep("drag hidden icon to first group", () =>
            {
                var icon = overlay.ChildrenOfType<EzModHiddenPool>().Single().ChildrenOfType<osu.Game.Rulesets.UI.ModSwitchSmall>().Single();
                InputManager.MoveMouseTo(icon);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(icon.ScreenSpaceDrawQuad.Centre + new Vector2(15));
            });
            AddStep("drop into group", () =>
            {
                InputManager.MoveMouseTo(groups.First().ToScreenSpace(new Vector2(30, 55)));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("mod restored to group", () => panels.Any(panel => panel.Mod.Acronym == acronym));
        }

        private EzModSelectVerticalContent verticalContent => overlay.ChildrenOfType<EzModSelectVerticalContent>().Single();
        private EzModHiddenPool hiddenPool => overlay.ChildrenOfType<EzModHiddenPool>().Single();
        private AddPresetButton addPresetButton => overlay.ChildrenOfType<AddPresetButton>().Single();
        private OsuSpriteText addPresetLabel => addPresetButton.ChildrenOfType<OsuSpriteText>().Single(text => text.Text.ToString() == "+");

        private static float screenShear(Drawable drawable)
        {
            var top = drawable.ToScreenSpace(Vector2.Zero);
            var bottom = drawable.ToScreenSpace(new Vector2(0, drawable.DrawHeight));
            return (top.X - bottom.X) / (bottom.Y - top.Y);
        }

        private void resizeViewport(int width, int height) => AddStep("resize viewport", () =>
        {
            overlay.RelativeSizeAxes = Axes.None;
            overlay.Size = new Vector2(width, height);
            overlay.Scale = new Vector2(Math.Min(1, Math.Min(DrawWidth / width, DrawHeight / height)));
        });

        private string headerLayoutProblems()
        {
            var toolbar = (FillFlowContainer)overlay.LayoutModeButton.Parent!;
            var controls = new Drawable[] { overlay.SearchTextBox }.Concat(toolbar.Children.Where(control => control.IsPresent))
                .Append(overlay.ChildrenOfType<ModCustomisationHeader>().Single()).ToArray();
            var page = overlay.ScreenSpaceDrawQuad.AABBFloat;
            float top = controls[0].ScreenSpaceDrawQuad.AABBFloat.Top;
            float previousRight = page.Left;

            foreach (var control in controls)
            {
                var bounds = control.ScreenSpaceDrawQuad.AABBFloat;
                if (Math.Abs(bounds.Top - top) > 0.5f)
                    return $"{control.GetType().Name} is on a different row: {bounds.Top} instead of {top}";
                if (bounds.Left < previousRight - 0.5f || bounds.Right > page.Right + 0.5f)
                    return $"{control.GetType().Name} overlaps the previous control or exceeds the viewport";

                previousRight = bounds.Right;
            }

            return string.Empty;
        }

        private string drawerLayoutProblems()
        {
            var bounds = hiddenPool.ScreenSpaceDrawQuad.AABBFloat;
            var page = overlay.ScreenSpaceDrawQuad.AABBFloat;
            var viewport = overlay.ChildrenOfType<ModSelectContent>().Single().ScreenSpaceDrawQuad.AABBFloat;
            float footerTop = overlay.DisplayedFooterContent?.ScreenSpaceDrawQuad.AABBFloat.Top ?? page.Bottom;
            float footerClearance = overlay.DisplayedFooterContent?.DisplaysStackedVertically == true ? 75 : 15;
            float scale = overlay.ToScreenSpace(Vector2.UnitY).Y - overlay.ToScreenSpace(Vector2.Zero).Y;

            if (!hiddenPool.IsPresent || hiddenPool.Alpha < 0.9f)
                return "drawer is hidden";
            if (bounds.Left < page.Left - 0.5f || bounds.Right > page.Right + 0.5f || bounds.Top < page.Top)
                return "drawer extends beyond the viewport";
            if (bounds.Bottom > footerTop - footerClearance * scale + 0.5f)
                return $"drawer overlaps footer content: bottom={bounds.Bottom}, footer top={footerTop}";
            if (bounds.Top < viewport.Bottom - 0.5f)
                return "scrolling content overlaps the drawer";

            return string.Empty;
        }

        private bool isInViewport(Drawable drawable)
        {
            var centre = verticalContent.ToLocalSpace(drawable.ScreenSpaceDrawQuad.Centre);
            return centre.X >= -drawable.DrawWidth / 2 && centre.X <= verticalContent.DrawWidth + drawable.DrawWidth / 2
                && centre.Y >= -drawable.DrawHeight / 2 && centre.Y <= verticalContent.DrawHeight + drawable.DrawHeight / 2;
        }

        private string visibleBounds() => string.Join("; ", groups.SelectMany(group => group.ChildrenOfType<EzModLayoutPanel>()
            .Where(panel => panel.IsPresent && isInViewport(panel)).Select(panel =>
            {
                var topLeft = panel.ToSpaceOfOtherDrawable(Vector2.Zero, group);
                var bottomLeft = panel.ToSpaceOfOtherDrawable(new Vector2(0, panel.DrawHeight), group);
                var topRight = panel.ToSpaceOfOtherDrawable(new Vector2(panel.DrawWidth, 0), group);
                return Math.Min(topLeft.X, bottomLeft.X) >= -0.5f && topRight.X <= group.DrawWidth + 0.5f
                    ? string.Empty
                    : $"{group.GroupId}/{panel.Mod.Acronym}: left={Math.Min(topLeft.X, bottomLeft.X)}, right={topRight.X}, section width={group.DrawWidth}";
            })).Where(message => message.Length > 0));

        private string visibleOverlap()
        {
            foreach (var group in groups)
            {
                var cards = group.ChildrenOfType<EzModLayoutPanel>().Where(panel => panel.IsPresent && isInViewport(panel)).ToArray();

                for (int i = 0; i < cards.Length; i++)
                {
                    var first = cards[i].ToSpaceOfOtherDrawable(Vector2.Zero, group);

                    for (int j = i + 1; j < cards.Length; j++)
                    {
                        var second = cards[j].ToSpaceOfOtherDrawable(Vector2.Zero, group);
                        if (Math.Abs(first.Y - second.Y) < cards[i].DrawHeight - 0.5f && Math.Abs(first.X - second.X) < cards[i].DrawWidth - 0.5f)
                            return $"{cards[i].Mod.Acronym} overlaps {cards[j].Mod.Acronym} in {group.GroupId}";
                    }
                }
            }

            return string.Empty;
        }

        private void open(int ruleset = 0, EzModSelectLayoutMode mode = EzModSelectLayoutMode.Horizontal)
        {
            AddStep("set initial layout mode", () => store.SaveModSelectLayoutMode(mode));
            LoadSongSelect();
            AddStep("set ruleset", () => Ruleset.Value = Rulesets.GetRuleset(ruleset)!);
            AddStep("open mod overlay", () => InputManager.Key(Key.F1));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            waitForContent();
        }

        private void waitForContent() => AddUntilStep("content loaded", () => groups.Length == 0
            ? "no groups"
            : string.Join("; ", groups.Where(group => !group.IsLoaded || !group.ItemsLoaded)
                                     .Select(group => $"{group.GroupId}: loaded={group.IsLoaded}, items={group.ItemsLoaded}, alpha={group.Alpha}, mods={group.AvailableMods.Count}")), () => Is.Empty);

        private void switchTo(EzModSelectLayoutMode mode)
        {
            bool enteredEditing = false;
            AddStep("show layout switch", () =>
            {
                enteredEditing = !overlay.LayoutModeButton.IsPresent;
                if (enteredEditing)
                    clickEditToggle();
            });
            AddUntilStep("layout switch available", () => overlay.LayoutModeButton.IsPresent && overlay.LayoutModeButton.Enabled.Value);
            AddStep($"switch to {mode}", () =>
            {
                if (overlay.LayoutMode != mode)
                    click(overlay.LayoutModeButton);
            });
            AddUntilStep("layout mode changed", () => overlay.LayoutMode == mode);
            waitForContent();
            AddAssert("group layout matches mode", () => groups.All(group => group.WrapItems == (mode == EzModSelectLayoutMode.Vertical)));
            AddStep("restore editing state", () =>
            {
                if (enteredEditing)
                    clickEditToggle();
            });
            AddAssert("editing state restored", () => overlay.LayoutModeButton.IsPresent != enteredEditing);
        }

        private void enterEditing()
        {
            AddStep("enter editing", clickEditToggle);
            AddUntilStep("hidden pool visible", () => overlay.ChildrenOfType<EzModHiddenPool>().Single().Alpha > 0.9f);
        }

        private void clickEditToggle() => click(overlay.ChildrenOfType<FillFlowContainer>()
            .Single(flow => flow.ChildrenOfType<EzLayoutShearControl>().Any()).ChildrenOfType<ShearedButton>().First());

        private void click(Drawable drawable)
        {
            InputManager.MoveMouseTo(drawable);
            InputManager.Click(MouseButton.Left);
        }

        private void clearRestore()
        {
            InputManager.PressKey(Key.LAlt);
            InputManager.Key(Key.Number1);
            InputManager.ReleaseKey(Key.LAlt);
        }
    }
}
