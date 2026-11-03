// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 叠在进度条主体上的 miss 区间着色（与休息段同风格）；密集 miss 聚成更宽的红段。
    /// </summary>
    public partial class EzSongProgressMissOverlay : CompositeDrawable
    {
        private const float cluster_radius_px = 8;
        private const float min_segment_width = 4;

        private static readonly Colour4 miss_colour_left = Colour4.FromHex(@"FF5555");
        private static readonly Colour4 miss_colour_right = Colour4.FromHex(@"CC2222");

        public readonly BindableBool ShowMissMarkers = new BindableBool(true);
        public readonly BindableBool IsReplay = new BindableBool();

        public double StartTime { get; set; }
        public double EndTime { get; set; }

        private readonly List<double> missTimes = new List<double>();
        private float lastLayoutWidth = -1;

        public EzSongProgressMissOverlay()
        {
            RelativeSizeAxes = Axes.X;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        public override bool HandlePositionalInput => false;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ShowMissMarkers.BindValueChanged(_ => rebuild());
            IsReplay.BindValueChanged(_ => rebuild(), true);
        }

        public void SetMissTimes(IEnumerable<double> times)
        {
            missTimes.Clear();
            missTimes.AddRange(times);
            missTimes.Sort();
            rebuild();
        }

        public void ClearMisses()
        {
            if (missTimes.Count == 0)
                return;

            missTimes.Clear();
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

            if (!ShowMissMarkers.Value || !IsReplay.Value || DrawWidth <= 0 || EndTime <= StartTime || missTimes.Count == 0)
                return;

            foreach (var cluster in clusterMisses())
            {
                float xStart = timeToX(cluster.Times[0]);
                float xEnd = timeToX(cluster.Times[^1]);
                float width = Math.Max(xEnd - xStart, min_segment_width + (cluster.Count - 1) * 1.5f);
                float x = (xStart + xEnd) * 0.5f - width * 0.5f;

                x = Math.Clamp(x, 0, Math.Max(0, DrawWidth - width));

                AddInternal(new Box
                {
                    X = x,
                    Width = width,
                    RelativeSizeAxes = Axes.Y,
                    Colour = ColourInfo.GradientHorizontal(miss_colour_left, miss_colour_right),
                    Alpha = 0.65f,
                    Blending = BlendingParameters.Additive,
                });
            }
        }

        private float timeToX(double time)
        {
            double clamped = Math.Clamp(time, StartTime, EndTime);
            return (float)((clamped - StartTime) / (EndTime - StartTime) * DrawWidth);
        }

        private List<MissCluster> clusterMisses()
        {
            var clusters = new List<MissCluster>();

            foreach (double time in missTimes)
            {
                float x = timeToX(time);

                if (clusters.Count > 0 && Math.Abs(x - timeToX(clusters[^1].Times[^1])) <= cluster_radius_px)
                    clusters[^1].Times.Add(time);
                else
                    clusters.Add(new MissCluster(time));
            }

            return clusters;
        }

        private class MissCluster
        {
            public readonly List<double> Times = new List<double>();
            public int Count => Times.Count;

            public MissCluster(double firstTime) => Times.Add(firstTime);
        }
    }
}
