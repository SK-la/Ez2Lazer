// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Drawables;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneStoryboardSamples : OsuTestScene, IStorageResourceProvider
    {
        [Resolved]
        private OsuConfigManager config { get; set; }

        [Resolved]
        private GameHost host { get; set; }

        [Test]
        public void TestRetrieveTopLevelSample()
        {
            ISkin skin = null;
            ISample channel = null;

            AddStep("create skin", () => skin = new TestSkin("test-sample", this));
            AddStep("retrieve sample", () => channel = skin.GetSample(new SampleInfo("test-sample")));

            AddAssert("sample is non-null", () => channel != null);
        }

        [Test]
        public void TestRetrieveSampleInSubFolder()
        {
            ISkin skin = null;
            ISample channel = null;

            AddStep("create skin", () => skin = new TestSkin("folder/test-sample", this));
            AddStep("retrieve sample", () => channel = skin.GetSample(new SampleInfo("folder/test-sample")));

            AddAssert("sample is non-null", () => channel != null);
        }

        [Test]
        public void TestSamplePlaybackAtZero()
        {
            GameplayClockContainer gameplayContainer = null;
            DrawableStoryboardSample sample = null;

            AddStep("create container", () =>
            {
                var working = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);

                Add(gameplayContainer = new MasterGameplayClockContainer(working, 0)
                {
                    Child = new FrameStabilityContainer
                    {
                        Child = sample = new DrawableStoryboardSample(new StoryboardSampleInfo(StoryboardElementSource.Beatmap, string.Empty, 0, 1))
                    }
                });
            });

            AddStep("reset clock", () => gameplayContainer.Reset(startClock: true));

            AddUntilStep("sample played", () => sample.RequestedPlaying);
            AddUntilStep("sample has lifetime end", () => sample.LifetimeEnd < double.MaxValue);
        }

        /// <summary>
        /// Sample at 0ms, start time at 1000ms (so the sample should not be played).
        /// </summary>
        [Test]
        public void TestSampleHasLifetimeEndWithInitialClockTime()
        {
            MasterGameplayClockContainer gameplayContainer = null;
            DrawableStoryboardSample sample = null;

            AddStep("create container", () =>
            {
                var working = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);

                const double start_time = 1000;

                Add(gameplayContainer = new MasterGameplayClockContainer(working, start_time)
                {
                    Child = new FrameStabilityContainer
                    {
                        Child = sample = new DrawableStoryboardSample(new StoryboardSampleInfo(StoryboardElementSource.Beatmap, string.Empty, 0, 1))
                    }
                });

                gameplayContainer.Reset(start_time);
            });

            AddStep("start time", () => gameplayContainer.Start());

            AddUntilStep("sample not played", () => !sample.RequestedPlaying);
            AddUntilStep("sample has lifetime end", () => sample.LifetimeEnd < double.MaxValue);
        }

        [Test]
        public void TestSamplePlaybackWithBeatmapHitsoundsOff()
        {
            GameplayClockContainer gameplayContainer = null;
            DrawableStoryboardSample sample = null;

            AddStep("disable beatmap hitsounds", () => config.SetValue(OsuSetting.BeatmapHitsounds, false));

            AddStep("setup storyboard sample", () =>
            {
                Beatmap.Value = new TestCustomSkinWorkingBeatmap(new OsuRuleset().RulesetInfo, this);

                var beatmapSkinSourceContainer = new BeatmapSkinProvidingContainer(Beatmap.Value.Skin);

                Add(gameplayContainer = new MasterGameplayClockContainer(Beatmap.Value, 0)
                {
                    Child = beatmapSkinSourceContainer
                });

                beatmapSkinSourceContainer.Add(sample = new DrawableStoryboardSample(new StoryboardSampleInfo(StoryboardElementSource.Beatmap, "test-sample", 1, 1))
                {
                    Clock = gameplayContainer
                });
            });

            AddStep("reset clock", () => gameplayContainer.Reset(startClock: true));

            AddUntilStep("sample played", () => sample.IsPlayed);
            AddUntilStep("sample has lifetime end", () => sample.LifetimeEnd < double.MaxValue);

            AddStep("restore default", () => config.GetBindable<bool>(OsuSetting.BeatmapHitsounds).SetDefault());
        }

        [Test]
        public void TestHitsoundClassifiedSampleFollowsHitsoundVolume()
        {
            DrawableStoryboardSample sample = null;
            var hitsoundVolume = config.GetBindable<double>(OsuSetting.HitsoundVolume);

            AddStep("create hitsound classified sample", () => Add(sample = new DrawableStoryboardSample(
                new TestHitsoundSampleInfo("test-sample", 0, 1))));

            AddUntilStep("volume follows hitsound volume", () => sample.Volume.Value == hitsoundVolume.Value);

            AddStep("adjust hitsound volume", () => hitsoundVolume.Value = 0.13);
            AddUntilStep("volume follows again", () => sample.Volume.Value == 0.13);

            AddStep("restore default", () => hitsoundVolume.SetDefault());
        }

        [Test]
        public void TestRegularSampleIgnoresHitsoundVolume()
        {
            DrawableStoryboardSample sample = null;
            var hitsoundVolume = config.GetBindable<double>(OsuSetting.HitsoundVolume);
            double initialVolume = 0;

            AddStep("create regular sample", () => Add(sample = new DrawableStoryboardSample(
                new StoryboardSampleInfo(StoryboardElementSource.Beatmap, "test-sample", 0, 1))));

            AddUntilStep("loaded", () => sample.IsLoaded);
            AddStep("record volume", () => initialVolume = sample.Volume.Value);

            AddStep("adjust hitsound volume", () => hitsoundVolume.Value = 0.13);
            AddAssert("volume unchanged", () => sample.Volume.Value == initialVolume);

            AddStep("restore default", () => hitsoundVolume.SetDefault());
        }

        /// <summary>
        /// A detached sample stands in for a note that a mod removed, so it has to be resolved from the sample
        /// definition of that note. Resolving the storyboard wrapper around it instead hides the bank, suffix and
        /// beatmap-sample flags a lookup reads, silently yielding a different sound.
        /// </summary>
        [Test]
        public void TestDetachedSampleResolvesNoteDefinition()
        {
            DetachedSampleSkin skin = null;
            DrawableDetachedStoryboardSample sample = null;

            AddStep("create detached sample", () =>
            {
                var info = new DetachedStoryboardSampleInfo(new HitSampleInfo("keysound", HitSampleInfo.BANK_SOFT, "2", 60, useBeatmapSamples: true), 0, 0.25, true);

                var container = new SkinProvidingContainer(skin = new DetachedSampleSkin());
                container.Add(sample = (DrawableDetachedStoryboardSample)info.CreateDrawable());

                Add(container);
            });

            AddUntilStep("resolved", () => sample.IsLoaded);

            AddAssert("lookup used the note's definition", () => skin.Lookups.Count > 0 && skin.Lookups.All(lookup => lookup is HitSampleInfo));
        }

        /// <summary>
        /// A detached hitsound plays under the gameplay mixer the note played under, so an adjustment a ruleset or mod
        /// applies to gameplay audio reaches it. Wired to the hitsound volume setting on its own, it would keep
        /// sounding at full volume while the note it replaced was silenced.
        /// </summary>
        [Test]
        public void TestDetachedHitsoundPlaysUnderGameplayMixer()
        {
            DrawableRuleset drawableRuleset = null;
            DrawableDetachedStoryboardSample sample = null;
            double volumeBeforeAdjustment = 0;

            AddStep("create gameplay holding a detached sample", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = CreateWorkingBeatmap(ruleset.RulesetInfo).GetPlayableBeatmap(ruleset.RulesetInfo);

                ((IDetachedSampleHost)beatmap).DetachedSamples.Add(new DetachedNoteSample(0, new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 60), DetachedNoteSample.CENTRE));

                Add(drawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap));
            });

            AddUntilStep("detached sample loaded", () => (sample = drawableRuleset.ChildrenOfType<DrawableDetachedStoryboardSample>().FirstOrDefault())?.IsLoaded == true);

            AddUntilStep("sample plays on the mixer chain", () => sample.AggregateVolume.Value, () => Is.EqualTo(drawableRuleset.Audio.AggregateVolume.Value).Within(1e-6));

            AddStep("record volume", () => volumeBeforeAdjustment = sample.AggregateVolume.Value);
            AddStep("lower gameplay volume", () => drawableRuleset.Audio.AddAdjustment(AdjustableProperty.Volume, new BindableDouble(0.5)));

            AddUntilStep("sample follows gameplay volume", () => sample.AggregateVolume.Value, () => Is.EqualTo(0.5 * volumeBeforeAdjustment).Within(1e-6));
        }

        /// <summary>
        /// Stands in for the type-sensitive dispatch a ruleset or beatmap skin performs: only a note's own sample
        /// definition is recognised, so a wrapper around one resolves nothing.
        /// </summary>
        private class DetachedSampleSkin : ISkin
        {
            public readonly List<ISampleInfo> Lookups = new List<ISampleInfo>();

            public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull => null;

            public ISample GetSample(ISampleInfo sampleInfo)
            {
                Lookups.Add(sampleInfo);

                return sampleInfo is HitSampleInfo ? new SampleVirtual("keysound") : null;
            }
        }

        private class TestHitsoundSampleInfo : StoryboardSampleInfo
        {
            public TestHitsoundSampleInfo(string path, double time, int volume)
                : base(StoryboardElementSource.Beatmap, path, time, volume)
            {
            }

            public override bool IsHitsound => true;
        }

        private class TestSkin : LegacySkin
        {
            public TestSkin(string resourceName, IStorageResourceProvider resources)
                : base(DefaultLegacySkin.CreateInfo(), resources, new TestResourceStore(resourceName))
            {
            }
        }

        private class TestResourceStore : IResourceStore<byte[]>
        {
            private readonly string resourceName;

            public TestResourceStore(string resourceName)
            {
                this.resourceName = resourceName;
            }

            public byte[] Get(string name) => name == resourceName ? TestResources.GetStore().Get("Resources/Samples/test-sample.mp3") : null;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => name == resourceName ? TestResources.GetStore().GetAsync("Resources/Samples/test-sample.mp3", cancellationToken) : null;

            public Stream GetStream(string name) => name == resourceName ? TestResources.GetStore().GetStream("Resources/Samples/test-sample.mp3") : null;

            public IEnumerable<string> GetAvailableResources() => new[] { resourceName };

            public void Dispose()
            {
            }
        }

        private class TestCustomSkinWorkingBeatmap : ClockBackedTestWorkingBeatmap
        {
            private readonly IStorageResourceProvider resources;

            public TestCustomSkinWorkingBeatmap(RulesetInfo ruleset, IStorageResourceProvider resources)
                : base(ruleset, null, resources.AudioManager)
            {
                this.resources = resources;
            }

            protected internal override ISkin GetSkin() => new TestSkin("test-sample", resources);
        }

        #region IResourceStorageProvider

        public IRenderer Renderer => host.Renderer;
        public AudioManager AudioManager => Audio;
        public IResourceStore<byte[]> Files => null!;
        public new IResourceStore<byte[]> Resources => base.Resources;
        public RealmAccess RealmAccess => null!;
        public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => null;

        #endregion
    }
}
