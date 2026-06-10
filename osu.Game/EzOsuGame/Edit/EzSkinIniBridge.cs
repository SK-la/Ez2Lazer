// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Helpers for skin.ini fields participating in <see cref="EzSkinEditorComparisonSnapshot"/> default baselines.
    /// </summary>
    public static class EzSkinIniBridge
    {
        public static EzSkinIniDocument? TryParseSnapshotDocument(string? skinIniDraftText) =>
            string.IsNullOrEmpty(skinIniDraftText) ? null : EzSkinIniDocument.Parse(skinIniDraftText);

        public static void SyncTextDefault(Bindable<string> bindable, string? snapshotValue, string currentValue) =>
            bindable.Default = snapshotValue ?? currentValue;

        public static void SyncBoolDefault(Bindable<bool> bindable, string? snapshotRaw, bool currentValue) =>
            bindable.Default = snapshotRaw != null ? snapshotRaw == "1" : currentValue;

        public static void SyncColourDefault(Bindable<Colour4> bindable, Colour4? snapshotColour, Colour4 currentValue) =>
            bindable.Default = snapshotColour ?? currentValue;
    }
}
