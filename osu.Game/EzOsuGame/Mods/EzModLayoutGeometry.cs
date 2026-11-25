// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.EzOsuGame.Mods
{
    internal static class EzModLayoutGeometry
    {
        public const float CARD_SPACING = 12;
        public const float DEFAULT_CARD_WIDTH = 280;
        public const float COMPACT_CARD_WIDTH = 200;

        public static float CardWidth(float availableWidth, float shearWidth = 0, float targetCardWidth = DEFAULT_CARD_WIDTH)
        {
            float width = Math.Max(1, availableWidth);
            int columns = Math.Max(1, (int)MathF.Floor((width + CARD_SPACING) / (targetCardWidth + shearWidth + CARD_SPACING)));
            return Math.Max(1, (width - (columns - 1) * CARD_SPACING) / columns - shearWidth);
        }

        public static int InsertionIndex(IReadOnlyList<(float X, float Y, float Width, float Height)> cards, Vector2 position)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];

                if (position.Y < card.Y || (position.Y <= card.Y + card.Height && position.X < card.X + card.Width / 2))
                    return i;
            }

            return cards.Count;
        }

        public static int GroupInsertionIndex(IEnumerable<float> centres, float coordinate)
        {
            int index = 0;

            foreach (float centre in centres)
            {
                if (coordinate < centre)
                    break;

                index++;
            }

            return index;
        }
    }
}
