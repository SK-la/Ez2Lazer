// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.Wiki;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.EzOsuGame.Wiki
{
    public partial class EzWikiSidebarColumn : OverlaySidebar
    {
        private WikiTableOfContents tableOfContents = null!;
        private EzWikiMarkdownContainer? navigationMarkdown;

        private readonly string? navigationMarkdownText;
        private readonly string documentBase;
        private readonly Action<string> showPage;
        private readonly EzWikiLocalePages localePages;

        public EzWikiSidebarColumn(string? navigationMarkdownText, string documentBase, Action<string> showPage, EzWikiLocalePages localePages)
        {
            this.navigationMarkdownText = navigationMarkdownText;
            this.documentBase = documentBase;
            this.showPage = showPage;
            this.localePages = localePages;
        }

        protected override Drawable CreateContent()
        {
            var flow = new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(0, 15),
            };

            if (!string.IsNullOrWhiteSpace(navigationMarkdownText))
            {
                flow.Add(navigationMarkdown = new EzWikiSidebarMarkdownContainer(showPage, localePages)
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = navigationMarkdownText,
                    CurrentPath = documentBase,
                });
            }

            flow.Add(new OsuSpriteText
            {
                Text = WikiStrings.ShowToc.ToUpper(),
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                Margin = new MarginPadding { Top = string.IsNullOrWhiteSpace(navigationMarkdownText) ? 0 : 10, Bottom = 5 },
                Alpha = 0.85f,
            });
            flow.Add(tableOfContents = new WikiTableOfContents());

            return flow;
        }

        public void AddEntry(HeadingBlock headingBlock, MarkdownHeading heading)
        {
            switch (headingBlock.Level)
            {
                case 2:
                case 3:
                    if (headingBlock.Inline != null)
                        tableOfContents.AddEntry(getTitle(headingBlock.Inline), heading, headingBlock.Level == 3);

                    break;
            }
        }

        private static string getTitle(ContainerInline containerInline)
        {
            foreach (var inline in containerInline)
            {
                switch (inline)
                {
                    case LiteralInline literalInline:
                        return literalInline.Content.ToString();

                    case LinkInline linkInline:
                        if (!linkInline.IsImage)
                            return getTitle(linkInline);

                        break;
                }
            }

            return string.Empty;
        }

        private partial class EzWikiSidebarMarkdownContainer : EzWikiMarkdownContainer
        {
            public EzWikiSidebarMarkdownContainer(Action<string> showPage, EzWikiLocalePages localePages)
                : base(showPage, localePages)
            {
                LineSpacing = 8;
            }

            public override SpriteText CreateSpriteText() => new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Regular),
            };
        }
    }
}
