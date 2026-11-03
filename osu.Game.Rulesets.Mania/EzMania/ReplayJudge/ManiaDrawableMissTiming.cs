// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Drawable 被动 miss 的 stored TimeOffset，与 Session <c>ResolveMissStoredOffset</c> 对齐。
    /// </summary>
    internal static class ManiaDrawableMissTiming
    {
        internal static double ResolveStoredOffset(DrawableHitObject drawable)
        {
            ManiaJudgeHotPathTrace.RecordMissStoredOffsetResolve();

            if (drawable.HitObject is not IHasColumn)
                return drawable.Time.Current - drawable.HitObject.GetEndTime();

            var column = drawable.FindClosestParent<Column>();

            if (column == null)
                return drawable.Time.Current - drawable.HitObject.GetEndTime();

            return ManiaReplaySessionSimulator.ResolveMissStoredOffset(drawable.HitObject, column.PressTimes);
        }
    }
}
