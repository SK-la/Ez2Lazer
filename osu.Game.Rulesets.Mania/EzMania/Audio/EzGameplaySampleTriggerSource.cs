// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
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

        /// <summary>
        /// 下一颗未判定 note 还太远、或本列已无未判定 note 时，退回「本列最近一颗已经过线的 note」。
        /// </summary>
        /// <remarks>
        /// BMS 键音谱的按键取样语义（beatoraja 的判定外按键分支即取「时间早于按下时刻的最后一颗」）：
        /// 有 note 可打就打那颗，没有也照常发声，取样点只随进度前进。上游的两个退回目标都不满足这点：
        /// 「本容器第一项」会被 latch 在谱面开头某颗上（键音谱里 sample 就是 note 自己的音频，那颗若带
        /// 超长键音，此后每次远离 note 的按键都会把它再打断重播一次），「沿用上次取样对象」在长间隔处
        /// 也会停在旧 note 上。
        /// </remarks>
        protected override HitObjectLifetimeEntry? FindFallbackEntry(HitObjectLifetimeEntry? nextUnjudged, double referenceTime)
            => FindLatestPassedEntry(referenceTime);

        public EzGameplaySampleTriggerSource(HitObjectContainer hitObjectContainer)
            : base(hitObjectContainer)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 有 playfield 时共用它的池，避免同一份谱面被预载两次。
            if (playfield == null)
                AddInternal(fallbackPool = new EzManiaSampleChannelPool(@"Mania fallback"));
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
