// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Writes resolved Ez column colours into legacy <c>skin.ini</c> Mania blocks.
    /// </summary>
    public static class EzSkinColourSkinIniWriter
    {
        public static bool TryWriteManiaColumnColours(int keyMode, Ez2ConfigManager config, EzSkinIniSession session)
        {
            if (keyMode <= 0 || session is not { IsSupported: true })
                return false;

            var document = session.ParseDraftDocument();
            document.EnsureManiaBlock(keyMode);

            for (int i = 0; i < keyMode; i++)
            {
                Colour4 colour = config.GetColumnColor(keyMode, i);
                document.SetManiaValue(keyMode, $"Colour{i + 1}", EzSkinIniColourFormat.ToIniString(colour, includeAlpha: true));
            }

            session.ApplyDocument(document);
            return true;
        }
    }
}
