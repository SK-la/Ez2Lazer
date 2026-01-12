// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Reflection;
using osu.Framework.Localisation;

namespace osu.Game.LAsEzExtensions.Configuration
{
    public class EzLocalizationManager
    {
        static EzLocalizationManager()
        {
            // 使用反射为未设置英文的属性自动生成英文（属性名替换_为空格）
            var fields = typeof(EzLocalizationManager).GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(EzLocalisableString))
                {
                    if (field.GetValue(null) is EzLocalisableString instance && instance.English == null)
                    {
                        instance.English = field.Name.Replace("_", " ");
                    }
                }
            }
        }

        /// <summary>
        /// 本地化字符串类，直接持有中文和英文文本。
        /// 支持隐式转换为字符串，根据当前 UI 文化自动返回相应语言的文本。
        /// </summary>
        /// <example>
        /// 便捷用法：如果不提供英文参数，系统会自动从属性名生成英文（将 '_' 替换为空格）。
        /// <code>
        /// public static readonly EzLocalisableString My_Button = new EzLocalisableString("我的按钮");
        /// // 中文: "我的按钮"
        /// // 英文: "My Button" (自动生成)
        /// </code>
        /// 手动提供英文：
        /// <code>
        /// public static readonly EzLocalisableString My_Button = new EzLocalisableString("我的按钮", "Custom English");
        /// </code>
        /// </example>
        public class EzLocalisableString : ILocalisableStringData
        {
            public string Chinese { get; }

            /// <summary>
            /// 英文文本。如果为 null，英文时显示中文，或通过反射自动生成。
            /// </summary>
            public string? English { get; set; }

            /// <summary>
            /// 初始化本地化字符串。
            /// </summary>
            /// <param name="chinese">中文文本。</param>
            /// <param name="english">英文文本。如果为 null，英文时显示中文，或自动生成。</param>
            public EzLocalisableString(string chinese, string? english = null)
            {
                Chinese = chinese;
                English = english;
            }

            /// <summary>
            /// 便捷构造函数：只提供中文，英文稍后自动生成或显示中文。
            /// </summary>
            /// <param name="chinese">中文文本。</param>
            public EzLocalisableString(string chinese)
                : this(chinese, null) { }

            /// <summary>
            /// 隐式转换为字符串，根据当前语言返回相应文本。
            /// </summary>
            /// <param name="s">本地化字符串实例。</param>
            /// <returns>当前语言的文本。</returns>
            public static implicit operator string(EzLocalisableString s) => s.getString();

            /// <summary>
            /// 隐式转换为 LocalisableString，用于与 osu.Framework 兼容。
            /// </summary>
            /// <param name="s">本地化字符串实例。</param>
            /// <returns>LocalisableString 实例。</returns>
            public static implicit operator LocalisableString(EzLocalisableString s) => new LocalisableString((ILocalisableStringData)s);

            /// <summary>
            /// 支持格式化，返回格式化后的字符串。
            /// </summary>
            /// <param name="args">格式化参数。</param>
            /// <returns>格式化后的文本。</returns>
            public string Format(params object[] args) => string.Format(getString(), args);

            /// <summary>
            /// 返回当前语言的文本。
            /// </summary>
            /// <returns>文本字符串。</returns>
            public override string ToString() => getString();

            private string getString()
            {
                string lang = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.Ordinal) ? "zh" : "en";
                return lang == "zh" ? Chinese : (English ?? Chinese);
            }

            /// <summary>
            /// 实现ILocalisableStringData接口的方法。
            /// </summary>
            public string GetLocalised(LocalisationParameters parameters) => getString();

            /// <summary>
            /// 实现ILocalisableStringData接口的Equals方法。
            /// </summary>
            public bool Equals(ILocalisableStringData? other)
            {
                if (other is not EzLocalisableString ezOther)
                    return false;

                return Chinese == ezOther.Chinese && English == ezOther.English;
            }
        }

        // 公共属性定义本地化字符串，直接指定中文和英文
        public static readonly EzLocalisableString SettingsTitle = new EzLocalisableString("设置", "Settings");
        public static readonly EzLocalisableString SaveButton = new EzLocalisableString("保存", "Save");
        public static readonly EzLocalisableString CancelButton = new EzLocalisableString("取消", "Cancel");

        public static readonly EzLocalisableString GlobalTextureName = new EzLocalisableString("全局纹理名称", "Global Texture Name");
        public static readonly EzLocalisableString GlobalTextureNameTooltip = new EzLocalisableString("(全局纹理名称)统一修改当前皮肤中所有组件的纹理名称", "Set a global texture name for all components in the current skin");

        public static readonly EzLocalisableString StageSet = new EzLocalisableString("Stage套图", "Stage Set");

        public static readonly EzLocalisableString StageSetTooltip = new EzLocalisableString(
            "统一指定主面板, 如果有动效，则关联实时BPM。"
            + "\n支持在本地EzResources/Stage中增减子文件夹来自定义，选项会在重载时重新读取文件夹名称。"
            + "\n子文件夹可以自己改名，但内容文件夹及文件的名称必须完全一致。",
            "Set a stage set for Stage Bottom, related to real-time BPM"
            + "\nSupport adding or removing subfolders in the local EzResources/Stage for customization. Options will be reloaded when reloading."
            + "\nSubfolders can be renamed, but the names of content folders and files must be exactly the same.");

        public static readonly EzLocalisableString NoteSet = new EzLocalisableString("Note套图", "Note Set Sprite");

        public static readonly EzLocalisableString NoteSetTooltip = new EzLocalisableString(
            "统一指定整组note套图, 含note和打击光效。"
            + "\n支持在本地EzResources/Stage中增减子文件夹来自定义，选项会在重载时重新读取文件夹名称。"
            + "\n子文件夹可以自己改名，但内容文件夹及文件的名称必须完全一致。",
            "Set a note set for all notes and hit effects. "
            + "\nSupport adding or removing subfolders in the local EzResources/Stage for customization. Options will be reloaded when reloading."
            + "\nSubfolders can be renamed, but the names of content folders and files must be exactly the same.");

        public static readonly EzLocalisableString ColumnWidthStyle = new EzLocalisableString("列宽计算风格", "Column Width Calculation Style");

        public static readonly EzLocalisableString ColumnWidthStyleTooltip = new EzLocalisableString(
            "全局设置可以用在所有皮肤上。"
            + "\n全局总列宽=设置值×10，单列宽度=key数/总列宽。"
            + "\n其他是字面意思（功能不完善！）",
            "Global is can be applied to all skins. "
            + "\nGlobal Total Column Width = Configured Value × 10"
            + "\nOther styles are literal meaning (functionality not perfect!)");

        public static readonly EzLocalisableString ColumnWidth = new EzLocalisableString("单轨宽度", "Column Width");
        public static readonly EzLocalisableString ColumnWidthTooltip = new EzLocalisableString("设置每列轨道的宽度", "Set the width of each column");

        public static readonly EzLocalisableString SpecialFactor = new EzLocalisableString("特殊轨宽度倍率", "Special Column Width Factor");

        public static readonly EzLocalisableString SpecialFactorTooltip = new EzLocalisableString(
            "关联ColumnType设置，S列类型为特殊列，以此实现两种宽度的区分。",
            "The S column type are Special columns, achieving a distinction between two widths.");

        public static readonly EzLocalisableString GlobalHitPosition = new EzLocalisableString("全局判定线位置", "Global HitPosition");
        public static readonly EzLocalisableString GlobalHitPositionTooltip = new EzLocalisableString("全局判定线位置开关", "Global HitPosition Toggle");

        public static readonly EzLocalisableString HitPosition = new EzLocalisableString("判定线位置", "Hit Position");
        public static readonly EzLocalisableString HitPositionTooltip = new EzLocalisableString("设置可视的判定线位置", "Set the visible hit position");

        public static readonly EzLocalisableString HitTargetAlpha = new EzLocalisableString("note命中靶透明度(EzPro专用)", "Hit Target Alpha");

        public static readonly EzLocalisableString HitTargetAlphaTooltip = new EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶的透明度，可见判定线上与note一样的判定板",
            "Set the transparency of the note Hit Target in Ez Style Pro skin, making the hit plate on the hit position visible like the note");

        public static readonly EzLocalisableString HitTargetFloatFixed = new EzLocalisableString("命中靶的浮动修正(EzPro专用)", "Hit Target Float Fixed");

        public static readonly EzLocalisableString HitTargetFloatFixedTooltip = new EzLocalisableString(
            "设置Ez Style Pro皮肤中note命中靶，修改浮动效果的正弦函数运动范围",
            "Set the note Hit Target in Ez Style Pro skin, modifying the sine function motion range of the floating effect");

        public static readonly EzLocalisableString NoteHeightScale = new EzLocalisableString("note 高度比例", "Note Height Scale");
        public static readonly EzLocalisableString NoteHeightScaleTooltip = new EzLocalisableString("统一修改note的高度的比例", "Fixed Height for square notes");

        public static readonly EzLocalisableString ManiaHoldTailAlpha = new EzLocalisableString("Tail面尾透明度(未实装)", "Mania Hold Tail Alpha");
        public static readonly EzLocalisableString ManiaHoldTailAlphaTooltip = new EzLocalisableString("Mania Tail面尾的透明度", "Modify the transparency of the Mania hold tail");

        public static readonly EzLocalisableString ManiaHoldTailMaskGradientHeight = new EzLocalisableString("调整缩短面尾的距离(投)", "Adjust LN Tail Length (Opportunistic)");

        public static readonly EzLocalisableString ManiaHoldTailMaskGradientHeightTooltip = new EzLocalisableString(
            "(投皮) 缩短面条中部实现，不改变面尾形状",
            "(Opportunistic) Shorten the middle of the hold tail without changing its shape");

        public static readonly EzLocalisableString NoteTrackLine = new EzLocalisableString("Note辅助线", "Note Track Line");
        public static readonly EzLocalisableString NoteTrackLineTooltip = new EzLocalisableString("(Ez风格)note两侧辅助轨道线的高度", "(Ez Style)note side auxiliary track line height");

        public static readonly EzLocalisableString RefreshSaveSkin = new EzLocalisableString("强制刷新、保存皮肤", "Refresh & Save Skin");
        public static readonly EzLocalisableString SwitchToAbsolute = new EzLocalisableString("强制刷新, 并切换至 绝对位置（不稳定）", "Refresh, Switch to Absolute(Unstable)");
        public static readonly EzLocalisableString SwitchToRelative = new EzLocalisableString("强制刷新, 并切换至 相对位置（不稳定）", "Refresh, Switch to Relative(Unstable)");

        public static readonly EzLocalisableString DisableCmdSpace = new EzLocalisableString("游戏时禁用 Cmd+Space（聚焦搜索）", "Disable Cmd+Space (Spotlight) during gameplay");

        public static readonly EzLocalisableString HitMode = new EzLocalisableString("Mania 判定系统", "(Mania) Hit Mode");

        public static readonly EzLocalisableString HitModeTooltip = new EzLocalisableString(
            "Mania 判定系统, 获得不同音游的打击体验, 但是不保证所有模式都完全一比一复刻",
            "(Mania) Hit Mode, get different rhythm game hit experiences, but not guaranteed to perfectly replicate all modes");

        public static readonly EzLocalisableString HealthMode = new EzLocalisableString("Mania 血量系统", "(Mania) Health Mode");
        public static readonly EzLocalisableString HealthModeTooltip = new EzLocalisableString("目前主要用于O2Jam，其他模式图一乐", "Mainly used for O2Jam, other mode charts are for fun");

        public static readonly EzLocalisableString PoorHitResult = new EzLocalisableString("Mania Poor 判定系统", "(Mania) Poor HitResult Mode");

        public static readonly EzLocalisableString PoorHitResultTooltip = new EzLocalisableString(
            "Mania增加Pool判定，范围是比Miss提前150ms范围内时出现，动态严格扣血(连续累积将加剧，最大10%)",
            "Mania add the Poor HitResult, which appears within 150ms before Miss, with dynamic and strict health deduction (continuous accumulation will worsen, up to 10%)");

        public static readonly EzLocalisableString AccuracyCutoffS = new EzLocalisableString("Acc S评级线", "Accuracy Cutoff S");
        public static readonly EzLocalisableString AccuracyCutoffA = new EzLocalisableString("Acc A评级线", "Accuracy Cutoff A");

        // Storage folder messages
        public static readonly EzLocalisableString StorageFolder_Created = new EzLocalisableString("已创建目录：{0}\n请将文件放入该目录", "Created folder: {0}\nAdd files to the folder");
        public static readonly EzLocalisableString StorageFolder_Empty = new EzLocalisableString("目录为空：{0}", "Folder is empty: {0}");

    }

    public static class EzLocalizationExtensions
    {
        public static string Localize(this string key)
        {
            // 由于不再使用字典，这个扩展方法可能不再需要，但保留兼容性
            return key;
        }
    }
}
