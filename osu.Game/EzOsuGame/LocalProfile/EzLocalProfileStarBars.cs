// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileStarBars : FillFlowContainer
    {
        public EzLocalProfileStarBars(IEnumerable<EzLocalProfileStarPlayCount> stars)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 10);

            var list = stars.OrderBy(s => s.StarBucket).ToList();

            if (list.Count == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            int max = list.Max(s => s.Count);

            var barFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };

            foreach (var star in list)
                barFlow.Add(new StarBarRow(star.StarBucket, star.Count, max));

            Add(barFlow);
            Add(new EzLocalProfileLabeledLineChart(
                list.Select(s => (float)s.Count).ToArray(),
                list.Select(s => $"{s.StarBucket}★").ToArray()));
        }

        private partial class StarBarRow : Container
        {
            private readonly int bucket;
            private readonly int count;
            private readonly int max;

            public StarBarRow(int bucket, int count, int max)
            {
                this.bucket = bucket;
                this.count = count;
                this.max = max;

                RelativeSizeAxes = Axes.X;
                Height = 22;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                float ratio = max <= 0 ? 0 : (float)count / max;

                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = $"{bucket}★–{bucket + 1}★",
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Width = 72,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 80, Right = 56 },
                        Child = new EzLocalProfileRoundedBar(ratio),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = count.ToString("N0"),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Colour = colours.Content2,
                    }
                };
            }
        }
    }
}
