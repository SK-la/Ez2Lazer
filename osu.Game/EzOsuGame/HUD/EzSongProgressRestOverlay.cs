// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.Timing;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 叠在进度条主体上的休息区间：每段从淡绿渐变到淡黄。
    /// </summary>
    public partial class EzSongProgressRestOverlay : CompositeDrawable
    {
        private static readonly Colour4 rest_start_colour = Colour4.FromHex(@"A8E6A1");
        private static readonly Colour4 rest_end_colour = Colour4.FromHex(@"F5E6A3");

        public readonly BindableBool ShowRestMarkers = new BindableBool(true);

        public double StartTime { get; set; }
        public double EndTime { get; set; }

        private readonly List<(double Start, double End)> rests = new List<(double, double)>();
        private float lastLayoutWidth = -1;

        public EzSongProgressRestOverlay()
        {
            RelativeSizeAxes = Axes.X;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        public override bool HandlePositionalInput => false;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ShowRestMarkers.BindValueChanged(_ => rebuild(), true);
        }

        public void SetBreaks(IEnumerable<BreakPeriod> breaks)
        {
            rests.Clear();

            foreach (var b in breaks.Where(b => b.HasEffect))
                rests.Add((b.StartTime, b.EndTime));

            rebuild();
        }

        protected override void Update()
        {
            base.Update();

            if (Math.Abs(DrawWidth - lastLayoutWidth) >= 0.5f)
                rebuild();
        }

        private void rebuild()
        {
            ClearInternal();
            lastLayoutWidth = DrawWidth;

            if (!ShowRestMarkers.Value || DrawWidth <= 0 || EndTime <= StartTime)
                return;

            double length = EndTime - StartTime;

            foreach (var (start, end) in rests)
            {
                double clampedStart = Math.Clamp(start, StartTime, EndTime);
                double clampedEnd = Math.Clamp(end, StartTime, EndTime);

                if (clampedEnd <= clampedStart)
                    continue;

                float x = (float)((clampedStart - StartTime) / length * DrawWidth);
                float width = (float)((clampedEnd - clampedStart) / length * DrawWidth);

                if (width < 1)
                    continue;

                AddInternal(new Box
                {
                    X = x,
                    Width = width,
                    RelativeSizeAxes = Axes.Y,
                    Colour = ColourInfo.GradientHorizontal(rest_start_colour, rest_end_colour),
                    Alpha = 0.55f,
                    Blending = BlendingParameters.Additive,
                });
            }
        }
    }
}
