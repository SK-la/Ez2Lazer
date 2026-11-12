// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Wiki;

namespace osu.Game.Overlays
{
    public partial class WikiOverlay : OnlineOverlay<WikiHeader>
    {
        public const string INDEX_PATH = @"Main_page";

        public string CurrentPath => Path.Value;

        protected readonly Bindable<string> Path = new Bindable<string>(INDEX_PATH);
        protected readonly Bindable<APIWikiPage?> WikiData = new Bindable<APIWikiPage?>();
        private readonly IBindable<Language> language = new Bindable<Language>();

        [Resolved]
        private IAPIProvider apiProvider { get; set; } = null!;

        [Resolved]
        private OsuGameBase gameBase { get; set; } = null!;

        protected IAPIProvider APIProvider => apiProvider;

        protected OsuGameBase Game => gameBase;

        private GetWikiRequest? request;
        protected CancellationTokenSource? CancellationToken;
        protected CompositeDrawable? ArticlePage;

        private bool displayUpdateRequired = true;

        public WikiOverlay()
            : base(OverlayColourScheme.Orange, false)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Path.BindValueChanged(onPathChanged);
            WikiData.BindTo(Header.WikiPageData);

            language.BindTo(Game.CurrentLanguage);
            language.BindValueChanged(onLangChanged);
        }

        public virtual void ShowPage(string pagePath = INDEX_PATH)
        {
            Path.Value = pagePath.Trim('/');
            Show();
        }

        protected override WikiHeader CreateHeader() => CreateWikiHeader();

        protected virtual WikiHeader CreateWikiHeader() => new WikiHeader
        {
            ShowIndexPage = () => ShowPage(),
            ShowParentPage = ShowParentPage,
        };

        protected override void PopIn()
        {
            base.PopIn();

            if (displayUpdateRequired)
            {
                Path.TriggerChange();
                displayUpdateRequired = false;
            }
        }

        protected override void PopOutComplete()
        {
            base.PopOutComplete();
            displayUpdateRequired = true;
        }

        protected void LoadDisplay(Drawable display)
        {
            ScrollFlow.ScrollToStart();
            LoadComponentAsync(display, loaded =>
            {
                Child = loaded;
                Loading.Hide();
            }, (CancellationToken = new CancellationTokenSource()).Token);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            switch (ArticlePage)
            {
                case WikiArticlePage official:
                    official.SidebarContainer.Height = DrawHeight;
                    official.SidebarContainer.Y = (float)Math.Clamp(ScrollFlow.Current - Header.DrawHeight, 0, Math.Max(ScrollFlow.ScrollContent.DrawHeight - DrawHeight - Header.DrawHeight, 0));
                    break;

                case EzOsuGame.Wiki.EzWikiArticlePage ez:
                    ez.SidebarContainer.Height = DrawHeight;
                    ez.SidebarContainer.Y = (float)Math.Clamp(ScrollFlow.Current - Header.DrawHeight, 0, Math.Max(ScrollFlow.ScrollContent.DrawHeight - DrawHeight - Header.DrawHeight, 0));
                    break;
            }
        }

        private void onPathChanged(ValueChangedEvent<string> e)
        {
            // the path could change as a result of redirecting to a newer location of the same page.
            // we already have the correct wiki data, so we can safely return here.
            if (e.NewValue == WikiData.Value?.Path)
                return;

            if (e.NewValue == "error")
                return;

            loadPage(e.NewValue, language.Value);
        }

        private void onLangChanged(ValueChangedEvent<Language> e)
        {
            // Path unmodified, just reload the page with new language value.
            loadPage(Path.Value, e.NewValue);
        }

        protected virtual void FetchWikiPage(string path, Language lang)
        {
            CancellationToken?.Cancel();
            request?.Cancel();

            // Language code + path, or just path1 + path2 in case
            string[] values = path.Split('/', 2);

            if (values.Length > 1 && LanguageExtensions.TryParseCultureCode(values[0], out var parsedLang))
                request = new GetWikiRequest(values[1], parsedLang);
            else
                request = new GetWikiRequest(path, lang);

            Loading.Show();

            request.Success += response => Schedule(() => OnWikiFetchSuccess(response));
            request.Failure += ex =>
            {
                if (ex is not OperationCanceledException)
                    Schedule(() => OnWikiFetchFailure(request.Path));
            };

            APIProvider.PerformAsync(request);
        }

        private void loadPage(string path, Language lang) => FetchWikiPage(path, lang);

        protected virtual void OnWikiFetchSuccess(APIWikiPage response)
        {
            WikiData.Value = response;
            Path.Value = response.Path;

            if (ShouldUseMainPageLayout(response))
            {
                LoadDisplay(new WikiMainPage
                {
                    Markdown = response.Markdown,
                    Padding = new MarginPadding
                    {
                        Vertical = 20,
                        Horizontal = HORIZONTAL_PADDING,
                    },
                });
            }
            else
            {
                LoadDisplay(ArticlePage = CreateArticlePage(GetArticleDocumentBase(Path.Value), response.Markdown));
            }
        }

        protected virtual bool ShouldUseMainPageLayout(APIWikiPage response) =>
            response.Layout.Equals(INDEX_PATH, StringComparison.OrdinalIgnoreCase);

        protected virtual CompositeDrawable CreateArticlePage(string documentBase, string markdown) =>
            new WikiArticlePage(documentBase, markdown);

        protected virtual string GetArticleDocumentBase(string wikiPath) =>
            $@"{APIProvider.Endpoints.WebsiteUrl}/wiki/{wikiPath}/";

        protected virtual void OnWikiFetchFailure(string originalPath)
        {
            WikiData.Value = null;
            Path.Value = "error";

            LoadDisplay(ArticlePage = CreateArticlePage(GetArticleDocumentBase(string.Empty),
                $"Something went wrong when trying to fetch page \"{originalPath}\".\n\n[Return to the main page](./{INDEX_PATH})."));
        }

        protected void ShowParentPage()
        {
            string parentPath = string.Join("/", Path.Value.Split('/').SkipLast(1));
            ShowPage(parentPath);
        }

        protected override void Dispose(bool isDisposing)
        {
            CancellationToken?.Cancel();
            request?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
