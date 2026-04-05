// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using JetBrains.Annotations;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace osu.Game.EzOsuGame.Configuration
{
    public interface IEzSkinInfo
    {
        Bindable<string> NoteSetName { get; }
        Bindable<double> ColumnWidth { get; }
        Bindable<double> SpecialFactor { get; }

        Bindable<double> HitPosition { get; }

        Bindable<double> NoteHeightScaleToWidth { get; }
        Bindable<double> NoteTrackLineHeight { get; }

        Bindable<double> HitTargetFloatFixed { get; }
        Bindable<double> HitTargetAlpha { get; }

        Bindable<double> HoldTailAlpha { get; }
        Bindable<double> HoldTailMaskHeight { get; }

        Bindable<bool> ColorSettingsEnabled { get; }
    }

    // public class EzProNoteInfo : IEzSkinInfo
    // {
    //     public IBindable<double> ColumnWidth { get; } = new Bindable<double>();
    //     public IBindable<double> SpecialFactor { get; }  = new Bindable<double>();
    //     public IBindable<double> NoteHeightScaleToWidth { get; }   = new Bindable<double>();
    //
    //     public IBindable<double> HitPosition { get; }   = new Bindable<double>();
    //
    //     public IBindable<double> NoteTrackLineHeight { get; } = new Bindable<double>();
    //
    //     public IBindable<double> HitTargetFloatFixed { get; } = new Bindable<double>();
    //     public IBindable<double> HitTargetAlpha { get; } = new Bindable<double>();
    //
    //     public IBindable<double> HoldTailAlpha { get; } = new Bindable<double>();
    //     public IBindable<double> HoldTailMaskHeight { get; } = new Bindable<double>();
    //
    //     public IBindable<bool> ColorSettingsEnabled { get; } = new Bindable<bool>();
    // }

    public class EzSkinInfo : IEzSkinInfo
    {
        public Bindable<string> NoteSetName { get; } = new Bindable<string>();
        public Bindable<double> ColumnWidth { get; } = new Bindable<double>();
        public Bindable<double> SpecialFactor { get; }  = new Bindable<double>();
        public Bindable<double> NoteHeightScaleToWidth { get; } = new Bindable<double>();

        public Bindable<double> HitPosition { get; } = new Bindable<double>();

        public Bindable<double> NoteTrackLineHeight { get; } = new Bindable<double>();

        public Bindable<double> HitTargetFloatFixed { get; } = new Bindable<double>();
        public Bindable<double> HitTargetAlpha { get; } = new Bindable<double>();

        public Bindable<double> HoldTailAlpha { get; } = new Bindable<double>();
        public Bindable<double> HoldTailMaskHeight { get; } = new Bindable<double>();

        public Bindable<bool> ColorSettingsEnabled { get; } = new Bindable<bool>();

        public void BindWith(Ez2ConfigManager ezConfig)
        {
            ezConfig.BindWith(Ez2Setting.NoteSetName, NoteSetName);
            ezConfig.BindWith(Ez2Setting.ColumnWidth, ColumnWidth);
            ezConfig.BindWith(Ez2Setting.SpecialFactor, SpecialFactor);
            ezConfig.BindWith(Ez2Setting.NoteHeightScaleToWidth, NoteHeightScaleToWidth);
            ezConfig.BindWith(Ez2Setting.HitPosition, HitPosition);
            ezConfig.BindWith(Ez2Setting.NoteTrackLineHeight, NoteTrackLineHeight);
            ezConfig.BindWith(Ez2Setting.HitTargetFloatFixed, HitTargetFloatFixed);
            ezConfig.BindWith(Ez2Setting.HitTargetAlpha, HitTargetAlpha);
            ezConfig.BindWith(Ez2Setting.ManiaHoldTailAlpha, HoldTailAlpha);
            ezConfig.BindWith(Ez2Setting.ManiaHoldTailMaskGradientHeight, HoldTailMaskHeight);
            ezConfig.BindWith(Ez2Setting.ColorSettingsEnabled, ColorSettingsEnabled);
        }

        [UsedImplicitly]
        public EzSkinInfo()
        {
        }

        public EzSkinInfo(Ez2ConfigManager config)
        {
            BindWith(config);
        }
    }

    public class EzSkinColour
    {
        public event Action? OnNoteColourChanged;

        public EzSkinColour(Ez2ConfigManager ezConfig)
        {
            var colorSettingsEnabled1 = ezConfig.GetBindable<bool>(Ez2Setting.ColorSettingsEnabled);
            var columnTypeA = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeA);
            var columnTypeB = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeB);
            var columnTypeS = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeS);
            var columnTypeE = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeE);
            var columnTypeP = ezConfig.GetBindable<Colour4>(Ez2Setting.ColumnTypeP);
            var columnTypeLists = new IBindable<string>[]
            {
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf4K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf5K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf6K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf7K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf8K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf9K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf10K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf12K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf14K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf16K),
                ezConfig.GetBindable<string>(Ez2Setting.ColumnTypeOf18K),
            };

            colorSettingsEnabled1.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeA.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeB.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeS.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeE.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
            columnTypeP.BindValueChanged(_ => OnNoteColourChanged?.Invoke());

            foreach (var columnTypeList in columnTypeLists)
                columnTypeList.BindValueChanged(_ => OnNoteColourChanged?.Invoke());
        }
    }
}
