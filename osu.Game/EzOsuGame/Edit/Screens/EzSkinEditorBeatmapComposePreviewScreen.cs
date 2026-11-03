// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Menu;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Screens
{
    /// <summary>
    /// Beatmap-editor group compose preview: in-memory chart copy, read-only mania compose with beat snap grid.
    /// </summary>
    public partial class EzSkinEditorBeatmapComposePreviewScreen : OsuScreen
    {
        public override bool AllowExternalScreenChange => true;

        public override bool HideOverlaysOnEnter => false;

        public override bool? AllowGlobalTrackControl => false;

        /// <summary>
        /// Clock driving audio and the playback bar while this screen is current.
        /// </summary>
        public EditorClock? ComposeClock { get; private set; }

        private IWorkingBeatmap? workingBeatmap;
        private RulesetInfo? rulesetInfo;
        private readonly BindableBeatDivisor previewGridBeatDivisor;

        private Container viewport = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        public EzSkinEditorBeatmapComposePreviewScreen(IWorkingBeatmap? workingBeatmap, RulesetInfo? rulesetInfo, BindableBeatDivisor previewGridBeatDivisor)
        {
            this.workingBeatmap = workingBeatmap;
            this.rulesetInfo = rulesetInfo;
            this.previewGridBeatDivisor = previewGridBeatDivisor;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        protected override void LogoArriving(OsuLogo? logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);
            logo?.FadeOut();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(viewport = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            rebuild();
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (ComposeClock == null)
                return false;

            if (e.ScrollDelta.Y > 0)
                ComposeClock.SeekBackward(true);
            else
                ComposeClock.SeekForward(true);

            return true;
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            ComposeClock?.Stop();
            return base.OnExiting(e);
        }

        public void SetSource(IWorkingBeatmap? beatmap, RulesetInfo? ruleset)
        {
            bool unchanged = workingBeatmap?.BeatmapInfo.Hash == beatmap?.BeatmapInfo.Hash
                             && rulesetInfo?.ShortName == ruleset?.ShortName;

            if (unchanged)
                return;

            workingBeatmap = beatmap;
            rulesetInfo = ruleset;

            if (IsLoaded)
                rebuild();
        }

        private void rebuild()
        {
            ComposeClock?.Stop();
            ComposeClock = null;
            viewport.Clear();

            RulesetInfo? ruleset = resolveRulesetForRender(rulesetInfo, workingBeatmap);

            if (workingBeatmap == null || ruleset == null || !EzBeatmapPreviewModes.IsManiaRuleset(ruleset))
            {
                viewport.Add(createPlaceholder(ManiaBeatmapPreviewStrings.BEATMAP_NOT_MANIA));
                return;
            }

            IBeatmap playableBeatmap;

            try
            {
                playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset);
                playableBeatmap.ControlPointInfo = playableBeatmap.ControlPointInfo.DeepClone();
            }
            catch
            {
                viewport.Add(createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_LOAD_FAILED));
                return;
            }

            if (playableBeatmap.HitObjects.Count == 0)
            {
                viewport.Add(createPlaceholder(EzEditorStrings.PLACEHOLDER_BEATMAP_NOT_LOADED));
                return;
            }

            var root = new ComposePreviewRoot(workingBeatmap, playableBeatmap, ruleset, previewGridBeatDivisor, colourProvider);
            root.OnClockReady = clock => ComposeClock = clock;
            viewport.Add(root);
        }

        private RulesetInfo? resolveRulesetForRender(RulesetInfo? ruleset, IWorkingBeatmap? beatmap)
        {
            if (beatmap?.BeatmapInfo.Ruleset is not RulesetInfo beatmapRuleset)
                return ruleset;

            return EzSkinEditorBeatmapPicker.ResolveRulesetForPreview(rulesets, beatmapRuleset, ruleset) ?? ruleset;
        }

        private static OsuSpriteText createPlaceholder(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Text = text,
            Colour = Color4.White,
        };

        private partial class ComposePreviewRoot : CompositeDrawable
        {
            public Action<EditorClock>? OnClockReady { get; set; }

            public EditorClock Clock { get; private set; } = null!;

            private readonly IWorkingBeatmap workingBeatmap;
            private readonly IBeatmap playableBeatmap;
            private readonly RulesetInfo rulesetInfo;
            private readonly BindableBeatDivisor previewGridBeatDivisor;
            private readonly OverlayColourProvider colourProvider;

            private EditorBeatmap editorBeatmap = null!;

            public ComposePreviewRoot(
                IWorkingBeatmap workingBeatmap,
                IBeatmap playableBeatmap,
                RulesetInfo rulesetInfo,
                BindableBeatDivisor previewGridBeatDivisor,
                OverlayColourProvider colourProvider)
            {
                this.workingBeatmap = workingBeatmap;
                this.playableBeatmap = playableBeatmap;
                this.rulesetInfo = rulesetInfo;
                this.previewGridBeatDivisor = previewGridBeatDivisor;
                this.colourProvider = colourProvider;
                RelativeSizeAxes = Axes.Both;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var concrete = (WorkingBeatmap)workingBeatmap;

                // LoadTrack() 会换成新实例。音乐控制器仍持有旧实例并继续播放，播放条够不着。
                if (!concrete.TrackLoaded)
                    concrete.LoadTrack();

                if (dependencies.TryGet(out MusicController music))
                    music.Stop();

                previewGridBeatDivisor.SetArbitraryDivisor(concrete.BeatmapInfo.BeatDivisor);

                Clock = new EditorClock(playableBeatmap, previewGridBeatDivisor);
                Clock.ChangeSource(concrete.Track);
                Clock.Stop();

                editorBeatmap = new EditorBeatmap(
                    playableBeatmap,
                    concrete.GetSkin(),
                    concrete.Storyboard,
                    concrete.BeatmapInfo);

                dependencies.Cache(previewGridBeatDivisor);
                dependencies.CacheAs(Clock);
                dependencies.CacheAs(editorBeatmap);
                dependencies.CacheAs<IBeatmap>(editorBeatmap);
                dependencies.CacheAs<IBeatSnapProvider>(editorBeatmap);
                dependencies.CacheAs(colourProvider);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var ruleset = rulesetInfo.CreateInstance();
                var composer = ruleset.CreateSkinEditorComposePreview();

                if (composer != null)
                    AddInternal(new EditorSkinProvidingContainer(editorBeatmap).WithChild(composer));

                AddInternal(Clock);
                AddInternal(new OsuSpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Margin = new MarginPadding(12),
                    Text = EzEditorStrings.BEATMAP_EDITOR_COMPOSE_READONLY_WATERMARK,
                    Colour = Color4.White,
                    Font = OsuFont.GetFont(size: 14),
                });

                OnClockReady?.Invoke(Clock);
            }
        }
    }
}
