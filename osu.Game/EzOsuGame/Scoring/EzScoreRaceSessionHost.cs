// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局共享的 <see cref="EzScoreRaceSession"/> 宿主，供多个角逐 HUD 组件注入。
    /// </summary>
    [Cached]
    public partial class EzScoreRaceSessionHost : Component
    {
        public EzScoreRaceSession? Session { get; private set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private Player player { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (gameplayState == null)
                return;

            var playMode = EzScoreRacePlayModeResolver.Resolve(player);
            Session = new EzScoreRaceSession(realm, scoreManager, beatmaps, gameplayState, playMode, action => Schedule(action));
            Session.EnsureLoaded();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                Session?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
