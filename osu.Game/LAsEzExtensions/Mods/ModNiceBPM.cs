// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class ModNiceBPM : Mod, IApplicableToRate, IApplicableToDrawableHitObject, IApplicableToBeatmap, IUpdatableByPlayfield
    {
        public override string Name => "Nice BPM";

        public override string Acronym => "NB";

        public override LocalisableString Description => EzModStrings.NiceBPM_Description;

        public override ModType Type => ModType.LA_Mod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override Type[] IncompatibleMods => new[] { typeof(ModRateAdjust), typeof(ModTimeRamp), typeof(ModAutoplay) };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.FreeBPM_Label), nameof(EzModStrings.FreeBPM_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> FreeBPM { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.InitialRate_Label), nameof(EzModStrings.InitialRate_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> InitialRate { get; } = new BindableDouble(1)
        {
            MinValue = 0.2,
            MaxValue = 2,
            Precision = 0.01
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.EnableDynamicBPM_Label), nameof(EzModStrings.EnableDynamicBPM_Description), SettingControlType = typeof(SettingsCheckbox))]
        public BindableBool EnableDynamicBPM { get; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.AdjustPitch_Label), nameof(EzModStrings.AdjustPitch_Description))]
        public BindableBool AdjustPitch { get; } = new BindableBool(false);

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.MinAllowableRate_Label), nameof(EzModStrings.MinAllowableRate_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MinAllowableRate { get; } = new BindableDouble(0.7)
        {
            MinValue = 0.1,
            MaxValue = 1.0,
            Precision = 0.1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.MaxAllowableRate_Label), nameof(EzModStrings.MaxAllowableRate_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MaxAllowableRate { get; } = new BindableDouble(1.5)
        {
            MinValue = 1.0,
            MaxValue = 3.0,
            Precision = 0.1
        };

        // 每次速度变化的阈值，如果有需要以后再增加设置。
        private const double min_rate_change_factor = 0.9d;
        private const double max_rate_change_factor = 1.11d;

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.MissCountThreshold_Label), nameof(EzModStrings.MissCountThreshold_Description), SettingControlType = typeof(SettingsSlider<int>))]
        public BindableInt MissThreshold { get; } = new BindableInt(3)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzModStrings), nameof(EzModStrings.RateChangeOnMiss_Label), nameof(EzModStrings.RateChangeOnMiss_Description), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> RateChangeOnMiss { get; } = new BindableDouble(0.95)
        {
            MinValue = 0.5,
            MaxValue = 1.0,
            Precision = 0.01
        };

        public BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 3.0,
        };

        private double targetRate = 1d;

        private const int recent_rate_count = 8;

        private readonly List<double> recentRates = Enumerable.Repeat(1d, recent_rate_count).ToList();

        /// <summary>
        /// 对于地图中的每个 <see cref="HitObject"/>，此字典将对象映射到任何其他对象的最新结束时间
        /// 这些结束时间早于给定对象的结束时间。
        /// 在没有重叠打击对象的规则集中，可以粗略地将其解释为前一个打击对象的结束时间。
        /// </summary>
        private readonly Dictionary<HitObject, double> precedingEndTimes = new Dictionary<HitObject, double>();

        /// <summary>
        /// 对于地图中的每个 <see cref="HitObject"/>，当击中对象时，此字典将对象映射到从
        /// <see cref="recentRates"/> 中出队的轨道速率（即队列中最旧的值）。如果随后撤销了击中，
        /// 可以将映射值重新引入 <see cref="recentRates"/> 以正确回滚队列。
        /// </summary>
        private readonly Dictionary<HitObject, double> ratesForRewinding = new Dictionary<HitObject, double>();

        private readonly RateAdjustModHelper rateAdjustHelper;

        private double originalBPM;
        private bool hasAppliedFreeBPM;
        private int currentMissCount;

        public ModNiceBPM()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            // 当最小/最大允许速率值更改时更新速度变化范围
            MinAllowableRate.BindValueChanged(val =>
            {
                SpeedChange.MinValue = val.NewValue;
                if (SpeedChange.Value < val.NewValue)
                    SpeedChange.Value = val.NewValue;

                // 确保最小允许速率不超过最大允许速率
                if (val.NewValue > MaxAllowableRate.Value)
                    MinAllowableRate.Value = MaxAllowableRate.Value;
            }, true);

            MaxAllowableRate.BindValueChanged(val =>
            {
                SpeedChange.MaxValue = val.NewValue;
                if (SpeedChange.Value > val.NewValue)
                    SpeedChange.Value = val.NewValue;

                // 确保最大允许速率不低于最小允许速率
                if (val.NewValue < MinAllowableRate.Value)
                    MaxAllowableRate.Value = MinAllowableRate.Value;
            }, true);

            InitialRate.BindValueChanged(val =>
            {
                // 仅在未设置FreeBPM时应用初始速率
                if (!FreeBPM.Value.HasValue)
                {
                    SpeedChange.Value = val.NewValue;
                    targetRate = val.NewValue;
                }
            }, true);

            FreeBPM.BindValueChanged(val =>
            {
                if (val.NewValue.HasValue && val.NewValue > 0)
                {
                    // 如果原始BPM已可用，立即应用FreeBPM
                    if (originalBPM > 0)
                    {
                        double freeRate = val.NewValue.Value / originalBPM;
                        SpeedChange.Value = freeRate;
                        targetRate = freeRate;
                        hasAppliedFreeBPM = true;
                    }
                    // 否则，延迟应用FreeBPM直到调用ApplyToBeatmap
                }
                else
                {
                    // 当清除FreeBPM时，如果动态BPM被禁用，则恢复到初始速率
                    if (!EnableDynamicBPM.Value)
                    {
                        SpeedChange.Value = InitialRate.Value;
                        targetRate = InitialRate.Value;
                    }

                    hasAppliedFreeBPM = false;
                    currentMissCount = 0; // 当FreeBPM被清除时重置失误计数
                }
            }, true);

            EnableDynamicBPM.BindValueChanged(val =>
            {
                if (!val.NewValue)
                {
                    // 如果动态BPM被禁用，则恢复到FreeBPM或初始速率
                    if (FreeBPM.Value.HasValue && FreeBPM.Value > 0)
                    {
                        // 仅在原始BPM可用时应用，否则等待ApplyToBeatmap
                        if (originalBPM > 0)
                        {
                            double freeRate = FreeBPM.Value.Value / originalBPM;
                            SpeedChange.Value = freeRate;
                            targetRate = freeRate;
                        }
                    }
                    else
                    {
                        SpeedChange.Value = InitialRate.Value;
                        targetRate = InitialRate.Value;
                    }

                    currentMissCount = 0; // 当动态BPM被禁用时重置失误计数
                }
            }, true);
        }

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            // 检查是否设置了FreeBPM且原始BPM可用
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && originalBPM > 0)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                SpeedChange.Value = freeRate;
                targetRate = freeRate;

                // 如果启用了动态BPM，则用自由速率初始化最近速率
                if (EnableDynamicBPM.Value)
                {
                    recentRates.Clear();
                    recentRates.AddRange(Enumerable.Repeat(freeRate, recent_rate_count));
                }
                else
                {
                    recentRates.Clear();
                    recentRates.AddRange(Enumerable.Repeat(freeRate, recent_rate_count));
                }
            }
            else if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && !hasAppliedFreeBPM && originalBPM <= 0)
            {
                // 如果设置了FreeBPM但原始BPM尚不可用，则推迟应用
                // 我们将在BPM可用时在ApplyToBeatmap中应用它
                // 目前，使用InitialRate作为临时后备
                SpeedChange.Value = InitialRate.Value;
                targetRate = InitialRate.Value;
                recentRates.Clear();
                recentRates.AddRange(Enumerable.Repeat(InitialRate.Value, recent_rate_count));
            }
            else
            {
                SpeedChange.Value = InitialRate.Value;
                targetRate = InitialRate.Value;
                recentRates.Clear();
                recentRates.AddRange(Enumerable.Repeat(InitialRate.Value, recent_rate_count));
            }

            rateAdjustHelper.ApplyToTrack(track);
        }

        public void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }

        public void Update(Playfield playfield)
        {
            SpeedChange.Value = Interpolation.DampContinuously(SpeedChange.Value, targetRate, 50, playfield.Clock.ElapsedFrameTime);
        }

        public double ApplyToRate(double time, double rate = 1)
        {
            // 如果设置了FreeBPM且原始BPM可用，使用自由BPM速率而不是初始速率
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && originalBPM > 0)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                return rate * freeRate;
            }

            // 如果设置了FreeBPM但原始BPM尚不可用，我们暂时不应应用任何速率调整
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && originalBPM <= 0)
            {
                return rate; // 返回未修改的速率，直到原始BPM可用
            }

            // 如果启用了动态BPM，我们不乘以初始速率，因为它已经在其他地方考虑过了
            if (EnableDynamicBPM.Value)
            {
                return rate; // 如果动态BPM处于活动状态，则不应用初始速率乘数
            }

            // 否则，像以前一样使用初始速率
            return rate * InitialRate.Value;
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.OnNewResult += (_, result) =>
            {
                if (ratesForRewinding.ContainsKey(result.HitObject)) return;
                if (!shouldProcessResult(result)) return;

                ratesForRewinding.Add(result.HitObject, recentRates[0]);
                recentRates.RemoveAt(0);

                recentRates.Add(Math.Clamp(getRelativeRateChange(result) * SpeedChange.Value, MinAllowableRate.Value, MaxAllowableRate.Value));

                updateTargetRate();
            };
            drawable.OnRevertResult += (_, result) =>
            {
                if (!ratesForRewinding.TryGetValue(result.HitObject, out double rate)) return;
                if (!shouldProcessResult(result)) return;

                recentRates.Insert(0, rate);
                ratesForRewinding.Remove(result.HitObject);

                recentRates.RemoveAt(recentRates.Count - 1);

                updateTargetRate();
            };
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            // 从谱面计算原始BPM
            originalBPM = beatmap.BeatmapInfo.BPM;

            // 如果设置了FreeBPM且我们尚未应用它，现在是时候了
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && !hasAppliedFreeBPM)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                SpeedChange.Value = freeRate;
                targetRate = freeRate;
                hasAppliedFreeBPM = true;

                // 如果启用了动态BPM，用自由速率初始化recentRates
                if (EnableDynamicBPM.Value)
                {
                    recentRates.Clear();
                    recentRates.AddRange(Enumerable.Repeat(freeRate, recent_rate_count));
                }
                else
                {
                    recentRates.Clear();
                    recentRates.AddRange(Enumerable.Repeat(freeRate, recent_rate_count));
                }
            }
            // 如果设置了FreeBPM且已经应用，如果BPM发生变化，我们可能需要更新速率
            else if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && hasAppliedFreeBPM)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                SpeedChange.Value = freeRate;
                targetRate = freeRate;
            }

            // 当加载新谱面时重置失误计数器
            currentMissCount = 0;

            var hitObjects = getAllApplicableHitObjects(beatmap.HitObjects).ToList();
            var endTimes = hitObjects.Select(x => x.GetEndTime()).Order().Distinct().ToList();

            foreach (HitObject hitObject in hitObjects)
            {
                int index = endTimes.BinarySearch(hitObject.GetEndTime());
                if (index < 0) index = ~index; // 如果没有完全匹配，BinarySearch将以按位补码形式返回下一个更大的元素
                index -= 1;

                if (index >= 0)
                    precedingEndTimes.Add(hitObject, endTimes[index]);
            }
        }

        private IEnumerable<HitObject> getAllApplicableHitObjects(IEnumerable<HitObject> hitObjects)
        {
            foreach (var hitObject in hitObjects)
            {
                if (hitObject.HitWindows != HitWindows.Empty)
                    yield return hitObject;

                foreach (HitObject nested in getAllApplicableHitObjects(hitObject.NestedHitObjects))
                    yield return nested;
            }
        }

        private bool shouldProcessResult(JudgementResult result)
        {
            if (!result.Type.AffectsAccuracy()) return false;
            if (!precedingEndTimes.ContainsKey(result.HitObject)) return false;

            // 如果禁用了动态BPM，则不要处理结果以进行速率调整
            if (!EnableDynamicBPM.Value) return false;

            return true;
        }

        private double getRelativeRateChange(JudgementResult result)
        {
            if (!result.IsHit)
            {
                // 当出现失误时增加失误计数器
                currentMissCount++;

                // 如果失误计数达到阈值，返回失误速率变化
                if (currentMissCount >= MissThreshold.Value)
                {
                    return RateChangeOnMiss.Value;
                }

                // 如果失误计数未达到阈值，返回1.0（无速率变化）
                return 1.0;
            }
            else
            {
                // 当命中时重置失误计数器
                currentMissCount = 0;
                // 根据时机计算正常速率变化
                double prevEndTime = precedingEndTimes[result.HitObject];
                return Math.Clamp(
                    (result.HitObject.GetEndTime() - prevEndTime) / (result.TimeAbsolute - prevEndTime),
                    min_rate_change_factor,
                    max_rate_change_factor
                );
            }
        }

        /// <summary>
        /// 基于 <see cref="recentRates"/> 中的值更新 <see cref="targetRate"/>。
        /// </summary>
        private void updateTargetRate()
        {
            // 比较recentRates中的值以查看玩家的速度有多一致
            // 如果玩家一半音符打得太快而另一半太慢：Abs(一致性) = 0
            // 如果玩家所有的音符都打得太快或太慢：Abs(一致性) = recent_rate_count - 1
            int consistency = 0;

            for (int i = 1; i < recentRates.Count; i++)
            {
                consistency += Math.Sign(recentRates[i] - recentRates[i - 1]);
            }

            // 根据一致性缩放速率调整
            targetRate = Interpolation.Lerp(targetRate, recentRates.Average(), Math.Abs(consistency) / (recent_rate_count - 1d));
        }
    }
}
