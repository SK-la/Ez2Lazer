// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.Database;

namespace osu.Game
{
    public partial class OsuGame
    {
        private DetachedBeatmapStoreSyncPolicy detachedBeatmapSyncPolicy = null!;

        private void cacheDetachedBeatmapStoreSyncPolicy()
        {
            detachedBeatmapSyncPolicy = new DetachedBeatmapStoreSyncPolicy();
            Dependencies.Cache(detachedBeatmapSyncPolicy);
        }

        private void notifyDetachedBeatmapStoreScreenTransition()
        {
            detachedBeatmapSyncPolicy.NotifyScreenTransition(Clock.CurrentTime);
        }

        private void onEzDetachedBeatmapStoreScreenPushed(IScreen lastScreen, IScreen newScreen)
        {
            notifyDetachedBeatmapStoreScreenTransition();
        }

        private void onEzDetachedBeatmapStoreScreenExited(IScreen lastScreen, IScreen newScreen)
        {
            notifyDetachedBeatmapStoreScreenTransition();
        }
    }
}
