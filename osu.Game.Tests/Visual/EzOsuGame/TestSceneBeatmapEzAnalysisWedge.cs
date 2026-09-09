// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Database;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Rulesets.Mania;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual.SongSelect;
using osuTK;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneBeatmapEzAnalysisWedge : SongSelectComponentsTestScene
    {
        private const string test_player = "ez-test-player";
        private const int test_keys = 4;

        private BeatmapEzAnalysisWedge wedge = null!;
        private EzSkillStore skillStore = null!;

        /// <summary>Match MetadataWedge host; DualPanel uses Horizontal RC|LN (not Auto on a pinned narrow box).</summary>
        protected override float InitialRelativeWidth => 0.5f;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            skillStore = new EzSkillStore(dependencies.Get<RealmAccess>());
            dependencies.CacheAs(new EzSkillProvider(skillStore));
            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Child = wedge = new BeatmapEzAnalysisWedge
            {
                State = { Value = Visibility.Visible },
            };
        }

        [Test]
        public void TestPopulatedWedge()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddUntilStep("skillset chips visible", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Count(c => c.Alpha > 0) >= 3);
            AddAssert("aggregate dan visible", () =>
                wedge.ChildrenOfType<EzDisplayDan>().Any(c => c.Alpha > 0 && Precision.AlmostEquals(c.Scale.X, 1.5f)));
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddStep("hide wedge", () => wedge.Hide());
            AddStep("show wedge", () => wedge.Show());
            AddUntilStep("chips still visible", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Any(c => c.Alpha > 0));
        }

        [Test]
        public void TestDanPanelPresentAndScaled()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddAssert("dan panel present", () => wedge.DanPanel != null);
            AddAssert("dan panel visible width", () => wedge.DanPanel.DrawWidth > 0);

            AddUntilStep("skillset chips loaded", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Any(c => c.Alpha > 0));

            AddAssert("display chips scaled 1.5", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Where(c => c.Alpha > 0).All(c =>
                    Precision.AlmostEquals(c.Scale.X, 1.5f) && Precision.AlmostEquals(c.Scale.Y, 1.5f)));

            AddAssert("header aggregate dan uses 1.5 scale", () =>
                wedge.ChildrenOfType<EzDisplayDan>().Any(c =>
                    Precision.AlmostEquals(c.Scale.X, 1.5f) && Precision.AlmostEquals(c.Scale.Y, 1.5f)));
        }

        [Test]
        public void TestDanPanelStaysInsideWedge()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddUntilStep("layout settled", () => wedge.DanPanel.DrawWidth > 0);

            AddAssert("dan panel left edge within wedge", () =>
            {
                float panelLeft = wedge.DanPanel.ToSpaceOfOtherDrawable(Vector2.Zero, wedge).X;
                return panelLeft >= -2f;
            });
        }

        [Test]
        public void TestDataSourceAndLayout()
        {
            AddStep("seed mania + skills", seedPopulatedScene);

            AddStep("source Chart", () => wedge.DanPanel.DataSource.Value = EzDanPanelDataSource.Chart);
            AddUntilStep("chart chips visible", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Any(c => c.Alpha > 0));

            AddStep("source Player", () => wedge.DanPanel.DataSource.Value = EzDanPanelDataSource.Player);
            AddUntilStep("player chips visible", () =>
                wedge.ChildrenOfType<EzDisplaySkillsDan>().Any(c => c.Alpha > 0));

            AddStep("source Both", () => wedge.DanPanel.DataSource.Value = EzDanPanelDataSource.Both);

            AddStep("layout Horizontal", () => wedge.DanPanel.DualLayout.Value = EzDanPanelDualLayout.Horizontal);
            AddStep("layout Vertical", () => wedge.DanPanel.DualLayout.Value = EzDanPanelDualLayout.Vertical);
            AddStep("layout Auto", () => wedge.DanPanel.DualLayout.Value = EzDanPanelDualLayout.Auto);
        }

        [Test]
        public void TestFourKeySkillsetSlotsNotMinaAxes()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddUntilStep("rc has four skillset chips", () =>
                wedge.ChildrenOfType<EzDanLabeledStatList>()
                     .Where(l => l.Side == EzDanSide.Rc)
                     .SelectMany(l => l.ChildrenOfType<EzDisplaySkillsDan>())
                     .Count(c => c.Alpha > 0) == 4);

            AddAssert("ln 4k has no skillset chips", () =>
                wedge.ChildrenOfType<EzDanLabeledStatList>()
                     .Where(l => l.Side == EzDanSide.Ln)
                     .SelectMany(l => l.ChildrenOfType<EzDisplaySkillsDan>())
                     .All(c => c.Alpha <= 0));

            AddAssert("slot table is four for 4k rc", () =>
                EzDanSkillsetBuckets.Slots(test_keys, EzDanSide.Rc).Count == 4
                && EzDanSkillsetBuckets.Slots(test_keys, EzDanSide.Ln).Count == 0);
        }

        [Test]
        public void TestHeaderAggregatesBothSides()
        {
            AddStep("seed mania + skills", seedPopulatedScene);
            AddUntilStep("both side lists present", () =>
                wedge.ChildrenOfType<EzDanLabeledStatList>().Count() == 2);

            AddAssert("rc and ln player aggregates seeded", () =>
            {
                var rc = skillStore.GetDanEstimate(test_player, test_keys, EzDanSide.Rc.ToId());
                var ln = skillStore.GetDanEstimate(test_player, test_keys, EzDanSide.Ln.ToId());
                return rc != null && ln != null
                       && !string.IsNullOrEmpty(rc.Label) && !string.IsNullOrEmpty(ln.Label)
                       && rc.Label != ln.Label;
            });

            AddUntilStep("header aggregate dans visible", () =>
                wedge.ChildrenOfType<EzDisplayDan>().Count(c => c.Alpha > 0 && Precision.AlmostEquals(c.Scale.X, 1.5f)) >= 2);
        }

        private void seedPopulatedScene()
        {
            wedge.DanPanel.DataSource.Value = EzDanPanelDataSource.Both;
            wedge.DanPanel.DualLayout.Value = EzDanPanelDualLayout.Horizontal;
            var beatmap = CreateWorkingBeatmap(new TestBeatmap(new ManiaRuleset().RulesetInfo));
            beatmap.BeatmapInfo.Difficulty.CircleSize = test_keys;
            beatmap.BeatmapInfo.XxyStarRating = 18.5;

            string hash = beatmap.BeatmapInfo.Hash;

            if (string.IsNullOrEmpty(hash))
            {
                // Test beatmaps sometimes ship empty hash; DualPanel keys off Hash for MSD lookup.
                hash = "ez-analysis-wedge-test-hash";
                beatmap.BeatmapInfo.Hash = hash;
            }

            var chartMsd = new EzSkillsetVector(
                Overall: 22.4,
                Stream: 21.0,
                Jumpstream: 23.5,
                Handstream: 20.2,
                Stamina: 19.8,
                JackSpeed: 24.1,
                Chordjack: 18.6,
                Technical: 22.0);

            var playerSsr = new EzSkillsetVector(
                Overall: 20.1,
                Stream: 19.5,
                Jumpstream: 21.2,
                Handstream: 18.8,
                Stamina: 17.9,
                JackSpeed: 22.4,
                Chordjack: 16.5,
                Technical: 20.0);

            skillStore.WriteBeatmapMsd(hash, chartMsd, beatmap.BeatmapInfo.ID, holdRatio: 0.22);
            skillStore.WritePlayerSsr(test_player, test_keys, playerSsr, analyzedPlays: 40, provisional: false);

            writeDan(test_player, EzDanSide.Rc, chartMsd.Overall, clears: 12);
            writeDan(test_player, EzDanSide.Ln, chartMsd.Overall * 0.92, clears: 5);

            Beatmap.Value = beatmap;
            SelectedMods.Value = [];
            wedge.TargetUsername.Value = test_player;
            wedge.LeftRadarMode.Value = EzRadarDisplayMode.XxySrPattern;
            wedge.RightRadarMode.Value = EzRadarDisplayMode.Skill;
            wedge.Show();
        }

        private void writeDan(string username, EzDanSide side, double overallSr, int clears)
        {
            double raw = EzDanLabels.SrToRawDan(overallSr, EzMinaSkillAxis.Stream);
            string label = EzDanLadders.For(test_keys, side).ParseLabel(raw);

            skillStore.WriteDanEstimate(new EzDanEstimate
            {
                Username = username,
                KeyCount = test_keys,
                Side = side.ToId(),
                RawDan = raw,
                Label = label,
                Clears = clears,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
                ComputedAt = DateTimeOffset.UtcNow,
            });
        }
    }
}
