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

        public override string Acronym => "NCl";

        public override LocalisableString Description => NoteCleanupStrings.NOTE_CLEANUP_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Eraser;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.LN_BODY_MODE_LABEL), nameof(NoteCleanupStrings.LN_BODY_MODE_DESCRIPTION))]
        public Bindable<LnBodyTapMode> BodyTapMode { get; } = new Bindable<LnBodyTapMode>
        {
            Default = LnBodyTapMode.Continue,
            Value = LnBodyTapMode.Continue,
        };

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.ENFORCE_MIN_GAPS_LABEL), nameof(NoteCleanupStrings.ENFORCE_MIN_GAPS_DESCRIPTION))]
        public BindableBool EnforceMinGaps { get; } = new BindableBool(true);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.MINIMUM_GAP_MS_LABEL), nameof(NoteCleanupStrings.MINIMUM_GAP_MS_DESCRIPTION))]
        public BindableNumber<int> MinimumGapMs { get; } = new BindableInt(16)
        {
            MinValue = 1,
            MaxValue = 125,
            Precision = 1,
        };

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.USE_BEAT_GAP_LABEL), nameof(NoteCleanupStrings.USE_BEAT_GAP_DESCRIPTION))]
        public BindableBool UseBeatGap { get; } = new BindableBool(false);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.BEAT_DIVISOR_LABEL), nameof(NoteCleanupStrings.BEAT_DIVISOR_DESCRIPTION))]
        public BindableNumber<int> BeatDivisor { get; } = new BindableInt(8)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.LN_DENSITY_LABEL), nameof(NoteCleanupStrings.LN_DENSITY_DESCRIPTION))]
        public BindableBool EnforceLnDensity { get; } = new BindableBool(true);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.USE_KEEP_STRATEGY_LABEL), nameof(NoteCleanupStrings.USE_KEEP_STRATEGY_DESCRIPTION))]
        public BindableBool UseKeepStrategy { get; } = new BindableBool(false);

        [SettingSource(typeof(NoteCleanupStrings), nameof(NoteCleanupStrings.KEEP_STRATEGY_LABEL), nameof(NoteCleanupStrings.KEEP_STRATEGY_DESCRIPTION))]
        public BindableNumber<int> KeepStrategy { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 2,
            Precision = 1,
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(1000)
        {
            MinValue = 0,
            MaxValue = 1000,
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (NoteCleanupStrings.LN_BODY_MODE_LABEL, BodyTapMode.Value.ToString());
                if (EnforceMinGaps.Value) yield return (NoteCleanupStrings.ENFORCE_MIN_GAPS_LABEL, "On");
                if (EnforceLnDensity.Value) yield return (NoteCleanupStrings.LN_DENSITY_LABEL, "On");
                if (UseBeatGap.Value) yield return (NoteCleanupStrings.BEAT_DIVISOR_LABEL, $"1/{BeatDivisor.Value}");
                if (UseKeepStrategy.Value) yield return (NoteCleanupStrings.KEEP_STRATEGY_LABEL, $"{KeepStrategy.Value}");

                yield return (NoteCleanupStrings.MINIMUM_GAP_MS_LABEL, $"{MinimumGapMs.Value}ms");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var options = new NoteCleanupOptions
            {
                LnBodyTapMode = BodyTapMode.Value,
                CleanDenseNotes = EnforceMinGaps.Value,
                CleanLnDensity = EnforceLnDensity.Value,
                BeatDivisor = UseBeatGap.Value ? BeatDivisor.Value : null,
                MinimumGapMs = MinimumGapMs.Value,
                UseKeepStrategy = UseKeepStrategy.Value,
                KeepStrategy = (NoteCleanupKeepStrategy)KeepStrategy.Value,
            };

            ManiaNoteCleanupTool.CleanupBeatmap((ManiaBeatmap)beatmap, options);
        }
    }

    public static class NoteCleanupStrings
    {
        public static readonly LocalisableString NOTE_CLEANUP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "删除重叠单点，按所选方式处理 LN 体内单点，并清理过密音符。",
            "Remove overlapping taps, handle taps inside LNs, and thin overly dense notes.");

        public static readonly LocalisableString LN_BODY_MODE_LABEL = new EzLocalizationManager.EzLocalisableString("LN 体内单点", "Tap Inside LN");

        public static readonly LocalisableString LN_BODY_MODE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "单点落在 LN 体内时：删单点、只截断 LN，或截断并把单点接成后半段 LN。",
            "When a tap sits inside an LN: drop the tap, truncate the LN, or truncate and continue the LN from the tap.");

        public static readonly LocalisableString LN_BODY_DROP_TAP = new EzLocalizationManager.EzLocalisableString("删单点", "Drop Tap");

        public static readonly LocalisableString LN_BODY_TRUNCATE = new EzLocalizationManager.EzLocalisableString("截断 LN", "Truncate LN");

        public static readonly LocalisableString LN_BODY_CONTINUE = new EzLocalizationManager.EzLocalisableString("截断并延续", "Truncate and Continue");

        public static readonly LocalisableString ENFORCE_MIN_GAPS_LABEL = new EzLocalizationManager.EzLocalisableString("Note密度处理", "Note Density");

        public static readonly LocalisableString ENFORCE_MIN_GAPS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "过密单点向后收到第一颗非密集 note，删掉它前面的第 2、4、6 颗。间隙用下面的最小毫秒或节拍。",
            "In a dense tap run, drop the 2nd, 4th and 6th notes before the closing note. Uses the gap below.");

        public static readonly LocalisableString LN_DENSITY_LABEL = new EzLocalizationManager.EzLocalisableString("LongNote密度处理", "Long Note Density");

        public static readonly LocalisableString LN_DENSITY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "LN 与相邻 note 过近时，按上面的同一间隙收短尾巴，或把 LN 头后移。不把后一颗卷进删除。",
            "When an LN is too close to a neighbour, open the same gap by trimming the tail or delaying the head. The neighbour is kept.");

        public static readonly LocalisableString BEAT_DIVISOR_LABEL = new EzLocalizationManager.EzLocalisableString("节拍分割", "Beat Divisor");

        public static readonly LocalisableString USE_BEAT_GAP_LABEL = new EzLocalizationManager.EzLocalisableString("用节拍间隙", "Use Beat Gap");

        public static readonly LocalisableString USE_BEAT_GAP_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "打开后，间隙改为当前音符时刻的 BeatLength / 节拍分割，不再用最小毫秒。",
            "When on, the gap is BeatLength at the note divided by the beat divisor, instead of the millisecond value.");

        public static readonly LocalisableString BEAT_DIVISOR_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "只在「用节拍间隙」打开时生效。如 8 表示 1/8 beat。",
            "Used only when beat gap is on. 8 means 1/8 beat.");

        public static readonly LocalisableString MINIMUM_GAP_MS_LABEL = new EzLocalizationManager.EzLocalisableString("最小毫秒", "Minimum Gap Ms");

        public static readonly LocalisableString MINIMUM_GAP_MS_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "Note 密度和 LongNote 密度共用的固定间隙。默认 16ms。节拍间隙打开时不使用。",
            "Shared gap for note density and long note density. Defaults to 16ms. Unused while beat gap is on.");

        public static readonly LocalisableString USE_KEEP_STRATEGY_LABEL = new EzLocalizationManager.EzLocalisableString("使用保留策略", "Use Keep Strategy");

        public static readonly LocalisableString USE_KEEP_STRATEGY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "打开后，同一时刻的重叠单点按保留策略留旧或留新。默认关掉，总是留较早的那颗。",
            "When on, same-time overlapping taps follow the keep strategy. Off keeps the earlier tap.");

        public static readonly LocalisableString KEEP_STRATEGY_LABEL = new EzLocalizationManager.EzLocalisableString("保留策略", "Keep Strategy");

        public static readonly LocalisableString KEEP_STRATEGY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "同一时刻的重叠单点保留较旧(1)或较新(2)。只在「使用保留策略」打开时生效。",
            "For same-time overlapping taps, keep the older (1) or newer (2). Used only when keep strategy is on.");
    }
}
