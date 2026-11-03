// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public partial class StaticFullMapPreviewRenderer : CompositeDrawable, IManiaStaticPreviewRenderer
    {
        private readonly Container scaleContainer;
        private readonly Container content;
        private readonly ManiaPreviewBatchDrawable batchDrawable;

        private ManiaPreviewData data;
        private List<ManiaPreviewLayoutEntry> layoutEntries = new List<ManiaPreviewLayoutEntry>();
        private ManiaPreviewColumnLayout layout;

        private int totalRows = 1;
        private bool hasData;
        private float lastWidth;
        private float lastHeight;
        private float density = 1f;

        private EzPreviewManiaSettings? settings;

        public StaticFullMapPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;

            InternalChild = scaleContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Child = content = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.None,
                    Child = batchDrawable = new ManiaPreviewBatchDrawable
                    {
                        RelativeSizeAxes = Axes.None,
                    }
                }
            };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (hasData && (DrawWidth != lastWidth || DrawHeight != lastHeight))

                rebuild();
        }

        public void SetData(ManiaPreviewData data)
        {
            this.data = data;

            hasData = true;
            rebuild();
        }

        /// <summary>
        /// 设置绘制外观（部件颜色 + 厚度）。与预览屏共享同一实例时，其 bindable 变化由屏驱动重绘。
        /// 数据未就绪时只记录，等 <see cref="SetData"/> 再按新外观重建。
        /// </summary>
        public void SetSettings(EzPreviewManiaSettings settings)
        {
            this.settings = settings;

            if (hasData)
                rebuild();
        }

        public void SetCurrentTime(double time)
        {
        }

        public void SetDensity(float density)
        {
            float clamped = Math.Clamp(density, 0.1f, 5f);

            if (Math.Abs(this.density - clamped) <= 0.001f)
                return;

            this.density = clamped;
            rebuild();
        }

        public void SetZoom(bool zoomed)
        {
        }

        private void rebuild()
        {
            if (!hasData || settings == null || DrawWidth <= 1 || DrawHeight <= 1)
                return;

            lastWidth = DrawWidth;
            lastHeight = DrawHeight;

            layoutEntries = ManiaPreviewFixedLayout.BuildTimeBased(data);

            layout = ManiaPreviewColumnLayout.ForFullMapMeasureGrid(
                Math.Max(data.MsPerMeasure, data.EndTimeMs - data.StartTimeMs),
                data.MsPerMeasure,
                DrawWidth,
                DrawHeight,
                density);

            totalRows = layout.TotalGridRows;

            content.Size = new Vector2(layout.ContentWidth, layout.PanelHeight);
            batchDrawable.Size = new Vector2(layout.ContentWidth, layout.PanelHeight);

            float scaleX = layout.WidthScale > 0 ? layout.WidthScale : layout.FitScale;
            scaleContainer.Scale = new Vector2(scaleX, layout.FitScale);

            var quads = new List<PreviewQuad>(layoutEntries.Count + layout.ColumnCount * (data.TotalColumns + layout.RowsPerColumn));

            ManiaPreviewColumnRenderer.BuildColumnPanels(quads, layoutEntries, layout, data, totalRows, layout.PanelHeight, settings);

            batchDrawable.SetQuads(quads);
        }
    }
}
