// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public class ManiaModKrrDPRefiner : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder, IHasSeed
    {
        public override string Name => "Krr DP Refiner";

        public override string Acronym => "DRe";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => KrrDPRefinerStrings.DRE_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Stream;

        public override ModType Type => ModType.LA_Mod;
        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(KrrDPRefinerStrings), nameof(KrrDPRefinerStrings.TIME_WINDOW_LEVEL_LABEL), nameof(KrrDPRefinerStrings.TIME_WINDOW_LEVEL_DESCRIPTION))]
        public BindableNumber<int> TimeWindowLevel { get; } = new BindableInt(3)
        {
            MinValue = 1,
            MaxValue = 5
        };

        [SettingSource(typeof(KrrDPRefinerStrings), nameof(KrrDPRefinerStrings.PROCESS_LEVEL_LABEL), nameof(KrrDPRefinerStrings.PROCESS_LEVEL_DESCRIPTION))]
        public BindableNumber<int> ProcessLevel { get; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 6
        };

        [SettingSource(typeof(KrrDPRefinerStrings), nameof(KrrDPRefinerStrings.MAX_HARD_FILL_LABEL), nameof(KrrDPRefinerStrings.MAX_HARD_FILL_DESCRIPTION))]
        public BindableNumber<int> MaxHardFillPerRow { get; } = new BindableInt(1)
        {
            MinValue = 0,
            MaxValue = 2
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        // 禁止选择的硬约束时间窗口（毫秒）
        private const float forbidden_window_ms = 60f;

        // ProcessLevel 映射表：用于快速查找 startPriority 和 threshold
        // 索引对应 ProcessLevel.Value (0-6)，值对应该等级下的映射结果

        /// <summary>
        /// 有列冲突时的 startPriority 映射
        /// 索引: ProcessLevel (0-6)
        /// 值: startPriority (从哪个LV开始选排列)
        /// 例如: ProcessLevel=0 → startPriority=7 (卡手模式，从LV7开始选)
        ///       ProcessLevel=6 → startPriority=1 (完全重排，从LV1开始选)
        /// </summary>
        private static readonly int[] conflict_start_priority_map = { 7, 6, 5, 4, 3, 2, 1 };

        /// <summary>
        /// 无列冲突时的 threshold 映射
        /// 索引: ProcessLevel (0-6)
        /// 值: threshold (currentLv > threshold 时才重排)
        /// 特殊值: -1 表示 ProcessLevel=6 (完全重排，无条件重排)
        /// 例如: ProcessLevel=0 → threshold=7 (仅LV8重排)
        ///       ProcessLevel=5 → threshold=2 (LV3-8重排)
        /// </summary>
        private static readonly int[] no_conflict_threshold_map = { 7, 6, 5, 4, 3, 2, -1 };

        /// <summary>
        /// 获取时间窗口的描述文本
        /// </summary>
        private LocalisableString getTimeWindowDescription(int level)
        {
            return level switch
            {
                1 => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_1,
                2 => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_2,
                3 => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_3,
                4 => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_4,
                5 => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_5,
                _ => KrrDPRefinerStrings.TIME_WINDOW_LEVEL_3
            };
        }

        /// <summary>
        /// 将等级转换为N值
        /// </summary>
        private double getNValue(int level)
        {
            return level switch
            {
                1 => 4.0,
                2 => 3.0,
                3 => 2.0,
                4 => 1.5,
                5 => 1.0,
                _ => 2.0 // 默认
            };
        }

        /// <summary>
        /// 数字列转换为实际列下标
        /// </summary>
        /// <param name="digit">数字列 1-7</param>
        /// <param name="isLeftHand">是否为左手</param>
        /// <param name="leftColumn">区域起始列下标</param>
        /// <returns>实际列下标</returns>
        private int digitToActualColumn(int digit, bool isLeftHand, int leftColumn)
        {
            if (isLeftHand)
            {
                // 左手：数字7→最左侧，数字1→最右侧
                return leftColumn + (7 - digit);
            }
            else
            {
                // 右手：数字1→最左侧，数字7→最右侧
                return leftColumn + (digit - 1);
            }
        }

        /// <summary>
        /// 实际列下标转换为数字列
        /// </summary>
        /// <param name="actualColumn">实际列下标</param>
        /// <param name="isLeftHand">是否为左手</param>
        /// <param name="leftColumn">区域起始列下标</param>
        /// <returns>数字列 1-7</returns>
        private int actualColumnToDigit(int actualColumn, bool isLeftHand, int leftColumn)
        {
            int offset = actualColumn - leftColumn;

            if (isLeftHand)
            {
                // 左手：下标0→数字7，下标6→数字1
                return 7 - offset;
            }
            else
            {
                // 右手：下标0→数字1，下标6→数字7
                return offset + 1;
            }
        }

        /// <summary>
        /// 获取排列的优先级等级（LV）
        /// </summary>
        /// <param name="digits">去重排序后的数字列列表</param>
        /// <param name="threshold">阈值，如果超过此阈值可提前返回（传0表示不启用提前退出）</param>
        /// <returns>优先级等级 1-8（1最高，8最低），如果找不到返回8</returns>
        private int getPermutationLevel(List<int> digits, int threshold = 0)
        {
            int n = digits.Count;
            if (n == 0 || n > 5) return 8;

            // 使用 TryGetValue 避免双重查找
            if (!permutation_dict.TryGetValue(n, out var lvDict)) return 8;

            // 获取该n值的最大LV等级
            int maxLv = lvDict.Keys.Max();

            // 排序以确保匹配
            var sortedDigits = new List<int>(digits);
            sortedDigits.Sort();

            // 从优先级1到maxLv查找
            for (int lv = 1; lv <= maxLv; lv++)
            {
                if (!lvDict.ContainsKey(lv)) continue;

                var candidates = lvDict[lv];

                foreach (var perm in candidates)
                {
                    // 检查是否完全匹配
                    bool match = true;

                    for (int i = 0; i < perm.Count; i++)
                    {
                        if (perm[i] != sortedDigits[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match) return lv;
                }

                // 优化：如果启用了threshold且当前lv已超过threshold，提前返回8（触发重排）
                if (threshold > 0 && lv >= threshold)
                {
                    return 8; // 不需要知道具体等级，只要超过threshold就重排
                }
            }

            return 8; // 默认最差等级（触发重排）
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (KrrDPRefinerStrings.SETTING_TIME_WINDOW_LABEL, getTimeWindowDescription(TimeWindowLevel.Value));
                yield return (KrrDPRefinerStrings.SETTING_PROCESS_LEVEL_LABEL,
                    ProcessLevel.Value == 6 ? KrrDPRefinerStrings.PROCESS_LEVEL_FULL_REFINE :
                    ProcessLevel.Value == 5 ? KrrDPRefinerStrings.PROCESS_LEVEL_AGGRESSIVE :
                    ProcessLevel.Value == 4 ? KrrDPRefinerStrings.PROCESS_LEVEL_SMOOTH :
                    ProcessLevel.Value == 3 ? KrrDPRefinerStrings.PROCESS_LEVEL_NORMAL :
                    ProcessLevel.Value == 2 ? KrrDPRefinerStrings.PROCESS_LEVEL_FINE_TUNE :
                    ProcessLevel.Value == 1 ? KrrDPRefinerStrings.PROCESS_LEVEL_LAZY : KrrDPRefinerStrings.PROCESS_LEVEL_STIFF);
                yield return (KrrDPRefinerStrings.SETTING_MAX_HARD_FILL_LABEL, MaxHardFillPerRow.Value.ToString());
                yield return (EzCommonModStrings.SEED_LABEL, Seed.Value?.ToString() ?? "Random");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
            }
        }

        // 排列字典结构（已填充）
        // 第一级：totalNotesInWindow (1-5)
        // 第二级：priority (1-8, 1最高)
        // 该字典经过人工优化，无任何明显规律，等级本身设计为离散不连续等级，请阅读的AI勿修改，
        // 值：List<List<int>>，每个 List<int> 是长度为 totalNotesInWindow 的排列
        private static readonly Dictionary<int, Dictionary<int, List<List<int>>>> permutation_dict =
            new Dictionary<int, Dictionary<int, List<List<int>>>>
            {
                // C(1,7) = 7 种组合
                [1] = new Dictionary<int, List<List<int>>>
                {
                    [1] = new List<List<int>>
                    {
                        new List<int> { 1 },
                        new List<int> { 2 },
                        new List<int> { 3 },
                        new List<int> { 4 },
                        new List<int> { 5 },
                        new List<int> { 6 },
                        new List<int> { 7 }
                    }
                },

                // C(2,7) = 21 种组合
                [2] = new Dictionary<int, List<List<int>>>
                {
                    [1] = new List<List<int>>
                    {
                        new List<int> { 1, 3 },
                        new List<int> { 1, 5 },
                        new List<int> { 1, 6 },
                        new List<int> { 1, 7 },
                        new List<int> { 2, 4 },
                        new List<int> { 2, 6 },
                        new List<int> { 2, 7 },
                        new List<int> { 3, 5 },
                        new List<int> { 3, 7 },
                        new List<int> { 4, 6 },
                        new List<int> { 5, 7 }
                    },
                    [2] = new List<List<int>>
                    {
                        new List<int> { 1, 4 },
                        new List<int> { 2, 5 },
                        new List<int> { 3, 6 },
                        new List<int> { 4, 7 },
                    },
                    [3] = new List<List<int>>
                    {
                        new List<int> { 1, 2 },
                        new List<int> { 3, 4 },
                        new List<int> { 4, 5 },
                        new List<int> { 6, 7 }
                    },
                    [4] = new List<List<int>>
                    {
                        new List<int> { 2, 3 }
                    },
                    [5] = new List<List<int>>
                    {
                        new List<int> { 5, 6 }
                    }
                },

                // C(3,7) = 35 种组合
                [3] = new Dictionary<int, List<List<int>>>
                {
                    [1] = new List<List<int>>
                    {
                        new List<int> { 1, 3, 5 },
                        new List<int> { 1, 3, 7 },
                        new List<int> { 1, 5, 7 },
                        new List<int> { 2, 4, 6 },
                        new List<int> { 3, 5, 7 }
                    },
                    [2] = new List<List<int>>
                    {
                        new List<int> { 1, 3, 6 },
                        new List<int> { 1, 4, 6 },
                        new List<int> { 1, 4, 7 },
                        new List<int> { 2, 4, 7 },
                        new List<int> { 2, 5, 7 }
                    },
                    [4] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4 },
                        new List<int> { 1, 2, 5 },
                        new List<int> { 1, 2, 6 },
                        new List<int> { 1, 2, 7 },
                        new List<int> { 1, 3, 4 },
                        new List<int> { 1, 4, 5 },
                        new List<int> { 1, 6, 7 },
                        new List<int> { 2, 4, 5 },
                        new List<int> { 2, 6, 7 },
                        new List<int> { 3, 4, 6 },
                        new List<int> { 3, 4, 7 },
                        new List<int> { 4, 5, 7 },
                        new List<int> { 4, 6, 7 }
                    },
                    [5] = new List<List<int>>
                    {
                        new List<int> { 2, 3, 5 },
                        new List<int> { 2, 3, 7 },
                        new List<int> { 3, 6, 7 }
                    },
                    [6] = new List<List<int>>
                    {
                        new List<int> { 1, 5, 6 },
                        new List<int> { 2, 3, 6 },
                        new List<int> { 3, 4, 5 },
                        new List<int> { 3, 5, 6 }
                    },
                    [7] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3 },
                        new List<int> { 2, 3, 4 },
                        new List<int> { 2, 5, 6 },
                        new List<int> { 4, 5, 6 },
                        new List<int> { 5, 6, 7 }
                    }
                },

                // C(4,7) = 35 种组合
                [4] = new Dictionary<int, List<List<int>>>
                {
                    [1] = new List<List<int>>
                    {
                        new List<int> { 1, 3, 5, 7 }
                    },
                    [4] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4, 6 },
                        new List<int> { 1, 3, 4, 6 },
                        new List<int> { 2, 4, 5, 7 },
                        new List<int> { 2, 4, 6, 7 }
                    },
                    [5] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4, 7 },
                        new List<int> { 1, 2, 5, 7 },
                        new List<int> { 1, 3, 4, 7 },
                        new List<int> { 1, 3, 6, 7 },
                        new List<int> { 1, 4, 5, 7 },
                        new List<int> { 1, 4, 6, 7 }
                    },
                    [6] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4, 5 },
                        new List<int> { 1, 2, 6, 7 },
                        new List<int> { 1, 3, 4, 5 },
                        new List<int> { 1, 3, 5, 6 },
                        new List<int> { 2, 3, 5, 7 },
                        new List<int> { 3, 4, 5, 7 },
                        new List<int> { 3, 4, 6, 7 }
                    },
                    [7] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3, 5 },
                        new List<int> { 1, 2, 3, 6 },
                        new List<int> { 1, 2, 3, 7 },
                        new List<int> { 1, 5, 6, 7 },
                        new List<int> { 2, 3, 4, 6 },
                        new List<int> { 2, 3, 4, 7 },
                        new List<int> { 2, 3, 6, 7 },
                        new List<int> { 2, 4, 5, 6 },
                        new List<int> { 3, 5, 6, 7 }
                    },
                    [8] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3, 4 },
                        new List<int> { 1, 2, 5, 6 },
                        new List<int> { 1, 4, 5, 6 },
                        new List<int> { 2, 3, 4, 5 },
                        new List<int> { 2, 3, 5, 6 },
                        new List<int> { 2, 5, 6, 7 },
                        new List<int> { 3, 4, 5, 6 },
                        new List<int> { 4, 5, 6, 7 }
                    }
                },

                // C(5,7) = 21 种组合
                [5] = new Dictionary<int, List<List<int>>>
                {
                    [1] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4, 6, 7 },
                    },
                    [5] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 4, 5, 7 },
                        new List<int> { 1, 3, 4, 5, 7 },
                        new List<int> { 1, 3, 4, 6, 7 }
                    },
                    [6] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3, 5, 7 }
                    },
                    [7] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3, 4, 6 },
                        new List<int> { 1, 3, 5, 6, 7 },
                        new List<int> { 2, 3, 4, 5, 7 },
                        new List<int> { 2, 3, 4, 6, 7 }
                    },
                    [8] = new List<List<int>>
                    {
                        new List<int> { 1, 2, 3, 4, 5 },
                        new List<int> { 1, 2, 3, 4, 7 },
                        new List<int> { 1, 2, 3, 5, 6 },
                        new List<int> { 1, 2, 3, 6, 7 },
                        new List<int> { 1, 2, 4, 5, 6 },
                        new List<int> { 1, 2, 5, 6, 7 },
                        new List<int> { 1, 3, 4, 5, 6 },
                        new List<int> { 1, 4, 5, 6, 7 },
                        new List<int> { 2, 3, 4, 5, 6 },
                        new List<int> { 2, 3, 5, 6, 7 },
                        new List<int> { 2, 4, 5, 6, 7 },
                        new List<int> { 3, 4, 5, 6, 7 }
                    }
                }
            };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            try
            {
                var maniaBeatmap = (ManiaBeatmap)beatmap;
                int totalColumns = (int)maniaBeatmap.Difficulty.CircleSize;

                // 只处理14k和16k
                if (totalColumns != 14 && totalColumns != 16)
                {
                    return;
                }

                // 初始化随机数生成器
                Seed.Value ??= RNG.Next();
                var random = new Random(Seed.Value.Value);

                int timeWindowLevel = TimeWindowLevel.Value;
                double timeWindowN = getNValue(timeWindowLevel);

                // 根据谱面类型确定处理的区域
                if (totalColumns == 14)
                {
                    processRegion(maniaBeatmap.HitObjects, 0, 6, timeWindowN, random, isLeftHand: true, controlPoints: beatmap.ControlPointInfo); // 左手：下标0~6
                    processRegion(maniaBeatmap.HitObjects, 7, 13, timeWindowN, random, isLeftHand: false, controlPoints: beatmap.ControlPointInfo); // 右手：下标7~13
                }
                else // 16k
                {
                    processRegion(maniaBeatmap.HitObjects, 1, 7, timeWindowN, random, isLeftHand: true, controlPoints: beatmap.ControlPointInfo); // 左手：下标1~7（忽略0）
                    processRegion(maniaBeatmap.HitObjects, 8, 14, timeWindowN, random, isLeftHand: false, controlPoints: beatmap.ControlPointInfo); // 右手：下标8~14（忽略15）
                }

                // 清理所有被标记为删除的Note（Column = -1），避免难度计算器访问越界
                var notesToRemove = maniaBeatmap.HitObjects.Where(h => h.Column == -1).ToList();

                foreach (var note in notesToRemove)
                {
                    maniaBeatmap.HitObjects.Remove(note);
                }
            }
            catch
            {
                // 静默失败，避免影响游戏体验
            }
        }

        /// <summary>
        /// 处理连续7个轨道的DP规整（新算法）
        /// </summary>
        private void processRegion(IList<ManiaHitObject> hitObjects, int leftColumn, int rightColumn, double timeWindowN, Random random, bool isLeftHand, ControlPointInfo controlPoints)
        {
            // 步骤1：提取当前区域的Note并按StartTime排序
            var regionNotes = hitObjects
                              .OfType<ManiaHitObject>()
                              .Where(h => h.Column >= leftColumn && h.Column <= rightColumn)
                              .OrderBy(n => n.StartTime)
                              .ToList();

            if (regionNotes.Count == 0)
                return;

            // 步骤2：按StartTime分组
            var timeGroups = regionNotes.GroupBy(n => n.StartTime).OrderBy(g => g.Key).ToList();

            // 初始化 lastUsedTime 数组（索引0-6对应数字列1-7）
            float[] lastUsedTime = new float[7];
            for (int i = 0; i < 7; i++)
                lastUsedTime[i] = -999999f;

            // 滑动窗口索引：记录上一次扫描的起始位置，避免重复遍历
            int windowScanIndex = 0;

            // 步骤3：逐行处理
            foreach (var group in timeGroups)
            {
                float currentTime = (float)group.Key;
                var notesInRow = group.ToList();
                int originalCount = notesInRow.Count;

                // 计算当前行的时间窗口 T = beatLength / N + 16ms
                var timingPoint = controlPoints.TimingPointAt(currentTime);
                float beatLength = (float)timingPoint.BeatLength;

                // 限制最大 BeatLength 为 1000ms（对应最低 60 BPM），避免低 BPM 时窗口过大
                if (beatLength > 1000f)
                    beatLength = 1000f;

                float timeWindowMs = beatLength / (float)timeWindowN + 16f;

                // 如果原始行超过5个Note，删除整行并重排
                if (originalCount > 5)
                {
                    // 删除当前行所有Note
                    for (int i = 0; i < originalCount; i++)
                    {
                        notesInRow[i].Column = -1;
                    }

                    // 重置为需要重排的状态
                    originalCount = 0;
                }

                // 3.1 收集Tl：T窗口内之前行的数字列（去重，不包含当前行）
                // 使用滑动窗口优化：从 windowScanIndex 开始扫描，而不是每次都从头开始
                var tl = new HashSet<int>();
                float windowStart = currentTime - timeWindowMs;

                // 先移除超出窗口的旧数据（如果需要维护更精确的状态，可以在这里处理）
                // 由于 tl 是每行重新构建的 HashSet，只需要从上次位置继续扫描即可

                for (int i = windowScanIndex; i < regionNotes.Count; i++)
                {
                    var note = regionNotes[i];

                    // 如果已经到达当前行或之后，停止扫描
                    if (note.StartTime >= currentTime)
                        break;

                    // 检查是否在时间窗口内
                    if (note.StartTime >= windowStart)
                    {
                        if (note.Column != -1) // 跳过已删除的Note
                        {
                            int digit = actualColumnToDigit(note.Column, isLeftHand, leftColumn);

                            if (digit >= 1 && digit <= 7)
                            {
                                tl.Add(digit);
                            }
                        }
                    }
                    else
                    {
                        // 更新扫描起始位置：跳过已超出窗口的 Note
                        windowScanIndex = i + 1;
                    }
                }

                // 3.2 收集Cl：当前行的数字列（去重）
                var cl = new HashSet<int>();

                foreach (var note in notesInRow)
                {
                    if (note.Column != -1) // 跳过已删除的Note
                    {
                        int digit = actualColumnToDigit(note.Column, isLeftHand, leftColumn);

                        if (digit >= 1 && digit <= 7)
                        {
                            cl.Add(digit);
                        }
                    }
                }

                // 组合窗口内所有数字列（用于评估当前排列等级）
                // 使用 SortedSet 自动保持有序，避免后续调用 Sort()
                // 虽然 SortedSet 插入是 O(log N)，但对于 N≤5 的小集合，总体性能优于 HashSet + Sort()
                // 原因：Sort() 的常数因子较大，且需要额外的内存分配和拷贝
                var allDigitsInWindow = new SortedSet<int>(tl);
                allDigitsInWindow.UnionWith(cl);
                var sortedAllDigits = allDigitsInWindow.ToList(); // 已有序，无需 Sort()

                // 检查是否有列冲突（Cl中的数字是否在Tl中已存在）
                bool hasColumnConflict = false;

                foreach (int digit in cl)
                {
                    if (tl.Contains(digit))
                    {
                        hasColumnConflict = true;
                        break;
                    }
                }

                // 根据ProcessLevel判断是否需要重排
                // 注意：ProcessLevel是滑条设置（0-6），与字典的LV等级（1-8）是两个不同的概念
                // ProcessLevel决定threshold阈值，currentLv是字典中查找到的排列等级
                bool shouldRefine = false;
                int startPriority = 1; // 重排时从哪个优先级开始选（往低数字查找）
                int threshold = 0; // 用于优化getPermutationLevel的提前退出

                if (hasColumnConflict)
                {
                    // 有列冲突，强制重排，根据 ProcessLevel 从映射表获取 startPriority
                    shouldRefine = true;
                    startPriority = conflict_start_priority_map[ProcessLevel.Value];
                }
                else
                {
                    // 无列冲突时，根据 ProcessLevel 和 currentLv 决定是否重排
                    // currentLv 是字典中的二级键值（1-8，不连续），表示当前排列的难度等级
                    // ProcessLevel 决定 threshold 阈值：currentLv > threshold 时才重排

                    int processLevel = ProcessLevel.Value;

                    if (processLevel == 6)
                    {
                        // 完全重排：无论 currentLv 是多少，都强制重排
                        shouldRefine = true;
                        startPriority = 1;
                    }
                    else
                    {
                        // 从映射表获取 threshold
                        threshold = no_conflict_threshold_map[processLevel];

                        // 获取当前排列的 LV 等级，传入 threshold 以启用提前退出优化
                        int currentLv = getPermutationLevel(sortedAllDigits, threshold);

                        if (currentLv > threshold)
                        {
                            shouldRefine = true;
                            // startPriority 等于 threshold（与昨天旧代码一致）
                            startPriority = threshold;
                        }
                    }
                }

                // 如果不重排，保持原样但更新lastUsedTime
                if (!shouldRefine)
                {
                    foreach (var note in notesInRow)
                    {
                        if (note.Column == -1) continue;

                        int digit = actualColumnToDigit(note.Column, isLeftHand, leftColumn);

                        if (note is HoldNote hold)
                        {
                            lastUsedTime[digit - 1] = (float)hold.EndTime + 100f;
                        }
                        else
                        {
                            lastUsedTime[digit - 1] = currentTime;
                        }
                    }

                    continue; // 跳过重排流程
                }

                // === 以下为重排流程 ===

                // 如果需要重排，先删除当前行所有Note
                if (originalCount > 0)
                {
                    for (int i = 0; i < notesInRow.Count; i++)
                    {
                        notesInRow[i].Column = -1;
                    }

                    originalCount = notesInRow.Count; // 重新计算原始数量
                }

                // 3.3 计算可填入数量n和硬塞数量o（直接使用Tl，无需重新扫描）
                int m = tl.Count;
                int n, o;

                if (m + originalCount > 5)
                {
                    n = 5 - m;
                    o = originalCount - n;
                }
                else
                {
                    n = originalCount;
                    o = 0;
                }

                // 如果n <= 0，说明窗口内已满，所有Note都硬塞或删除
                if (n <= 0)
                {
                    n = 0;
                    o = originalCount;
                }

                // 3.4 查找排列字典
                List<int> selectedPerm = null;
                int totalNotesInWindow = m + n;

                if (n > 0 && permutation_dict.TryGetValue(totalNotesInWindow, out var nDict))
                {
                    // 收集当前优先级的所有可用排列
                    var allAvailablePerms = new List<List<int>>();

                    for (int priority = startPriority; priority >= 1; priority--)
                    {
                        if (!nDict.TryGetValue(priority, out var candidates))
                            continue;

                        // 筛选出包含所有Tl中数字的排列
                        foreach (var perm in candidates)
                        {
                            bool containsAllExisting = true;

                            foreach (int digit in tl)
                            {
                                if (!perm.Contains(digit))
                                {
                                    containsAllExisting = false;
                                    break;
                                }
                            }

                            if (containsAllExisting)
                            {
                                allAvailablePerms.Add(perm);
                            }
                        }

                        // 如果当前优先级有可用排列，立即停止搜索
                        if (allAvailablePerms.Count > 0)
                        {
                            break;
                        }
                    }

                    // 从所有可用排列中随机选择一个
                    if (allAvailablePerms.Count > 0)
                    {
                        selectedPerm = allAvailablePerms[random.Next(allAvailablePerms.Count)];
                    }
                }

                // 如果找不到合法排列，将所有Note转为硬塞处理
                var digitsToFill = new List<int>();

                if (selectedPerm == null)
                {
                    n = 0;
                    o = originalCount;
                }
                else
                {
                    // 3.5 从selectedPerm中提取不在Tl中的数字（按顺序）
                    foreach (int digit in selectedPerm)
                    {
                        if (!tl.Contains(digit))
                        {
                            digitsToFill.Add(digit);
                        }
                    }

                    // 3.6 填入前n个Note
                    for (int i = 0; i < n && i < digitsToFill.Count; i++)
                    {
                        int digit = digitsToFill[i];
                        int actualCol = digitToActualColumn(digit, isLeftHand, leftColumn);
                        notesInRow[i].Column = actualCol;

                        // 立即更新lastUsedTime
                        if (notesInRow[i] is HoldNote hold)
                        {
                            lastUsedTime[digit - 1] = (float)hold.EndTime + 100f;
                        }
                        else
                        {
                            lastUsedTime[digit - 1] = currentTime;
                        }
                    }
                }

                // 3.7 处理硬塞的o个Note（包括找不到合法排列的情况）
                int hardFillCount = 0; // 当前行已硬塞的数量
                int maxHardFill = MaxHardFillPerRow.Value; // 每行最大硬塞数量

                // 如果设置为0，禁止硬塞，直接删除所有需要硬塞的Note
                if (maxHardFill == 0)
                {
                    for (int i = n; i < notesInRow.Count; i++)
                    {
                        notesInRow[i].Column = -1;
                    }
                }
                else
                {
                    for (int i = n; i < n + o && i < notesInRow.Count; i++)
                    {
                        // 检查是否已达到硬塞上限
                        if (hardFillCount >= maxHardFill)
                        {
                            // 超出上限，删除剩余Note
                            for (int j = i; j < notesInRow.Count; j++)
                            {
                                notesInRow[j].Column = -1;
                            }

                            break;
                        }

                        // 计算邻居空闲时间（用于评估区域空闲度，增加随机性）
                        float[] neighborIdleTime = new float[7];
                        const float max_idle_time = 600f; // 最大空闲时间上限

                        // 边界轨道：2个轨道平均
                        float avg0 = (lastUsedTime[0] + lastUsedTime[1]) / 2f;
                        neighborIdleTime[0] = Math.Min(currentTime - avg0, max_idle_time);

                        float avg6 = (lastUsedTime[5] + lastUsedTime[6]) / 2f;
                        neighborIdleTime[6] = Math.Min(currentTime - avg6, max_idle_time);

                        // 中间轨道：3个轨道平均
                        for (int j = 1; j <= 5; j++)
                        {
                            float avg = (lastUsedTime[j - 1] + lastUsedTime[j] + lastUsedTime[j + 1]) / 3f;
                            neighborIdleTime[j] = Math.Min(currentTime - avg, max_idle_time);
                        }

                        // 收集所有可用列
                        var hardFillAvailableCols = new List<(int digit, float idleTime, float timeDiff)>();

                        for (int digit = 1; digit <= 7; digit++)
                        {
                            float timeDiff = currentTime - lastUsedTime[digit - 1];

                            // 检查1：不在60ms禁止区（使用原始lastUsedTime判断）
                            if (timeDiff <= forbidden_window_ms)
                                continue;

                            // 检查2：不在当前行已使用的数字列中
                            if (digitsToFill.Contains(digit))
                                continue;

                            // 检查3：不在Tl中（避免与时间窗口内之前行的Note同列）
                            if (tl.Contains(digit))
                                continue;

                            // 添加可用列，记录邻居空闲时间和timeDiff
                            hardFillAvailableCols.Add((digit, neighborIdleTime[digit - 1], timeDiff));
                        }

                        if (hardFillAvailableCols.Count == 0)
                        {
                            // 没有可用列，删除剩余Note
                            for (int j = i; j < notesInRow.Count; j++)
                            {
                                notesInRow[j].Column = -1;
                            }

                            break;
                        }

                        // 选择邻居空闲时间最大的列（最空闲的区域， capped at 600ms增加随机性）
                        float maxIdle = -1f;
                        var tiedIndices = new List<int>();

                        for (int j = 0; j < hardFillAvailableCols.Count; j++)
                        {
                            if (hardFillAvailableCols[j].idleTime > maxIdle)
                            {
                                maxIdle = hardFillAvailableCols[j].idleTime;
                                tiedIndices.Clear();
                                tiedIndices.Add(j);
                            }
                            else if (Math.Abs(hardFillAvailableCols[j].idleTime - maxIdle) < 0.001f)
                            {
                                tiedIndices.Add(j);
                            }
                        }

                        // 若有多个列空闲时间相同（都达到600ms上限），随机选择
                        int selectedIndex = tiedIndices[random.Next(tiedIndices.Count)];
                        int selectedDigit = hardFillAvailableCols[selectedIndex].digit;

                        // 填入Note
                        int actualCol = digitToActualColumn(selectedDigit, isLeftHand, leftColumn);
                        notesInRow[i].Column = actualCol;

                        // 立即更新lastUsedTime
                        if (notesInRow[i] is HoldNote hold)
                        {
                            lastUsedTime[selectedDigit - 1] = (float)hold.EndTime + 100f;
                        }
                        else
                        {
                            lastUsedTime[selectedDigit - 1] = currentTime;
                        }

                        // 加入已使用列表，避免后续硬塞重复选择
                        digitsToFill.Add(selectedDigit);

                        // 硬塞计数+1
                        hardFillCount++;
                    }
                }
            }
        }
    }

    public static class KrrDPRefinerStrings
    {
        // MOD 描述
        public static readonly LocalisableString DRE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "优化14k/16k谱面的DP（单手7键）排列，避免同指冲突和困难指法。通过智能重排Note到更舒适的列，提升DP的流畅度。",
            "Optimize DP (single-hand 7-key) patterns in 14k/16k beatmaps to avoid same-finger conflicts and difficult fingerings. Intelligently reorders notes to more comfortable columns for smoother DP gameplay.");

        // 时间窗口设置
        public static readonly LocalisableString TIME_WINDOW_LEVEL_LABEL = new EzLocalizationManager.EzLocalisableString(
            "时间窗口(1/N节拍)",
            "Time Window (1/N Beat)");

        public static readonly LocalisableString TIME_WINDOW_LEVEL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "等级1=1/4节拍, 2=1/3节拍, 3=1/2节拍(默认), 4=1/1.5节拍, 5=1/1节拍",
            "Level 1=1/4 beat, 2=1/3 beat, 3=1/2 beat (default), 4=1/1.5 beat, 5=1/1 beat");

        // 时间窗口等级描述
        public static readonly LocalisableString TIME_WINDOW_LEVEL_1 = new EzLocalizationManager.EzLocalisableString("1/4节拍", "1/4 beat");
        public static readonly LocalisableString TIME_WINDOW_LEVEL_2 = new EzLocalizationManager.EzLocalisableString("1/3节拍", "1/3 beat");
        public static readonly LocalisableString TIME_WINDOW_LEVEL_3 = new EzLocalizationManager.EzLocalisableString("1/2节拍", "1/2 beat");
        public static readonly LocalisableString TIME_WINDOW_LEVEL_4 = new EzLocalizationManager.EzLocalisableString("1/1.5节拍", "1/1.5 beat");
        public static readonly LocalisableString TIME_WINDOW_LEVEL_5 = new EzLocalizationManager.EzLocalisableString("1/1节拍", "1/1 beat");

        // 处理等级设置
        public static readonly LocalisableString PROCESS_LEVEL_LABEL = new EzLocalizationManager.EzLocalisableString(
            "处理等级",
            "Process Level");

        public static readonly LocalisableString PROCESS_LEVEL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "0=卡手, 1=懒惰, 2=微调, 3=普通, 4=顺手, 5=激进, 6=完全重排",
            "0=Stiff, 1=Lazy, 2=Fine-tune, 3=Normal, 4=Smooth, 5=Aggressive, 6=Full Refine");

        // 硬塞数量设置
        public static readonly LocalisableString MAX_HARD_FILL_LABEL = new EzLocalizationManager.EzLocalisableString(
            "可硬塞数量",
            "Max Hard Fill");

        public static readonly LocalisableString MAX_HARD_FILL_DESCRIPTION = new EzLocalizationManager.EzLocalisableString(
            "每行最多硬塞的Note数量：0=禁止硬塞, 1=最多1个(默认), 2=最多2个",
            "Maximum hard-filled notes per row: 0=Disable hard fill, 1=Max 1 (default), 2=Max 2");

        // SettingDescription 中使用的标签和值
        public static readonly LocalisableString SETTING_TIME_WINDOW_LABEL = new EzLocalizationManager.EzLocalisableString("时间窗口", "Time Window");

        public static readonly LocalisableString SETTING_PROCESS_LEVEL_LABEL = new EzLocalizationManager.EzLocalisableString("处理等级", "Process Level");
        public static readonly LocalisableString PROCESS_LEVEL_STIFF = new EzLocalizationManager.EzLocalisableString("卡手", "Stiff");
        public static readonly LocalisableString PROCESS_LEVEL_LAZY = new EzLocalizationManager.EzLocalisableString("懒惰", "Lazy");
        public static readonly LocalisableString PROCESS_LEVEL_FINE_TUNE = new EzLocalizationManager.EzLocalisableString("微调", "Fine-tune");
        public static readonly LocalisableString PROCESS_LEVEL_NORMAL = new EzLocalizationManager.EzLocalisableString("普通", "Normal");
        public static readonly LocalisableString PROCESS_LEVEL_SMOOTH = new EzLocalizationManager.EzLocalisableString("顺手", "Smooth");
        public static readonly LocalisableString PROCESS_LEVEL_AGGRESSIVE = new EzLocalizationManager.EzLocalisableString("激进", "Aggressive");
        public static readonly LocalisableString PROCESS_LEVEL_FULL_REFINE = new EzLocalizationManager.EzLocalisableString("完全重排", "Full Refine");

        public static readonly LocalisableString SETTING_MAX_HARD_FILL_LABEL = new EzLocalizationManager.EzLocalisableString("可硬塞数量", "Max Hard Fill");
    }
}
