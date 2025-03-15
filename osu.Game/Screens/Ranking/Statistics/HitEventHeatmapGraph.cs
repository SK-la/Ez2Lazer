// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Scoring;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics
{
    public partial class HitEventHeatmapGraph : CompositeDrawable
    {
        private readonly IReadOnlyList<HitEvent> hitEvents;

        public HitEventHeatmapGraph(IReadOnlyList<HitEvent> hitEvents)
        {
            this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic() && e.Result.IsHit()).ToList();
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (hitEvents.Count == 0)
                return;

            var groupedEvents = hitEvents.GroupBy(e => e.HitObject.StartTime + e.HitObject.HitWindows.WindowFor(e.Result))
                                         .OrderBy(g => g.Key)
                                         .ToList();

            var hitResultColors = new Dictionary<HitResult, Color4>
            {
                { HitResult.Perfect, Color4.Green },
                { HitResult.Great, Color4.Blue },
                { HitResult.Good, Color4.Yellow },
                { HitResult.Ok, Color4.Orange },
                { HitResult.Meh, Color4.Purple },
                { HitResult.Miss, Color4.Red },
                { HitResult.IgnoreHit, Color4.Gray },
                { HitResult.IgnoreMiss, Color4.DarkGray }
            };

            double minOffset = groupedEvents.First().Key;
            double maxOffset = groupedEvents.Last().Key;

            foreach (var group in groupedEvents)
            {
                double offset = group.Key;
                var hitResultCounts = group.GroupBy(e => e.Result)
                                           .Where(g => hitResultColors.ContainsKey(g.Key))
                                           .ToDictionary(g => g.Key, g => g.Count());

                float yOffset = 0;

                foreach (var hitResult in hitResultCounts)
                {
                    var color = hitResultColors[hitResult.Key];
                    int count = hitResult.Value;

                    AddInternal(new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        X = (float)((offset - minOffset) / (maxOffset - minOffset)),
                        Width = 20f,
                        Height = count / (float)hitEvents.Count,
                        Colour = color,
                        Y = yOffset
                    });

                    yOffset += count / (float)hitEvents.Count;
                }
            }
        }
    }
}
