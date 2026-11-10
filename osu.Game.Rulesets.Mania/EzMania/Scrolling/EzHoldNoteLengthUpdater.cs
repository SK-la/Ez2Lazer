// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Mania.EzMania.Scrolling
{
    /// <summary>
    /// 变速滚动下长条的头尾处在曲线的不同位置，长度必须每帧按两端绝对位置重算。
    /// 挂在列内最后一个容器，更新时机晚于滚动容器。
    /// </summary>
    /// <remarks>
    /// 滚动容器只在对象首次布局时算一次几何：长条的 <c>Height</c> 用 <c>LengthAtTime</c>，
    /// 嵌套的尾标再用"以头部为原点"的静态偏移定位。两者都等价于把这段时长放在判定线上量像素，
    /// 匀速下平移不变所以处处正确，开启速度补偿后只剩判定线一处正确。这里改用当前时刻分别取两端位置，
    /// 让长度和尾标都按真实几何走。
    /// </remarks>
    internal partial class EzHoldNoteLengthUpdater : Drawable
    {
        /// <summary>
        /// 小于该像素变化不写，避免曲线平缓处每帧失效布局。
        /// </summary>
        private const float write_threshold = 0.5f;

        private readonly Column column;

        public EzHoldNoteLengthUpdater(Column column)
        {
            this.column = column;

            RelativeSizeAxes = Axes.Both;
        }

        protected override void Update()
        {
            base.Update();

            // 没有速度补偿在跑时长度恒定，交给滚动容器的首次布局即可。
            if (column.ScrollingInfo.Algorithm.Value is not EzSpeedCompensationScrollAlgorithm)
                return;

            var container = column.HitObjectContainer;

            if (!container.IsLoaded)
                return;

            foreach (var entry in container.AliveEntries)
            {
                if (entry.Value is not DrawableHoldNote hold)
                    continue;

                if (hold.HitObject is not IHasDuration duration)
                    continue;

                float headY = container.PositionAtTime(hold.HitObject.StartTime);
                float tailY = container.PositionAtTime(duration.EndTime);
                float length = MathF.Abs(tailY - headY);

                if (MathF.Abs(hold.Height - length) >= write_threshold)
                    hold.Height = length;

                // 尾标是嵌套对象，Y 相对头部。容器给的是"尾端贴着判定线"的静态偏移，
                // 不逐帧同步的话它会按最慢速度匀速下落，跑进长条中间，直到触碰判定线才与末端重合。
                if (hold.Tail != null)
                {
                    float tailOffset = tailY - headY;

                    if (MathF.Abs(hold.Tail.Y - tailOffset) >= write_threshold)
                        hold.Tail.Y = tailOffset;
                }
            }
        }
    }
}
