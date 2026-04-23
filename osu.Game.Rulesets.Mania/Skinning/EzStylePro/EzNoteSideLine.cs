// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzNoteSideLine : CompositeDrawable
    {
        private IBindable<double> noteTrackLineHeight = null!;

        private Drawable separator = null!;
        private Sprite? sprite;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, Ez2ConfigManager ezConfig)
        {
            AlwaysPresent = true;

            // 使用共享纹理避免重复加载
            var texture = textures.Get("EzResources/note/NoteSideLine.png");

            sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Y,
                Width = 10,
                Scale = new Vector2(1f, 1),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = texture,
            };

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
                        Child = sprite,
                    }
                }
            };

            noteTrackLineHeight = ezConfig.GetBindable<double>(Ez2Setting.NoteTrackLineHeight);
            noteTrackLineHeight.BindValueChanged(UpdateTrackLineHeight, true);
        }

        public void UpdateTrackLineHeight(ValueChangedEvent<double> v)
        {
            separator.Height = (float)v.NewValue;
        }

        // public void UpdateGlowEffect(Colour4 color)
        // {
        //     separator.Colour = new ColourInfo
        //     {
        //         TopLeft = color,
        //         BottomRight = color.Lighten(1.05f),
        //     };
        // }
    }
}
