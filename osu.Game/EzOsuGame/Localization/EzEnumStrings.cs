// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Localization
{
    /// <summary>
    /// 提供 LocalisableString 本地化
    /// </summary>
    public static class EzEnumStrings
    {
        public static readonly LocalisableString EZ_STYLE_PRO_ONLY = new EzLocalizationManager.EzLocalisableString("Ez 内置皮肤专用", "Ez Skin Only");
        public static readonly LocalisableString GLOBAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局宽度", "Global Width");
        public static readonly LocalisableString GLOBAL_TOTAL_WIDTH = new EzLocalizationManager.EzLocalisableString("全局总宽度", "Global Total Width");

        public static readonly LocalisableString COLOURING_MODE_DIRECT = new EzLocalizationManager.EzLocalisableString("直接着色（默认）", "Direct (default)");
        public static readonly LocalisableString COLOURING_MODE_TINT = new EzLocalizationManager.EzLocalisableString("Tint 着色", "Tint");

        public static readonly LocalisableString SCALE = new EzLocalizationManager.EzLocalisableString("缩放动效", "Scale");
        public static readonly LocalisableString BOUNCE = new EzLocalizationManager.EzLocalisableString("跳跃动效", "Bounce");

        public static readonly LocalisableString AUTO_PREVIEW = new EzLocalizationManager.EzLocalisableString("全K音预览", "Auto Preview");
        public static readonly LocalisableString AUTO_PLAY_PLUS = new EzLocalizationManager.EzLocalisableString("全K音预览+游戏内自动播放", "Auto Preview + In-game Auto Play");

        public static readonly LocalisableString STATIC = new EzLocalizationManager.EzLocalisableString("静态预览", "Static Preview");
        public static readonly LocalisableString DYNAMIC = new EzLocalizationManager.EzLocalisableString("动态预览", "Dynamic Preview");
        public static readonly LocalisableString STATIC_FULL_MAP = new EzLocalizationManager.EzLocalisableString("全图预览", "Full-map Preview");
        public static readonly LocalisableString STATIC_SCROLL = new EzLocalizationManager.EzLocalisableString("卷轴预览", "Scroll Preview");

        public static readonly LocalisableString JUDGE_PRECEDENCE_COMBO = new EzLocalizationManager.EzLocalisableString("Combo优先(LR2风格)", "By Combo");
        public static readonly LocalisableString JUDGE_PRECEDENCE_DURATION = new EzLocalizationManager.EzLocalisableString("时差优先(raja风格)", "By Duration");
        public static readonly LocalisableString JUDGE_PRECEDENCE_EARLIEST = new EzLocalizationManager.EzLocalisableString("最早note优先(osu风格)", "By Earliest Note");
        public static readonly LocalisableString JUDGE_PRECEDENCE_SCORE = new EzLocalizationManager.EzLocalisableString("分数优先(势利)", "By Score");

        public static readonly LocalisableString SCORE_BONUS_TENDENCY_HIGH_JUDGE_RATIO = new EzLocalizationManager.EzLocalisableString(
            "高判定比例",
            "High Judge Ratio");

        public static readonly LocalisableString SCORE_BONUS_TENDENCY_LOW_ERROR_RATE = new EzLocalizationManager.EzLocalisableString(
            "低失误率",
            "Low Error Rate");

        public static readonly LocalisableString SCORE_BONUS_TENDENCY_HIDDEN = new EzLocalizationManager.EzLocalisableString(
            "不展示",
            "Hidden");

        public static readonly LocalisableString NOTIFICATION_BEHAVIOUR_NORMAL = new EzLocalizationManager.EzLocalisableString("正常", "Normal");
        public static readonly LocalisableString NOTIFICATION_BEHAVIOUR_IN_GAME_FOCUS = new EzLocalizationManager.EzLocalisableString("游戏内聚焦", "In-game focus");
        public static readonly LocalisableString NOTIFICATION_BEHAVIOUR_NEVER = new EzLocalizationManager.EzLocalisableString("永不通知", "Never notify");

        public static readonly LocalisableString SCREENSHOT_ACTION_SAVE_ONLY = new EzLocalizationManager.EzLocalisableString("仅保存图片", "Save image only");
        public static readonly LocalisableString SCREENSHOT_ACTION_SAVE_AND_COPY = new EzLocalizationManager.EzLocalisableString("保存图片并复制到剪贴板", "Save image and copy to clipboard");
        public static readonly LocalisableString SCREENSHOT_ACTION_COPY_ONLY = new EzLocalizationManager.EzLocalisableString("仅将图片复制到剪贴板", "Copy image to clipboard only");

        public static readonly LocalisableString IGNORE_BETTER = new EzLocalizationManager.EzLocalisableString("忽略更好", "Ignore Better");
        public static readonly LocalisableString IGNORE_WORSE = new EzLocalizationManager.EzLocalisableString("忽略更差", "Ignore Worse");

        public static readonly LocalisableString DATA_REBUILD_ACTION_MINA_NAME = new EzLocalizationManager.EzLocalisableString("Mina 技能评分", "Mina skill rating");
        public static readonly LocalisableString DATA_REBUILD_ACTION_MINA_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "引擎评级，喂养雷达与段位", "Engine rating; feeds the radar and dan");

        public static readonly LocalisableString DATA_REBUILD_ACTION_CSI_NAME = new EzLocalizationManager.EzLocalisableString("CSI 谱面结构", "CSI chart structure");
        public static readonly LocalisableString DATA_REBUILD_ACTION_CSI_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "结构轴与谱面标签，读 Mina", "Structure axes and chart tags; reads Mina");

        public static readonly LocalisableString DATA_REBUILD_ACTION_DAN_NAME = new EzLocalizationManager.EzLocalisableString("段位（Dan）", "Dan rank");
        public static readonly LocalisableString DATA_REBUILD_ACTION_DAN_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "由 Mina 与 CSI 推导", "Derived from Mina and CSI");

        public static readonly LocalisableString DATA_REBUILD_ACTION_PPP_NAME = new EzLocalizationManager.EzLocalisableString("PP+ 技能", "PP+ skills");
        public static readonly LocalisableString DATA_REBUILD_ACTION_PPP_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "四个模式各自的谱面向量，不依赖图表链", "Per-mode chart vector; independent of the chart chain");

        public static readonly LocalisableString DATA_REBUILD_ACTION_SSR_NAME = new EzLocalizationManager.EzLocalisableString("Sunny 星级", "Sunny star rating");
        public static readonly LocalisableString DATA_REBUILD_ACTION_SSR_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "独立评级，不依赖其他系统", "Independent rating; depends on nothing else");

        public static readonly LocalisableString DATA_REBUILD_ACTION_OFFICIAL_NAME = new EzLocalizationManager.EzLocalisableString("官方 PP 基线", "Official PP baseline");
        public static readonly LocalisableString DATA_REBUILD_ACTION_OFFICIAL_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "Perfect 成绩的 PP 列，读官方难度", "Perfect-play PP column; reads official difficulty");

        public static readonly LocalisableString DATA_REBUILD_ACTION_TAGS_NAME = new EzLocalizationManager.EzLocalisableString("谱面标签", "Chart tags");
        public static readonly LocalisableString DATA_REBUILD_ACTION_TAGS_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "视频 / Storyboard 标记", "Video / storyboard flags");

        public static readonly LocalisableString DATA_REBUILD_ACTION_SCORES_NAME = new EzLocalizationManager.EzLocalisableString("成绩全量重算", "Full score recalculation");
        public static readonly LocalisableString DATA_REBUILD_ACTION_SCORES_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "修复历史转换；耗时最长", "Repairs legacy conversions; the slowest action");

        public static readonly LocalisableString DATA_REBUILD_ACTION_ALL_NAME = new EzLocalizationManager.EzLocalisableString("Realm 全部元数据", "All Realm metadata");
        public static readonly LocalisableString DATA_REBUILD_ACTION_ALL_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "以上 Realm 项目一次跑完", "Runs every Realm item above in one pass");

        public static readonly LocalisableString DATA_REBUILD_ACTION_SQLITE_MAIN_NAME = new EzLocalizationManager.EzLocalisableString("主库 kps / KPC", "Main kps / KPC");
        public static readonly LocalisableString DATA_REBUILD_ACTION_SQLITE_MAIN_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "NoMod 分析切片", "NoMod analysis slice");

        public static readonly LocalisableString DATA_REBUILD_ACTION_SQLITE_BRANCHES_NAME = new EzLocalizationManager.EzLocalisableString("分支曲库 ssr/ PP", "Songs branches ssr/ PP");
        public static readonly LocalisableString DATA_REBUILD_ACTION_SQLITE_BRANCHES_DETAIL = new EzLocalizationManager.EzLocalisableString(
            "按分支快照构建，读主库切片", "Built per branch snapshot; reads the main slice");

        public static readonly LocalisableString LOGO_VIS_BARS = new EzLocalizationManager.EzLocalisableString("柱状", "Bars");
        public static readonly LocalisableString LOGO_VIS_POLYLINE = new EzLocalizationManager.EzLocalisableString("折线", "Polyline");
        public static readonly LocalisableString LOGO_VIS_WAVE = new EzLocalizationManager.EzLocalisableString("波形", "Waveform");
        public static readonly LocalisableString LOGO_VIS_DOTS = new EzLocalizationManager.EzLocalisableString("圆点", "Dots");
        public static readonly LocalisableString LOGO_VIS_NET = new EzLocalizationManager.EzLocalisableString("网状", "Net");
        public static readonly LocalisableString LOGO_VIS_OFF = new EzLocalizationManager.EzLocalisableString("关闭", "Off");

        // public static readonly LocalisableString LOGO_BACKDROP_LINKED = new EzLocalizationManager.EzLocalisableString("联动背景", "Linked");
        public static readonly LocalisableString LOGO_BACKDROP_CLEAR = new EzLocalizationManager.EzLocalisableString("透明", "Clear");

        public static readonly LocalisableString UI_MODE_DEFAULT = new EzLocalizationManager.EzLocalisableString("默认", "Default");
        public static readonly LocalisableString UI_MODE_ACRYLIC = new EzLocalizationManager.EzLocalisableString("亚克力", "Acrylic");
        public static readonly LocalisableString UI_MODE_LIQUID_GLASS = new EzLocalizationManager.EzLocalisableString("液态玻璃", "Liquid glass");
    }
}
