// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 可选的 PlayerLoader 启动门控：谱面加载阶段完成 ghost timeline 准备后再推进 <see cref="Player"/>。
    /// 服务未注册 DI 时 <see cref="PlayerLoader"/> 视为无门控。
    /// </summary>
    public interface IEzScoreRacePlayerStartGate
    {
        /// <summary>
        /// 是否允许 PlayerLoader 将已加载的 <see cref="Player"/> 推入屏幕栈。
        /// </summary>
        bool CanStartPlayer { get; }
    }
}
