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
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
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
                Child = new ScrollingPreview()
            };
        }

        private sealed partial class ScrollingPreview : CompositeDrawable
        {
            private static int timeSpeed { get; set; } = 3000;
            private static int holdDuration { get; set; } = 1000;
            private int cycleLength { get; } = holdDuration * preview_key_count;

            private readonly PreviewScrollingInfo scrollingInfo = new PreviewScrollingInfo();
            private readonly PreviewGameplayClock gameplayClockDependency = new PreviewGameplayClock();
            private readonly StageDefinition stageDefinition = new StageDefinition(preview_key_count);
            private readonly IBeatmap beatmapDependency;

            private readonly StopwatchClock playbackClock = new StopwatchClock(true);
            private readonly ManualClock manualClock = new ManualClock();

            private Stage stage = null!;
            private int nextCycleIndex;
            private double lastAddedStart = double.NegativeInfinity;

            public ScrollingPreview()
            {
                beatmapDependency = new ManiaBeatmap(stageDefinition);

                RelativeSizeAxes = Axes.Both;
                Clock = new FramedClock(manualClock);
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs<IScrollingInfo>(scrollingInfo);
                dependencies.Cache(stageDefinition);
                dependencies.CacheAs(beatmapDependency);
                dependencies.CacheAs<IGameplayClock>(gameplayClockDependency);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                scrollingInfo.TimeRangeBindable.Value = timeSpeed;

                ManiaAction action = ManiaAction.Key1;
                stage = new Stage(0, stageDefinition, ref action)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Y,
                    Height = 1,
                };

                InternalChild = stage;

                // Add a small number of initial cycles; further cycles will be added on-demand
                // during Update to ensure a continuous stream of upcoming hitobjects.
                for (int r = 0; r < 3; r++)
                {
                    addCycle(r);
                }

                nextCycleIndex = 3;
            }

            private void addCycle(int r)
            {
                for (int i = 0; i < preview_key_count; i++)
                {
                    var hold = new HoldNote
                    {
                        StartTime = r * cycleLength + (1 + i) * holdDuration,
                        Duration = holdDuration,
                        Column = i,
                    };

                    hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    stage.Add(hold);
                    lastAddedStart = Math.Max(lastAddedStart, hold.StartTime);
                }
            }

            protected override void Update()
            {
                base.Update();

                // Use the continuous playback clock as the gameplay clock so notes
                // scroll smoothly. Ensure we always have upcoming cycles generated
                // up to a small buffer beyond the visible TimeRange.
                double currentTime = playbackClock.CurrentTime;
                // Add cycles on-demand to cover current time + visible range + buffer.
                double requiredUpTo = currentTime + scrollingInfo.TimeRangeBindable.Value + cycleLength * 2;

                while (lastAddedStart < requiredUpTo)
                {
                    addCycle(nextCycleIndex++);
                }

                manualClock.CurrentTime = currentTime;
                gameplayClockDependency.CurrentTime = currentTime;
            }
        }
    }
}
