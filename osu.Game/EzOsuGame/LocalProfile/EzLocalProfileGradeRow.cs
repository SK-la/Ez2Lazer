// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileGradeRow : FillFlowContainer
    {
        public EzLocalProfileGradeRow(IEnumerable<EzLocalProfileGradeCount> grades)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(12, 0);

            var list = grades.OrderByDescending(g => g.Rank).ToList();

            if (list.Count == 0)
            {
                Add(new OsuSpriteText
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_NO_RULESET_DATA,
                    Font = OsuFont.GetFont(size: 14),
                });
                return;
            }

            foreach (var grade in list)
                Add(new GradeCell(grade.Rank, grade.Count));
        }

        private partial class GradeCell : CompositeDrawable
        {
            public GradeCell(ScoreRank rank, int count)
            {
                AutoSizeAxes = Axes.Both;
                InternalChild = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    Width = 48,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        new DrawableRank(rank)
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 22,
                        },
                        new OsuSpriteText
                        {
                            Text = count.ToLocalisableString("#,##0"),
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                        }
                    }
                };
            }
        }
    }
}
