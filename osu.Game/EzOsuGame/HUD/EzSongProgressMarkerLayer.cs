// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 回放悬停时展开的 miss 引线 / 圆点 / 计数（默认态由 <see cref="EzSongProgressMissOverlay"/> 做条上区间着色）。
    /// </summary>
    public partial class EzSongProgressMarkerLayer : CompositeDrawable
    {
        public const float AREA_HEIGHT = 28;

        private const float cluster_radius_px = 8;
        private const float stem_height = 16;
        private const float base_line_width = 1.2f;
        private const float base_dot_size = 4;
        private const float max_line_width = 5;
        private const float max_dot_size = 12;
        private const double expand_duration = 200;
        private const double collapse_duration = 400;

        private static readonly Colour4 miss_colour = Colour4.FromHex(@"FF3B3B");

        public readonly BindableBool ShowMissMarkers = new BindableBool(true);
        public readonly BindableBool IsReplay = new BindableBool();
        public readonly BindableBool Expanded = new BindableBool();

        public double StartTime { get; set; }
        public double EndTime { get; set; }

        private readonly List<double> missTimes = new List<double>();
        private readonly List<ProgressMarker> activeMarkers = new List<ProgressMarker>();

        private float lastLayoutWidth = -1;

        public EzSongProgressMarkerLayer()
        {
            RelativeSizeAxes = Axes.X;
            Height = AREA_HEIGHT;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        public override bool HandlePositionalInput => false;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ShowMissMarkers.BindValueChanged(_ => rebuild());
            IsReplay.BindValueChanged(_ => rebuild());
            Expanded.BindValueChanged(v => applyExpanded(v.NewValue), true);
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
            activeMarkers.Clear();
            lastLayoutWidth = DrawWidth;

            if (DrawWidth <= 0 || EndTime <= StartTime)
                return;

            if (!ShowMissMarkers.Value || !IsReplay.Value || missTimes.Count == 0)
                return;

            foreach (var cluster in clusterMisses())
                addMarker(cluster.X, cluster.Count, cluster.Count > 1 ? cluster.Count.ToString() : null);

            applyExpanded(Expanded.Value, immediate: true);
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

            foreach (var c in clusters)
                c.X = timeToX(c.Times.Average());

            return clusters;
        }

        private void addMarker(float x, int count, string? label)
        {
            float lineWidth = Math.Min(base_line_width + (count - 1) * 0.75f, max_line_width);
            float dotSize = Math.Min(base_dot_size + (count - 1) * 1.5f, max_dot_size);

            var marker = new ProgressMarker(miss_colour, lineWidth, dotSize, label)
            {
                X = x,
            };

            activeMarkers.Add(marker);
            AddInternal(marker);
        }

        private void applyExpanded(bool expanded, bool immediate = false)
        {
            foreach (var marker in activeMarkers)
                marker.SetExpanded(expanded, immediate);
        }

        private partial class ProgressMarker : Container
        {
            private readonly Box stem;
            private readonly Circle dot;
            private readonly OsuSpriteText? labelText;

            public ProgressMarker(Colour4 colour, float lineWidth, float dotSize, string? label)
            {
                Origin = Anchor.BottomCentre;
                Anchor = Anchor.BottomLeft;
                Height = AREA_HEIGHT;
                Width = Math.Max(dotSize, lineWidth) + 2;

                Children = new Drawable[]
                {
                    stem = new Box
                    {
                        Name = "Stem",
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Width = lineWidth,
                        Height = stem_height,
                        Colour = colour,
                        Alpha = 0,
                    },
                    dot = new Circle
                    {
                        Name = "Dot",
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(dotSize),
                        Colour = colour,
                        Y = -stem_height,
                        Alpha = 0,
                    },
                };

                if (label != null)
                {
                    Add(labelText = new OsuSpriteText
                    {
                        Text = label,
                        Font = OsuFont.Torus.With(size: 11, weight: FontWeight.Bold),
                        Colour = colour,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Y = -(stem_height + dotSize * 0.5f + 1),
                        Alpha = 0,
                    });
                }
            }

            public void SetExpanded(bool expanded, bool immediate)
            {
                double duration = immediate ? 0 : (expanded ? expand_duration : collapse_duration);
                var easing = expanded ? Easing.Out : Easing.OutQuint;

                float target = expanded ? 1 : 0;
                stem.FadeTo(target, duration, easing);
                dot.FadeTo(target, duration, easing);
                labelText?.FadeTo(target, duration, easing);
            }
        }

        private class MissCluster
        {
            public readonly List<double> Times = new List<double>();
            public float X;

            public int Count => Times.Count;

            public MissCluster(double firstTime)
            {
                Times.Add(firstTime);
            }
        }
    }
}
