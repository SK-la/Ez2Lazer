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
    /// 按键音（含 Autoplay 触发的 note 音）改由按文件名复用通道的发声池播放。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="osu.Game.Rulesets.Mania.Objects.Drawables.DrawableManiaHitObject"/> 共用 <see cref="ManiaPlayfield.SampleChannels"/>；
    /// 无 <see cref="ManiaPlayfield"/>（皮肤预览、编辑器等单独构造 <see cref="Column"/> 的场景）时才自建私有池。
    /// </remarks>
    internal partial class EzGameplaySampleTriggerSource : GameplaySampleTriggerSource
    {
        [Resolved(canBeNull: true)]
        private ManiaPlayfield? playfield { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        private EzManiaSampleChannelPool? fallbackPool;

        public EzGameplaySampleTriggerSource(HitObjectContainer hitObjectContainer)
            : base(hitObjectContainer)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 有 playfield 时共用它的池，避免同一份谱面被预载两次。
            if (playfield == null)
                AddInternal(fallbackPool = new EzManiaSampleChannelPool());
        }

        protected override void PlaySamples(ISampleInfo[] samples) => Schedule(() =>
        {
            var pool = playfield?.SampleChannels ?? fallbackPool;

            if (pool == null)
                return;

            foreach (var sample in samples)
                pool.Play(sample, 0);

            gameplayState?.ApplySamples(samples);
        });
    }
}
