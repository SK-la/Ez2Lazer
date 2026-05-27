// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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

        public StaticFullMapPreviewRenderer()
        {
            RelativeSizeAxes = Axes.Both;

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
            layoutEntries = ManiaPreviewFixedLayout.Build(data);
            totalRows = ManiaPreviewFixedLayout.GetTotalRows(layoutEntries);
            hasData = true;
            rebuild();
        }

        public void SetCurrentTime(double time)
        {
        }

        public void SetDensity(float density)
        {
        }

        public void SetZoom(bool zoomed)
        {
            Scale = Vector2.One;
        }

        private void rebuild()
        {
            if (DrawWidth <= 1 || DrawHeight <= 1)
                return;

            lastWidth = DrawWidth;
            lastHeight = DrawHeight;

            layout = ManiaPreviewColumnLayout.ForFullMap(totalRows, DrawWidth, DrawHeight);

            content.Size = new Vector2(layout.ContentWidth, layout.PanelHeight);
            batchDrawable.Size = new Vector2(layout.ContentWidth, layout.PanelHeight);
            scaleContainer.Scale = new Vector2(layout.FitScale);

            var quads = new List<PreviewQuad>(layoutEntries.Count + layout.ColumnCount * (data.TotalColumns + layout.RowsPerColumn));
            ManiaPreviewColumnRenderer.BuildColumnPanels(quads, layoutEntries, layout, data, totalRows, layout.PanelHeight);

            batchDrawable.SetQuads(quads);
        }
    }
}
