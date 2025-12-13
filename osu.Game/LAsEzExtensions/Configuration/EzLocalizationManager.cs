// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public class EzLocalizationManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> resources
            = new Dictionary<string, Dictionary<string, string>>();

        static EzLocalizationManager()
        {
            initializeResources();
        }

        private static void initializeResources()
        {
            addResource("SettingsTitle", "设置", "Settings");
            addResource("SaveButton", "保存", "Save");
            addResource("CancelButton", "取消", "Cancel");

            addResource("GlobalTextureName", "全局纹理名称", "Global Texture Name");
            addResource("GlobalTextureNameTooltip", "(全局纹理名称)统一修改当前皮肤中所有组件的纹理名称", "Set a global texture name for all components in the current skin");

            addResource("StageSet", "Stage套图", "Stage Set");
            addResource("StageSetTooltip", "统一指定主面板, 如果有动效，则关联实时BPM", "Set a stage set for Stage Bottom, related to real-time BPM");

            addResource("NoteSet", "Note套图", "Note Set Sprite");
            addResource("NoteSetTooltip", "统一指定整组note套图, 含note和打击光效", "Set a note set for all notes and hit effects");

            addResource("ColumnWidthStyle", "列宽计算风格", "Column Width Calculation Style");
            addResource("ColumnWidthStyleTooltip", "全局总列宽=设置值×10，其他是字面意思（功能不完善！）", "Global Total Column Width = Configured Value × 10");

            addResource("ColumnWidth", "单轨宽度", "Column Width");
            addResource("ColumnWidthTooltip", "设置每列轨道的宽度", "Set the width of each column");

            addResource("SpecialFactor", "特殊轨宽度倍率", "Special Column Width Factor");
            addResource("SpecialFactorTooltip", "S列类型为特殊列, 可自定义", "The S column type are Special columns, customizable");

            addResource("GlobalHitPosition", "全局判定线位置", "Global HitPosition");
            addResource("GlobalHitPositionTooltip", "全局判定线位置开关", "全局判定线位置开关");

            addResource("HitPosition", "判定线位置", "Hit Position");
            addResource("HitPositionTooltip", "设置可视的判定线位置", "Set the visible hit position");

            addResource("HitTargetAlpha", "note命中靶透明度", "Hit Target Alpha");
            addResource("HitTargetFloatFixed", "命中靶的浮动修正", "Hit Target Float Fixed");
            addResource("HitTargetFloatFixedTooltip", "在note的视觉命中靶，修改其正弦函数动效的浮动范围", "In the Hit Target, modify the floating range of its sine function dynamic effect");

            addResource("NoteHeightScale", "note 高度比例", "Note Height Scale");
            addResource("NoteHeightScaleTooltip", "统一修改note的高度的比例", "Fixed Height for square notes");

            addResource("NoteTrackLine", "Note辅助线", "Note Track Line");
            addResource("NoteTrackLineTooltip", "note两侧辅助轨道线的高度", "note side auxiliary track line height");

            addResource("RefreshSaveSkin", "强制刷新、保存皮肤", "Refresh & Save Skin");
            addResource("SwitchToAbsolute", "强制刷新, 并切换至 绝对位置（不稳定）", "Refresh, Switch to Absolute(Unstable)");
            addResource("SwitchToRelative", "强制刷新, 并切换至 相对位置（不稳定）", "Refresh, Switch to Relative(Unstable)");
        }

        private static void addResource(string key, string chinese, string english)
        {
            resources[key] = new Dictionary<string, string>
            {
                ["zh"] = chinese,
                ["en"] = english
            };
        }

        public static string GetString(string key)
        {
            if (!resources.TryGetValue(key, out var value))
                return key;

            string lang = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.Ordinal) ? "zh" : "en";
            return value[lang];
        }

        public static string GetString(string key, params object[] args)
        {
            string format = GetString(key);
            return string.Format(format, args);
        }
    }

    public static class EzLocalizationExtensions
    {
        public static string Localize(this string key)
        {
            return EzLocalizationManager.GetString(key);
        }
    }
}
