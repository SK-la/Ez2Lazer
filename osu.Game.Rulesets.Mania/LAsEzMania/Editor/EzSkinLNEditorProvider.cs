// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.LAsEzExtensions.Edit;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Editor
{
    public partial class EzSkinLNEditorProvider : ISkinEditorVirtualProvider
    {
        public Drawable CreateDynamicPart(ISkin skin) => createDynamicPartImpl(skin);

        public Drawable CreateStaticPart(ISkin skin) => createStaticPartImpl(skin);

        public Drawable CreateParametersPart(ISkin skin) => createParametersPartImpl(skin);

        private const int preview_key_count = 4;
        private const int preview_column_width = 100;

        private static ISkin createTransformedSkin(ISkin skin)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new ManiaBeatmap(new StageDefinition(preview_key_count));
            return ruleset.CreateSkinTransformer(skin, beatmap) ?? skin;
        }

        static EzSkinLNEditorProvider()
        {
            // Register this provider for the Mania ruleset in the global registry.
            try
            {
                int rulesetId = new ManiaRuleset().RulesetInfo.OnlineID;
                SkinEditorProviderRegistry.Register(rulesetId, () => new EzSkinLNEditorProvider());
            }
            catch
            {
                // swallow - registration is best-effort (assembly load ordering may vary)
            }
        }

        protected sealed class PreviewScrollingInfo : IScrollingInfo
        {
            public readonly Bindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>(ScrollingDirection.Down);
            public readonly Bindable<double> TimeRangeBindable = new Bindable<double>(500);

            IBindable<ScrollingDirection> IScrollingInfo.Direction => Direction;
            IBindable<double> IScrollingInfo.TimeRange => TimeRangeBindable;
            IBindable<IScrollAlgorithm> IScrollingInfo.Algorithm { get; } = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());
        }

        private sealed class PreviewGameplayClock : IGameplayClock
        {
            private readonly BindableBool isPaused = new BindableBool(false);

            public double StartTime => 0;

            public double GameplayStartTime => 0;

            public IAdjustableAudioComponent AdjustmentsFromMods { get; } = new AudioAdjustments();

            public IBindable<bool> IsPaused => isPaused;

            public bool IsRewinding => false;

            public double Rate => 1;

            public double CurrentTime { get; set; }

            public bool IsRunning => true;

            public void ProcessFrame()
            {
            }

            public double ElapsedFrameTime => 0;

            public double FramesPerSecond => 1000;
        }
    }
}
