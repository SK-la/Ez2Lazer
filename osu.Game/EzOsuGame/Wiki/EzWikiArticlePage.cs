// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Markdig.Syntax;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Wiki
{
    public partial class EzWikiArticlePage : CompositeDrawable
    {
        public Container SidebarContainer { get; }

        public EzWikiArticlePage(string currentPath, string markdown, string? sidebarMarkdown, Action<string> showPage, EzWikiLocalePages localePages)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            EzWikiSidebarColumn sidebar;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                },
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        SidebarContainer = new Container
                        {
                            AutoSizeAxes = Axes.X,
                            Child = sidebar = new EzWikiSidebarColumn(sidebarMarkdown, currentPath, showPage, localePages),
                        },
                        new ArticleMarkdownContainer(showPage, localePages)
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            CurrentPath = currentPath,
                            Text = markdown,
                            DocumentMargin = new MarginPadding(0),
                            DocumentPadding = new MarginPadding
                            {
                                Vertical = 20,
                                Left = 30,
                                Right = WaveOverlayContainer.HORIZONTAL_PADDING,
                            },
                            OnAddHeading = sidebar.AddEntry,
                        }
                    },
                },
            };
        }

        private partial class ArticleMarkdownContainer : EzWikiMarkdownContainer
        {
            public Action<HeadingBlock, MarkdownHeading> OnAddHeading = null!;

            public ArticleMarkdownContainer(Action<string> showPage, EzWikiLocalePages localePages)
                : base(showPage, localePages)
            {
            }

            protected override MarkdownHeading CreateHeading(HeadingBlock headingBlock)
            {
                var heading = base.CreateHeading(headingBlock);
                OnAddHeading(headingBlock, heading);
                return heading;
            }
        }
    }
}
