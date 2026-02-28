// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNoteSideLine : CompositeDrawable
    {
        private Drawable separator = null!;
        private readonly IBindable<double> noteTrackLineHeight = new BindableDouble();

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo _)
        {
            AlwaysPresent = true;
            var texture = textures.Get("EzResources/note/NoteSideLine.png");

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new[]
                {
                    separator = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        // Blending = BlendingParameters.Additive,
                        Child = new Sprite
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 10,
                            Scale = new Vector2(1f, 1),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                        },
                    }
                }
            };
        }

        public void UpdateTrackLineHeight(ValueChangedEvent<double> v)
        {
            separator.Height = (float)v.NewValue;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            noteTrackLineHeight.BindTo(column.EzSkinInfo.NoteTrackLineHeight);
            noteTrackLineHeight.BindValueChanged(UpdateTrackLineHeight, true);
        }

        public void UpdateGlowEffect(Colour4 color)
        {
            separator.Colour = new ColourInfo
            {
                TopLeft = color,
                BottomRight = color.Lighten(1.05f),
            };
        }
    }
}
