// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    /// <summary>
    /// Horizontal rounded bar list for bucketed play counts (official star, xxy SR, etc.).
    /// </summary>
    public partial class EzLocalProfileBucketBars : FillFlowContainer
    {
        public EzLocalProfileBucketBars(IEnumerable<(int bucket, int count)> buckets, Func<int, string> formatBucketLabel)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 8);

            var list = buckets.OrderBy(b => b.bucket).ToList();

            if (list.Count == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            int max = list.Max(b => b.count);

            foreach (var (bucket, count) in list)
                Add(new BucketBarRow(bucket, count, max, formatBucketLabel));
        }

        public static EzLocalProfileBucketBars FromStarPlayCounts(IEnumerable<EzLocalProfileStarPlayCount> stars) =>
            new EzLocalProfileBucketBars(
                stars.Select(s => (s.StarBucket, s.Count)),
                bucket => $"{bucket}★–{bucket + 1}★");

        public static EzLocalProfileBucketBars FromXxyPlayCounts(IEnumerable<EzLocalProfileXxyPlayCount> plays) =>
            new EzLocalProfileBucketBars(
                plays.Select(s => (s.StarBucket, s.Count)),
                bucket => $"{bucket}xxy–{bucket + 1}xxy");

        private partial class BucketBarRow : Container
        {
            public BucketBarRow(int bucket, int count, int max, Func<int, string> formatBucketLabel)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Padding = new MarginPadding { Horizontal = 4 };

                float ratio = max <= 0 ? 0 : (float)count / max;

                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = formatBucketLabel(bucket),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Width = 72,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 80, Right = 56 },
                        Child = new EzLocalProfileRoundedBar(ratio),
                    },
                    new CountText(count),
                };
            }

            private partial class CountText : OsuSpriteText
            {
                public CountText(int count)
                {
                    Anchor = Anchor.CentreRight;
                    Origin = Anchor.CentreRight;
                    Text = count.ToString("N0");
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold);
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colours) => Colour = colours.Content2;
            }
        }
    }
}
