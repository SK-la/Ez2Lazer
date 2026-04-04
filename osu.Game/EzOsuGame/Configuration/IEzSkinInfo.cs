// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Configuration
{
    // string类不建议在此绑定
    public interface IEzSkinInfo
    {
        IBindable<double> ColumnWidth { get; }
        IBindable<double> SpecialFactor { get; }

        IBindable<double> HitPosition { get; }

        IBindable<double> NoteHeightScaleToWidth { get; }
        IBindable<double> NoteTrackLineHeight { get; }

        IBindable<double> HitTargetFloatFixed { get; }
        IBindable<double> HitTargetAlpha { get; }

        IBindable<double> HoldTailAlpha { get; }
        IBindable<double> HoldTailMaskHeight { get; }

        IBindable<bool> ColorSettingsEnabled { get; }
    }

    public class EzSkinInfo : IEzSkinInfo
    {
        public readonly Bindable<string> NoteSetNameBindable = new Bindable<string>();
        public readonly Bindable<string> StageNameBindable = new Bindable<string>();

        public readonly Bindable<double> ColumnWidthBindable = new Bindable<double>();
        public readonly Bindable<double> SpecialFactorBindable = new Bindable<double>();

        public readonly Bindable<double> HitPositionBindable = new Bindable<double>();

        public readonly Bindable<double> NoteHeightScaleToWidthBindable = new Bindable<double>();
        public readonly Bindable<double> NoteTrackLineHeightBindable = new Bindable<double>();

        public readonly Bindable<double> HitTargetFloatFixedBindable = new Bindable<double>();
        public readonly Bindable<double> HitTargetAlphaBindable = new Bindable<double>();

        public readonly Bindable<double> HoldTailAlphaBindable = new Bindable<double>();
        public readonly Bindable<double> HoldTailMaskHeightBindable = new Bindable<double>();

        public readonly Bindable<bool> ColorSettingsEnabledBindable = new Bindable<bool>();

        public void BindTo(Ez2ConfigManager config)
        {
            NoteSetNameBindable.BindTo(config.GetBindable<string>(Ez2Setting.NoteSetName));
            StageNameBindable.BindTo(config.GetBindable<string>(Ez2Setting.StageName));

            ColumnWidthBindable.BindTo(config.GetBindable<double>(Ez2Setting.ColumnWidth));
            SpecialFactorBindable.BindTo(config.GetBindable<double>(Ez2Setting.SpecialFactor));

            HitPositionBindable.BindTo(config.GetBindable<double>(Ez2Setting.HitPosition));

            NoteHeightScaleToWidthBindable.BindTo(config.GetBindable<double>(Ez2Setting.NoteHeightScaleToWidth));
            NoteTrackLineHeightBindable.BindTo(config.GetBindable<double>(Ez2Setting.NoteTrackLineHeight));

            HitTargetFloatFixedBindable.BindTo(config.GetBindable<double>(Ez2Setting.HitTargetFloatFixed));
            HitTargetAlphaBindable.BindTo(config.GetBindable<double>(Ez2Setting.HitTargetAlpha));

            HoldTailAlphaBindable.BindTo(config.GetBindable<double>(Ez2Setting.ManiaHoldTailAlpha));
            HoldTailMaskHeightBindable.BindTo(config.GetBindable<double>(Ez2Setting.ManiaHoldTailMaskGradientHeight));

            ColorSettingsEnabledBindable.BindTo(config.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled));
        }

        public EzSkinInfo()
        {
        }

        public EzSkinInfo(Ez2ConfigManager config)
        {
            BindTo(config);
        }

        IBindable<double> IEzSkinInfo.ColumnWidth => ColumnWidthBindable;
        IBindable<double> IEzSkinInfo.SpecialFactor => SpecialFactorBindable;

        IBindable<double> IEzSkinInfo.HitPosition => HitPositionBindable;

        IBindable<double> IEzSkinInfo.NoteHeightScaleToWidth => NoteHeightScaleToWidthBindable;
        IBindable<double> IEzSkinInfo.NoteTrackLineHeight => NoteTrackLineHeightBindable;

        IBindable<double> IEzSkinInfo.HitTargetFloatFixed => HitTargetFloatFixedBindable;
        IBindable<double> IEzSkinInfo.HitTargetAlpha => HitTargetAlphaBindable;

        IBindable<double> IEzSkinInfo.HoldTailAlpha => HoldTailAlphaBindable;
        IBindable<double> IEzSkinInfo.HoldTailMaskHeight => HoldTailMaskHeightBindable;

        IBindable<bool> IEzSkinInfo.ColorSettingsEnabled => ColorSettingsEnabledBindable;
    }
}
