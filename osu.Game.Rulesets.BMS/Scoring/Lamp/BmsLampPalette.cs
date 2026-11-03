// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.Scoring.Lamp
{
    /// <summary>
    /// Centralised default colours for each <see cref="BmsClearLamp"/>.
    /// Picked to match common BMS-community conventions (mirroring beatoraja /
    /// LR2 score-table colours) so existing players read the carousel intuitively:
    /// <list type="bullet">
    /// <item><description>NoPlay: muted slate (chart untouched)</description></item>
    /// <item><description>Failed: dim red</description></item>
    /// <item><description>Assist/LightAssist: purple shades</description></item>
    /// <item><description>Easy: green</description></item>
    /// <item><description>Normal: cyan-blue</description></item>
    /// <item><description>Hard: amber/gold</description></item>
    /// <item><description>ExHard: bright yellow</description></item>
    /// <item><description>FullCombo: cyan-white</description></item>
    /// <item><description>Perfect: pale gold</description></item>
    /// <item><description>Max: rainbow stand-in (solid white-gold)</description></item>
    /// </list>
    /// Schemes that want a different palette can either subclass this or just bypass
    /// it entirely and define their own <see cref="IBmsLampScheme.GetLampColour"/>.
    /// </summary>
    public static class BmsLampPalette
    {
        public static Color4 GetDefaultColour(BmsClearLamp lamp) => lamp switch
        {
            BmsClearLamp.NoPlay => Color4Extensions.FromHex("3F4248"),
            BmsClearLamp.Failed => Color4Extensions.FromHex("B73C3C"),
            BmsClearLamp.AssistEasy => Color4Extensions.FromHex("9D55C7"),
            BmsClearLamp.LightAssistEasy => Color4Extensions.FromHex("C58CE0"),
            BmsClearLamp.Easy => Color4Extensions.FromHex("4CD063"),
            BmsClearLamp.Normal => Color4Extensions.FromHex("3FA6FF"),
            BmsClearLamp.Hard => Color4Extensions.FromHex("F0A040"),
            BmsClearLamp.ExHard => Color4Extensions.FromHex("F0E040"),
            BmsClearLamp.FullCombo => Color4Extensions.FromHex("4FE3C4"),
            BmsClearLamp.Perfect => Color4Extensions.FromHex("F7E08C"),
            BmsClearLamp.Max => Color4Extensions.FromHex("FFFFFF"),
            _ => Color4Extensions.FromHex("3F4248"),
        };

        /// <summary>
        /// Default foreground colour (text/icon) intended to read on top of
        /// <see cref="GetDefaultColour"/>. Light lamps (Easy and below) take dark text;
        /// brighter / wider-gamut lamps still read well against near-black.
        /// </summary>
        public static Color4 GetDefaultTextColour(BmsClearLamp lamp) => lamp switch
        {
            BmsClearLamp.NoPlay => Color4Extensions.FromHex("D0D0D0"),
            BmsClearLamp.LightAssistEasy => Color4Extensions.FromHex("1A1A1A"),
            BmsClearLamp.Easy => Color4Extensions.FromHex("0A2A0A"),
            BmsClearLamp.ExHard => Color4Extensions.FromHex("1A1A1A"),
            BmsClearLamp.Perfect => Color4Extensions.FromHex("1A1A1A"),
            BmsClearLamp.Max => Color4Extensions.FromHex("1A1A1A"),
            _ => Color4.White,
        };
    }
}
