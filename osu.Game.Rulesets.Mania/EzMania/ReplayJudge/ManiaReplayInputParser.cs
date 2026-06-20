// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Replays;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    public static class ManiaReplayInputParser
    {
        /// <summary>
        /// 将 replay 帧序列解析为有序输入事件流。
        /// 排序规则（对齐 DrawableManiaRuleset 现场帧处理序）：
        ///   1. 时间升序
        ///   2. 同时刻：release 优先于 press（与 Drawable <c>HandleInputStateChange</c> 一致）
        ///   3. 同类型同时刻：按列索引升序（确定性排序，替代不稳定 HashSet 迭代）
        /// </summary>
        public static List<ManiaReplayInputEvent> Parse(Replay replay)
        {
            var frames = replay.Frames.OfType<ManiaReplayFrame>().OrderBy(f => f.Time).ToList();
            var inputEvents = new List<ManiaReplayInputEvent>(frames.Count * 2);

            // 使用 List 保留 Actions 录制顺序，避免 HashSet 迭代的非确定性
            var lastActions = new List<ManiaAction>();

            foreach (var frame in frames)
            {
                var current = frame.Actions.ToList();

                // 检测新按下（current 有但 lastActions 无）
                foreach (var action in current)
                {
                    if (lastActions.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, true));
                }

                // 检测释放（lastActions 有但 current 无）
                foreach (var action in lastActions)
                {
                    if (current.Contains(action))
                        continue;

                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(frame.Time, column, false));
                }

                lastActions = current;
            }

            // 末尾未释放的键补 Release 事件
            if (lastActions.Count > 0)
            {
                double endTime = frames[^1].Time;

                foreach (var action in lastActions)
                {
                    int column = (int)action;
                    if (column >= 0)
                        inputEvents.Add(new ManiaReplayInputEvent(endTime, column, false));
                }
            }

            // 排序：时间升序 → release 优先 → 列索引升序
            inputEvents.Sort((a, b) =>
            {
                int timeComparison = a.Time.CompareTo(b.Time);
                if (timeComparison != 0)
                    return timeComparison;

                // 对齐 Drawable 路径：release 优先于 press
                //   Drawable: HandleInputStateChange → foreach released → foreach pressed
                if (a.IsPress != b.IsPress)
                    return a.IsPress ? 1 : -1;

                // 同类型同时刻：按列索引升序（确定性）
                return a.Column.CompareTo(b.Column);
            });

            return inputEvents;
        }
    }
}
