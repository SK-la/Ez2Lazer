// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// The BMS game player screen.
    /// </summary>
    public partial class BMSPlayer : OsuScreen
    {
        protected override bool InitialBackButtonVisibility => false;

        private readonly BMSWorkingBeatmap workingBeatmap;
        private readonly BMSRuleset ruleset;
        private DrawableRuleset? drawableRuleset;
        private OsuSpriteText debugText = null!;

        public BMSPlayer(BMSWorkingBeatmap workingBeatmap, BMSRuleset ruleset)
        {
            this.workingBeatmap = workingBeatmap;
            this.ruleset = ruleset;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var beatmap = workingBeatmap.Beatmap;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(20),
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = workingBeatmap.BeatmapInfo.Metadata.Title,
                                Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                            },
                            new OsuSpriteText
                            {
                                Text = workingBeatmap.BeatmapInfo.Metadata.Artist,
                                Font = OsuFont.GetFont(size: 16),
                                Colour = colours.Yellow,
                            },
                            debugText = new OsuSpriteText
                            {
                                Text = $"Notes: {beatmap.HitObjects.Count} | Press ESC to exit",
                                Font = OsuFont.GetFont(size: 14),
                                Colour = colours.Gray9,
                            },
                        },
                    },
                },
            };

            // Try to create the drawable ruleset
            try
            {
                drawableRuleset = ruleset.CreateDrawableRulesetWith(beatmap, new List<Mod>());
                drawableRuleset.RelativeSizeAxes = Axes.Both;

                AddInternal(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 0.8f,
                    Height = 0.9f,
                    Child = drawableRuleset,
                });
            }
            catch (System.Exception ex)
            {
                debugText.Text = $"Failed to create ruleset: {ex.Message}";
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Start the track
            var track = workingBeatmap.Track;
            if (track != null)
            {
                track.Start();
            }
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape)
            {
                workingBeatmap.Track?.Stop();
                this.Exit();
                return true;
            }

            return base.OnKeyDown(e);
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(500);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            workingBeatmap.Track?.Stop();
            this.FadeOut(200);
            return base.OnExiting(e);
        }
    }
}
