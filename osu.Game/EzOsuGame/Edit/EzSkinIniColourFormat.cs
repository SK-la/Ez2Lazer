// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Edit
{
    public static class EzSkinIniColourFormat
    {
        public static bool TryParse(string? value, out Colour4 colour)
        {
            colour = Colour4.White;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            string[] split = value.Split(',');

            if (split.Length < 3)
                return false;

            try
            {
                byte r = byte.Parse(split[0], CultureInfo.InvariantCulture);
                byte g = byte.Parse(split[1], CultureInfo.InvariantCulture);
                byte b = byte.Parse(split[2], CultureInfo.InvariantCulture);
                byte a = split.Length >= 4 ? byte.Parse(split[3], CultureInfo.InvariantCulture) : (byte)255;
                colour = new Colour4(r, g, b, a);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public static string ToIniString(Colour4 colour, bool includeAlpha = false)
        {
            byte r = toByte(colour.R);
            byte g = toByte(colour.G);
            byte b = toByte(colour.B);
            byte a = toByte(colour.A);

            if (includeAlpha && a < 255)
                return FormattableString.Invariant($"{r},{g},{b},{a}");

            return FormattableString.Invariant($"{r},{g},{b}");
        }

        private static byte toByte(float component) =>
            (byte)Math.Clamp((int)MathF.Round(component * byte.MaxValue), 0, byte.MaxValue);
    }
}
