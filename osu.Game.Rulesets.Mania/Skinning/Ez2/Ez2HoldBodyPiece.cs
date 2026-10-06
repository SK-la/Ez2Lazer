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
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HoldBodyPiece : FastNoteBase, IHoldNoteBody
    {
        /// <summary>
        /// 本列当前颜色的 live 副本，供按住高亮层（<see cref="Ez2HoldNoteHittingLayer"/>）在松手后还原。
        /// </summary>
        public IBindable<Color4> LiveColour => liveColour;

        private readonly Bindable<Color4> liveColour = new Bindable<Color4>();

        private Drawable background = null!;
        private Container tailContainer = null!;

        private Ez2HoldNoteHittingLayer hittingLayer = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        // 10k2s1p 开启时用 Ez 内置色彩模板，关闭时回到 FastNoteBase 的编辑器列色。
        protected override IBindable<bool>? BuiltInColourTemplateSwitch => ezConfig.GetBindable<bool>(Ez2Setting.ManiaSkipEmptyEdgeColumns);

        protected override Colour4 BuiltInColourTemplate => stageDefinition.GetColourForLayout(Column.Index);

        public Ez2HoldBodyPiece()
        {
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Masking = true;
            Colour = ColourInfo.GradientVertical(Colour4.White.Opacity(0.35f), Colour4.White.Opacity(1.0f));
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            MainContainer.Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Children = new[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Height = 1f,
                            Alpha = 1,
                        },
                        tailContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            CornerRadius = 0,
                            Height = CornerRadius,
                            Masking = true,
                            // Colour = ColourInfo.GradientVertical(Color4.White.Opacity(1f), Color4.White.Opacity(0f)),
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                // Colour = ColourInfo.GradientVertical(Color4.White.Opacity(1f), Color4.White.Opacity(1f)),
                            }
                        }
                    }
                },
            };

            hittingLayer = new Ez2HoldNoteHittingLayer(this);
            hittingLayer.BindAccentColour(LiveColour);
            AddInternal(hittingLayer);

            if (drawableObject is DrawableHoldNote holdNote)
                ((IBindable<bool>)hittingLayer.IsHitting).BindTo(holdNote.IsHolding);
        }

        protected override void UpdateColor()
        {
            var noteColour = NoteColor;

            liveColour.Value = noteColour;

            background.Colour = noteColour.Opacity(1f);
            tailContainer.Colour = ColourInfo.GradientVertical(noteColour.Opacity(1f), noteColour.Opacity(1f));
        }

        protected override void Update()
        {
            base.Update();
            background.Height = 1f - DrawWidth / 2;
            tailContainer.CornerRadius = DrawWidth / 2;
            tailContainer.Height = DrawWidth / 2;
        }

        public void UpdateAppearance(Color4 startColour, Color4 endColour, float alpha)
        {
            this.FadeColour(ColourInfo.GradientVertical(startColour, endColour), 200, Easing.OutQuint);
            this.FadeTo(alpha, 200, Easing.OutQuint);
        }

        public void Recycle()
        {
            hittingLayer.Recycle();
        }
    }
}
