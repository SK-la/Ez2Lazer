// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Runtime mania playfield layout, persisted as <see cref="ManiaPlayfieldLayout"/> through the theme layout store.
    /// Shared by the playfield and by HUD elements that match the judgement line layout.
    /// </summary>
    public class ManiaPlayfieldLayoutConfig
    {
        public const float MAX_OFFSET = 1f;

        /// <summary>
        /// Stage horizontal offset as a percentage. ±100 puts the stage flush against the corresponding screen edge.
        /// </summary>
        public BindableFloat StageX { get; } = new BindableFloat
        {
            MinValue = -100,
            MaxValue = 100,
            Precision = 0.5f,
        };

        /// <summary>
        /// Stage vertical offset as a percentage of half the screen height. Positive is up.
        /// </summary>
        public BindableFloat StageY { get; } = new BindableFloat
        {
            MinValue = -100,
            MaxValue = 100,
            Precision = 0.5f,
        };

        /// <summary>
        /// <see cref="StageX"/> as a fraction of the free space beside the stage.
        /// </summary>
        public float StageXFraction => StageX.Value / percent_scale;

        /// <summary>
        /// <see cref="StageY"/> as a fraction of half the container height.
        /// </summary>
        public float StageYFraction => StageY.Value / percent_scale;

        /// <summary>
        /// Stage centre offset from the screen centre as a fraction of the screen width, written by the playfield each frame.
        /// Lets HUD elements follow the stage without knowing the playfield container size.
        /// </summary>
        public float StageOffsetFractionX { get; set; }

        private const float percent_scale = 100;

        public void ApplySaved(ManiaPlayfieldLayout saved)
        {
            StageX.Value = Math.Clamp(saved.Stage.X * percent_scale, -percent_scale * MAX_OFFSET, percent_scale * MAX_OFFSET);
            StageY.Value = Math.Clamp(saved.Stage.Y * percent_scale, -percent_scale * MAX_OFFSET, percent_scale * MAX_OFFSET);
        }
    }
}
