// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Overlays.Settings;

namespace osu.Game.Skinning.Components
{
    // 强类型动态枚举值
    public readonly struct DynamicEnumValue : IEquatable<DynamicEnumValue>
    {
        public string Value { get; }

        public DynamicEnumValue(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;

        public override bool Equals(object? obj) => obj is DynamicEnumValue other && Equals(other);

        public bool Equals(DynamicEnumValue other) => Value == other.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(DynamicEnumValue left, DynamicEnumValue right) => left.Equals(right);

        public static bool operator !=(DynamicEnumValue left, DynamicEnumValue right) => !left.Equals(right);
    }

    // 动态枚举类，用于模拟枚举行为，静态全局访问
    public static class DynamicEnums
    {
        private static readonly List<string> notsets = new List<string>();
        private static readonly List<string> stagesets = new List<string>();
        private static readonly List<string> gamethemes = new List<string>();

        public static IEnumerable<string> NoteSets => notsets;
        public static IEnumerable<string> StageSets => stagesets;
        public static IEnumerable<string> GameThemes => gamethemes;

        // 强类型版本
        public static IEnumerable<DynamicEnumValue> NoteSetsStrong => notsets.Select(s => new DynamicEnumValue(s));
        public static IEnumerable<DynamicEnumValue> StageSetsStrong => stagesets.Select(s => new DynamicEnumValue(s));
        public static IEnumerable<DynamicEnumValue> GameThemesStrong => gamethemes.Select(s => new DynamicEnumValue(s));

        public static void SetNoteSets(IEnumerable<string> sets) => notsets.AddRange(sets);
        public static void SetStageSets(IEnumerable<string> sets) => stagesets.AddRange(sets);
        public static void SetGameThemes(IEnumerable<string> sets) => gamethemes.AddRange(sets);

        public static void ClearAll()
        {
            notsets.Clear();
            stagesets.Clear();
            gamethemes.Clear();
        }
    }

    public partial class EzSelectorEnumList : SettingsDropdown<string>
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            Items = DynamicEnums.GameThemes;
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
    // public enum EzSelectorGameThemeSet
    // {
    //     // ReSharper disable InconsistentNaming
    //     EZ2DJ_1st,
    //     EZ2DJ_1stSE,
    //     EZ2DJ_2nd,
    //     EZ2DJ_3rd,
    //     EZ2DJ_4th,
    //     EZ2DJ_6th,
    //     EZ2DJ_7th,
    //     AIR,
    //     AZURE_EXPRESSION,
    //     Celeste_Lumiere,
    //     CV_CRAFT,
    //     D2D_Station,
    //     Dark_Concert,
    //     DJMAX,
    //     EC_1304,
    //     EC_Wheel,
    //     EVOLVE,
    //     EZ2ON,
    //     FIND_A_WAY,
    //     Fortress2,
    //     Fortress3_Future,
    //     Fortress3_Gear,
    //     Fortress3_Green,
    //     Fortress3_Modern,
    //     GC,
    //     GC_EZ,
    //     Gem,
    //     HX_1121,
    //     HX_STANDARD,
    //     JIYU,
    //     Kings,
    //     Limited,
    //     NIGHT_FALL,
    //     O2_A9100,
    //     O2_EA05,
    //     O2_Jam,
    //     Platinum,
    //     QTZ_01,
    //     QTZ_02,
    //     REBOOT,
    //     SG_701,
    //     SH_512,
    //     Star,
    //     TANOc,
    //     TANOc2,
    //     TECHNIKA,
    //     TIME_TRAVELER,
    //     TOMATO,
    //     Turtle,
    //     Various_Ways,
    //     ArcadeScore,
    //     // ReSharper restore InconsistentNaming
    // }
}
