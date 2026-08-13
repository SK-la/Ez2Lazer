// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;

namespace osu.Game.EzOsuGame.Fonts
{
    /// <summary>
    /// Registers system outline fonts: localized CJK fallbacks, platform emoji, and English UI typeface remaps.
    /// Settings slots are entry points for choosing families — not the boundary of “system fonts”.
    /// </summary>
    public static class EzUiFontBootstrap
    {
        private static bool systemEmojiRegistered;
        private static bool localizedFallbacksRegistered;

        /// <summary>
        /// Registers localized (CJK etc.) outline fonts into the empty-name fallback chain.
        /// Must run before Noto CJK BMFont so missing glyphs prefer the user's system localized face.
        /// </summary>
        public static void RegisterLocalizedFallbacks(OsuGameBase game, Ez2ConfigManager ezConfig)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(ezConfig);

            if (localizedFallbacksRegistered)
                return;

            localizedFallbacksRegistered = true;

            tryRegisterFallback(game, EzUiFontIds.UI_DEFAULT_LOCALIZED, ezConfig.Get<string>(Ez2Setting.UiFontDefaultLocalized));
            tryRegisterFallback(game, EzUiFontIds.TITLE_ALTERNATE_LOCALIZED, ezConfig.Get<string>(Ez2Setting.UiFontTitleAlternateLocalized));
        }

        /// <summary>
        /// Registers the platform / configured colour emoji font into the global FontStore fallback chain.
        /// Must run before resource BMFont emoji. Empty config = auto-detect; otherwise use the chosen family.
        /// </summary>
        public static void RegisterSystemEmojiFallback(OsuGameBase game, Ez2ConfigManager ezConfig)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(ezConfig);

            if (systemEmojiRegistered)
                return;

            systemEmojiRegistered = true;

            string configured = ezConfig.Get<string>(Ez2Setting.UiFontEmoji);
            EzSystemFontEntry? entry = null;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                entry = EzSystemFontCatalog.FindByFamily(configured);

                if (entry == null)
                    Logger.Log($"Ez system emoji: configured family '{configured}' not found; falling back to platform auto-detect.", Ez2ConfigManager.LOGGER_NAME);
            }

            entry ??= EzSystemFontCatalog.FindSystemEmojiFont();

            if (entry == null)
            {
                Logger.Log("Ez system emoji: no platform emoji font found; BMFont / resource emoji remain fallbacks.", Ez2ConfigManager.LOGGER_NAME);
                return;
            }

            try
            {
                game.AddOutlineFontFromFile(entry.Value.Path, EzUiFontIds.EMOJI, entry.Value.FaceIndex);
                Logger.Log($"Ez system emoji: {entry.Value.Family} -> {EzUiFontIds.EMOJI} ({entry.Value.Path})", Ez2ConfigManager.LOGGER_NAME);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez system emoji: failed to load '{entry.Value.Path}': {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
        }

        /// <summary>
        /// Remaps English UI / title / numeric typefaces to system outline families at cold start.
        /// Localized faces are registered separately via <see cref="RegisterLocalizedFallbacks"/>.
        /// </summary>
        public static void Apply(OsuGameBase game, Ez2ConfigManager ezConfig)
        {
            ArgumentNullException.ThrowIfNull(game);
            ArgumentNullException.ThrowIfNull(ezConfig);

            OsuFont.ClearFamilyOverrides();

            tryApplySlot(game, Typeface.Torus, EzUiFontIds.UI_DEFAULT, ezConfig.Get<string>(Ez2Setting.UiFontDefault));
            tryApplySlot(game, Typeface.TorusAlternate, EzUiFontIds.TITLE_ALTERNATE, ezConfig.Get<string>(Ez2Setting.UiFontTitleAlternate));
            tryApplySlot(game, Typeface.Venera, EzUiFontIds.NUMERIC, ezConfig.Get<string>(Ez2Setting.UiFontNumeric));
        }

        private static void tryRegisterFallback(OsuGameBase game, string lookupId, string systemFamily)
        {
            if (string.IsNullOrWhiteSpace(systemFamily))
                return;

            var entry = EzSystemFontCatalog.FindByFamily(systemFamily);

            if (entry == null)
            {
                Logger.Log($"Ez localized font: system family '{systemFamily}' not found for {lookupId}.", Ez2ConfigManager.LOGGER_NAME);
                return;
            }

            try
            {
                game.AddOutlineFontFromFile(entry.Value.Path, lookupId, entry.Value.FaceIndex);
                Logger.Log($"Ez localized font: {entry.Value.Family} -> {lookupId}", Ez2ConfigManager.LOGGER_NAME);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez localized font: failed to load '{entry.Value.Path}': {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
        }

        private static void tryApplySlot(OsuGameBase game, Typeface typeface, string lookupId, string systemFamily)
        {
            if (string.IsNullOrWhiteSpace(systemFamily))
                return;

            var entry = EzSystemFontCatalog.FindByFamily(systemFamily);

            if (entry == null)
            {
                Logger.Log($"Ez UI font: system family '{systemFamily}' not found; keeping built-in for {typeface}.", Ez2ConfigManager.LOGGER_NAME);
                return;
            }

            try
            {
                game.AddOutlineFontFromFile(entry.Value.Path, lookupId, entry.Value.FaceIndex);
                OsuFont.SetFamilyOverride(typeface, lookupId);
                Logger.Log($"Ez UI font: {typeface} -> {entry.Value.Family} ({lookupId})", Ez2ConfigManager.LOGGER_NAME);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez UI font: failed to load '{entry.Value.Path}': {ex.Message}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }
        }
    }
}
