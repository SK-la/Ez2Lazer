// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Audio
{
    /// <summary>
    /// Gameplay 发声池：按 sample 的文件级 lookup 名复用发声体。
    /// </summary>
    /// <remarks>
    /// 每个文件名只保留一个 <see cref="PausableSkinnableSound"/>，同名再次触发时先停掉上一次再重放（<see cref="SkinnableSound.Play"/> 内部即 Stop 后 Play），
    /// 因此大量 note 命中同一文件时不会各自创建播放通道，也不会叠加响度。
    /// </remarks>
    internal partial class EzManiaSampleChannelPool : CompositeDrawable
    {
        private readonly Dictionary<string, PooledSample> samples = new Dictionary<string, PooledSample>(StringComparer.Ordinal);

        /// <summary>
        /// 触发一个 sample。
        /// </summary>
        /// <param name="sampleInfo">要发声的 sample 描述。</param>
        /// <param name="balance">声像（-1..1）。多键共用同一文件时以本次触发者为准。</param>
        public void Play(ISampleInfo sampleInfo, double balance)
        {
            string? key = sampleInfo.LookupNames.FirstOrDefault();

            if (string.IsNullOrEmpty(key))
                return;

            if (!samples.TryGetValue(key, out var pooled))
            {
                samples.Add(key, pooled = new PooledSample(sampleInfo));
                AddInternal(pooled.Sound);
            }

            pooled.Update(sampleInfo);
            pooled.Sound.Balance.Value = balance;
            pooled.Sound.Play();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // 显式停止，避免离开 gameplay 后仍有通道残留在播放。
                foreach (var pooled in samples.Values)
                    pooled.Sound.Stop();

                samples.Clear();
            }

            base.Dispose(isDisposing);
        }

        private class PooledSample
        {
            public readonly PausableSkinnableSound Sound;

            private int appliedVolume;

            public PooledSample(ISampleInfo sampleInfo)
            {
                Sound = new PausableSkinnableSound
                {
                    MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME,
                };

                apply(sampleInfo);
            }

            /// <summary>
            /// 音量在赋值时写入内部采样，因此仅在音量变化时重新赋值；
            /// 重新赋值会换用池内采样，先停掉当前通道以免留下无引用的播放中通道。
            /// </summary>
            public void Update(ISampleInfo sampleInfo)
            {
                if (sampleInfo.Volume == appliedVolume)
                    return;

                Sound.Stop();
                apply(sampleInfo);
            }

            private void apply(ISampleInfo sampleInfo)
            {
                Sound.Samples = new[] { sampleInfo };
                appliedVolume = sampleInfo.Volume;
            }
        }
    }
}
