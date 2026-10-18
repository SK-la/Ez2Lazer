// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Resolves and caches the pseudo-tail (投皮) distance for a single hold note so that the mask geometry is
    /// computed in one place instead of once per skin body piece.
    /// </summary>
    internal sealed class HoldNoteTailMaskSource
    {
        private readonly Column? column;
        private readonly DrawableManiaRuleset? ruleset;
        private readonly IEzSkinInfo? skinInfo;

        private float cachedMaskHeight = float.NaN;
        private double cachedBeatLength = double.NaN;
        private double cachedTimeRange = double.NaN;
        private float cachedScrollLength = float.NaN;

        public HoldNoteTailMaskSource(Column? column, DrawableManiaRuleset? ruleset, IEzSkinInfo? skinInfo)
        {
            this.column = column;
            this.ruleset = ruleset;
            this.skinInfo = skinInfo;
        }

        /// <summary>UI 档位。0 表示投皮关闭。</summary>
        public double Level => ManiaHoldTailMask.ResolveLevel(ruleset, skinInfo?.HoldTailMaskHeight.Value ?? 0);

        public float TailAlpha => (float)(skinInfo?.HoldTailAlpha.Value ?? 1);

        /// <summary>伪面尾是否接管 LN 第三段（需要渐变开关与档位同时开启）。</summary>
        public bool IsEnabled => (skinInfo?.ManiaLNGradientEnable.Value ?? false) && ManiaHoldTailMask.IsEnabled(Level);

        /// <summary>
        /// 伪面尾长度（像素）。开关关闭或尚未加载时返回 0。
        /// </summary>
        public float GetMaskHeight()
        {
            if (!IsEnabled || column == null)
                return 0;

            var hitObjectContainer = column.HitObjectContainer;

            if (hitObjectContainer == null)
                return 0;

            double beatLength = ManiaHoldTailMask.ResolveBeatLength(ruleset);
            double timeRange = ManiaHoldTailMask.ResolveTimeRange(column.ScrollingInfo);
            float scrollLength = hitObjectContainer.DrawHeight;

            if (float.IsNaN(cachedMaskHeight)
                || cachedBeatLength != beatLength
                || cachedTimeRange != timeRange
                || cachedScrollLength != scrollLength)
            {
                cachedBeatLength = beatLength;
                cachedTimeRange = timeRange;
                cachedScrollLength = scrollLength;
                cachedMaskHeight = ManiaHoldTailMask.GetLength(hitObjectContainer, Level, beatLength);
            }

            return cachedMaskHeight;
        }
    }
}
