// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// 提供一个接口，用于在通过 <see cref="BeatmapConverter{TObject}"/> 生成 <see cref="IBeatmap"/> 后应用更改的 <see cref="Mod"/>。
    /// <para>可以实现 n to m key。</para>
    /// 但需要注意转换过程。建议搭配 <see cref="IApplyOrder"/> 一起使用，以确保在正确环节进行转k。
    /// </summary>
    public interface IApplicableAfterConversion : IApplicableMod
    {
        /// <summary>
        /// Applies this <see cref="Mod"/> to the <see cref="IBeatmap"/> after conversion has taken place.
        /// </summary>
        /// <param name="beatmap">The converted <see cref="IBeatmap"/>.</param>
        void ApplyToBeatmapAfterConversion(IBeatmap beatmap);
    }
}
