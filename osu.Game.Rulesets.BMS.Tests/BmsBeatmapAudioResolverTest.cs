// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsBeatmapAudioResolverTest
    {
        [Test]
        public void TestReturnsNullForNonExternalSet()
        {
            var info = new BeatmapInfo(new BMSRuleset().RulesetInfo)
            {
                BeatmapSet = new BeatmapSetInfo(),
            };

            Assert.That(BmsBeatmapAudioResolver.TryPrepare(new TestWorkingBeatmap(info), null!, preloadKeysounds: false), Is.Null);
        }

        [Test]
        public void TestReturnsNullForNonBmsRuleset()
        {
            var info = new BeatmapInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                BeatmapSet = new BeatmapSetInfo
                {
                    HostingKind = BeatmapSetHostingKind.External,
                    ExternalContentRoot = @"C:\fake\bms",
                },
            };

            Assert.That(BmsBeatmapAudioResolver.TryPrepare(new TestWorkingBeatmap(info), null!, preloadKeysounds: false), Is.Null);
        }

        private class TestWorkingBeatmap : WorkingBeatmap
        {
            public TestWorkingBeatmap(BeatmapInfo beatmapInfo)
                : base(beatmapInfo, null)
            {
            }

            protected override IBeatmap GetBeatmap() => new BMSBeatmap { BeatmapInfo = BeatmapInfo };

            protected override Track GetBeatmapTrack() => null!;

            protected override ISkin GetSkin() => null!;

            public override Texture? GetBackground() => null;

            public override Stream? GetStream(string storagePath) => null;
        }
    }
}
