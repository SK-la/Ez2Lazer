// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModNoteCleanup : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "Note Cleanup";

        public override string Acronym => "NC";

        public override LocalisableString Description => NoteCleanupStrings.NOTE_CLEANUP_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Eraser;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.CLEAN_OVERLAP_LABEL), nameof(NoteCleanupStrings.CLEAN_OVERLAP_DESCRIPTION))]
        public BindableBool CleanOverlap { get; } = new BindableBool(true);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.ENFORCE_MIN_GAPS_LABEL), nameof(NoteCleanupStrings.ENFORCE_MIN_GAPS_DESCRIPTION))]
        public BindableBool EnforceMinGaps { get; } = new BindableBool(true);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.ENFORCE_LN_GAP_LABEL), nameof(NoteCleanupStrings.ENFORCE_LN_GAP_DESCRIPTION))]
        public BindableBool EnforceLNGap { get; } = new BindableBool(true);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.BEAT_DIVISOR_LABEL), nameof(NoteCleanupStrings.BEAT_DIVISOR_DESCRIPTION))]
        public BindableNumber<int> BeatDivisor { get; } = new BindableInt(8)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.MINIMUM_GAP_MS_LABEL), nameof(NoteCleanupStrings.MINIMUM_GAP_MS_DESCRIPTION))]
        public BindableNumber<int> MinimumGapMs { get; } = new BindableInt(30)
        {
            MinValue = 1,
            MaxValue = 125,
            Precision = 1,
        };

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.KEEP_STRATEGY_LABEL), nameof(NoteCleanupStrings.KEEP_STRATEGY_DESCRIPTION))]
        public BindableNumber<int> KeepStrategy { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 2,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(90)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (CleanOverlap.Value) yield return (NoteCleanupStrings.CLEAN_OVERLAP_LABEL, "On");
                if (EnforceMinGaps.Value) yield return (NoteCleanupStrings.ENFORCE_MIN_GAPS_LABEL, "On");
                if (EnforceLNGap.Value) yield return (NoteCleanupStrings.ENFORCE_LN_GAP_LABEL, "On");

                yield return (NoteCleanupStrings.BEAT_DIVISOR_LABEL, $"1/{BeatDivisor.Value}");
                yield return (NoteCleanupStrings.MINIMUM_GAP_MS_LABEL, $"{MinimumGapMs.Value}ms");
                yield return (NoteCleanupStrings.KEEP_STRATEGY_LABEL, $"{KeepStrategy.Value}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var options = new NoteCleanupOptions
            {
                CleanOverlap = CleanOverlap.Value,
                EnforceMinimumGaps = EnforceMinGaps.Value,
                EnforceHoldReleaseGap = EnforceLNGap.Value,
                BeatDivisor = BeatDivisor.Value,
                MinimumGapMs = MinimumGapMs.Value,
                KeepStrategy = (NoteCleanupKeepStrategy)KeepStrategy.Value,
            };

            ManiaNoteCleanupTool.CleanupBeatmap((ManiaBeatmap)beatmap, options);
        }
    }

    public static class NoteCleanupStrings
    {
        public static readonly LocalisableString NOTE_CLEANUP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "清理谱面中的重叠、过密音符与 LN 尾缝问题。",
            "Clean overlapping, overly dense notes and LN release gaps on the beatmap.");

        public static readonly LocalisableString CLEAN_OVERLAP_LABEL = new EzLocalizationManager.EzLocalisableString("去除重叠", "Clean Overlap");

        public static readonly LocalisableString CLEAN_OVERLAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "删除同列中与前一音符时间重叠的 note/LN。",
            "Remove notes/LNs that overlap the previous object on the same column.");

        public static readonly LocalisableString ENFORCE_MIN_GAPS_LABEL = new EzLocalizationManager.EzLocalisableString("去除过密", "Enforce Min Gaps");

        public static readonly LocalisableString ENFORCE_MIN_GAPS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "剔除间距小于 max(最小毫秒, beatLength/节拍分割) 的音符。",
            "Remove notes closer than max(minimum ms, beatLength/beat divisor).");

        public static readonly LocalisableString ENFORCE_LN_GAP_LABEL = new EzLocalizationManager.EzLocalisableString("LN 尾缝", "Enforce LN Gap");

        public static readonly LocalisableString ENFORCE_LN_GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "截断 LN 尾端以留出最小间距；过短 LN 转为单点 note。",
            "Trim LN ends to preserve minimum gap; convert too-short LNs to taps.");

        public static readonly LocalisableString BEAT_DIVISOR_LABEL = new EzLocalizationManager.EzLocalisableString("节拍分割", "Beat Divisor");

        public static readonly LocalisableString BEAT_DIVISOR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "最小间距的节拍分母（如 8 表示 1/8 beat）。",
            "Beat fraction denominator for minimum gap (e.g. 8 means 1/8 beat).");

        public static readonly LocalisableString MINIMUM_GAP_MS_LABEL = new EzLocalizationManager.EzLocalisableString("最小毫秒", "Minimum Gap Ms");

        public static readonly LocalisableString MINIMUM_GAP_MS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "与节拍间距取较大值作为最终最小间距。",
            "Fixed millisecond floor; the larger of this and beat-based gap is used.");

        public static readonly LocalisableString KEEP_STRATEGY_LABEL = new EzLocalizationManager.EzLocalisableString("保留策略", "Keep Strategy");

        public static readonly LocalisableString KEEP_STRATEGY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "过密冲突时保留较旧(1)或较新(2)的音符。",
            "When notes are too close, keep the older (1) or newer (2) note.");
    }
}
