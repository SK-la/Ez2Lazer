// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    public enum TrueReleaseUnit
    {
        Beats,
        Milliseconds,
    }

    /// <summary>
    /// 点/面互相焊接：消掉面头、把点和面尾拉成新面，短面塌缩回点，所有列对齐到公共结束时间。
    /// </summary>
    public class ManiaModTrueRelease : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "True Inverse";

        public override string Acronym => "TI";

        public override LocalisableString Description => "Weld adjacent notes into holds; collapse short holds.";

        public override IconUsage? Icon => FontAwesome.Solid.ExpandArrowsAlt;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource("Minimum hold length", "Holds shorter than this are treated as notes (unit below)")]
        public BindableDouble MinHoldLength { get; } = new BindableDouble(0.25)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource("Tail gap", "Gap left before the next object when stretching a note into a hold (unit below)")]
        public BindableDouble TailGap { get; } = new BindableDouble(0.5)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource("Minijack", "If a hold-tail-born head is closer than this to the next point-born head, it is dropped instead of becoming a head (unit below)")]
        public BindableDouble Minijack { get; } = new BindableDouble(0.5)
        {
            MinValue = 0,
            MaxValue = 4,
        };

        [SettingSource("Unit", "Time unit used by the parameters above")]
        public Bindable<TrueReleaseUnit> Unit { get; } = new Bindable<TrueReleaseUnit>(TrueReleaseUnit.Beats);

        public ManiaModTrueRelease()
        {
            Unit.BindValueChanged(_ => applyUnitRange(), true);
        }

        private void applyUnitRange()
        {
            bool ms = Unit.Value == TrueReleaseUnit.Milliseconds;
            double beatsPrecision = 1.0 / 12.0;

            applyRange(MinHoldLength, ms, 160, 0.25, beatsPrecision);
            applyRange(TailGap, ms, 160, 0.5, beatsPrecision);
            applyRange(Minijack, ms, 200, 0.5, beatsPrecision);
        }

        private static void applyRange(BindableDouble b, bool ms, double msDefault, double beatsDefault, double beatsPrecision)
        {
            b.MinValue = 0;
            b.MaxValue = ms ? 1000 : 4;
            b.Precision = ms ? 10 : beatsPrecision;
            b.Value = ms ? msDefault : beatsDefault;
        }

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100,
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Min hold", $"{MinHoldLength.Value:0.##} {Unit}");
                yield return ("Tail gap", $"{TailGap.Value:0.##} {Unit}");
                yield return ("Minijack", $"{Minijack.Value:0.##} {Unit}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        private double resolveMs(BindableDouble v, double atTime, IBeatmap beatmap)
            => Unit.Value == TrueReleaseUnit.Milliseconds
                ? v.Value
                : v.Value * beatmap.ControlPointInfo.TimingPointAt(atTime).BeatLength;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var mania = (ManiaBeatmap)beatmap;
            mania.HitObjects = TrueReleaseProcessor.Process(
                mania.HitObjects,
                t => resolveMs(MinHoldLength, t, beatmap),
                t => resolveMs(TailGap, t, beatmap),
                t => resolveMs(Minijack, t, beatmap)).OrderBy(h => h.StartTime).ToList();
        }
    }
}
