// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using osu.Framework.Localisation;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.LAsEzExtensions.Mods
{
    public class EzModStrings : EzLocalizationManager
    {
        static EzModStrings()
        {
            // 使用反射为未设置英文的属性自动生成英文（属性名替换_为空格）
            var fields = typeof(EzModStrings).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

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

        public static readonly LocalisableString LoopPlayClip_Description = new EzLocalisableString("将谱面切割成片段用于循环练习。",
            "Cut the beatmap into a clip for loop practice. (The original is YuLiangSSS's Duplicate Mod)");

        // SpaceBody
        public static readonly LocalisableString SpaceBody_Description = new EzLocalisableString("全LN面海，可调面缝", "Full LN, adjustable gaps");
        public static readonly LocalisableString SpaceBody_Label = new EzLocalisableString("全反键缝隙", "Space Body");
        public static readonly LocalisableString SpaceBodyGap_Description = new EzLocalisableString("调整前后两个面之间的间隔缝隙", "Full LN, adjustable gaps");
        public static readonly LocalisableString AddShield_Label = new EzLocalisableString("添加盾型", "Add Shield");
        public static readonly LocalisableString AddShield_Description = new EzLocalisableString("将每个面尾添加盾牌键型", "Add shield notes in the sea");

#region LoopPlayClip

        public static readonly LocalisableString LoopCount_Label = new EzLocalisableString("循环次数", "Loop Count");
        public static readonly LocalisableString LoopCount_Description = new EzLocalisableString("切片循环次数", "Loop Clip Count.");
        public static readonly LocalisableString SpeedChange_Label = new EzLocalisableString("改变倍速", "Speed Change");
        public static readonly LocalisableString SpeedChange_Description = new EzLocalisableString("改变倍速。不允许叠加其他变速mod。", "Speed Change. The actual decrease to apply. Don't add other rate-mod.");
        public static readonly LocalisableString AdjustPitch_Label = new EzLocalisableString("调整音调", "Adjust pitch");
        public static readonly LocalisableString AdjustPitch_Description = new EzLocalisableString("速度改变时是否调整音调。（变速又变调）", "Adjust pitch. Should pitch be adjusted with speed.(变速又变调)");
        public static readonly LocalisableString ConstantSpeed_Label = new EzLocalisableString("无SV变速", "Constant Speed");
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
        public static readonly LocalisableString BreakTime_Description = new EzLocalisableString("设置两个切片循环之间的休息时间（以四分之一拍为单位，范围 1-12，默认 4）", "Set the break between clip loops as multiples of 1/4 beat (1-12, default 4).");
        public static readonly LocalisableString Random_Label = new EzLocalisableString("随机", "Random");
        public static readonly LocalisableString Random_Description = new EzLocalisableString("在切片每次重复时进行随机", "Random. Do a Random on every duplicate.");
        public static readonly LocalisableString Mirror_Label = new EzLocalisableString("镜像", "Mirror");
        public static readonly LocalisableString Mirror_Description = new EzLocalisableString("在切片每次重复时进行镜像", "Mirror. Mirror next part.");
        public static readonly LocalisableString MirrorTime_Label = new EzLocalisableString("镜像时间", "Mirror Time");
        public static readonly LocalisableString MirrorTime_Description = new EzLocalisableString("每隔多少次循环做一次镜像", "Mirror Time. Every next time part will be mirrored.");
        public static readonly LocalisableString Seed_Label = new EzLocalisableString("种子", "Seed");
        public static readonly LocalisableString Seed_Description = new EzLocalisableString("使用自定义种子而不是随机种子", "Seed. Use a custom seed instead of a random one");

        public static readonly LocalisableString InfiniteLoop_Label = new EzLocalisableString("无限循环", "Infinite Loop");

        public static readonly LocalisableString InfiniteLoop_Description = new EzLocalisableString("启用无限循环播放。游戏中必须使用Esc退出才能结束，无法获得成绩结算。",
            "Infinite Loop. Enable infinite loop playback. You must use Esc to exit in the game to end, and you cannot get score settlement.");

#endregion

#region NiceBPM

        public static readonly LocalisableString NiceBPM_Description = new EzLocalisableString("自由调整BPM或速度", "Free BPM or Speed");
        public static readonly LocalisableString InitialRate_Label = new EzLocalisableString("初始速度倍率", "Initial rate");
        public static readonly LocalisableString InitialRate_Description = new EzLocalisableString("调整初始播放速度倍率", "Initial rate. The starting speed of the track");

        public static readonly LocalisableString FreeBPM_Label = new EzLocalisableString("初始BPM", "Initial BPM");
        public static readonly LocalisableString FreeBPM_Description = new EzLocalisableString("设置BPM值以调整初始播放速度", "BPM to speed");

        public static readonly LocalisableString EnableDynamicBPM_Label = new EzLocalisableString("启用动态BPM", "Enable Dynamic BPM");
        public static readonly LocalisableString EnableDynamicBPM_Description = new EzLocalisableString("基于表现启用动态BPM调整", "Enable dynamic BPM adjustment based on performance");

        public static readonly LocalisableString MinAllowableRate_Label = new EzLocalisableString("最小允许速率", "Min Allowable Rate");
        public static readonly LocalisableString MinAllowableRate_Description = new EzLocalisableString("动态BPM调整的最小速率", "Minimum rate for dynamic BPM adjustment");

        public static readonly LocalisableString MaxAllowableRate_Label = new EzLocalisableString("最大允许速率", "Max Allowable Rate");
        public static readonly LocalisableString MaxAllowableRate_Description = new EzLocalisableString("动态BPM调整的最大速率", "Maximum rate for dynamic BPM adjustment");

        public static readonly LocalisableString MissCountThreshold_Label = new EzLocalisableString("Miss计数阈值", "Miss Count Threshold");
        public static readonly LocalisableString MissCountThreshold_Description = new EzLocalisableString("触发降速所需的Miss数量", "Number of misses required to trigger rate decrease");

        public static readonly LocalisableString RateChangeOnMiss_Label = new EzLocalisableString("Miss时的速率变化", "Rate Change On Miss");
        public static readonly LocalisableString RateChangeOnMiss_Description = new EzLocalisableString("达到Miss阈值时应用的速率倍数", "Rate multiplier applied when miss threshold is reached");

#endregion

#region Reconcile

        public static readonly LocalisableString Reconcile_Description = new EzLocalisableString("满足条件时暂停，可选回溯到上一个目标位置。",
            "Pause when conditions are met, optionally rewinding to the previous target position.");

        public static readonly LocalisableString Reconcile_EnableMiss_Label = new EzLocalisableString("启用判定计数", "Enable judgement count");
        public static readonly LocalisableString Reconcile_EnableMiss_Description = new EzLocalisableString("当指定判定累计到阈值时触发", "Trigger when the selected judgement reaches the threshold.");
        public static readonly LocalisableString Reconcile_MissJudgement_Label = new EzLocalisableString("判定类型", "Judgement Type");
        public static readonly LocalisableString Reconcile_MissJudgement_Description = new EzLocalisableString("选择要计数的判定类型", "Select the judgement to count.");
        public static readonly LocalisableString Reconcile_MissCount_Label = new EzLocalisableString("判定计数阈值", "Judgement Count Threshold");
        public static readonly LocalisableString Reconcile_MissCount_Description = new EzLocalisableString("达到该数量时触发暂停", "Trigger pause when this count is reached.");

        public static readonly LocalisableString Reconcile_EnableAcc_Label = new EzLocalisableString("启用Acc条件", "Enable accuracy condition");
        public static readonly LocalisableString Reconcile_EnableAcc_Description = new EzLocalisableString("当Acc低于阈值时触发", "Trigger when accuracy falls below the threshold.");
        public static readonly LocalisableString Reconcile_AccThreshold_Label = new EzLocalisableString("Acc阈值(%)", "Accuracy Threshold (%)");
        public static readonly LocalisableString Reconcile_AccThreshold_Description = new EzLocalisableString("低于此Acc触发暂停", "Trigger pause when accuracy is below this value.");

        public static readonly LocalisableString Reconcile_EnableHealth_Label = new EzLocalisableString("启用血量条件", "Enable health condition");
        public static readonly LocalisableString Reconcile_EnableHealth_Description = new EzLocalisableString("当血量低于阈值时触发", "Trigger when health falls below the threshold.");
        public static readonly LocalisableString Reconcile_HealthThreshold_Label = new EzLocalisableString("血量阈值(%)", "Health Threshold (%)");
        public static readonly LocalisableString Reconcile_HealthThreshold_Description = new EzLocalisableString("低于此血量触发暂停", "Trigger pause when health is below this value.");

        public static readonly LocalisableString Reconcile_RewindEnabled_Label = new EzLocalisableString("启用回溯", "Enable rewind");

        public static readonly LocalisableString Reconcile_RewindEnabled_Description = new EzLocalisableString(
            "触发后回溯到目标位置再暂停。规则："
            + "\n判定回溯到阈值的2/3处；"
            + "\nAcc回溯到阈值+(100-阈值)/3；"
            + "\n血量回溯到阈值+(100-阈值)*0.8。",
            "Rewind to the target position before pausing. Rules: "
            + "\nJudgement rewinds to 2/3 of the threshold; "
            + "\nAcc rewinds to threshold+(100-threshold)/3; "
            + "\nHealth rewinds to threshold+(100-threshold)*0.8.");

#endregion
    }
}
