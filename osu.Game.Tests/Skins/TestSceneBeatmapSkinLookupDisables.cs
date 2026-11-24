// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    [HeadlessTest]
    public partial class TestSceneBeatmapSkinLookupDisables : OsuTestScene
    {
        private UserSkinSource userSource;
        private BeatmapSkinProvidingContainer beatmapSkinProvider;
        private BeatmapSkinSource beatmapSource;
        private SkinRequester requester;
        private SkinnableDrawable sliderBall;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Add(new SkinProvidingContainer(userSource = new UserSkinSource())
                .WithChild(beatmapSkinProvider = new BeatmapSkinProvidingContainer(beatmapSource = new BeatmapSkinSource())
                    .WithChildren(new Drawable[]
                    {
                        requester = new SkinRequester(),
                        sliderBall = new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.SliderBall), _ => Empty()),
                    })));
        });

        [TestCase(false)]
        [TestCase(true)]
        public void TestDrawableLookup(bool allowBeatmapLookups)
        {
            AddStep($"Set beatmap skin enabled to {allowBeatmapLookups}", () => beatmapSkinProvider.BeatmapSkins.Value = allowBeatmapLookups);

            string expected = allowBeatmapLookups ? "beatmap" : "user";

            AddAssert($"Check lookup is from {expected}", () => requester.GetDrawableComponent(new TestSkinComponentLookup())?.Name == expected);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestProviderLookup(bool allowBeatmapLookups)
        {
            AddStep($"Set beatmap skin enabled to {allowBeatmapLookups}", () => beatmapSkinProvider.BeatmapSkins.Value = allowBeatmapLookups);

            ISkin expected() => allowBeatmapLookups ? beatmapSource : userSource;

            AddAssert("Check lookup is from correct source", () => requester.FindProvider(s => s.GetDrawableComponent(new TestSkinComponentLookup()) != null) == expected());
        }

        [TestCase(true, true, OsuSkinColour.SliderTrackOverride)]
        [TestCase(true, false, OsuSkinColour.SliderTrackOverride)]
        [TestCase(false, true, OsuSkinColour.SliderTrackOverride)]
        [TestCase(false, false, OsuSkinColour.SliderTrackOverride)]
        [TestCase(true, true, OsuSkinColour.SliderBorder)]
        [TestCase(true, false, OsuSkinColour.SliderBorder)]
        [TestCase(false, true, OsuSkinColour.SliderBorder)]
        [TestCase(false, false, OsuSkinColour.SliderBorder)]
        [TestCase(true, true, OsuSkinColour.SliderBall)]
        [TestCase(true, false, OsuSkinColour.SliderBall)]
        [TestCase(false, true, OsuSkinColour.SliderBall)]
        [TestCase(false, false, OsuSkinColour.SliderBall)]
        public void TestSliderColoursFollowBeatmapColourSetting(bool allowBeatmapSkins, bool allowBeatmapColours, OsuSkinColour colour)
        {
            AddStep("set beatmap lookup toggles", () =>
            {
                beatmapSkinProvider.BeatmapSkins.Value = allowBeatmapSkins;
                beatmapSkinProvider.BeatmapColours.Value = allowBeatmapColours;
            });

            Color4 expected = allowBeatmapColours
                ? BeatmapSkinSource.GetSliderColour(colour)
                : UserSkinSource.GetSliderColour(colour);

            AddAssert("slider colour source respects BeatmapColours", () =>
            {
                var actual = requester.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(colour));
                return actual?.Value == expected;
            });
        }

        [Test]
        public void TestSliderColoursFollowRuntimeToggleChanges()
        {
            const OsuSkinColour colour = OsuSkinColour.SliderBall;

            AddStep("enable beatmap colours", () => beatmapSkinProvider.BeatmapColours.Value = true);
            AddAssert("beatmap slider colour is active", () => requester.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(colour))?.Value == BeatmapSkinSource.GetSliderColour(colour));

            AddStep("disable beatmap colours", () => beatmapSkinProvider.BeatmapColours.Value = false);
            AddAssert("user slider colour is restored", () => requester.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(colour))?.Value == UserSkinSource.GetSliderColour(colour));

            AddStep("enable beatmap colours again", () => beatmapSkinProvider.BeatmapColours.Value = true);
            AddAssert("beatmap slider colour is restored", () => requester.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(colour))?.Value == BeatmapSkinSource.GetSliderColour(colour));
        }

        [Test]
        public void TestLegacySliderBallUsesFilteredColourSource()
        {
            AddStep("enable beatmap skins and colours", () =>
            {
                beatmapSkinProvider.BeatmapSkins.Value = true;
                beatmapSkinProvider.BeatmapColours.Value = true;
            });
            AddUntilStep("beatmap slider ball loaded", () => sliderBall.Drawable is LegacySliderBall);
            AddAssert("beatmap slider ball colour is active", () => ((LegacySliderBall)sliderBall.Drawable).BallColour == BeatmapSkinSource.GetSliderColour(OsuSkinColour.SliderBall));

            AddStep("disable beatmap colours", () => beatmapSkinProvider.BeatmapColours.Value = false);
            AddUntilStep("user slider ball colour restored", () => sliderBall.Drawable is LegacySliderBall ball && ball.BallColour == UserSkinSource.GetSliderColour(OsuSkinColour.SliderBall));

            AddStep("enable beatmap colours again", () => beatmapSkinProvider.BeatmapColours.Value = true);
            AddUntilStep("beatmap slider ball colour restored", () => sliderBall.Drawable is LegacySliderBall ball && ball.BallColour == BeatmapSkinSource.GetSliderColour(OsuSkinColour.SliderBall));
        }

        [Test]
        public void TestHitsoundLookupEnabled()
        {
            AddStep("Set beatmap hitsounds to enabled", () => beatmapSkinProvider.BeatmapHitsounds.Value = true);
            AddAssert("Check hitsound lookup succeeds", () => beatmapSkinProvider.GetSample(new HitSampleInfo("test")), () => Is.Not.Null);
        }

        [Test]
        public void TestHitsoundLookupDisabled()
        {
            AddStep("Set beatmap hitsounds to disables", () => beatmapSkinProvider.BeatmapHitsounds.Value = false);
            AddAssert("Check hitsound lookup fails", () => beatmapSkinProvider.GetSample(new HitSampleInfo("test")), () => Is.Null);
        }

        [Test]
        public void TestStoryboardSoundLookupBypassesHitsoundDisable()
        {
            AddStep("Set beatmap hitsounds to disables", () => beatmapSkinProvider.BeatmapHitsounds.Value = false);
            AddAssert("Check storyboard sound lookup succeeds", () => beatmapSkinProvider.GetSample(new StoryboardSampleInfo(StoryboardElementSource.Beatmap, "test", 0, 1)), () => Is.Not.Null);
        }

        public class UserSkinSource : LegacySkin
        {
            public UserSkinSource()
                : base(new SkinInfo(), null, null, string.Empty)
            {
                foreach (OsuSkinColour colour in Enum.GetValues<OsuSkinColour>())
                    Configuration.CustomColours[colour.ToString()] = GetSliderColour(colour);
            }

            public static Color4 GetSliderColour(OsuSkinColour colour) => colour switch
            {
                OsuSkinColour.SliderTrackOverride => Color4.Red,
                OsuSkinColour.SliderBorder => Color4.Orange,
                OsuSkinColour.SliderBall => Color4.Yellow,
                _ => Color4.White,
            };

            public override Drawable GetDrawableComponent(ISkinComponentLookup lookup)
            {
                return new Container { Name = "user" };
            }

            public override ISample GetSample(ISampleInfo sampleInfo) => null;
        }

        public class BeatmapSkinSource : LegacyBeatmapSkin
        {
            public BeatmapSkinSource()
                : base(new TestBeatmap(new OsuRuleset().RulesetInfo).BeatmapInfo, null)
            {
                foreach (OsuSkinColour colour in Enum.GetValues<OsuSkinColour>())
                    Configuration.CustomColours[colour.ToString()] = GetSliderColour(colour);
            }

            public static Color4 GetSliderColour(OsuSkinColour colour) => colour switch
            {
                OsuSkinColour.SliderTrackOverride => Color4.Blue,
                OsuSkinColour.SliderBorder => Color4.Cyan,
                OsuSkinColour.SliderBall => Color4.Magenta,
                _ => Color4.White,
            };

            public override Drawable GetDrawableComponent(ISkinComponentLookup lookup)
            {
                if (lookup is OsuSkinComponentLookup osuLookup && osuLookup.Component == OsuSkinComponents.SliderBall)
                    return new LegacySliderBall(this);

                return new Container { Name = "beatmap" };
            }

            public override ISample GetSample(ISampleInfo sampleInfo) => new SampleVirtual();
        }

        public partial class SkinRequester : Drawable, ISkin
        {
            private ISkinSource skin;

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                this.skin = skin;
            }

            public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => skin.GetDrawableComponent(lookup);

            public Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => skin.GetTexture(componentName, wrapModeS, wrapModeT);

            public ISample GetSample(ISampleInfo sampleInfo) => skin.GetSample(sampleInfo);

            public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => skin.GetConfig<TLookup, TValue>(lookup);

            public ISkin FindProvider(Func<ISkin, bool> lookupFunction) => skin.FindProvider(lookupFunction);
        }

        private class TestSkinComponentLookup : ISkinComponentLookup
        {
            public string LookupName => string.Empty;
        }
    }
}
