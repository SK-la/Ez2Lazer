// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditor : EditorTestScene
    {
        private readonly Bindable<ManiaScrollingDirection> direction = new Bindable<ManiaScrollingDirection>();

        protected override Ruleset CreateEditorRuleset() => new ManiaRuleset();

        public TestSceneEditor()
        {
            AddStep("upwards scroll", () => direction.Value = ManiaScrollingDirection.Up);
            AddStep("downwards scroll", () => direction.Value = ManiaScrollingDirection.Down);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (ManiaRulesetConfigManager)RulesetConfigs.GetConfigFor(Ruleset.Value.CreateInstance()).AsNonNull();
            config.BindWith(ManiaRulesetSetting.ScrollDirection, direction);
        }

        [Test]
        public void TestReloadOnBPMChange()
        {
            HitObjectComposer oldComposer = null!;
            bool controlPointsChanged = false;

            AddStep("ensure timing point exists", () =>
            {
                if (EditorBeatmap.ControlPointInfo.TimingPoints.Any())
                    return;

                EditorBeatmap.BeginChange();
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.EndChange();
            });
            AddStep("store composer", () => oldComposer = this.ChildrenOfType<HitObjectComposer>().Single());
            AddUntilStep("composer stored", () => oldComposer, () => Is.Not.Null);
            AddStep("switch to timing tab", () => Editor.Mode.Value = EditorScreenMode.Timing);
            AddStep("change timing point BPM", () =>
            {
                var lastTimingPoint = EditorBeatmap.ControlPointInfo.TimingPoints.Last();
                EditorBeatmap.ControlPointInfo.ControlPointsChanged += () => controlPointsChanged = true;
                EditorBeatmap.BeginChange();
                EditorBeatmap.ControlPointInfo.Add(lastTimingPoint.Time + 1000, new TimingControlPoint { BeatLength = lastTimingPoint.BeatLength + 1 });
                EditorBeatmap.EndChange();
                EditorBeatmap.SaveState();
            });
            AddUntilStep("control point change observed", () => controlPointsChanged);
            AddStep("switch back to composer", () => Editor.Mode.Value = EditorScreenMode.Compose);
            AddUntilStep("composer reloaded", () =>
            {
                var composer = this.ChildrenOfType<HitObjectComposer>().SingleOrDefault();
                return composer != null && composer != oldComposer;
            });
        }
    }
}
