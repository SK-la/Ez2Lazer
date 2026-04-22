// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
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
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
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
    /// Uses Mania's DrawableRuleset for rendering.
    /// </summary>
    public partial class BMSPlayer : OsuScreen
    {
        protected override bool InitialBackButtonVisibility => false;

        private readonly BMSWorkingBeatmap workingBeatmap;
        private DrawableRuleset? drawableRuleset;
        private OsuSpriteText debugText = null!;

        public BMSPlayer(BMSWorkingBeatmap workingBeatmap)
        {
            this.workingBeatmap = workingBeatmap;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            var bmsBeatmap = workingBeatmap.Beatmap;

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
                                Text = $"Notes: {bmsBeatmap.HitObjects.Count} | Press ESC to exit",
                                Font = OsuFont.GetFont(size: 14),
                                Colour = colours.Gray9,
                            },
                        },
                    },
                },
            };

            // Convert BMS beatmap to Mania beatmap
            try
            {
                var maniaBeatmap = ConvertToManiaBeatmap(bmsBeatmap);
                var maniaRuleset = new ManiaRuleset();

                drawableRuleset = maniaRuleset.CreateDrawableRulesetWith(maniaBeatmap, new List<Mod>());
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

                debugText.Text = $"Notes: {maniaBeatmap.HitObjects.Count} | Using Mania renderer | Press ESC to exit";
            }
            catch (System.Exception ex)
            {
                debugText.Text = $"Failed to create ruleset: {ex.Message}";
            }
        }

        /// <summary>
        /// Convert BMS beatmap to Mania beatmap format.
        /// </summary>
        private ManiaBeatmap ConvertToManiaBeatmap(IBeatmap bmsBeatmap)
        {
            // Determine column count from difficulty settings or hit objects
            int columnCount = (int)bmsBeatmap.Difficulty.CircleSize;
            if (columnCount <= 0)
            {
                columnCount = bmsBeatmap.HitObjects.OfType<BMSHitObject>().Select(h => h.Column).DefaultIfEmpty(0).Max() + 1;
            }
            if (columnCount <= 0) columnCount = 7;

            var maniaBeatmap = new ManiaBeatmap(new StageDefinition(columnCount));

            // Copy metadata
            maniaBeatmap.BeatmapInfo = bmsBeatmap.BeatmapInfo;
            maniaBeatmap.Difficulty = bmsBeatmap.Difficulty;
            maniaBeatmap.ControlPointInfo = bmsBeatmap.ControlPointInfo;

            // Convert hit objects
            foreach (var hitObject in bmsBeatmap.HitObjects)
            {
                ManiaHitObject maniaHitObject;

                if (hitObject is BMSHoldNote holdNote)
                {
                    maniaHitObject = new HoldNote
                    {
                        Column = holdNote.Column,
                        StartTime = holdNote.StartTime,
                        Duration = holdNote.Duration,
                        Samples = holdNote.Samples,
                    };
                }
                else if (hitObject is BMSNote note)
                {
                    maniaHitObject = new Note
                    {
                        Column = note.Column,
                        StartTime = note.StartTime,
                        Samples = note.Samples,
                    };
                }
                else
                {
                    continue;
                }

                // Apply defaults to initialize HitWindows
                maniaHitObject.ApplyDefaults(maniaBeatmap.ControlPointInfo, maniaBeatmap.Difficulty);
                maniaBeatmap.HitObjects.Add(maniaHitObject);
            }

            return maniaBeatmap;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Load and start the track
            workingBeatmap.LoadTrack();
            var track = workingBeatmap.Track;
            track?.Start();
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
