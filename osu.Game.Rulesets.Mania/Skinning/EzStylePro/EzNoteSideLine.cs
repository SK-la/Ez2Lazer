// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Screens;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNoteSideLine : CompositeDrawable
    {
        private Drawable separator = null!;
        private Bindable<double> noteTrackLineHeight = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
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
                        Blending = BlendingParameters.Additive,
                        Child = new Sprite
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 10,
                            Scale = new Vector2(2f, 1),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                            // TextureRelativeSizeAxes = Axes.Y,
                        },
                    }
                }
            };

            noteTrackLineHeight = ezSkinConfig.GetBindable<double>(EzSkinSetting.NoteTrackLineHeight);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateSizes();
            noteTrackLineHeight.BindValueChanged(_ => updateSizes(), true);
        }

        private void updateSizes()
        {
            separator.Height = (float)noteTrackLineHeight.Value;
        }

        public void UpdateGlowEffect(Color4 color)
        {
            separator.Colour = new ColourInfo
            {
                TopLeft = color,
                BottomRight = color.Lighten(1.05f),
            };
        }
    }
}
