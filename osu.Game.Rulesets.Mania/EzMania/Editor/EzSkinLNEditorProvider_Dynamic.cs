// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Editor
{
    public partial class EzSkinLNEditorProvider
    {
        private Drawable createDynamicPartImpl(ISkin skin, int keyCount)
        {
            var transformedSkin = createTransformedSkin(skin, keyCount);

            return new SkinProvidingContainer(transformedSkin)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new VirtualPlayfieldPreview(keyCount),
            };
        }

        /// <summary>
        /// One phrase from [HitObjects] (times relative to 750ms), 5K x→column, looped every <see cref="loop_pattern_period"/> ms.
        /// Entry: (relativeStart, column, duration); duration 0 = tap.
        /// </summary>
        private static readonly (double time, int column, double duration)[] loop_pattern =
        {
            (0, 0, 0), (62.5, 1, 0), (125, 2, 0), (187.5, 3, 0),
            (250, 0, 125), (250, 4, 0), (250, 0, 0),
            (312.5, 1, 125), (312.5, 1, 0),
            (375, 2, 125), (375, 2, 0),
            (437.5, 3, 125), (437.5, 3, 0),
            (500, 4, 125), (500, 4, 0),
            (625, 0, 0), (687.5, 1, 0), (750, 2, 0),
            (750, 0, 125), (812.5, 3, 0), (812.5, 1, 125),
            (875, 4, 0), (875, 2, 125), (937.5, 3, 125),
            (1000, 4, 125), (1000, 0, 0),
            (1062.5, 1, 0), (1125, 2, 0), (1187.5, 3, 0), (1250, 4, 0),
        };

        private const double loop_pattern_period = 1250;
        private const int loop_pattern_cycles = 8;

        private static ManiaBeatmap buildVirtualPreviewBeatmap(int keyCount)
        {
            var stageDefinition = new StageDefinition(keyCount);
            var beatmap = new ManiaBeatmap(stageDefinition);
            var controlPoints = new ControlPointInfo();
            var difficulty = new BeatmapDifficulty();

            for (int cycle = 0; cycle < loop_pattern_cycles; cycle++)
            {
                double offset = cycle * loop_pattern_period;

                foreach ((double time, int column, double duration) in loop_pattern)
                {
                    if (column >= keyCount)
                        continue;

                    if (ManiaPlayfieldLayoutHelper.ShouldSkipBeatmapColumn(stageDefinition, column))
                        continue;

                    // Skip the period-boundary tap on non-first cycles — it coincides with the next phrase's t=0.
                    if (cycle > 0 && time >= loop_pattern_period)
                        continue;

                    ManiaHitObject hitObject = duration > 0
                        ? new HoldNote
                        {
                            Column = column,
                            StartTime = offset + time,
                            Duration = duration,
                        }
                        : new Note
                        {
                            Column = column,
                            StartTime = offset + time,
                        };

                    hitObject.ApplyDefaults(controlPoints, difficulty);
                    beatmap.HitObjects.Add(hitObject);
                }
            }

            return beatmap;
        }

        private sealed partial class VirtualPlayfieldPreview : Container, IEzSkinEditorScenePlaybackSource
        {
            private readonly int keyCount;
            private readonly StopwatchClock playbackClock = new StopwatchClock(true);
            private readonly FramedClock framedClock;

            private DrawableRuleset drawableRuleset = null!;
            private ManiaBeatmap previewBeatmap = null!;
            private double beatmapMinTime;
            private double beatmapMaxTime;
            private double? pendingSeekTime;

            public VirtualPlayfieldPreview(int keyCount)
            {
                this.keyCount = keyCount;
                framedClock = new FramedClock(playbackClock);
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                previewBeatmap = buildVirtualPreviewBeatmap(keyCount);
                beatmapMinTime = 0;
                // Wrap on the phrase boundary (not last-object+padding) so playback is seamless/infinite.
                beatmapMaxTime = loop_pattern_period * loop_pattern_cycles;

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

                if (!playbackClock.IsRunning)
                    return;

                // Infinite loop: jump back one full buffer when the clock reaches the end of materialised phrases.
                double wrapAt = loop_pattern_period * loop_pattern_cycles;
                double loopBack = loop_pattern_period * Math.Max(1, loop_pattern_cycles / 2);

                while (playbackClock.CurrentTime >= wrapAt)
                    playbackClock.Seek(playbackClock.CurrentTime - loopBack);
            }

            bool IEzSkinEditorScenePlaybackSource.IsActive => IsLoaded && !IsDisposed;

            double IEzSkinEditorScenePlaybackSource.BeatmapMinTime => beatmapMinTime;

            double IEzSkinEditorScenePlaybackSource.BeatmapMaxTime => beatmapMaxTime;

            double IEzSkinEditorScenePlaybackSource.CurrentTime => playbackClock.CurrentTime;

            bool IEzSkinEditorScenePlaybackSource.IsPlaying => playbackClock.IsRunning;

            void IEzSkinEditorScenePlaybackSource.Seek(double time)
            {
                double span = loop_pattern_period * loop_pattern_cycles;
                double wrapped = time % span;
                if (wrapped < 0)
                    wrapped += span;

                // ProgressBar fires seek every move; coalesce to one clock jump per frame.
                pendingSeekTime = wrapped;
                Scheduler.AddOnce(applyPendingSeek);
            }

            private void applyPendingSeek()
            {
                if (pendingSeekTime is not double time || IsDisposed)
                    return;

                pendingSeekTime = null;
                playbackClock.Seek(time);
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
