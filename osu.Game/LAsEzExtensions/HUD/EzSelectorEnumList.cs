// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Overlays.Settings;

namespace osu.Game.LAsEzExtensions.HUD
{
    public partial class EzSelectorEnumList : SettingsDropdown<EzEnumGameThemeName>
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        // public const string DEFAULT_NAME = "Celeste_Lumiere";
        public const EzEnumGameThemeName DEFAULT_NAME = EzEnumGameThemeName.Celeste_Lumiere;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // 动态加载GameTheme文件夹
            // var availableThemes = loadAvailableThemes();
            Items = Enum.GetValues(typeof(EzEnumGameThemeName)).Cast<EzEnumGameThemeName>().ToList();
        }

        private List<string> loadAvailableThemes()
        {
            var themes = new List<string>();

            try
            {
                string gameThemePath = storage.GetFullPath("EzResources/GameTheme");

                if (Directory.Exists(gameThemePath))
                {
                    string[] directories = Directory.GetDirectories(gameThemePath);
                    themes.AddRange(directories.Select(Path.GetFileName).Where(name => !string.IsNullOrEmpty(name))!);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to load GameTheme folders: {ex.Message}", LoggingTarget.Runtime, LogLevel.Error);
            }

            return themes;
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

    public enum EzComEffectType
    {
        Scale,
        Bounce,
        None
    }

    //TODO: 枚举维护不方便，修改后要清理重构，考虑改为读取配置文件，或自动搜索子文件夹生成列表
    //这里使用枚举，加载的是Resource.dll中的资源
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
