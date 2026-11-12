// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Wiki;

namespace osu.Game.EzOsuGame.Wiki
{
    public static class EzWikiPaths
    {
        public const string INDEX_PATH = "home";
        public const string DOCUMENT_HOST = "ez2lazer.local";
        public const string GITHUB_WIKI_EDIT_BASE = "https://github.com/SK-la/Ez2Lazer/wiki";
        public const string RAW_WIKI_BASE = "https://raw.githubusercontent.com/wiki/SK-la/Ez2Lazer/";
    }

    public sealed class EzWikiLocalePages
    {
        [JsonProperty]
        public Dictionary<string, Dictionary<string, string>> Pages { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        private readonly Dictionary<string, string> physicalToLogical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static EzWikiLocalePages FromJson(string json)
        {
            var pages = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json)
                        ?? new Dictionary<string, Dictionary<string, string>>();
            var parsed = new EzWikiLocalePages { Pages = pages };
            parsed.rebuildPhysicalIndex();
            return parsed;
        }

        public void MergeFrom(EzWikiLocalePages other)
        {
            foreach (var (key, locales) in other.Pages)
                Pages[key] = locales;

            rebuildPhysicalIndex();
        }

        private void rebuildPhysicalIndex()
        {
            physicalToLogical.Clear();

            foreach (var (logical, locales) in Pages)
            {
                foreach (string physical in locales.Values)
                {
                    if (!string.IsNullOrWhiteSpace(physical))
                        physicalToLogical[physical] = logical;
                }
            }
        }

        public string ResolvePhysical(string path, EzContentLocale locale)
        {
            string key = normalizeIncomingPath(path);
            string localeKey = locale.ToMarkdownHeader();

            if (Pages.TryGetValue(key, out var locales) && locales.TryGetValue(localeKey, out string? physical) && !string.IsNullOrWhiteSpace(physical))
                return physical;

            if (physicalToLogical.ContainsKey(key))
                return translatePhysicalToLocale(key, locale);

            return key;
        }

        public string? TryGetLogicalKey(string path)
        {
            string key = normalizeIncomingPath(path);

            if (Pages.ContainsKey(key))
                return key;

            return physicalToLogical.GetValueOrDefault(key);
        }

        public string TranslatePathForLocale(string path, EzContentLocale locale)
        {
            string? logical = TryGetLogicalKey(path);

            if (logical != null)
                return logical;

            return ResolvePhysical(path, locale);
        }

        private string translatePhysicalToLocale(string physical, EzContentLocale locale)
        {
            if (physicalToLogical.TryGetValue(physical, out string? logical)
                && Pages.TryGetValue(logical, out var locales)
                && locales.TryGetValue(locale.ToMarkdownHeader(), out string? target)
                && !string.IsNullOrWhiteSpace(target))
                return target;

            return physical;
        }

        private static string normalizeIncomingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return EzWikiPaths.INDEX_PATH;

            path = path.Trim().Trim('/');

            if (path.Equals(WikiOverlay.INDEX_PATH, StringComparison.OrdinalIgnoreCase))
                return EzWikiPaths.INDEX_PATH;

