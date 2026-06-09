// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Settings.Sections
{
    public partial class EzSkinEditorSkinIniPlaceholderSection : FillFlowContainer
    {
        public EzSkinEditorSkinIniPlaceholderSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(8);
            Padding = new MarginPadding(5);

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = "Skin.ini 编辑",
                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                    Colour = Color4.White,
                },
                new OsuSpriteText
                {
                    Text = "里程碑 3：将在此显示 Skin.ini 各项设置。",
                    Font = OsuFont.Default.With(size: 14),
                    Colour = Color4.Gray,
                },
            };
        }
    }
}
