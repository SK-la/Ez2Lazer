// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Fonts;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Menu
{
    /// <summary>
    /// Customisable text drawn in the centre of the menu cookie. Input format: <c>text,size</c>.
    /// Uses the default UI / localized outline face so kaomoji follow the user's custom font.
    /// </summary>
    public partial class EzMenuLogoCentreText : OsuSpriteText
    {
        public const float DEFAULT_FONT_SIZE = 80;
        public const float MIN_FONT_SIZE = 1;
        public const float MAX_FONT_SIZE = 512;

        private Bindable<string> setting = new Bindable<string>(string.Empty);
        private Bindable<string> uiFont = new Bindable<string>(string.Empty);
        private Bindable<string> uiFontLocalized = new Bindable<string>(string.Empty);

        public EzMenuLogoCentreText()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            BypassAutoSizeAxes = Axes.Both;
            UseFullGlyphHeight = false;
            Colour = Color4.White;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig)
        {
            setting = ezConfig.GetBindable<string>(Ez2Setting.MenuLogoText);
            uiFont = ezConfig.GetBindable<string>(Ez2Setting.UiFontDefault);
            uiFontLocalized = ezConfig.GetBindable<string>(Ez2Setting.UiFontDefaultLocalized);

            setting.BindValueChanged(_ => apply(), true);
        }

        private void apply()
        {
            if (!TryParse(setting.Value, out string text, out float size))
            {
                Text = string.Empty;
                Hide();
                return;
            }

            Font = ResolveFont(size, uiFont.Value, uiFontLocalized.Value);
            Text = text;
            Show();
        }

        /// <summary>
        /// Picks the same outline family as default UI text so CJK / kaomoji are not left on Torus-Alternate + Noto.
        /// Localized slot first (covers 颜文字); then the English UI remap; otherwise built-in Torus.
        /// </summary>
        public static FontUsage ResolveFont(float size, string? uiFontDefault, string? uiFontDefaultLocalized)
        {
            if (!string.IsNullOrWhiteSpace(uiFontDefaultLocalized))
                return new FontUsage(EzUiFontIds.UI_DEFAULT_LOCALIZED, size);

            if (!string.IsNullOrWhiteSpace(uiFontDefault) && OsuFont.HasFamilyOverride(Typeface.Torus))
                return new FontUsage(EzUiFontIds.UI_DEFAULT, size);

            return OsuFont.GetFont(size: size);
        }

        /// <summary>
        /// Parses <c>text,size</c>. The last ASCII or fullwidth comma followed by a positive number is the size.
        /// Remaining content (including other commas) is the displayed text. Kaomoji are passed through unchanged.
        /// </summary>
        public static bool TryParse(string? raw, out string text, out float size)
        {
            text = string.Empty;
            size = DEFAULT_FONT_SIZE;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            raw = raw.Trim();
            int separator = findLastSizeSeparator(raw);

            if (separator >= 0
                && float.TryParse(raw.AsSpan(separator + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                && parsed > 0)
            {
                text = raw[..separator];
                size = Math.Clamp(parsed, MIN_FONT_SIZE, MAX_FONT_SIZE);
                return text.Length > 0;
            }

            text = raw;
            return true;
        }

        private static int findLastSizeSeparator(string raw)
        {
            for (int i = raw.Length - 1; i >= 0; i--)
            {
                if (raw[i] is ',' or '，')
                    return i;
            }

            return -1;
        }
    }
}
