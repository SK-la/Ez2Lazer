// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.LAsEZMania.Helper;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.LAsEzMania.Analysis
{
    /// <summary>
    /// Mania-specific implementation of score graph that extends BaseEzScoreGraph.
    /// Provides LN (Long Note) aware scoring calculation for Classic mode.
    /// </summary>
    public partial class EzManiaScoreGraph : BaseEzScoreGraph
    {
        private readonly ManiaHitWindows maniaHitWindows = new ManiaHitWindows();

        public EzManiaScoreGraph(ScoreInfo score, IBeatmap beatmap)
            : base(score, beatmap, new ManiaHitWindows())
        {
            maniaHitWindows.SetDifficulty(beatmap.Difficulty.OverallDifficulty);
        }

        protected override IReadOnlyList<HitEvent> FilterHitEvents()
        {
            return Score.HitEvents.Where(e => maniaHitWindows.IsHitResultAllowed(e.Result)).ToList();
        }

        protected override double UpdateBoundary(HitResult result)
        {
            return maniaHitWindows.WindowFor(result);
        }

        private readonly CustomHitWindowsHelper hitWindows1 = new CustomHitWindowsHelper(EzMUGHitMode.Classic)
        {
            OverallDifficulty = OD
        };

        private readonly CustomHitWindowsHelper hitWindows2 = new CustomHitWindowsHelper
        {
            OverallDifficulty = OD
        };

        protected override HitResult RecalculateV1Result(HitEvent hitEvent)
        {
            return hitWindows1.ResultFor(hitEvent.TimeOffset);
        }

        protected override HitResult RecalculateV2Result(HitEvent hitEvent)
        {
            return hitWindows2.ResultFor(hitEvent.TimeOffset);
        }

        protected override void UpdateText()
        {
            double scAcc = Score.Accuracy * 100;
            long scScore = Score.TotalScore;

            AddInternal(new GridContainer
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Position = Vector2.Zero,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc org",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {scAcc:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc v2",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Accuracy * 100:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Acc v1",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V1Accuracy * 100:F1}%",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr org",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {scScore / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr v2",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Score / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Scr v1",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V1Score / 1000.0:F0}k",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Pauses",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {Score.Pauses.Count}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "PERFECT",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Perfect, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Perfect, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "GREAT",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Great, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Great, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "GOOD",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Good, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Good, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "OK",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Ok, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Ok, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "MEH",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Meh, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Meh, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                    new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "MISS",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Text = $" : {V2Counts.GetValueOrDefault(HitResult.Miss, 0)}\\{V1Counts.GetValueOrDefault(HitResult.Miss, 0)}",
                            Font = OsuFont.GetFont(size: 14),
                            Colour = Color4.White,
                        },
                    },
                }
            });
        }
    }
}
