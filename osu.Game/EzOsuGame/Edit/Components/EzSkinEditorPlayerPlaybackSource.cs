// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Testing;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 把插拔进来的 <see cref="Player"/> 时钟接到场景栏播放条。交互游玩走 <see cref="Player.Pause"/> /
    /// <see cref="Player.Resume"/>，与暂停界面、恢复覆盖层共用同一状态机；回放类无法暂停，只能直接控制时钟。
    /// </summary>
    public class EzSkinEditorPlayerPlaybackSource : IEzSkinEditorScenePlaybackSource
    {
        private readonly Player player;
        private readonly GameplayClockContainer? clock;

        // 回放谱面（含编辑器自带的无尽自动播放）不支持暂停，须绕过暂停状态机直接控制时钟。
        private readonly bool controlsClockDirectly;

        public EzSkinEditorPlayerPlaybackSource(Player player)
        {
            this.player = player;
            clock = player.ChildrenOfType<GameplayClockContainer>().FirstOrDefault();
            controlsClockDirectly = player is ReplayPlayer;
        }

        public bool IsActive => player.IsLoaded && clock != null;

        public double CurrentTime => clock?.CurrentTime ?? 0;

        public double BeatmapMinTime => 0;

        public double BeatmapMaxTime => System.Math.Max(player.Beatmap.Value.BeatmapInfo.Length, 1);

        public bool IsPlaying => clock != null && !clock.IsPaused.Value;

        public void Seek(double time) => clock?.Seek(time);

        public void SetPlaying(bool playing)
        {
            if (clock == null)
                return;

            if (controlsClockDirectly)
            {
                if (playing)
                    clock.Start();
                else
                    clock.Stop();

                return;
            }

            if (playing)
                player.Resume();
            else
                player.Pause();
        }
    }
}
