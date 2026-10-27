// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 可配置为不生成回放的 <see cref="ICreateReplayData"/> mod，从而由玩家亲手打而非旁观。
    /// </summary>
    /// <remarks>
    /// 是否生成回放决定使用哪个 <see cref="Screens.Play.Player"/>，开局后即固定。
    /// 只需「旁观」行为的调用方应先问此接口。
    /// </remarks>
    public interface IConditionalReplayGeneration
    {
        /// <summary>
        /// 以当前配置是否应为该 mod 生成回放。
        /// </summary>
        bool ReplayGenerationEnabled { get; }
    }
}
