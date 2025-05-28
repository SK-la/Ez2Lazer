// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Screens
{
    [Cached]
    public class EzSkinSettingsManager : IniConfigManager<EzSkinSetting>, IGameplaySettings
    {
        protected override string Filename => "EZSkin";

        protected override void InitialiseDefaults()
        {
            SetDefault(EzSkinSetting.NoteSetName, "evolve");
            SetDefault(EzSkinSetting.DynamicTracking, false);
            SetDefault(EzSkinSetting.GlobalTextureName, 4);
            SetDefault(EzSkinSetting.NonSquareNoteHeight, 20.0);
        }

        public EzSkinSettingsManager(Storage storage)
            : base(storage)
        {
        }

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => null!;
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => null!;
    }

    public enum EzSkinSetting
    {
        /// <summary>
        /// 选择的Note套图名称
        /// </summary>
        NoteSetName,

        /// <summary>
        /// 是否启用动态追踪刷新
        /// </summary>
        DynamicTracking,

        /// <summary>
        /// 全局纹理名称索引
        /// </summary>
        GlobalTextureName,

        // 统一高度
        NonSquareNoteHeight
    }
}