            return Uri.UnescapeDataString(path);
        }
    }

    public static class EzWikiGitHubClient
    {
        public static void ConfigureGitHubRequest(WebRequest request)
        {
            request.AddHeader("Accept", "application/vnd.github+json");

            string? token = Environment.GetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN")
                            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            if (!string.IsNullOrWhiteSpace(token))
                request.AddHeader("Authorization", $"Bearer {token.Trim()}");
        }

        public static async Task<string?> FetchTextAsync(string relativePath, CancellationToken cancellationToken)
        {
            string uri = $"{EzWikiPaths.RAW_WIKI_BASE}{relativePath}";
            var request = new OsuWebRequest(uri);
            ConfigureGitHubRequest(request);

            try
            {
                await request.PerformAsync(cancellationToken).ConfigureAwait(false);
                return request.GetResponseString();
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez wiki fetch failed for {relativePath}: {ex.Message}", level: LogLevel.Debug);
                return null;
            }
        }

        public static async Task<string?> FetchPageMarkdownAsync(string physicalPageName, CancellationToken cancellationToken)
        {
            string? body = await FetchTextAsync($"{Uri.EscapeDataString(physicalPageName)}.md", cancellationToken).ConfigureAwait(false);

            if (body != null)
                return body;

            return await FetchTextAsync($"{physicalPageName}.md", cancellationToken).ConfigureAwait(false);
        }

        public static Task<string?> FetchLocalePagesJsonAsync(CancellationToken cancellationToken) =>
            FetchTextAsync("locale-pages.json", cancellationToken);

        public static async Task<string?> FetchSidebarMarkdownAsync(EzContentLocale locale, CancellationToken cancellationToken)
        {
            string? raw = await FetchPageMarkdownAsync("_Sidebar", cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return EzWikiSidebarParser.ExtractLocaleSection(raw, locale);
        }
    }

    public sealed class EzWikiLocaleCatalogue
    {
        public EzWikiLocalePages Pages { get; private set; } = new EzWikiLocalePages();

        public async Task EnsureLoadedAsync(Storage storage, CancellationToken cancellationToken)
        {
            if (EzWikiLocalePagesCache.TryRead(storage, out string cached))
            {
                try
                {
                    Pages = EzWikiLocalePages.FromJson(cached);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Ez wiki locale-pages cache parse failed: {ex.Message}", level: LogLevel.Debug);
                }
            }

            string? remote = await EzWikiGitHubClient.FetchLocalePagesJsonAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(remote))
            {
                if (Pages.Pages.Count == 0)
                    Logger.Log("Ez wiki locale-pages unavailable (no cache); open Wiki online once.", level: LogLevel.Important);

                return;
            }

            try
            {
                Pages = EzWikiLocalePages.FromJson(remote);
                EzWikiLocalePagesCache.Write(storage, remote);
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez wiki locale-pages.json parse failed: {ex.Message}", level: LogLevel.Important);
            }
        }
    }

    public static class EzWikiLinkParser
    {
        public static bool TryGetInGamePath(string url, EzWikiLocalePages pages, out string logicalOrPhysicalPath)
        {
            logicalOrPhysicalPath = string.Empty;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.StartsWith('@'))
            {
                logicalOrPhysicalPath = url.TrimStart('@');
                return true;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            if (uri.Host.Equals(EzWikiPaths.DOCUMENT_HOST, StringComparison.OrdinalIgnoreCase))
                return tryParseWikiUri(uri, pages, out logicalOrPhysicalPath);

            if (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.Contains("/Ez2Lazer/wiki/", StringComparison.OrdinalIgnoreCase))
            {
                string segment = uri.Segments[^1].Trim('/');
                logicalOrPhysicalPath = pages.TryGetLogicalKey(segment) ?? segment;
                return true;
            }

            return false;
        }

        private static bool tryParseWikiUri(Uri uri, EzWikiLocalePages pages, out string path)
        {
            path = string.Empty;
            string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string segment = Uri.UnescapeDataString(segments[i]);

                if (segment.StartsWith('@'))
                {
                    path = segment.TrimStart('@');
                    return true;
                }

                string? logical = pages.TryGetLogicalKey(segment);

                if (logical != null)
                {
                    path = logical;
                    return true;
                }

                if (i == segments.Length - 1 && !string.IsNullOrEmpty(segment))
                {
                    path = segment;
                    return true;
                }
            }

            path = EzWikiPaths.INDEX_PATH;
            return true;
        }
    }

    public partial class EzWikiHeader : WikiHeader
    {
        protected override string GithubWikiEditBase => EzWikiPaths.GITHUB_WIKI_EDIT_BASE;

        protected override bool IsIndexPage(string wikiPath) =>
            wikiPath == EzWikiPaths.INDEX_PATH;

        protected override string GetGitHubEditPath(APIWikiPage page) =>
            string.IsNullOrEmpty(page.Subtitle)
                ? $"{GithubWikiEditBase}/{page.Path}"
                : $"{GithubWikiEditBase}/{page.Subtitle}";
    }

    public partial class EzWikiOverlay : WikiOverlay
    {
        private readonly EzWikiLocaleCatalogue localeCatalogue = new EzWikiLocaleCatalogue();
        private string? currentSidebarMarkdown;

        [Resolved]
        private Storage storage { get; set; } = null!;

        public EzWikiOverlay()
        {
            Path.Default = Path.Value = EzWikiPaths.INDEX_PATH;
        }

        public override void ShowPage(string pagePath = INDEX_PATH)
        {
            if (string.IsNullOrWhiteSpace(pagePath) || pagePath == INDEX_PATH || pagePath == WikiOverlay.INDEX_PATH)
            {
                base.ShowPage(EzWikiPaths.INDEX_PATH);
                return;
            }

            string normalized = localeCatalogue.Pages.TranslatePathForLocale(
                pagePath,
                EzContentLocaleExtensions.FromGameLanguage(Game.CurrentLanguage.Value));

            string logical = localeCatalogue.Pages.TryGetLogicalKey(normalized) ?? normalized;
            base.ShowPage(logical);
        }

        protected override WikiHeader CreateWikiHeader() => new EzWikiHeader
        {
            ShowIndexPage = () => ShowPage(EzWikiPaths.INDEX_PATH),
            ShowParentPage = ShowParentPage,
        };

        protected override void FetchWikiPage(string path, Language lang)
        {
            CancellationToken?.Cancel();
            CancellationToken = new CancellationTokenSource();

            var token = CancellationToken.Token;
            var locale = EzContentLocaleExtensions.FromGameLanguage(lang);
            Loading.Show();

            Task.Run(async () =>
            {
                try
                {
                    await localeCatalogue.EnsureLoadedAsync(storage, token).ConfigureAwait(false);

                    string logicalPath = localeCatalogue.Pages.TryGetLogicalKey(path) ?? path;
                    string physical = localeCatalogue.Pages.ResolvePhysical(logicalPath, locale);

                    Task<string?> pageTask = EzWikiGitHubClient.FetchPageMarkdownAsync(physical, token);
                    Task<string?> sidebarTask = EzWikiGitHubClient.FetchSidebarMarkdownAsync(locale, token);
                    await Task.WhenAll(pageTask, sidebarTask).ConfigureAwait(false);

                    string? markdown = await pageTask.ConfigureAwait(false);
                    string? sidebarMarkdown = await sidebarTask.ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        return;

                    if (!string.IsNullOrWhiteSpace(sidebarMarkdown))
                        sidebarMarkdown = EzWikiMarkdownRewrite.RewritePhysicalLinks(sidebarMarkdown, localeCatalogue.Pages);

                    if (string.IsNullOrWhiteSpace(markdown))
                    {
                        Schedule(() => OnWikiFetchFailure(path));
                        return;
                    }

                    currentSidebarMarkdown = sidebarMarkdown;
                    var response = buildPage(logicalPath, physical, markdown, locale);
                    Schedule(() =>
                    {
                        if (!token.IsCancellationRequested)
                            OnWikiFetchSuccess(response);
                    });
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    Schedule(() => OnWikiFetchFailure(path));
                }
            }, token);
        }

        protected override bool ShouldUseMainPageLayout(APIWikiPage response) => false;

        protected override CompositeDrawable CreateArticlePage(string documentBase, string markdown) =>
            new EzWikiArticlePage(documentBase, markdown, currentSidebarMarkdown, ShowPage, localeCatalogue.Pages);

        protected override string GetArticleDocumentBase(string wikiPath) =>
            $"https://{EzWikiPaths.DOCUMENT_HOST}/wiki/{Uri.EscapeDataString(wikiPath)}/";

        protected override void OnWikiFetchFailure(string originalPath)
        {
            WikiData.Value = null;
            Path.Value = "error";

            LoadDisplay(ArticlePage = CreateArticlePage(GetArticleDocumentBase(string.Empty),
                $"无法获取页面「{originalPath}」。\n\n[返回首页](./@{EzWikiPaths.INDEX_PATH})"));
        }

        private APIWikiPage buildPage(string logicalPath, string physicalPage, string markdown, EzContentLocale locale)
        {
            string title = extractTitle(markdown) ?? physicalPage;

            string rewritten = EzWikiMarkdownRewrite.RewritePhysicalLinks(markdown, localeCatalogue.Pages);

            return new APIWikiPage
            {
                Layout = logicalPath,
                Locale = locale.ToMarkdownHeader(),
                Markdown = rewritten,
                Path = logicalPath,
                Subtitle = physicalPage,
                Title = title,
            };
        }

        private static readonly Regex heading_regex = new Regex(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static string? extractTitle(string markdown)
        {
            var match = heading_regex.Match(markdown);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}
