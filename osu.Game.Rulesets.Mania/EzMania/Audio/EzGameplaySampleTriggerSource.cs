// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.EzMania.Audio
{
    /// <summary>
    /// 按键预览（含 Autoplay 触发的 note 音）改用 <see cref="ManiaPlayfield"/> 上的按文件名通道池发声。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="osu.Game.Rulesets.Mania.Objects.Drawables.DrawableManiaHitObject"/> 共用同一池，同一次按键命中 note 时同一文件只会重放一次而不会叠成两遍；
    /// 池不可用（皮肤预览、编辑器等无 gameplay 场景）时回退到基类的独立发声体。
    /// </remarks>
    internal partial class EzGameplaySampleTriggerSource : GameplaySampleTriggerSource
    {
        [Resolved(canBeNull: true)]
        private ManiaPlayfield? playfield { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        public EzGameplaySampleTriggerSource(HitObjectContainer hitObjectContainer)
            : base(hitObjectContainer)
        {
        }

        protected override void PlaySamples(ISampleInfo[] samples)
        {
            var pool = playfield?.SampleChannels;

            if (pool == null)
            {
                base.PlaySamples(samples);
                return;
            }

            Schedule(() =>
            {
                foreach (var sample in samples)
                    pool.Play(sample, 0);

                gameplayState?.ApplySamples(samples);
            });
        }
    }
}
