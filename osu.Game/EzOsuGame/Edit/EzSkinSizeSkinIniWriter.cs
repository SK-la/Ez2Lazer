// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Writes Ez size settings into legacy <c>skin.ini</c> Mania layout/position fields.
    /// </summary>
    public static class EzSkinSizeSkinIniWriter
    {
        public static bool TryWriteManiaSizeSettings(int keyMode, Ez2ConfigManager config, EzSkinIniSession session)
        {
            if (keyMode <= 0 || session is not { IsSupported: true })
                return false;

            var document = session.ParseDraftDocument();
            document.EnsureManiaBlock(keyMode);

            document.SetManiaValue(keyMode, "ColumnWidth", format(config.Get<double>(Ez2Setting.ColumnWidth)));
            document.SetManiaValue(keyMode, "HitPosition", format(config.Get<double>(Ez2Setting.HitPosition)));
            document.SetManiaValue(keyMode, "WidthForNoteHeightScale", format(config.Get<double>(Ez2Setting.ColumnWidth)));

            session.ApplyDocument(document);
            return true;
        }

        private static string format(double value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
