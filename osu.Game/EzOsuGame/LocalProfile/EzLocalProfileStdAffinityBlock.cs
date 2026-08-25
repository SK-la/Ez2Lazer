// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile.Sections;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileStdAffinityBlock : FillFlowContainer
    {
        public EzLocalProfileStdAffinityBlock(EzLocalProfileSnapshot snapshot)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 14);

            Add(createGroup(EzSettingsStrings.LOCAL_PROFILE_BEST_AR, snapshot.StdAttrAffinities.Where(a => a.Attr == EzLocalProfileStdAttr.ApproachRate)));
            Add(createGroup(EzSettingsStrings.LOCAL_PROFILE_BEST_CS, snapshot.StdAttrAffinities.Where(a => a.Attr == EzLocalProfileStdAttr.CircleSize)));
        }

        private static Drawable createGroup(LocalisableString title, IEnumerable<EzLocalProfileStdAttrAffinity> affinities)
        {
            var ordered = affinities
                          .OrderByDescending(a => a.PlayCount)
                          .ThenByDescending(a => a.HighGradeCount)
                          .Take(3)
                          .ToList();

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = title,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    }
                }
            };

            if (ordered.Count == 0)
            {
                flow.Add(new OsuSpriteText
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return flow;
            }

            for (int i = 0; i < ordered.Count; i++)
                flow.Add(new AffinityRow(ordered[i], isTop: i == 0, dimmed: i > 0));

            return flow;
        }

        private partial class AffinityRow : Container
        {
            private readonly EzLocalProfileStdAttrAffinity affinity;
            private readonly bool isTop;
            private readonly bool dimmed;
            private Box? accent;

            public AffinityRow(EzLocalProfileStdAttrAffinity affinity, bool isTop, bool dimmed)
            {
                this.affinity = affinity;
                this.isTop = isTop;
                this.dimmed = dimmed;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Alpha = dimmed ? 0.7f : 1;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                var pill = new CounterPill();
                pill.Current.Value = affinity.PlayCount;

                Children = new Drawable[]
                {
                    accent = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colours.Highlight1,
                        Alpha = isTop ? 1 : 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(12, 0),
                        Padding = new MarginPadding { Left = 10, Vertical = 4 },
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = affinity.Value.ToString("0.0", CultureInfo.InvariantCulture),
                                Font = OsuFont.GetFont(size: 16, weight: isTop ? FontWeight.Bold : FontWeight.Regular),
                                Width = 40,
                            },
                            pill.With(p =>
                            {
                                p.Anchor = Anchor.CentreLeft;
                                p.Origin = Anchor.CentreLeft;
                            }),
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = $"{affinity.HighGradeCount} S+",
                                Font = OsuFont.GetFont(size: 12),
                                Colour = colours.Content2,
                            }
                        }
                    }
                };
            }
        }
    }
}
