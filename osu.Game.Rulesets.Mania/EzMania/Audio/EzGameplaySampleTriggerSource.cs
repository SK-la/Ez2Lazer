// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Audio;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
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

        /// <summary>
        /// LN 的键音来自头部，因此取样窗口取头节点的 miss 窗口。
        /// </summary>
        /// <remarks>
        /// <see cref="HoldNote"/> 自身的 <see cref="HitObject.HitWindows"/> 是 <see cref="HitWindows.Empty"/>，
        /// miss 窗口恒为 0，上游的「差两倍 miss 窗口以内」于是退化成「不早于这件物件的开始时间」：
        /// 只要按早哪怕 1ms，这颗 LN 就被当成「离得还太远」，直接落进退回分支取到上一颗 note 的键音。
        /// 头的判定窗口才是「按下会不会判到这颗 LN」的依据（<see cref="osu.Game.Rulesets.Mania.Objects.Drawables.DrawableHoldNote"/>
        /// 判头与限制按早起点的都是 <c>Head.HitObject.HitWindows</c>）。
        /// </remarks>
        protected override double SampleWindowFor(HitObject hitObject)
        {
            if (hitObject is HoldNote hold && hold.Head?.HitWindows != null)
                return hold.Head.HitWindows.WindowFor(HitResult.Miss);

            return base.SampleWindowFor(hitObject);
        }

        /// <summary>
        /// 头部判定窗口内一律取头节点作为键音来源。
        /// </summary>
        /// <remarks>
        /// 头的 <c>GetEndTime()</c> 就是它自己的开始时间，而 LN 只有一个键音样本（在头上）。
        /// 上游按「结束时间晚于参考时间」筛嵌套对象，按下晚于头时刻时会跳过头部，改取尾/体：
        /// 键音谱的尾节点通常没有样本，于是这次按键静音，而头部判定随后才把键音补上，听起来就是键音跟着判定走、
        /// 和按键对不上。超出窗口（长按过程中再次按下）仍走上游分支。
        /// </remarks>
        protected override HitObject? SelectSampleSource(HitObjectLifetimeEntry entry, double referenceTime)
        {
            if (entry.HitObject is HoldNote hold && hold.Head != null
                && referenceTime <= hold.StartTime + SampleWindowFor(hold))
                return hold.Head;

            return base.SelectSampleSource(entry, referenceTime);
        }

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
