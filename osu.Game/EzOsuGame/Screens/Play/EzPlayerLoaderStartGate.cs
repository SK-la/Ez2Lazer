// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.EzOsuGame.Screens.Rotation;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Screens.Play
{
    /// <summary>
    /// Aggregates Ez-side <see cref="PlayerLoader"/> preparation work (quick rotation pool, mania texture preload, score race ghosts).
    /// </summary>
    public partial class EzPlayerLoaderStartGate : Component, IEzScoreRacePlayerStartGate
    {
        private readonly EzScoreRaceService? scoreRaceService;

        private bool loaderActive;

        public EzPlayerLoaderStartGate(EzScoreRaceService? scoreRaceService = null)
        {
            this.scoreRaceService = scoreRaceService;
        }

        public bool CanStartPlayer =>
            (scoreRaceService?.CanStartPlayer ?? true)
            && !blocksQuickRotationPool();

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            game.ScreenStack.ScreenPushed += onScreenChanged;
            game.ScreenStack.ScreenExited += onScreenChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                game.ScreenStack.ScreenPushed -= onScreenChanged;
                game.ScreenStack.ScreenExited -= onScreenChanged;
            }

            base.Dispose(isDisposing);
        }

        private void onScreenChanged(IScreen last, IScreen next)
        {
            if (next is PlayerLoader)
                Schedule(() => beginLoaderPreparation(next as OsuScreen));
            else if (last is PlayerLoader)
                Schedule(endLoaderPreparation);
        }

        private void beginLoaderPreparation(OsuScreen? screen)
        {
            loaderActive = true;

            var session = EzQuickRotationCoordinator.Session;

            if (session.IsActive)
                session.StartPoolBuild(beatmaps);
        }

        private void endLoaderPreparation()
        {
            loaderActive = false;
        }

        private bool blocksQuickRotationPool()
        {
            if (!loaderActive)
                return false;

            var session = EzQuickRotationCoordinator.Session;
            return session.IsActive && !session.IsPoolReady;
        }
    }
}
