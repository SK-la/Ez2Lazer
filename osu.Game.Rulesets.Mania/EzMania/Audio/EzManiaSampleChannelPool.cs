// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Statistics;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.EzMania.Audio
{
    /// <summary>
    /// Gameplay 发声池：按音频名复用通道，同一个音频名再次触发时打断上一次。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 池在开局（<see cref="LoadComplete"/>）就把本局 <see cref="DrawableRuleset.Objects"/>（含子对象与
    /// <see cref="HitObject.AuxiliarySamples"/>）用到的 sample 全部解析掉，此后每次触发只做「查表 → 取通道 → 播」，
    /// 不再中途查皮肤、不再中途往 drawable 层级里增删任何东西。常驻通道数由音频名数量决定，不随进度增长。
    /// </para>
    /// <para>
    /// 不复用 <see cref="SkinnableSound"/>：池化的 <see cref="PoolableSkinnableSample"/> 一个实例只能绑定一次
    /// <see cref="ISampleInfo"/>，换 sample 必须往已加载的层级里增删 drawable，并让 playfield 按 sample 永久留存
    /// <c>DrawablePool</c>；keysound 数量大的谱面会随进度持续劣化。
    /// </para>
    /// <para>
    /// 通道不在 drawable 层级里，因此上游由层级隐式提供的语义要显式补回：
    /// <see cref="ISamplePlaybackDisabler"/>（暂停/跳过 intro/追赶期间不出新声）与
    /// <see cref="DrawableRuleset.Audio"/>（gameplay 级音量，例如 Mute mod）。
    /// </para>
    /// <para>
    /// 池自身不建 <c>DrawablePool</c>，所以统计由 <see cref="EzSampleChannelStatistic"/> 自己上报：
    /// 名字固定、跨局复用同一条目，而不是像上游那样每局新增一批只增不减的条目。
    /// </para>
    /// </remarks>
    public partial class EzManiaSampleChannelPool : Drawable
    {
        [Resolved]
        private ISkinSource source { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private ISamplePlaybackDisabler? samplePlaybackDisabler { get; set; }

        [Resolved(canBeNull: true)]
        private DrawableRuleset? drawableRuleset { get; set; }

        /// <summary>
        /// 只用来判断「是否处于真实对局」：编辑器 / 皮肤预览不必为一个可能很大的谱面做开局预解析。
        /// </summary>
        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        /// <summary>
        /// 解析表：sample 描述 → 解析结果（包含解析不到的情况，避免重复查表）。
        /// </summary>
        private readonly Dictionary<ISampleInfo, ResolvedSample> resolvedSamples = new Dictionary<ISampleInfo, ResolvedSample>();

        /// <summary>
        /// 通道表：音频名 → 当前通道。一个音频名同时只保留一条通道，新触发先打断旧的。
        /// </summary>
        private readonly Dictionary<string, SampleChannel> channels = new Dictionary<string, SampleChannel>();

        /// <summary>
        /// 没有查表名的自定义 sample 的兜底键，使其不与其它 sample 互相打断。
        /// </summary>
        private readonly Dictionary<ISampleInfo, string> fallbackKeys = new Dictionary<ISampleInfo, string>();

        /// <summary>
        /// 固定名字的全局统计：同一名字跨局复用同一条目，因此可以逐局对比，
        /// 不像上游 <c>DrawablePool</c> 那样每局新增一批只增不减的统计项（见类注释）。
        /// </summary>
        private readonly GlobalStatistic<EzSampleChannelStatistic> statistic;

        private int fallbackKeyCounter;
        private ScheduledDelegate? pendingSourceChange;

        public EzManiaSampleChannelPool(string statisticName)
        {
            statistic = GlobalStatistics.Get<EzSampleChannelStatistic>(@"Ez gameplay samples", statisticName);
            statistic.Value = new EzSampleChannelStatistic();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            source.SourceChanged += onSourceChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            registerBeatmapSamples();
        }

        /// <summary>
        /// 触发一个 sample；该音频名若有通道在播，先打断它再重新开始。
        /// </summary>
        /// <param name="sampleInfo">要发声的 sample 描述。</param>
        /// <param name="balance">声像（-1..1）。多键共用同一文件时以本次触发者为准。</param>
        public void Play(ISampleInfo sampleInfo, double balance)
        {
            // 与 PausableSkinnableSound 一致：暂停、跳过 intro、框架追赶期间不出新声。
            if (samplePlaybackDisabler?.SamplePlaybackDisabled.Value == true)
                return;

            var resolved = resolve(sampleInfo);

            if (resolved.Sample == null)
                return;

            // 同名再次触发 = 打断上一次。BASS 通道只能从头播一次（播完无法重放，暂停后只能从中途续播），
            // 所以这里换一条新通道，与上游每次触发都取新通道的做法一致。
            if (channels.Remove(resolved.Key, out var previous))
            {
                disposeChannel(previous);
                statistic.Value.Choked++;
            }

            var channel = resolved.Sample.GetChannel();
            statistic.Value.Created++;

            channel.Volume.Value = Math.Max(sampleInfo.Volume, DrawableHitObject.MINIMUM_SAMPLE_VOLUME) / 100.0;
            channel.Balance.Value = balance;

            if (drawableRuleset != null)
                channel.BindAdjustments(drawableRuleset.Audio);

            // 与 SkinnableSound 一致：整条音量链路为 0（例如 Mute mod）时不发声，省下一次通道分配。
            if (channel.AggregateVolume.Value <= 0)
            {
                disposeChannel(channel);
                return;
            }

            channels[resolved.Key] = channel;
            statistic.Value.Active = channels.Count;
            channel.Play();
        }

        /// <summary>
        /// 把本局谱面用到的 sample 全部解析掉，使游戏中途不再出现加载。
        /// </summary>
        private void registerBeatmapSamples()
        {
            if (gameplayState == null || drawableRuleset == null)
                return;

            foreach (var hitObject in drawableRuleset.Objects)
                register(hitObject);

            statistic.Value.Resolved = resolvedSamples.Values.Count(s => s.Sample != null);
        }

        private void register(HitObject hitObject)
        {
            foreach (var sample in hitObject.Samples)
                resolve(sample);

            foreach (var sample in hitObject.AuxiliarySamples)
                resolve(sample);

            foreach (var nested in hitObject.NestedHitObjects)
                register(nested);
        }

        private ResolvedSample resolve(ISampleInfo sampleInfo)
        {
            if (resolvedSamples.TryGetValue(sampleInfo, out var cached))
                return cached;

            // 皮肤取不到时 GetSample 返回 null，这个结果也记住，避免每次触发都重新查一遍。
            resolvedSamples.Add(sampleInfo, cached = new ResolvedSample
            {
                Sample = source.GetSample(sampleInfo),
                Key = channelKeyFor(sampleInfo),
            });

            return cached;
        }

        private string channelKeyFor(ISampleInfo sampleInfo)
        {
            // 查表名的第一项就是皮肤实际要取的那个音频名：BMS 是 keysound 文件名，普通 note 是
            // Gameplay/normal-hitnormal 之类。同名即同一条通道。
            string? name = sampleInfo.LookupNames.FirstOrDefault();

            if (!string.IsNullOrEmpty(name))
                return name;

            if (!fallbackKeys.TryGetValue(sampleInfo, out string? fallbackKey))
                fallbackKeys.Add(sampleInfo, fallbackKey = $"\0{fallbackKeyCounter++}");

            return fallbackKey;
        }

        private void onSourceChanged()
        {
            // 与 SkinReloadableDrawable 一致：延后到下一次调度，避免在已销毁的层级上执行。
            pendingSourceChange?.Cancel();
            pendingSourceChange = Scheduler.Add(resetSamples);
        }

        private void resetSamples()
        {
            pendingSourceChange = null;

            // 皮肤来源变了：丢弃已解析的采样与在播通道，再按新皮肤重新解析一遍，保持「开局之后不再加载」。
            foreach (var channel in channels.Values)
                disposeChannel(channel);

            channels.Clear();
            fallbackKeys.Clear();
            resolvedSamples.Clear();
            statistic.Value.Active = 0;
            statistic.Value.Resolved = 0;

            registerBeatmapSamples();
        }

        private static void disposeChannel(SampleChannel? channel)
        {
            if (channel == null || channel.IsDisposed)
                return;

            try
            {
                // SampleChannel 的实际释放在音频线程执行，重复调用是安全的。
                channel.Dispose();
            }
            catch
            {
                // 与音频线程的释放竞争是无害的。
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                source.SourceChanged -= onSourceChanged;
                pendingSourceChange?.Cancel();
                pendingSourceChange = null;

                // 显式释放，避免退出对局后仍有通道在播。
                foreach (var channel in channels.Values)
                    disposeChannel(channel);

                channels.Clear();
                fallbackKeys.Clear();
                resolvedSamples.Clear();

                // 计数按局归零，便于逐局对比（对象本身留在全局统计里，不新增条目）。
                statistic.Value = new EzSampleChannelStatistic();
            }

            base.Dispose(isDisposing);
        }

        private class ResolvedSample
        {
            /// <summary>
            /// 解析结果；null 表示当前皮肤取不到该音频。
            /// </summary>
            public ISample? Sample;

            /// <summary>
            /// 该 sample 归属的通道键（音频名）。
            /// </summary>
            public string Key = string.Empty;
        }
    }

    /// <summary>
    /// 发声池自报的运行时计数（全局统计群组 "Ez gameplay samples"）。
    /// </summary>
    public class EzSampleChannelStatistic
    {
        /// <summary>
        /// 开局解析成功的 sample 数（皮肤取不到的 sample 不计入）。
        /// </summary>
        public int Resolved;

        /// <summary>
        /// 当前仍在播的通道数。
        /// </summary>
        public int Active;

        /// <summary>
        /// 本局累计分配的通道数。中途不再增长即说明没有运行时加载。
        /// </summary>
        public int Created;

        /// <summary>
        /// 本局累计打断次数（同名再次触发）。
        /// </summary>
        public int Choked;

        public override string ToString() => $"{Active}/{Resolved} ({Created} created, {Choked} choked)";
    }
}
