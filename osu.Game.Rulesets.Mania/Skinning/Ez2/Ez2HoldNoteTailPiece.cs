// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HoldNoteTailPiece : FastNoteBase
    {
        private Box foreground = null!;
        private Box foregroundAdditive = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        // 10k2s1p 开启时用 Ez 内置色彩模板，关闭时回到 FastNoteBase 的编辑器列色。
        protected override IBindable<bool>? BuiltInColourTemplateSwitch => ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);

        protected override Colour4 BuiltInColourTemplate => stageDefinition.GetColourForLayout(Column.Index);

        public Ez2HoldNoteTailPiece()
        {
            RelativeSizeAxes = Axes.X;
            Height = 0f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            MainContainer.RelativeSizeAxes = Axes.X;
            MainContainer.Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 0,
                    CornerRadius = Ez2NotePiece.CORNER_RADIUS,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(Colour4.Black.Opacity(0), Colour4.Black),
                            // Avoid ugly single pixel overlap.
                            Height = 0.9f,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Height = 0,
                            CornerRadius = Ez2NotePiece.CORNER_RADIUS,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                foreground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                // hittingLayer = new Ez2HoldNoteHittingLayer(),
                                foregroundAdditive = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Blending = BlendingParameters.Additive,
                                    Height = 0.5f,
                                },
                            },
                        },
                    }
                },
            };
        }

        protected override void UpdateColor()
        {
            var noteColour = NoteColor;

            foreground.Colour = noteColour.Darken(0.6f); // matches body

            foregroundAdditive.Colour = ColourInfo.GradientVertical(
                noteColour.Opacity(0.4f),
                noteColour.Opacity(0)
            );
        }
    }
}
