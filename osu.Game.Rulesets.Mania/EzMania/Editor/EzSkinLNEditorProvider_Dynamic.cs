// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class EzSkinLNEditorProvider
    {
        private Drawable createDynamicPartImpl(ISkin skin)
        {
            var transformedSkin = createTransformedSkin(skin);

            return new SkinProvidingContainer(transformedSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new VirtualPlayfieldPreview(),
            };
        }

        private static ManiaBeatmap buildVirtualPreviewBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(preview_key_count));
            const int spacing = preview_hold_duration;
            const int cycle_length = spacing * preview_key_count;

            for (int cycle = 0; cycle < 16; cycle++)
            {
                for (int column = 0; column < preview_key_count; column++)
                {
                    double holdStart = cycle * cycle_length + (column + 1) * spacing;
                    double noteStart = holdStart - spacing * 0.75;

                    var note = new Note
                    {
                        Column = column,
                        StartTime = noteStart,
                    };
                    note.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    beatmap.HitObjects.Add(note);

                    var hold = new HoldNote
                    {
                        Column = column,
                        StartTime = holdStart,
                        Duration = spacing * 0.9,
                    };
                    hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    beatmap.HitObjects.Add(hold);
                }
            }

            return beatmap;
        }

        private sealed partial class VirtualPlayfieldPreview : Container, IEzSkinEditorScenePlaybackSource
        {
            private readonly StopwatchClock playbackClock = new StopwatchClock(true);
            private readonly FramedClock framedClock;

            private DrawableRuleset drawableRuleset = null!;
            private ManiaBeatmap previewBeatmap = null!;
            private double beatmapMinTime;
            private double beatmapMaxTime;

            public VirtualPlayfieldPreview()
            {
                framedClock = new FramedClock(playbackClock);
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                previewBeatmap = buildVirtualPreviewBeatmap();
                beatmapMinTime = 0;
                beatmapMaxTime = Math.Max(previewBeatmap.GetLastObjectTime() + 1500, 1);

                var ruleset = new ManiaRuleset();
                Mod? autoplayMod = ruleset.GetAutoplayMod();

                drawableRuleset = ruleset.CreateDrawableRulesetWith(
                    previewBeatmap,
                    autoplayMod != null ? new[] { autoplayMod } : null);

                drawableRuleset.Clock = framedClock;
                drawableRuleset.Playfield.DisplayJudgements.Value = true;

                Child = drawableRuleset;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                EzSkinEditorRulesetPreviewBootstrap.ApplyAutoplayReplay(drawableRuleset, previewBeatmap);
            }

            protected override void Update()
            {
                base.Update();

                if (playbackClock.IsRunning && playbackClock.CurrentTime >= beatmapMaxTime)
                    playbackClock.Seek(beatmapMinTime);
            }

            bool IEzSkinEditorScenePlaybackSource.IsActive => IsLoaded && !IsDisposed;

            double IEzSkinEditorScenePlaybackSource.BeatmapMinTime => beatmapMinTime;

            double IEzSkinEditorScenePlaybackSource.BeatmapMaxTime => beatmapMaxTime;

            double IEzSkinEditorScenePlaybackSource.CurrentTime => playbackClock.CurrentTime;

            bool IEzSkinEditorScenePlaybackSource.IsPlaying => playbackClock.IsRunning;

            void IEzSkinEditorScenePlaybackSource.Seek(double time)
            {
                double clamped = Math.Clamp(time, beatmapMinTime, beatmapMaxTime);
                playbackClock.Seek(clamped);
            }

            void IEzSkinEditorScenePlaybackSource.SetPlaying(bool playing)
            {
                if (playing)
                    playbackClock.Start();
                else
                    playbackClock.Stop();
            }
        }
    }
}
