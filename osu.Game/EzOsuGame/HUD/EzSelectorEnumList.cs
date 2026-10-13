// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays.Settings;

// ReSharper disable UnusedMember.Global

namespace osu.Game.EzOsuGame.HUD
{
    public partial class EzSelectorEnumList : SettingsDropdown<string>
    {
        public const string DEFAULT_NAME = EzGameThemeNames.DEFAULT;

        [Resolved]
        private Storage storage { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (EzGameThemeCandidates.Items.Count == 0)
                EzGameThemeCandidates.Refresh(storage);

            refreshItems();
            EzGameThemeCandidates.Changed += refreshItems;
        }

        private void refreshItems()
        {
            Items = EzGameThemeCandidates.CreateDropdownItems(Current);
        }

        protected override void Dispose(bool isDisposing)
        {
            EzGameThemeCandidates.Changed -= refreshItems;
            base.Dispose(isDisposing);
        }
    }

    public partial class AnchorDropdown : SettingsDropdown<Anchor>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            // 限制选项范围
            Items = new List<Anchor>
            {
                Anchor.TopCentre,
                Anchor.Centre,
                Anchor.BottomCentre
            };
        }
    }

    //TODO: 枚举维护不方便，修改后要清理重构，考虑改为读取配置文件，或自动搜索子文件夹生成列表
    //这里使用枚举，主要为了全局纹理用，加载强依赖文件夹名称一致。note\stage使用动态加载，judge\score\earlylate使用枚举，后续可以考虑统一
    //注释用来备份，不要删除
    public enum EzEnumGameThemeName
    {
        // ReSharper disable InconsistentNaming
        EZ2DJ_1st,
        EZ2DJ_1stSE,
        EZ2DJ_2nd,
        EZ2DJ_3rd,
        EZ2DJ_4th,
        EZ2DJ_6th,
        EZ2DJ_7th,
        AIR,
        AZURE_EXPRESSION,
        Celeste_Lumiere,
        CV_CRAFT,
        D2D_Station,
        Dark_Concert,
        DJMAX,
        EC_1304,
        EC_Wheel,
        EVOLVE,
        EZ2ON,
        FIND_A_WAY,
        Fortress2,
        Fortress3_Future,
        Fortress3_Gear,
        Fortress3_Green,
        Fortress3_Modern,
        GC,
        GC_EZ,
        Gem,
        HX_1121,
        HX_STANDARD,
        JIYU,
        Kings,
        Limited,
        NIGHT_FALL,
        NIGHT_TRAVELER,
        O2_A9100,
        O2_EA05,
        O2_Jam,
        Platinum,
        QTZ_01,
        QTZ_02,
        REBOOT,
        SG_701,
        SH_512,
        Star,
        TANOc,
        TANOc2,
        TECHNIKA,
        TIME_TRAVELER,
        TOMATO,
        Turtle,
        Various_Ways,
        ArcadeScore,
        // ReSharper restore InconsistentNaming
    }
}
