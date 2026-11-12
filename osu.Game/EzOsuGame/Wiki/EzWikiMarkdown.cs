// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Markdig.Syntax.Inlines;
using osu.Framework.Allocation;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Online;
using osu.Game.Overlays.Wiki.Markdown;

namespace osu.Game.EzOsuGame.Wiki
{
    public partial class EzWikiMarkdownContainer : WikiMarkdownContainer
    {
        private readonly Action<string> showPage;
        private readonly EzWikiLocalePages localePages;

        public EzWikiMarkdownContainer(Action<string> showPage, EzWikiLocalePages localePages)
        {
            this.showPage = showPage;
            this.localePages = localePages;
        }

        public override OsuMarkdownTextFlowContainer CreateTextFlow() =>
            new EzWikiMarkdownTextFlowContainer(showPage, localePages);

        private partial class EzWikiMarkdownTextFlowContainer : OsuMarkdownTextFlowContainer
        {
            private readonly Action<string> showPage;
            private readonly EzWikiLocalePages localePages;

            [Resolved(canBeNull: true)]
            private ILinkHandler? linkHandler { get; set; }

            public EzWikiMarkdownTextFlowContainer(Action<string> showPage, EzWikiLocalePages localePages)
            {
                this.showPage = showPage;
                this.localePages = localePages;
            }

            protected override void AddLinkText(string text, LinkInline linkInline) =>
                AddDrawable(new EzWikiMarkdownLinkText(text, linkInline, showPage, localePages, linkHandler));

            protected override void AddImage(LinkInline linkInline) =>
                AddDrawable(new EzWikiMarkdownImage(linkInline));
        }
    }

    public partial class EzWikiMarkdownLinkText : OsuMarkdownLinkText
    {
        private readonly Action<string> showPage;
        private readonly EzWikiLocalePages localePages;
        private readonly ILinkHandler? linkHandler;

        public EzWikiMarkdownLinkText(string text, LinkInline linkInline, Action<string> showPage, EzWikiLocalePages localePages, ILinkHandler? linkHandler)
            : base(text, linkInline)
        {
            this.showPage = showPage;
            this.localePages = localePages;
            this.linkHandler = linkHandler;
        }

        protected override void OnLinkPressed()
        {
            if (EzWikiLinkParser.TryGetInGamePath(Url, localePages, out string path))
            {
                showPage(path);
                return;
            }

            linkHandler?.HandleLink(Url);
        }
    }

    public partial class EzWikiMarkdownImage : WikiMarkdownImage
    {
        public EzWikiMarkdownImage(LinkInline linkInline)
            : base(linkInline)
        {
        }

        protected override ImageContainer CreateImageContainer(string url)
        {
            url = resolveWikiImageUrl(url);
            return base.CreateImageContainer(url);
        }

        private static string resolveWikiImageUrl(string url)
        {
            if (url.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;

            if (url.StartsWith("./images/", StringComparison.Ordinal))
                return $"{EzWikiPaths.RAW_WIKI_BASE}images/{url["./images/".Length..]}";

            if (url.StartsWith("images/", StringComparison.Ordinal))
                return $"{EzWikiPaths.RAW_WIKI_BASE}{url}";

            return url;
        }
    }
}
