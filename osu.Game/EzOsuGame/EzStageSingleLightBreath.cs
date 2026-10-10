// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osuTK;

namespace osu.Game.EzOsuGame
{
    // 解包里没有这两张光的动画曲线。只有一帧时按当前拍长呼吸：两拍一个来回，拍点上最亮。
    internal partial class EzStageSingleLightBreath : CompositeDrawable
    {
        private const float breath_low_alpha = 0.35f;

        private readonly Sprite light;

        private IBeatmap beatmap = null!;

        public EzStageSingleLightBreath(Sprite light)
        {
            this.light = light;

            Anchor = light.Anchor;
            Origin = light.Origin;
            Position = light.Position;
            Size = light.Size;

            light.Anchor = Anchor.Centre;
            light.Origin = Anchor.Centre;
            light.Position = Vector2.Zero;
            InternalChild = light;
        }

        [BackgroundDependencyLoader]
        private void load(IBeatmap beatmap)
        {
            this.beatmap = beatmap;
        }

        protected override void Update()
        {
            base.Update();

            if (beatmap.ControlPointInfo.TimingPoints.Count == 0)
                return;

            TimingControlPoint timing = beatmap.ControlPointInfo.TimingPointAt(Clock.CurrentTime);

            if (!(timing.BeatLength > 0))
                return;

            // 舞台挂在游玩时钟上，CurrentTime 已经包含变速，这里按谱面拍长对齐即可。
            double beats = (Clock.CurrentTime - timing.Time) / timing.BeatLength;
            double wrapped = (beats / 2) % 1;

            if (wrapped < 0)
                wrapped += 1;

            float wave = (float)(0.5 + 0.5 * Math.Cos(wrapped * (Math.PI * 2)));
            light.Alpha = breath_low_alpha + (1 - breath_low_alpha) * wave;
        }
    }
}
