// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public class EzManiaModStrings : EzLocalizationManager
    {
        static EzManiaModStrings()
        {
            // 使用反射为未设置英文的属性自动生成英文（属性名替换_为空格）
            var fields = typeof(EzManiaModStrings).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

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

        // 本地化字符串类，直接持有中文和英文
        public new class EzLocalisableString : EzLocalizationManager.EzLocalisableString
        {
            public EzLocalisableString(string chinese, string? english = null)
                : base(chinese, english) { }

            // 便捷构造函数：如果不提供英文，则稍后通过反射从属性名生成
            public EzLocalisableString(string chinese)
                : base(chinese) { }
        }

        // ====================================================================================================
        // LAsMods - Mod Descriptions
        // ====================================================================================================

        public static readonly LocalisableString Ez2Settings_Description = new EzLocalisableString("移除盘子和踏板", "Remove Scratch, Panel.");
        public static readonly LocalisableString NiceBPM_Description = new EzLocalisableString("自由调整BPM或速度", "Free BPM or Speed");
        public static readonly LocalisableString SpaceBody_Description = new EzLocalisableString("全LN面海，可调面缝", "Full LN, adjustable gaps");

        public static readonly LocalisableString LoopPlayClip_Description = new EzLocalisableString("将谱面切割成片段用于循环练习。（原版是YuLiangSSS的Duplicate Mod）",
            "Cut the beatmap into a clip for loop practice. (The original is YuLiangSSS's Duplicate Mod)");

        // ====================================================================================================
        // LAsMods - SettingSource Labels & Descriptions
        // ====================================================================================================

        // Ez2Settings
        public static readonly LocalisableString NoScratch_Label = new EzLocalisableString("免盘", "No Scratch");
        public static readonly LocalisableString NoScratch_Description = new EzLocalisableString("免盘. 用于: 6-9k L-S; 12\\14\\16k LR-S", "No (EZ)Scratch. For: 6-9k L-S; 12\\14\\16k LR-S");
        public static readonly LocalisableString NoPanel_Label = new EzLocalisableString("免面板", "No Panel");
        public static readonly LocalisableString NoPanel_Description = new EzLocalisableString("免Ez街机谱脚踏. 用于: 7\\14\\18k", "No (EZ)Panel. For: 7\\14\\18k");
        public static readonly LocalisableString HealthyScratch_Label = new EzLocalisableString("健康盘子", "Healthy Scratch");
        public static readonly LocalisableString HealthyScratch_Description = new EzLocalisableString("优化盘子密度，将过快的盘子移动到其他列", "Healthy (EZ)Scratch. Move the fast Scratch to the other columns");
        public static readonly LocalisableString MaxBeat_Label = new EzLocalisableString("最大拍间隔", "Max Beat");
        public static readonly LocalisableString MaxBeat_Description = new EzLocalisableString("盘子最大间隔, 1/? 拍", "Scratch MAX Beat Space, MAX 1/? Beat");

        // SpaceBody
        public static readonly LocalisableString SpaceBody_Label = new EzLocalisableString("面海缝隙", "Space Body");
        public static readonly LocalisableString SpaceBodyGap_Description = new EzLocalisableString("全LN面海，可调面缝", "Full LN, adjustable gaps");
        public static readonly LocalisableString AddShield_Label = new EzLocalisableString("添加护盾", "Add Shield");
        public static readonly LocalisableString AddShield_Description = new EzLocalisableString("在面海中添加护盾音符", "Add shield notes in the sea");

        // LoopPlayClip
        public static readonly LocalisableString LoopCount_Label = new EzLocalisableString("循环次数", "Loop Count");
        public static readonly LocalisableString LoopCount_Description = new EzLocalisableString("切片循环次数", "Loop Clip Count.");
        public static readonly LocalisableString SpeedChange_Label = new EzLocalisableString("速度改变", "Speed Change");
        public static readonly LocalisableString SpeedChange_Description = new EzLocalisableString("改变倍速。不允许叠加其他变速mod。", "Speed Change. The actual decrease to apply. Don't add other rate-mod.");
        public static readonly LocalisableString AdjustPitch_Label = new EzLocalisableString("调整音调", "Adjust pitch");
        public static readonly LocalisableString AdjustPitch_Description = new EzLocalisableString("速度改变时是否调整音调。（变速又变调）", "Adjust pitch. Should pitch be adjusted with speed.(变速又变调)");
        public static readonly LocalisableString ConstantSpeed_Label = new EzLocalisableString("恒定速度", "Constant Speed");
        public static readonly LocalisableString ConstantSpeed_Description = new EzLocalisableString("去除SV变速。（恒定速度/忽略谱面中的变速）", "Constant Speed. No more tricky speed changes.(恒定速度/忽略谱面中的变速)");
        public static readonly LocalisableString CutStartTime_Label = new EzLocalisableString("切片开始时间", "Cut Start Time");
        public static readonly LocalisableString CutStartTime_Description = new EzLocalisableString("切片开始时间, 默认是秒。推荐通过谱面编辑器A-B控件设置，可自动输入", "Cut StartTime. Default is second.");
        public static readonly LocalisableString CutEndTime_Label = new EzLocalisableString("切片结束时间", "Cut End Time");
        public static readonly LocalisableString CutEndTime_Description = new EzLocalisableString("切片结束时间, 默认是秒。推荐通过谱面编辑器A-B控件设置，可自动输入", "Cut EndTime. Default is second.");
        public static readonly LocalisableString UseMillisecond_Label = new EzLocalisableString("使用毫秒", "Use Millisecond");
        public static readonly LocalisableString UseMillisecond_Description = new EzLocalisableString("改为使用ms单位", "Use millisecond(ms).");
        public static readonly LocalisableString UseGlobalABRange_Label = new EzLocalisableString("使用全局A-B范围", "Use Global A-B Range");

        public static readonly LocalisableString UseGlobalABRange_Description = new EzLocalisableString("始终使用谱面编辑器中A/B空间设置的范围（毫秒）。推荐保持开启",
            "Use global A-B range. Always use the editor A/B range stored for this session (ms).");

        public static readonly LocalisableString BreakTime_Label = new EzLocalisableString("休息时间", "Break Time");
        public static readonly LocalisableString BreakTime_Description = new EzLocalisableString("设置两个切片循环之间的休息时间（秒）", "Break Time. If you need break(second).");
        public static readonly LocalisableString Random_Label = new EzLocalisableString("随机", "Random");
        public static readonly LocalisableString Random_Description = new EzLocalisableString("在切片每次重复时进行随机", "Random. Do a Random on every duplicate.");
        public static readonly LocalisableString Mirror_Label = new EzLocalisableString("镜像", "Mirror");
        public static readonly LocalisableString Mirror_Description = new EzLocalisableString("在切片每次重复时进行镜像", "Mirror. Mirror next part.");
        public static readonly LocalisableString InfiniteLoop_Label = new EzLocalisableString("无限循环", "Infinite Loop");

        public static readonly LocalisableString InfiniteLoop_Description = new EzLocalisableString("启用无限循环播放。游戏中必须使用Esc退出才能结束，无法获得成绩结算。",
            "Infinite Loop. Enable infinite loop playback. You must use Esc to exit in the game to end, and you cannot get score settlement.");

        public static readonly LocalisableString MirrorTime_Label = new EzLocalisableString("镜像时间", "Mirror Time");
        public static readonly LocalisableString MirrorTime_Description = new EzLocalisableString("每隔多少次循环做一次镜像", "Mirror Time. Every next time part will be mirrored.");
        public static readonly LocalisableString Seed_Label = new EzLocalisableString("种子", "Seed");
        public static readonly LocalisableString Seed_Description = new EzLocalisableString("使用自定义种子而不是随机种子", "Seed. Use a custom seed instead of a random one");

        // Additional Adjust mod settings
        public static readonly LocalisableString RandomMirror_Label = new EzLocalisableString("随机镜像", "Random Mirror");
        public static readonly LocalisableString RandomMirror_Description = new EzLocalisableString("随机决定是否镜像音符", "Random Mirror. Mirror or not mirror notes by random.");
        public static readonly LocalisableString NoFail_Label = new EzLocalisableString("无失败", "No Fail");
        public static readonly LocalisableString NoFail_Description = new EzLocalisableString("无论如何都不会失败", "No Fail. You can't fail, no matter what.");
        public static readonly LocalisableString Restart_Label = new EzLocalisableString("失败重启", "Restart on fail");
        public static readonly LocalisableString Restart_Description = new EzLocalisableString("失败时自动重启", "Restart on fail. Automatically restarts when failed.");
        public static readonly LocalisableString RandomSelect_Label = new EzLocalisableString("随机选择", "Random");
        public static readonly LocalisableString RandomSelect_Description = new EzLocalisableString("随机排列按键", "Random. Shuffle around the keys.");
        public static readonly LocalisableString TrueRandom_Label = new EzLocalisableString("真随机", "True Random");

        public static readonly LocalisableString TrueRandom_Description = new EzLocalisableString("随机排列所有音符（使用NoLN（LN转换器等级-3），否则可能会重叠）",
            "True Random. Shuffle all notes(Use NoLN(LN Transformer Level -3), or you will get overlapping notes otherwise).");

        // ====================================================================================================
        // YuLiangSSSMods - Mod Descriptions
        // ====================================================================================================

        public static readonly LocalisableString ChangeSpeedByAccuracy_Description = new EzLocalisableString("根据准确度调整游戏速度", "Adapt the speed of the game based on the accuracy.");
        public static readonly LocalisableString Adjust_Description = new EzLocalisableString("凉雨Mod一卡通", "Set your settings.");
        public static readonly LocalisableString LN_Description = new EzLocalisableString("LN转换器", "LN Transformer");
        public static readonly LocalisableString Cleaner_Description = new EzLocalisableString("清理谱面中的子弹或其他音符（例如重叠音符）", "Clean bullet or other notes on map(e.g. Overlap note).");
        public static readonly LocalisableString LNJudgementAdjust_Description = new EzLocalisableString("调整LN的判定", "Adjust the judgement of LN.");
        public static readonly LocalisableString LNSimplify_Description = new EzLocalisableString("通过转换简化节奏", "Simplifies rhythms by converting.");
        public static readonly LocalisableString LNTransformer_Description = new EzLocalisableString("LN转换", "LN Transformer");
        public static readonly LocalisableString NewJudgement_Description = new EzLocalisableString("根据歌曲BPM设置新的判定", "New judgement set by BPM of the song.");
        public static readonly LocalisableString NtoM_Description = new EzLocalisableString("转换为更高的按键数模式", "Convert to upper Keys mode.");

        public static readonly LocalisableString NtoMAnother_Description = new EzLocalisableString("转Key，来自krrcream的工具（有一些bug，请使用Clean设置来清理）",
            "From krrcream's Tool (It has some bugs, please use Clean settings to clean it.)");

        public static readonly LocalisableString Gracer_Description = new EzLocalisableString("转换为grace", "Convert to grace.");
        public static readonly LocalisableString O2Judgement_Description = new EzLocalisableString("为O2JAM玩家设计的判定系统", "Judgement System for O2JAM players.");
        public static readonly LocalisableString PlayfieldTransformation_Description = new EzLocalisableString("根据连击数调整游戏区域缩放", "Adjusts playfield scale based on combo.");
        public static readonly LocalisableString O2Health_Description = new EzLocalisableString("为O2JAM玩家设计的生命值系统", "Health system for O2JAM players.");
        public static readonly LocalisableString Remedy_Description = new EzLocalisableString("修复较低的判定", "Remedy lower judgement.");
        public static readonly LocalisableString StarRatingRebirth_Description = new EzLocalisableString("sunnyxxy的星级算法，替换官方星级标记", "New algorithm by sunnyxxy.");
        public static readonly LocalisableString ReleaseAdjust_Description = new EzLocalisableString("不再需要计时长按音符的结尾", "No more timing the end of hold notes.");
        public static readonly LocalisableString NoteAdjust_Description = new EzLocalisableString("制作更多或更少的音符", "To make more or less note.");
        public static readonly LocalisableString LNLongShortAddition_Description = new EzLocalisableString("LN转换器附加版本", "LN Transformer additional version.");
        public static readonly LocalisableString MalodyStyleLN_Description = new EzLocalisableString("像Malody一样播放LN！", "Play LN like Malody!");
        public static readonly LocalisableString LNDoubleDistribution_Description = new EzLocalisableString("LN转换器另一个版本", "LN Transformer another version.");
        public static readonly LocalisableString JudgmentsAdjust_Description = new EzLocalisableString("修改你的判定", "Modify your judgement.");
        public static readonly LocalisableString JackAdjust_Description = new EzLocalisableString("Jack的模式", "Pattern of Jack");

        public static readonly LocalisableString CleanColumn_Description = new EzLocalisableString("清理Column, 推荐搭配Column Type使用",
            "Clean Column, use with Column Type.");

        // CleanColumn
        public static readonly LocalisableString DeleteSColumn_Label = new EzLocalisableString("删除S列", "Delete S Column Type");
        public static readonly LocalisableString DeleteSColumn_Description = new EzLocalisableString("开启时删除标记了S Column Type的列", "Delete columns marked with S column type when enabled");
        public static readonly LocalisableString DeletePColumn_Label = new EzLocalisableString("删除P列", "Delete P Column Type");
        public static readonly LocalisableString DeletePColumn_Description = new EzLocalisableString("开启时删除标记了P Column Type的列", "Delete columns marked with P column type when enabled");
        public static readonly LocalisableString DeleteEColumn_Label = new EzLocalisableString("删除E列", "Delete E Column Type");
        public static readonly LocalisableString DeleteEColumn_Description = new EzLocalisableString("开启时删除标记了E Column Type的列", "Delete columns marked with E column type when enabled");
        public static readonly LocalisableString EnableCustomDelete_Label = new EzLocalisableString("自定义删除列", "Enable Custom Delete");
        public static readonly LocalisableString EnableCustomDelete_Description = new EzLocalisableString("开启后启用自定义删除列功能", "Enable custom column deletion when enabled");
        public static readonly LocalisableString CustomDeleteColumn_Label = new EzLocalisableString("自定义删除列序号", "Custom Delete Column Index");
        public static readonly LocalisableString CustomDeleteColumn_Description = new EzLocalisableString("按照输入的序号，删除谱面中对应编号的列", "Delete the column with the specified index");

        // ====================================================================================================
        // YuLiangSSSMods - SettingSource Labels & Descriptions
        // ====================================================================================================

        // ChangeSpeedByAccuracy
        public static readonly LocalisableString ChangeSpeedAccuracy_Label = new EzLocalisableString("准确度", "Accuracy");
        public static readonly LocalisableString ChangeSpeedAccuracy_Description = new EzLocalisableString("应用速度变化的准确度", "Accuracy. Accuracy for speed change to be applied.");
        public static readonly LocalisableString MaxSpeed_Label = new EzLocalisableString("最大速度", "Max Speed");
        public static readonly LocalisableString MaxSpeed_Description = new EzLocalisableString("最大速度", "Max Speed");
        public static readonly LocalisableString MinSpeed_Label = new EzLocalisableString("最小速度", "Min Speed");
        public static readonly LocalisableString MinSpeed_Description = new EzLocalisableString("最小速度", "Min Speed");

        // NiceBPM
        public static readonly LocalisableString InitialRate_Label = new EzLocalisableString("初始速度", "Initial rate");
        public static readonly LocalisableString InitialRate_Description = new EzLocalisableString("轨道的起始速度", "Initial rate. The starting speed of the track");

        // Gracer
        public static readonly LocalisableString Bias_Label = new EzLocalisableString("偏差", "Bias");
        public static readonly LocalisableString Bias_Description = new EzLocalisableString("原始时机的偏差", "Bias. The bias of original timing.");
        public static readonly LocalisableString Interval_Label = new EzLocalisableString("间隔", "Interval");
        public static readonly LocalisableString Interval_Description = new EzLocalisableString("音符的最小间隔（防止重叠）", "Interval. The minimum interval of note(To prevent overlap).");
        public static readonly LocalisableString Probability_Label = new EzLocalisableString("概率", "Probability");
        public static readonly LocalisableString Probability_Description = new EzLocalisableString("转换概率", "Probability. The Probability of convertion.");

        // NtoM, Gracer, JackAdjust
        public static readonly LocalisableString Key_Label = new EzLocalisableString("按键数", "Key");
        public static readonly LocalisableString Key_Description = new EzLocalisableString("目标按键数（只能从低按键数转换为高按键数）", "Key. To Keys(Can only convert lower keys to higher keys.)");
        public static readonly LocalisableString BlankColumn_Label = new EzLocalisableString("空白列", "Blank Column");

        public static readonly LocalisableString BlankColumn_Description = new EzLocalisableString("要添加的空白列数。（注意：如果按键数-圆形大小小于空白列数，则不会添加。）",
            "Number of blank columns to add. (Notice: If the number of Key - CircleSize is less than the number of blank columns, it won't be added.)");

        public static readonly LocalisableString NtoMGap_Label = new EzLocalisableString("间隙", "Gap");

        public static readonly LocalisableString NtoMGap_Description = new EzLocalisableString("在每个区域重新排列音符。（间隙越大，音符分布越广。）",
            "Rearrange the notes in every area. (If Gap is bigger, the notes will be more spread out.)");

        public static readonly LocalisableString Clean_Label = new EzLocalisableString("清理", "Clean");
        public static readonly LocalisableString Clean_Description = new EzLocalisableString("尝试清理谱面中的一些音符。", "Try to clean some notes in the map.");
        public static readonly LocalisableString CleanDivide_Label = new EzLocalisableString("清理分割", "Clean Divide");

        public static readonly LocalisableString CleanDivide_Description = new EzLocalisableString("选择清理的分割（0表示不分割清理，4推荐用于流，8推荐用于Jack）。（如果清理为false，此设置不会被使用。）",
            "Choose the divide(0 For no Divide Clean, 4 is Recommended for Stream, 8 is Recommended for Jack) of cleaning. (If Clean is false, this setting won't be used.)");

        public static readonly LocalisableString Adjust4Jack_Label = new EzLocalisableString("1/4 Jack", "1/4 Jack");

        public static readonly LocalisableString Adjust4Jack_Description = new EzLocalisableString("（如100+ BPM 1/4 Jack）清理分割 * 1/2，用于1/4 Jack，避免清理1/4 Jack。",
            "(Like 100+ BPM 1/4 Jack)Clean Divide * 1/2, for 1/4 Jack, avoiding cleaning 1/4 Jack.");

        public static readonly LocalisableString Adjust4Speed_Label = new EzLocalisableString("1/4 Speed", "1/4 Speed");

        public static readonly LocalisableString Adjust4Speed_Description = new EzLocalisableString("（如300+ BPM 1/4 Speed）清理分割 * 2，用于1/4 Speed，避免额外的1/2 Jack。",
            "(Like 300+ BPM 1/4 Speed)Clean Divide * 2, for 1/4 Speed, avoiding additional 1/2 Jack.");

        // JackAdjust
        public static readonly LocalisableString ToStream_Label = new EzLocalisableString("转换为流", "To Stream");

        public static readonly LocalisableString ToStream_Description = new EzLocalisableString("尽可能作为JumpJack（推荐使用中等概率50~80）",
            "To Stream. As Jumpjack as possible(Recommend to use a medium(50~80) probability).");

        public static readonly LocalisableString Line_Label = new EzLocalisableString("线", "Line");
        public static readonly LocalisableString Line_Description = new EzLocalisableString("Jack的线", "Line. Line for Jack.");
        public static readonly LocalisableString Alignment_Label = new EzLocalisableString("对齐", "Alignment");

        public static readonly LocalisableString Alignment_Description = new EzLocalisableString("最后一行（false）或第一行（true），true会得到一些子弹，false会得到许多长jack",
            "Alignment. Last line(false) or first line(true), true will get some bullet, false will get many long jack.");

        // NtoM, Gracer, JackAdjust, LNJudgementAdjust
        public static readonly LocalisableString ApplyOrder_Label = new EzLocalisableString("应用顺序", "Apply Order");

        public static readonly LocalisableString ApplyOrder_Description = new EzLocalisableString("此mod在谱面转换后应用的顺序。数字越小越先运行。",
            "Apply Order. Order in which this mod is applied after beatmap conversion. Lower runs earlier.");

        // JudgmentsAdjust
        public static readonly LocalisableString CustomHitRange_Label = new EzLocalisableString("自定义打击范围", "Custom Hit Range");
        public static readonly LocalisableString CustomHitRange_Description = new EzLocalisableString("调整音符的打击范围", "Custom Hit Range. Adjust the hit range of notes.");
        public static readonly LocalisableString CustomProportionScore_Label = new EzLocalisableString("自定义比例分数", "Custom Proportion Score");
        public static readonly LocalisableString CustomProportionScore_Description = new EzLocalisableString("自定义比例分数", "Custom Proportion Score");

        // LNJudgementAdjust
        public static readonly LocalisableString BodyJudgementSwitch_Label = new EzLocalisableString("主体判定开关", "Body Judgement Switch");
        public static readonly LocalisableString BodyJudgementSwitch_Description = new EzLocalisableString("开启/关闭主体判定", "Turn on/off body judgement.");
        public static readonly LocalisableString TailJudgementSwitch_Label = new EzLocalisableString("尾部判定开关", "Tail Judgement Switch");
        public static readonly LocalisableString TailJudgementSwitch_Description = new EzLocalisableString("开启/关闭尾部判定", "Turn on/off tail judgement.");

        // O2Judgement
        public static readonly LocalisableString PillSwitch_Label = new EzLocalisableString("药丸开关", "Pill Switch");
        public static readonly LocalisableString PillSwitch_Description = new EzLocalisableString("使用O2JAM药丸功能", "Use O2JAM pill function.");

        // Cleaner
        public static readonly LocalisableString Style_Label = new EzLocalisableString("样式", "Style");
        public static readonly LocalisableString Style_Description = new EzLocalisableString("选择你的样式", "Choose your style.");
        public static readonly LocalisableString LNInterval_Label = new EzLocalisableString("LN间隔", "LN Interval");
        public static readonly LocalisableString LNInterval_Description = new EzLocalisableString("你决定的释放和按压速度", "The release & press speed you decide.");

        // LN
        public static readonly LocalisableString Divide_Label = new EzLocalisableString("分割", "Divide");
        public static readonly LocalisableString Divide_Description = new EzLocalisableString("使用1/?", "Use 1/?");
        public static readonly LocalisableString Percentage_Label = new EzLocalisableString("百分比", "Percentage");
        public static readonly LocalisableString Percentage_Description = new EzLocalisableString("LN内容", "LN Content");
        public static readonly LocalisableString OriginalLN_Label = new EzLocalisableString("原始LN", "Original LN");
        public static readonly LocalisableString OriginalLN_Description = new EzLocalisableString("原始LN不会被转换", "Original LN won't be converted.");
        public static readonly LocalisableString ColumnNum_Label = new EzLocalisableString("列数", "Column Num");
        public static readonly LocalisableString ColumnNum_Description = new EzLocalisableString("选择要转换的列数", "Select the number of column to transform.");
        public static readonly LocalisableString Gap_Label = new EzLocalisableString("间隙", "Gap");
        public static readonly LocalisableString Gap_Description = new EzLocalisableString("转换后改变随机列的音符数量间隙", "For changing random columns after transforming the gap's number of notes.");
        public static readonly LocalisableString LineSpacing_Label = new EzLocalisableString("行间距", "Line Spacing");
        public static readonly LocalisableString LineSpacing_Description = new EzLocalisableString("设置为0时转换每一行", "Transform every line when set to 0.");
        public static readonly LocalisableString InvertLineSpacing_Label = new EzLocalisableString("反转行间距", "Invert Line Spacing");
        public static readonly LocalisableString InvertLineSpacing_Description = new EzLocalisableString("反转行间距", "Invert the Line Spacing.");
        public static readonly LocalisableString DurationLimit_Label = new EzLocalisableString("持续时间限制", "Duration Limit");
        public static readonly LocalisableString DurationLimit_Description = new EzLocalisableString("LN的最大持续时间（秒）。（设置为0时无限制）", "The max duration(second) of a LN.(No limit when set to 0)");

        // LNSimplify
        public static readonly LocalisableString LimitDivide_Label = new EzLocalisableString("限制分割", "Limit Divide");
        public static readonly LocalisableString LimitDivide_Description = new EzLocalisableString("选择限制", "Select limit.");
        public static readonly LocalisableString EasierDivide_Label = new EzLocalisableString("简化分割", "Easier Divide");
        public static readonly LocalisableString EasierDivide_Description = new EzLocalisableString("选择复杂度", "Select complexity.");
        public static readonly LocalisableString LongestLN_Label = new EzLocalisableString("最长LN", "Longest LN");
        public static readonly LocalisableString LongestLN_Description = new EzLocalisableString("最长LN", "Longest LN.");
        public static readonly LocalisableString ShortestLN_Label = new EzLocalisableString("最短LN", "Shortest LN");
        public static readonly LocalisableString ShortestLN_Description = new EzLocalisableString("最短LN", "Shortest LN.");

        // O2Health
        public static readonly LocalisableString Difficulty_Label = new EzLocalisableString("难度", "Difficulty");
        public static readonly LocalisableString Difficulty_Description = new EzLocalisableString("1: 简单  2: 普通  3: 困难", "1: Easy  2: Normal  3: Hard");

        // DoublePlay
        public static readonly LocalisableString DoublePlayStyle_Label = new EzLocalisableString("样式", "Style");

        public static readonly LocalisableString DoublePlayStyle_Description = new EzLocalisableString(
            "1: NM+NM   2: MR+MR   3: NM+MR   4: MR+NM   5: Bracket NM+NM   6: Bracket MR   7: Wide Bracket   8: Wide Bracket MR",
            "1: NM+NM   2: MR+MR   3: NM+MR   4: MR+NM   5: Bracket NM+NM   6: Bracket MR   7: Wide Bracket   8: Wide Bracket MR");

        // PlayfieldTransformation
        public static readonly LocalisableString MinimumScale_Label = new EzLocalisableString("最小缩放", "Minimum scale");
        public static readonly LocalisableString MinimumScale_Description = new EzLocalisableString("游戏区域的最小缩放", "The minimum scale of the playfield.");

        // ModStarRatingRebirth
        public static readonly LocalisableString UseOriginalOD_Label = new EzLocalisableString("使用原始OD", "Use original OD");
        public static readonly LocalisableString UseOriginalOD_Description = new EzLocalisableString("高优先级", "High Priority");
        public static readonly LocalisableString UseCustomOD_Label = new EzLocalisableString("使用自定义OD", "Use custom OD");
        public static readonly LocalisableString UseCustomOD_Description = new EzLocalisableString("低优先级", "Low Priority");
        public static readonly LocalisableString OD_Label = new EzLocalisableString("OD", "OD");
        public static readonly LocalisableString OD_Description = new EzLocalisableString("选择要重新计算的OD", "Choose the OD you want to recalculate.");

        // Adjust
        public static readonly LocalisableString ScoreMultiplier_Label = new EzLocalisableString("分数倍数", "Score Multiplier");
        public static readonly LocalisableString HPDrain_Label = new EzLocalisableString("HP消耗", "HP Drain");
        public static readonly LocalisableString HPDrain_Description = new EzLocalisableString("覆盖谱面的HP设置", "Override a beatmap's set HP.");
        public static readonly LocalisableString AdjustAccuracy_Label = new EzLocalisableString("准确度", "Accuracy");
        public static readonly LocalisableString AdjustAccuracy_Description = new EzLocalisableString("覆盖谱面的OD设置", "Override a beatmap's set OD.");
        public static readonly LocalisableString ReleaseLenience_Label = new EzLocalisableString("释放宽容度", "Release Lenience");

        public static readonly LocalisableString ReleaseLenience_Description = new EzLocalisableString("调整LN尾部释放窗口宽容度。（Score v2中的尾部默认有1.5倍打击窗口）",
            "Adjust LN tail release window lenience.(Tail in Score v2 has default 1.5x hit window)");

        public static readonly LocalisableString CustomHP_Label = new EzLocalisableString("自定义HP", "Custom HP");
        public static readonly LocalisableString CustomOD_Label = new EzLocalisableString("自定义OD", "Custom OD");
        public static readonly LocalisableString CustomRelease_Label = new EzLocalisableString("自定义释放", "Custom Release");
        public static readonly LocalisableString ExtendedLimits_Label = new EzLocalisableString("扩展限制", "Extended Limits");
        public static readonly LocalisableString ExtendedLimits_Description = new EzLocalisableString("调整难度超出合理限制", "Adjust difficulty beyond sane limits.");
        public static readonly LocalisableString AdjustConstantSpeed_Label = new EzLocalisableString("恒定速度", "Constant Speed");
        public static readonly LocalisableString AdjustConstantSpeed_Description = new EzLocalisableString("不再有棘手的速度变化", "No more tricky speed changes.");

        // NoteAdjust
        public static readonly LocalisableString NoteAdjustStyle_Label = new EzLocalisableString("样式", "Style");

        public static readonly LocalisableString NoteAdjustStyle_Description = new EzLocalisableString("1: 适用于Jack模式。2&3: 适用于Stream模式。4&5: 适用于Speed模式（无Jack）。6: DIY（将使用↓↓↓所有选项）（1~5将仅使用↓种子选项）",
            "1: Applicable to Jack Pattern.  2&3: Applicable to Stream Pattern.  4&5: Applicable to Speed Pattern(No Jack).  6: DIY(Will use ↓↓↓ all options) (1~5 will only use ↓ seed option).");

        public static readonly LocalisableString NoteAdjustProbability_Label = new EzLocalisableString("概率", "Probability");
        public static readonly LocalisableString NoteAdjustProbability_Description = new EzLocalisableString("增加音符的概率", "The Probability of increasing note.");
        public static readonly LocalisableString Extremum_Label = new EzLocalisableString("极值", "Extremum");

        public static readonly LocalisableString Extremum_Description = new EzLocalisableString("取决于你在一行上保留多少音符（可用最大音符或最小音符）",
            "Depending on how many notes on one line you keep(Available maximum note or minimum note).");

        public static readonly LocalisableString ComparisonStyle_Label = new EzLocalisableString("比较样式", "Comparison Style");

        public static readonly LocalisableString ComparisonStyle_Description = new EzLocalisableString("1: 当此行的音符数量>=上一行和下一行时处理一行。2: 当此行的音符数量<=上一行和下一行时处理一行",
            "1: Dispose a line when this line's note quantity >= Last&Next line. 2: Dispose a line when this line's note quantity <= Last&Next line.");

        public static readonly LocalisableString NoteAdjustLine_Label = new EzLocalisableString("线", "Line");

        public static readonly LocalisableString NoteAdjustLine_Description = new EzLocalisableString("取决于这张图的难度（0推荐用于Jack，1推荐用于（Jump/Hand/Etc.）Stream，2推荐用于Speed）",
            "Depending on how heavy about this map(0 is recommended for Jack,  1 is recommended for (Jump/Hand/Etc.)Stream, 2 is recommended for Speed).");

        public static readonly LocalisableString Step_Label = new EzLocalisableString("步长", "Step");
        public static readonly LocalisableString Step_Description = new EzLocalisableString("在一行上成功转换时跳过\"Step\"行", "Skip \"Step\" line when converting successfully on a line.");
        public static readonly LocalisableString IgnoreComparison_Label = new EzLocalisableString("忽略比较", "Ignore Comparison");
        public static readonly LocalisableString IgnoreComparison_Description = new EzLocalisableString("忽略比较条件", "Ignore condition of Comparison.");
        public static readonly LocalisableString IgnoreInterval_Label = new EzLocalisableString("忽略间隔", "Ignore Interval");
        public static readonly LocalisableString IgnoreInterval_Description = new EzLocalisableString("忽略音符间隔", "Ignore interval of note.");

        // LNLongShortAddition
        public static readonly LocalisableString LongShortPercent_Label = new EzLocalisableString("长/短百分比", "Long / Short %");
        public static readonly LocalisableString LongShortPercent_Description = new EzLocalisableString("形状", "The Shape");

        // LNDoubleDistribution
        public static readonly LocalisableString Divide1_Label = new EzLocalisableString("分割1", "Divide 1");
        public static readonly LocalisableString Divide1_Description = new EzLocalisableString("使用1/?", "Use 1/?");
        public static readonly LocalisableString Divide2_Label = new EzLocalisableString("分割2", "Divide 2");
        public static readonly LocalisableString Divide2_Description = new EzLocalisableString("使用1/?", "Use 1/?");
        public static readonly LocalisableString Mu1_Label = new EzLocalisableString("μ1", "Mu 1");
        public static readonly LocalisableString Mu1_Description = new EzLocalisableString("分布中的μ（百分比）", "Mu in distribution (Percentage).");
        public static readonly LocalisableString Mu2_Label = new EzLocalisableString("μ2", "Mu 2");
        public static readonly LocalisableString Mu2_Description = new EzLocalisableString("分布中的μ（百分比）", "Mu in distribution (Percentage).");
        public static readonly LocalisableString MuRatio_Label = new EzLocalisableString("μ1/μ2", "Mu 1 / Mu 2");
        public static readonly LocalisableString MuRatio_Description = new EzLocalisableString("百分比", "Percentage");
        public static readonly LocalisableString SigmaInteger_Label = new EzLocalisableString("σ整数部分", "Sigma Integer Part");
        public static readonly LocalisableString SigmaInteger_Description = new EzLocalisableString("σ除数（不是σ）", "Sigma Divisor (not sigma).");
        public static readonly LocalisableString SigmaDecimal_Label = new EzLocalisableString("σ小数部分", "Sigma Decimal Part");
        public static readonly LocalisableString SigmaDecimal_Description = new EzLocalisableString("σ除数（不是σ）", "Sigma Divisor (not sigma).");
    }
}
