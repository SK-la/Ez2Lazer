// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.EzOsuGame.Mods.LAsMods
{
    public partial class ModNiceBPM : ModDynamicSpeedAdjust, IApplicableToDrawableHitObject, IApplicableToBeatmap, IUpdatableByPlayfield
    {
        public override string Name => "Nice BPM";

        public override string Acronym => "NB";

        public override LocalisableString Description => NiceBPMStrings.NICE_BPM_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.TachometerAlt;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.FREE_BPM_LABEL), nameof(NiceBPMStrings.FREE_BPM_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> FreeBPM { get; } = new Bindable<int?>();

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.INITIAL_RATE_LABEL), nameof(NiceBPMStrings.INITIAL_RATE_DESCRIPTION), SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> InitialRate { get; } = new BindableDouble(1)
        {
            MinValue = 0.2,
            MaxValue = 2,
            Precision = 0.01
        };

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.ENABLE_DYNAMIC_BPM_LABEL), nameof(NiceBPMStrings.ENABLE_DYNAMIC_BPM_DESCRIPTION), SettingControlType = typeof(SettingsCheckbox))]
        public BindableBool EnableDynamicBPM { get; } = new BindableBool(false);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.ADJUST_PITCH_LABEL), nameof(EzCommonModStrings.ADJUST_PITCH_DESCRIPTION))]
        public BindableBool AdjustPitch { get; } = new BindableBool(false);

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.MIN_ALLOWABLE_RATE_LABEL), nameof(NiceBPMStrings.MIN_ALLOWABLE_RATE_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MinAllowableRate { get; } = new BindableDouble(0.7)
        {
            MinValue = 0.1,
            MaxValue = 1.5,
            Precision = 0.01
        };

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.MAX_ALLOWABLE_RATE_LABEL), nameof(NiceBPMStrings.MAX_ALLOWABLE_RATE_DESCRIPTION),
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MaxAllowableRate { get; } = new BindableDouble(1.2)
        {
            MinValue = 0.5,
            MaxValue = 2.0,
            Precision = 0.01
        };

        [SettingSource(typeof(NiceBPMStrings), nameof(NiceBPMStrings.MISS_COUNT_THRESHOLD_LABEL), nameof(NiceBPMStrings.MISS_COUNT_THRESHOLD_DESCRIPTION),
            SettingControlType = typeof(SettingsSlider<int>))]
        public BindableInt MissThreshold { get; } = new BindableInt(3)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        public override BindableNumber<double> SpeedChange { get; } = new BindableDouble(1)
        {
            MinValue = 0.1,
            MaxValue = 3.0,
            Precision = 0.01,
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

        private double originalBPM;
        private bool hasAppliedFreeBPM;
        private int currentMissCount;

        // 防止 Min/Max AllowableRate 相互调整时的循环触发
        private bool isUpdatingMinMax;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (NiceBPMStrings.FREE_BPM_LABEL, FreeBPM.Value.HasValue ? FreeBPM.Value.Value.ToString() : "Auto");
                yield return (NiceBPMStrings.INITIAL_RATE_LABEL, $"{InitialRate.Value:N2}");
                yield return (NiceBPMStrings.ENABLE_DYNAMIC_BPM_LABEL, EnableDynamicBPM.Value ? "On" : "Off");
                yield return (EzCommonModStrings.ADJUST_PITCH_LABEL, AdjustPitch.Value ? "On" : "Off");
                yield return (NiceBPMStrings.MIN_ALLOWABLE_RATE_LABEL, $"{MinAllowableRate.Value:N2}");
                yield return (NiceBPMStrings.MAX_ALLOWABLE_RATE_LABEL, $"{MaxAllowableRate.Value:N2}");
                yield return (NiceBPMStrings.MISS_COUNT_THRESHOLD_LABEL, $"{MissThreshold.Value}");
                yield return (DynamicSpeedAdjustStrings.RATE_CHANGE_STEP_LABEL, $"{RateChangeStep.Value:N3}");
            }
        }

        public ModNiceBPM()
        {
            InitialiseDynamicSpeedAdjust(AdjustPitch);

            // 当最小/最大允许速率值更改时更新速度变化范围
            MinAllowableRate.BindValueChanged(val =>
            {
                if (isUpdatingMinMax) return;

                // 如果新最小值大于当前最大值，需要先调整最大值
                if (val.NewValue > SpeedChange.MaxValue)
                {
                    isUpdatingMinMax = true;
                    SpeedChange.MaxValue = val.NewValue;
                    MaxAllowableRate.Value = val.NewValue;
                    isUpdatingMinMax = false;
                }

                SpeedChange.MinValue = val.NewValue;
                if (GameplaySpeed.Value < val.NewValue)
                    SetGameplayAndDisplaySpeed(val.NewValue);

                // 确保最小允许速率不超过最大允许速率
                if (val.NewValue > MaxAllowableRate.Value)
                {
                    isUpdatingMinMax = true;
                    MinAllowableRate.Value = MaxAllowableRate.Value;
                    isUpdatingMinMax = false;
                }
            }, true);

            MaxAllowableRate.BindValueChanged(val =>
            {
                if (isUpdatingMinMax) return;

                // 如果新最大值小于当前最小值，需要先调整最小值
                if (val.NewValue < SpeedChange.MinValue)
                {
                    isUpdatingMinMax = true;
                    SpeedChange.MinValue = val.NewValue;
                    MinAllowableRate.Value = val.NewValue;
                    isUpdatingMinMax = false;
                }

                SpeedChange.MaxValue = val.NewValue;
                if (GameplaySpeed.Value > val.NewValue)
                    SetGameplayAndDisplaySpeed(val.NewValue);

                // 确保最大允许速率不低于最小允许速率
                if (val.NewValue < MinAllowableRate.Value)
                {
                    isUpdatingMinMax = true;
                    MaxAllowableRate.Value = MinAllowableRate.Value;
                    isUpdatingMinMax = false;
                }
            }, true);

            InitialRate.BindValueChanged(val =>
            {
                // 仅在未设置FreeBPM时应用初始速率
                if (!FreeBPM.Value.HasValue)
                {
                    SetGameplayAndDisplaySpeed(val.NewValue);
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
                        SetGameplayAndDisplaySpeed(freeRate);
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
                        SetGameplayAndDisplaySpeed(InitialRate.Value);
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
                            SetGameplayAndDisplaySpeed(freeRate);
                            targetRate = freeRate;
                        }
                    }
                    else
                    {
                        SetGameplayAndDisplaySpeed(InitialRate.Value);
                        targetRate = InitialRate.Value;
                    }

                    currentMissCount = 0; // 当动态BPM被禁用时重置失误计数
                }
            }, true);
        }

        public override void ApplyToTrack(IAdjustableAudioComponent track)
        {
            // 检查是否设置了FreeBPM且原始BPM可用
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && originalBPM > 0)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                SetGameplayAndDisplaySpeed(freeRate);
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
                // 如果设置了FreeBPM但原始BPM尚不可用，则推迟应用（保持 1.0x，与基类 ApplyToRate 一致）
                SetGameplayAndDisplaySpeed(1);
                targetRate = 1;
                recentRates.Clear();
                recentRates.AddRange(Enumerable.Repeat(1d, recent_rate_count));
            }
            else
            {
                SetGameplayAndDisplaySpeed(InitialRate.Value);
                targetRate = InitialRate.Value;
                recentRates.Clear();
                recentRates.AddRange(Enumerable.Repeat(InitialRate.Value, recent_rate_count));
            }

            RateAdjustHelper.ApplyToTrack(track);
        }

        public void Update(Playfield playfield)
        {
            DampGameplaySpeedTowards(targetRate, playfield.Clock.ElapsedFrameTime);
        }

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.OnNewResult += (_, result) =>
            {
                if (ratesForRewinding.ContainsKey(result.HitObject)) return;
                if (!shouldProcessResult(result)) return;

                ratesForRewinding.Add(result.HitObject, recentRates[0]);
                recentRates.RemoveAt(0);

                recentRates.Add(Math.Clamp(getRelativeRateChange(result) * GameplaySpeed.Value, MinAllowableRate.Value, MaxAllowableRate.Value));

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
            precedingEndTimes.Clear();
            ratesForRewinding.Clear();

            // 从谱面计算原始BPM
            originalBPM = beatmap.BeatmapInfo.BPM;

            // 如果设置了FreeBPM且我们尚未应用它，现在是时候了
            if (FreeBPM.Value.HasValue && FreeBPM.Value > 0 && !hasAppliedFreeBPM)
            {
                double freeRate = FreeBPM.Value.Value / originalBPM;
                SetGameplayAndDisplaySpeed(freeRate);
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
                SetGameplayAndDisplaySpeed(freeRate);
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

            // 只有超过Good的判定偏移才参与统计，Great和Perfect不影响统计
            // Perfect和Great的精确度太高，不需要进行速度调整
            if (result.Type == HitResult.Perfect || result.Type == HitResult.Great)
                return false;

            return true;
        }

        private double getRelativeRateChange(JudgementResult result)
        {
            if (!result.IsHit)
            {
                // 当出现失误时增加失误计数器
                currentMissCount++;

                // 累积达到阈值时降速一次，然后重新计数
                if (currentMissCount >= MissThreshold.Value)
                {
                    currentMissCount = 0;
                    return MissRateChangeFactor;
                }

                // 未达阈值时不改变速度
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
                    MinRateChangeFactor,
                    MaxRateChangeFactor
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

            // 根据一致性缩放速率调整；稳定同向判定时 consistency 为 0，仍需最小响应
            const double min_target_rate_lerp = 0.25;
            double lerpFactor = Math.Abs(consistency) / (recent_rate_count - 1d);
            lerpFactor = Math.Max(min_target_rate_lerp, lerpFactor);
            targetRate = Interpolation.Lerp(targetRate, recentRates.Average(), lerpFactor);
        }
    }

    public static class NiceBPMStrings
    {
        public static readonly LocalisableString NICE_BPM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("自由调整BPM或速度", "Free BPM or Speed");
        public static readonly LocalisableString INITIAL_RATE_LABEL = new EzLocalizationManager.EzLocalisableString("初始速度倍率", "Initial rate");
        public static readonly LocalisableString INITIAL_RATE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("调整初始播放速度倍率", "Initial rate. The starting speed of the track");
        public static readonly LocalisableString FREE_BPM_LABEL = new EzLocalizationManager.EzLocalisableString("初始BPM", "Initial BPM");
        public static readonly LocalisableString FREE_BPM_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("设置BPM值以调整初始播放速度", "BPM to speed");
        public static readonly LocalisableString ENABLE_DYNAMIC_BPM_LABEL = new EzLocalizationManager.EzLocalisableString("启用动态BPM", "Enable Dynamic BPM");

        public static readonly LocalisableString ENABLE_DYNAMIC_BPM_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("基于判定表现进行动态BPM调整，过滤P判", "Enable dynamic BPM adjustment based on performance");

        public static readonly LocalisableString MIN_ALLOWABLE_RATE_LABEL = new EzLocalizationManager.EzLocalisableString("最小允许速率", "Min Allowable Rate");
        public static readonly LocalisableString MIN_ALLOWABLE_RATE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动态BPM调整的最小速率", "Minimum rate for dynamic BPM adjustment");
        public static readonly LocalisableString MAX_ALLOWABLE_RATE_LABEL = new EzLocalizationManager.EzLocalisableString("最大允许速率", "Max Allowable Rate");
        public static readonly LocalisableString MAX_ALLOWABLE_RATE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("动态BPM调整的最大速率", "Maximum rate for dynamic BPM adjustment");
        public static readonly LocalisableString MISS_COUNT_THRESHOLD_LABEL = new EzLocalizationManager.EzLocalisableString("Miss计数阈值", "Miss Count Threshold");

        public static readonly LocalisableString MISS_COUNT_THRESHOLD_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("每累积该数量的 Miss 降速一次，之后重新计数", "Decrease speed once per this many accumulated misses, then count again");
    }
}
