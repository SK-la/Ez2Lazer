// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.CommunityMod
{
    /// <summary>
    /// Projects the mania playfield into a receding track (Malody-style track angle / distance compensation).
    /// Judgement, input and skin resolution are untouched; only the rendered playfield is warped.
    /// </summary>
    /// <remarks>
    /// 自带透视容器，因此与皮肤级 <c>Ez2Setting.ManiaPseudo3DRotation</c> 互斥：后者已生效时本 Mod 不做任何事。
    /// </remarks>
    public partial class ManiaModTiltedTrack : Mod, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Tilted Track";

        public override string Acronym => "TT";

        public override LocalisableString Description => TiltedTrackStrings.DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.CompressArrowsAlt;

        public override ModType Type => ModType.CommunityMod;

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(TiltedTrackStrings), nameof(TiltedTrackStrings.TRACK_ANGLE_LABEL), nameof(TiltedTrackStrings.TRACK_ANGLE_DESCRIPTION))]
        public BindableFloat TrackAngle { get; } = new BindableFloat(30f)
        {
            MinValue = 0,
            MaxValue = 90,
            Precision = 1,
        };

        [SettingSource(typeof(TiltedTrackStrings), nameof(TiltedTrackStrings.COMPENSATION_LABEL), nameof(TiltedTrackStrings.COMPENSATION_DESCRIPTION))]
        public BindableFloat CompensationDistance { get; } = new BindableFloat(0.2f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (TiltedTrackStrings.TRACK_ANGLE_LABEL, $"{TrackAngle.Value:0}");
                yield return (TiltedTrackStrings.COMPENSATION_LABEL, $"{CompensationDistance.Value:0.00}");
            }
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (TrackAngle.Value <= 0)
                return;

            if (GlobalConfigStore.EzConfig.Get<double>(Ez2Setting.ManiaPseudo3DRotation) > 0)
                return;

            var playfield = drawableRuleset.Playfield;

            if (playfield.Parent is not Container parent)
                return;

            parent.Remove(playfield, false);

            var tilt = new TiltBufferContainer { Child = playfield };
            tilt.ApplyTilt(TrackAngle.Value, CompensationDistance.Value);

            parent.Add(tilt);
        }
    }

    public static class TiltedTrackStrings
    {
        public static readonly LocalisableString DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "面板向后收窄的斜轨（Malody 轨道倾斜）。", "The track recedes towards the distance.");

        public static readonly LocalisableString TRACK_ANGLE_LABEL = new EzLocalizationManager.EzLocalisableString(
            "轨道倾斜角", "Track angle");

        public static readonly LocalisableString TRACK_ANGLE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "倾斜程度，0 关闭效果。", "How far the track recedes. 0 disables the effect.");

        public static readonly LocalisableString COMPENSATION_LABEL = new EzLocalizationManager.EzLocalisableString(
            "距离补偿", "Distance compensation");

        public static readonly LocalisableString COMPENSATION_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "削弱远端的间距压缩，保留形状收窄。", "Reduces the spacing compression at the far end.");
    }
}
