// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.UI.Scrolling;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI
{
    /// <summary>
    /// A single column in the BMS playfield.
    /// </summary>
    public partial class BMSColumn : ScrollingPlayfield
    {
        public readonly int ColumnIndex;

        public BMSColumn(int columnIndex)
        {
            ColumnIndex = columnIndex;

            RelativeSizeAxes = Axes.Y;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;

            InternalChildren = new Drawable[]
            {
                // Background
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = GetColumnColour(columnIndex),
                    Alpha = 0.3f,
                },
                // Border
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Colour = Color4.White,
                    Alpha = 0.2f,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                },
                // Hit target area
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 20,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -100, // Position from bottom
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0.5f,
                    }
                },
                // Hit object container
                HitObjectContainer,
            };
        }

        public static Color4 GetColumnColour(int columnIndex)
        {
            // Standard BMS color scheme
            return columnIndex switch
            {
                0 => new Color4(255, 0, 0, 255),     // Scratch - Red
                1 => new Color4(255, 255, 255, 255), // White
                2 => new Color4(0, 150, 255, 255),   // Blue
                3 => new Color4(255, 255, 255, 255), // White
                4 => new Color4(0, 150, 255, 255),   // Blue
                5 => new Color4(255, 255, 255, 255), // White
                6 => new Color4(0, 150, 255, 255),   // Blue
                7 => new Color4(255, 255, 255, 255), // White
                _ => new Color4(200, 200, 200, 255), // Default gray
            };
        }
    }
}
