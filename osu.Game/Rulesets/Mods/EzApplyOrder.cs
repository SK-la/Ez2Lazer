// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// <see cref="IEzApplyOrder"/> 的唯一排序入口。
    /// </summary>
    public static class EzApplyOrder
    {
        /// <summary>
        /// 未实现 <see cref="IEzApplyOrder"/> 的 mod 使用的顺序值。负数排它之前，正数排它之后。
        /// </summary>
        public const int DEFAULT_ORDER = 0;

        /// <summary>
        /// 按 <see cref="IEzApplyOrder.ApplyOrder"/> 稳定排序；顺序相同的保持入参顺序。
        /// </summary>
        public static IEnumerable<TMod> Sort<TMod>(IEnumerable<TMod> mods)
            => mods.OrderBy(m => m is IEzApplyOrder order ? order.ApplyOrder : DEFAULT_ORDER);
    }
}
