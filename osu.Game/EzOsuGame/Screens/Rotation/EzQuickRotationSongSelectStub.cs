// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    /// <summary>
    /// No-op <see cref="ISongSelect"/> for wedge components that only need search-link actions disabled.
    /// </summary>
    internal sealed class EzQuickRotationSongSelectStub : ISongSelect
    {
        public bool CanPresentScore => false;

        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet { get; } = new Bindable<BeatmapSetInfo?>();

        public void Delete(BeatmapSetInfo beatmapBeatmapSetInfo)
        {
        }

        public void RestoreAllHidden(BeatmapSetInfo beatmapSet)
        {
        }

        public void ManageCollections()
        {
        }

        public void PresentScore(ScoreInfo score, ScorePresentType presentType = ScorePresentType.Results)
        {
        }

        public void AddToSearch(string query)
        {
        }

        public IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap) => Array.Empty<OsuMenuItem>();

        public void ScopeToBeatmapSet(BeatmapSetInfo beatmapSet)
        {
        }

        public void UnscopeBeatmapSet()
        {
        }
    }
}
