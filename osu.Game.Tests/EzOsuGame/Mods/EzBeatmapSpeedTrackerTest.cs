// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Mods;
using osu.Game.EzOsuGame.Mods.LAsMods;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Mods
{
    [TestFixture]
    public class EzBeatmapSpeedTrackerTest
    {
        #region ResolveBeatLength

        [Test]
        public void TestResolveBeatLengthFollowsActiveTimingPoint()
        {
            var beatmap = createBeatmap((0, 500), (10000, 400));

            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 0, 0), Is.EqualTo(500).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 5000, 0), Is.EqualTo(500).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 10000, 0), Is.EqualTo(400).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 99999, 0), Is.EqualTo(400).Within(0.001));
        }

        [Test]
        public void TestResolveBeatLengthWithoutSongTimeUsesMostCommon()
        {
            var beatmap = createBeatmap((0, 500), (10000, 400));

            // With nothing playing there is no section to report, so the representative beat length stands - the same
            // value the song select title labels "mostly".
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, null, 0), Is.EqualTo(beatmap.GetMostCommonBeatLength()).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, null, 0), Is.EqualTo(500).Within(0.001));
        }

        [Test]
        public void TestResolveBeatLengthFallsBackToBpm()
        {
            var empty = new Beatmap();

            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(empty, null, 120), Is.EqualTo(500).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(null, null, 120), Is.EqualTo(500).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(empty, null, 0), Is.EqualTo(0));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(null, 1234, 0), Is.EqualTo(0));
        }

        [Test]
        public void TestBeatmapResolvesSectionAtPlayheadOrMostCommon()
        {
            // The review scenario: an SV-style chart with a mid-song BPM change. The playhead decides, so the reported
            // beat length tracks the section being played rather than an average - and falls back to the average when
            // there is no playhead.
            var beatmap = createBeatmap((0, 500), (10000, 60000.0 / 180));

            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 5000, 0), Is.EqualTo(500).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, 15000, 0), Is.EqualTo(60000.0 / 180).Within(0.001));
            Assert.That(EzBeatmapSpeedTracker.ResolveBeatLength(beatmap, null, 0), Is.EqualTo(beatmap.GetMostCommonBeatLength()).Within(0.001));
        }

        #endregion

        #region ResolveTrackRate

        [Test]
        public void TestResolveTrackRateCarriesTheFinalRateOfRampMods()
        {
            // Wind Up is IApplicableToRate but neither ModRateAdjust nor ILinkedDynamicSpeedHUD, so the previously
            // considered rate sources reported 1x for it. The track it is applied to plays at its final rate in song
            // select, so that is what the tracker reads.
            var windUp = new ModWindUp();
            windUp.InitialRate.Value = 1.1;
            windUp.FinalRate.Value = 1.4;

            Assert.That(EzModRate.ResolvePlaybackRate(new Mod[] { windUp }), Is.EqualTo(1.1).Within(0.001));
            Assert.That(EzModRate.Resolve(new Mod[] { windUp }), Is.EqualTo(1f).Within(0.001f));

            var adjustments = new AudioAdjustments();
            windUp.ApplyToTrack(adjustments);

            // Adjust pitch defaults on, so the ramp registers as a Frequency adjustment, promoted to the track at its
            // final rate (the song select preview value).
            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(adjustments.AggregateFrequency, adjustments.AggregateTempo), Is.EqualTo(1.4).Within(0.0001));

            double? observed = null;
            adjustments.AggregateFrequency.BindValueChanged(change => observed = change.NewValue);

            // Mid-ramp the mod rewrites SpeedChange every frame; the aggregate follows each change, so a consumer does
            // not have to poll for it.
            windUp.SpeedChange.Value = 1.32;

            Assert.That(observed, Is.EqualTo(1.32).Within(0.0001));
            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(adjustments.AggregateFrequency, adjustments.AggregateTempo), Is.EqualTo(1.32).Within(0.0001));
        }

        [Test]
        public void TestResolveTrackRateCoversBothPitchModesAndComposes()
        {
            // Nice BPM keeps pitch, so its speed registers as a Tempo adjustment - the played rate is
            // Frequency * Tempo, which is why both aggregates are multiplied.
            var niceBpm = new ModNiceBPM();
            var adjustments = new AudioAdjustments();

            niceBpm.InitialRate.Value = 1.25;
            niceBpm.ApplyToTrack(adjustments);

            Assert.That(adjustments.AggregateFrequency.Value, Is.EqualTo(1).Within(0.0001));
            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(adjustments.AggregateFrequency, adjustments.AggregateTempo), Is.EqualTo(1.25).Within(0.0001));

            // GameplaySpeed is the full-precision runtime value the audio binds to, while SpeedChange is the
            // 0.01-quantised display mirror - so the rate follows what is actually played.
            niceBpm.GameplaySpeed.Value = 1.2537;

            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(adjustments.AggregateFrequency, adjustments.AggregateTempo), Is.EqualTo(1.2537).Within(0.0001));
            Assert.That(niceBpm.SpeedChange.Value, Is.EqualTo(1.25).Within(0.0001));

            // A pitch-adjusting mod composes with it, because the two land in different aggregates.
            new OsuModDoubleTime().ApplyToTrack(adjustments);

            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(adjustments.AggregateFrequency, adjustments.AggregateTempo), Is.EqualTo(1.2537 * 1.5).Within(0.001));

            Assert.That(EzBeatmapSpeedTracker.ResolveTrackRate(null, null), Is.EqualTo(1));
        }

        #endregion

        #region ResolveTimingBeatmap

        [Test]
        public void TestResolveTimingBeatmapNeverConverts()
        {
            // Conversion never rewrites timing points (the converted beatmap shares the source ControlPointInfo), so the
            // loaded beatmap is used directly rather than paying for a full synchronous conversion on every selection
            // change - which would also run each mod's ApplyToBeatmap as a side effect, and with the wrong mod set when
            // browsing a beatmap from another ruleset.
            var working = new CountingWorkingBeatmap(createBeatmap((0, 500)));

            Assert.That(EzBeatmapSpeedTracker.ResolveTimingBeatmap(null, working), Is.SameAs(working.Beatmap));
            Assert.That(working.PlayableBeatmapRequests, Is.Zero);
        }

        [Test]
        public void TestResolveTimingBeatmapPrefersGameplayBeatmap()
        {
            var working = new CountingWorkingBeatmap(createBeatmap((0, 500)));
            var gameplay = createBeatmap((0, 400));

            Assert.That(EzBeatmapSpeedTracker.ResolveTimingBeatmap(gameplay, working), Is.SameAs(gameplay));
            Assert.That(working.PlayableBeatmapRequests, Is.Zero);
            Assert.That(EzBeatmapSpeedTracker.ResolveTimingBeatmap(null, null), Is.Null);
        }

        [Test]
        public void TestNeedsBeatmapLoadOnlyWhileSelectionIsDecoding()
        {
            // A carousel move hands out a WorkingBeatmap whose decode has only just started, and reading its timing
            // points would block the update thread on that decode. The tracker stands in with the metadata BPM and
            // waits for this flag to clear instead.
            var working = new CountingWorkingBeatmap(createBeatmap((0, 500)));

            Assert.That(EzBeatmapSpeedTracker.NeedsBeatmapLoad(null, working), Is.False);

            working.Loaded = false;
            Assert.That(EzBeatmapSpeedTracker.NeedsBeatmapLoad(null, working), Is.True);

            // Gameplay never waits: its beatmap is the already loaded, mod-applied one.
            Assert.That(EzBeatmapSpeedTracker.NeedsBeatmapLoad(createBeatmap((0, 400)), working), Is.False);

            // Nothing selected is nothing to wait for.
            Assert.That(EzBeatmapSpeedTracker.NeedsBeatmapLoad(null, null), Is.False);
        }

        #endregion

        #region EzModRate.ResolvePlaybackRate

        [Test]
        public void TestResolvePlaybackRateCoversEveryApplicableToRateMod()
        {
            // Resolve is difficulty-facing and only sees ModRateAdjust, so it silently reports 1x for these.
            var adaptive = new ModAdaptiveSpeed();
            adaptive.InitialRate.Value = 1.4;

            Assert.That(EzModRate.ResolvePlaybackRate(new Mod[] { adaptive }), Is.EqualTo(1.4).Within(0.001));
            Assert.That(EzModRate.Resolve(new Mod[] { adaptive }), Is.EqualTo(1f).Within(0.001f));

            // Rate mods of different types still compose.
            Assert.That(EzModRate.ResolvePlaybackRate(new Mod[] { new OsuModDoubleTime(), adaptive }), Is.EqualTo(1.5 * 1.4).Within(0.001));
        }

        [Test]
        public void TestResolvePlaybackRateDoesNotClamp()
        {
            // Real ranges are wider than the difficulty-facing clamp (Nice BPM 0.1-3.0, the adaptive-speed family
            // 0.4-2.5), so a playback rate must not be clamped to Resolve's bounds.
            var mod = new TestRateMod();
            mod.SpeedChange.Value = 2.5;

            Assert.That(EzModRate.ResolvePlaybackRate(new Mod[] { mod }), Is.EqualTo(2.5).Within(0.001));
            Assert.That(EzModRate.Resolve(new Mod[] { mod }), Is.EqualTo(2f).Within(0.001f));
        }

        #endregion

        #region EzModRate.ResolveBindable

        [Test]
        public void TestResolveBindableTracksRateModSettingChanges()
        {
            var mod = new OsuModDoubleTime();
            var resolved = EzModRate.ResolveBindable(new Mod[] { mod });

            Assert.That(resolved.Value, Is.EqualTo(1.5f).Within(0.001f));

            mod.SpeedChange.Value = 1.8;
            Assert.That(resolved.Value, Is.EqualTo(1.8f).Within(0.001f));
        }

        [Test]
        public void TestResolveBindableStacksAndClamps()
        {
            var stacked = EzModRate.ResolveBindable(new Mod[] { new OsuModDoubleTime(), new OsuModHalfTime() });

            // 1.5 * 0.75
            Assert.That(stacked.Value, Is.EqualTo(1.125f).Within(0.001f));

            var clamped = EzModRate.ResolveBindable(new Mod[] { new OsuModDoubleTime(), new OsuModDoubleTime() });

            // 1.5 * 1.5 = 2.25, clamped to the lazer rate bounds.
            Assert.That(clamped.Value, Is.EqualTo(2f).Within(0.001f));
        }

        #endregion

        #region Helpers

        private static Beatmap createBeatmap(params (double time, double beatLength)[] timingPoints)
        {
            var beatmap = new Beatmap();

            foreach ((double time, double beatLength) in timingPoints)
                beatmap.ControlPointInfo.Add(time, new TimingControlPoint { BeatLength = beatLength });

            return beatmap;
        }

        /// <summary>
        /// Working beatmap double recording how often a full conversion was requested.
        /// </summary>
        private class CountingWorkingBeatmap : TestWorkingBeatmap
        {
            public int PlayableBeatmapRequests { get; private set; }

            /// <summary>
            /// Whether the asynchronous beatmap decode has finished, mirroring <see cref="WorkingBeatmap.BeatmapLoaded"/>.
            /// </summary>
            public bool Loaded { get; set; } = true;

            public override bool BeatmapLoaded => Loaded;

            public CountingWorkingBeatmap(IBeatmap beatmap)
                : base(beatmap)
            {
            }

            public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            {
                PlayableBeatmapRequests++;
                return new Beatmap();
            }
        }

        /// <summary>
        /// Minimal rate-only mod with Nice BPM's bounds, so a rate outside the difficulty-facing clamp can be
        /// exercised.
        /// </summary>
        private class TestRateMod : ModRateAdjust
        {
            public override string Name => "Test Rate";

            public override string Acronym => "TR";

            public override LocalisableString Description => "Test rate mod";

            public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1.5)
            {
                MinValue = 0.1,
                MaxValue = 3.0,
            };

            public override void ApplyToTrack(IAdjustableAudioComponent track)
            {
            }
        }

        #endregion
    }
}
