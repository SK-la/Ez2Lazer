// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// Shared Ez-analysis player filter (wedge dropdown + SongSelect HUD panels).
    /// Value is a stored username or <see cref="LocalProfile.EzLocalProfileConstants.ALL_PLAYERS"/>
    /// (archive-wide / hypothetical All — see <see cref="LocalProfile.EzLocalProfileConstants.IsAllPlayersFilter"/>).
    /// </summary>
    public class EzAnalysisPlayerSelection
    {
        public Bindable<string?> Current { get; } = new Bindable<string?>();
    }
}
