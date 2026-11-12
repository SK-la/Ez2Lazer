// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Changelog;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Updater;

namespace osu.Game.EzOsuGame.Changelog
{
    public enum EzContentLocale
    {
        Zh,
        En,
    }

    public static class EzContentLocaleExtensions
    {
        public static EzContentLocale FromGameLanguage(Language language) =>
            language is Language.zh or Language.zh_hant ? EzContentLocale.Zh : EzContentLocale.En;

        public static string ToMarkdownHeader(this EzContentLocale locale) => locale == EzContentLocale.Zh ? "zh" : "en";
    }

    public sealed class EzChangelogParseResult
    {
        public string Markdown { get; init; } = string.Empty;
    }

    public static class EzChangelogParser
    {
        private const string format_marker = "<!-- ez-changelog-format: 1 -->";

        private static readonly Regex locale_heading = new Regex(@"^##\s+(zh|en)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static EzChangelogParseResult Parse(string? body, EzContentLocale locale)
        {
            if (string.IsNullOrWhiteSpace(body))
                return new EzChangelogParseResult();

            string working = stripFormatMarker(body);
            string? localeSection = extractLocaleSection(working, locale.ToMarkdownHeader());

            if (localeSection == null)
                return new EzChangelogParseResult { Markdown = body.Trim() };

            string preamble = extractPreamble(working);
            string markdown = string.IsNullOrWhiteSpace(preamble)
                ? localeSection.Trim()
                : $"{preamble.TrimEnd()}\n\n{localeSection.Trim()}";

            return new EzChangelogParseResult { Markdown = markdown };
        }

        private static string stripFormatMarker(string body)
        {
            int markerIndex = body.IndexOf(format_marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return body;

            return body[..markerIndex] + body[(markerIndex + format_marker.Length)..];
        }

        private static string extractPreamble(string markdown)
        {
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var preamble = new List<string>();

            foreach (string rawLine in lines)
            {
                if (locale_heading.IsMatch(rawLine))
                    break;

                preamble.Add(rawLine);
            }

            return string.Join('\n', preamble).Trim();
        }

        private static string? extractLocaleSection(string markdown, string localeHeader)
        {
            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            int start = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                var match = locale_heading.Match(lines[i]);
                if (!match.Success)
                    continue;

                if (string.Equals(match.Groups[1].Value, localeHeader, StringComparison.OrdinalIgnoreCase))
                {
                    start = i + 1;
                    break;
                }
            }

            if (start < 0)
                return null;

            int end = lines.Length;

            for (int i = start; i < lines.Length; i++)
            {
                var match = locale_heading.Match(lines[i]);

                if (match.Success)
                {
                    end = i;
                    break;
                }
            }

            return string.Join('\n', lines.Skip(start).Take(end - start));
        }
    }

    internal static class EzGitHubChangelogReleaseCache
    {
        private const string cache_relative_path = "EzData/github-changelog-releases.json";
        private static readonly TimeSpan fresh_ttl = TimeSpan.FromHours(6);

        private sealed class CachedFile
        {
            [JsonProperty("fetchedAt")]
            public DateTimeOffset FetchedAt { get; set; }

            [JsonProperty("releases")]
            public List<GitHubRelease> Releases { get; set; } = new List<GitHubRelease>();
        }

        public static bool TryRead(Storage storage, out GitHubRelease[] releases, out bool fresh)
        {
            releases = Array.Empty<GitHubRelease>();
            fresh = false;

            try
            {
                string path = storage.GetFullPath(cache_relative_path, createIfNotExisting: false);
                if (!File.Exists(path))
                    return false;

                var cached = JsonConvert.DeserializeObject<CachedFile>(File.ReadAllText(path));
                if (cached?.Releases == null || cached.Releases.Count == 0)
                    return false;

                releases = cached.Releases.ToArray();
                fresh = DateTimeOffset.UtcNow - cached.FetchedAt < fresh_ttl;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez changelog cache read failed: {ex.Message}", level: LogLevel.Debug);
                return false;
            }
        }

        public static void Write(Storage storage, IReadOnlyList<GitHubRelease> releases)
        {
            try
            {
                var cached = new CachedFile
                {
                    FetchedAt = DateTimeOffset.UtcNow,
                    Releases = releases.ToList(),
                };

                string path = storage.GetFullPath(cache_relative_path, createIfNotExisting: true);
                File.WriteAllText(path, JsonConvert.SerializeObject(cached));
            }
            catch (Exception ex)
            {
                Logger.Log($"Ez changelog cache write failed: {ex.Message}", level: LogLevel.Debug);
            }
        }
    }

    public sealed class EzGitHubChangelogCatalogue
    {
        private const string releases_api_base = "https://api.github.com/repos/SK-la/Ez2Lazer/releases";
        private const int releases_per_page = 10;
        /// <summary>Only the newest pages are refreshed live; older tags stay in <see cref="EzGitHubChangelogReleaseCache"/>.</summary>
        private const int dynamic_head_page_count = 2;

        private static readonly APIUpdateStream ez2lazer_stream = new APIUpdateStream
        {
            Id = 1,
            Name = "ez2lazer",
            DisplayName = "Ez2Lazer",
        };

        private readonly Dictionary<string, string> bodiesByTag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private List<APIChangelogBuild> builds = new List<APIChangelogBuild>();

        public async Task<IReadOnlyList<APIChangelogBuild>> FetchBuildsAsync(Storage? storage, CancellationToken cancellationToken = default)
        {
            GitHubRelease[] cached = Array.Empty<GitHubRelease>();
            bool fresh = false;
            bool hasCache = storage != null
                              && EzGitHubChangelogReleaseCache.TryRead(storage, out cached, out fresh)
                              && cached.Length > 0;

            if (hasCache && fresh)
                return materialise(cached);

            try
            {
                GitHubRelease[] head = await fetchHeadPagesFromGitHubAsync(cancellationToken).ConfigureAwait(false);
                var merged = mergeByTag(cached, head);

                if (storage != null)
                    EzGitHubChangelogReleaseCache.Write(storage, merged);

                return materialise(merged);
            }
            catch (Exception ex) when (hasCache)
            {
                Logger.Log($"GitHub releases API failed ({ex.Message}); using cached changelog.", level: LogLevel.Important);
                return materialise(cached);
            }
        }

        private static IReadOnlyList<GitHubRelease> mergeByTag(IReadOnlyList<GitHubRelease>? cached, IReadOnlyList<GitHubRelease> head)
        {
            var byTag = new Dictionary<string, GitHubRelease>(StringComparer.OrdinalIgnoreCase);

            if (cached != null)
            {
                foreach (var release in cached)
                {
                    if (!string.IsNullOrEmpty(release.TagName))
                        byTag[release.TagName] = release;
                }
            }

            foreach (var release in head)
            {
                if (!string.IsNullOrEmpty(release.TagName))
                    byTag[release.TagName] = release;
            }

            return byTag.Values.OrderByDescending(r => r.PublishedAt ?? DateTime.MinValue).ToList();
        }

        private static void configureGitHubRequest(OsuJsonWebRequest<GitHubRelease[]> request)
        {
            request.AddHeader("Accept", "application/vnd.github+json");

            string? token = Environment.GetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            if (!string.IsNullOrWhiteSpace(token))
                request.AddHeader("Authorization", $"Bearer {token.Trim()}");
        }

        private static async Task<GitHubRelease[]> fetchHeadPagesFromGitHubAsync(CancellationToken cancellationToken)
        {
            var byTag = new Dictionary<string, GitHubRelease>(StringComparer.OrdinalIgnoreCase);

            for (int page = 1; page <= dynamic_head_page_count; page++)
            {
                string uri = $"{releases_api_base}?per_page={releases_per_page}&page={page}";
                var request = new OsuJsonWebRequest<GitHubRelease[]>(uri);
                configureGitHubRequest(request);
                await request.PerformAsync(cancellationToken).ConfigureAwait(false);

                GitHubRelease[] batch = request.ResponseObject ?? Array.Empty<GitHubRelease>();

                if (batch.Length == 0)
                    break;

                foreach (var release in batch)
                {
                    if (!string.IsNullOrEmpty(release.TagName))
                        byTag[release.TagName] = release;
                }

                if (batch.Length < releases_per_page)
                    break;
            }

            return byTag.Values.ToArray();
        }

        private IReadOnlyList<APIChangelogBuild> materialise(IReadOnlyList<GitHubRelease> releases)
        {
            bodiesByTag.Clear();
            builds = new List<APIChangelogBuild>();

            foreach (var release in releases.OrderByDescending(r => r.PublishedAt ?? DateTime.MinValue))
            {
                if (string.IsNullOrEmpty(release.TagName))
                    continue;

                bodiesByTag[release.TagName] = release.Body ?? string.Empty;

                string versionPart = release.TagName.Contains('-')
                    ? release.TagName[..release.TagName.LastIndexOf('-')]
                    : release.TagName;

                builds.Add(new APIChangelogBuild
                {
                    Id = release.TagName.GetHashCode(),
                    Version = versionPart,
                    DisplayVersion = release.TagName,
                    CreatedAt = release.PublishedAt ?? DateTimeOffset.MinValue,
                    UpdateStream = ez2lazer_stream,
                    ChangelogEntries = new List<APIChangelogEntry>(),
                });
            }

            for (int i = 0; i < builds.Count; i++)
            {
                builds[i].Versions = new APIChangelogBuild.VersionNavigation
                {
                    Next = i > 0 ? builds[i - 1] : null,
                    Previous = i < builds.Count - 1 ? builds[i + 1] : null,
                };
            }

            return builds;
        }

        public static IReadOnlyList<APIUpdateStream> CreateUpdateStreams(IReadOnlyList<APIChangelogBuild> builds)
        {
            var stream = new APIUpdateStream
            {
                Id = ez2lazer_stream.Id,
                Name = ez2lazer_stream.Name,
                DisplayName = ez2lazer_stream.DisplayName,
                LatestBuild = builds.FirstOrDefault(),
            };

            return new[] { stream };
        }

        public string? GetBody(string tagOrDisplayVersion) =>
            bodiesByTag.GetValueOrDefault(tagOrDisplayVersion);
    }

    public partial class EzChangelogListing : ChangelogContent
    {
        private readonly List<APIChangelogBuild>? builds;

        public EzChangelogListing(List<APIChangelogBuild>? builds) => this.builds = builds;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            if (builds == null)
                return;

            var currentDate = DateTime.MinValue;

            foreach (var build in builds)
            {
                if (build.CreatedAt.Date != currentDate)
                {
                    if (Children.Count != 0)
                    {
                        Add(new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 2,
                            Colour = colourProvider.Background6,
                            Margin = new MarginPadding { Top = 30 },
                        });
                    }

                    Add(new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Margin = new MarginPadding { Top = 20 },
                        Text = build.CreatedAt.Date.ToLocalisableString("dd MMMM yyyy"),
                        Font = OsuFont.GetFont(weight: FontWeight.Regular, size: 24),
                    });

                    currentDate = build.CreatedAt.Date;
                }

                Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING },
                    Margin = new MarginPadding { Top = 20 },
                    Child = new OsuHoverContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Action = () => SelectBuild(build),
                        Child = new OsuSpriteText
                        {
                            Text = build.DisplayVersion,
                            Font = OsuFont.GetFont(weight: FontWeight.Medium, size: 18),
                            Colour = build.UpdateStream.Colour,
                        },
                    },
                });
            }
        }
    }

    public partial class EzChangelogHeader : ChangelogHeader
    {
        protected override OverlayTitle CreateTitle() => new EzChangelogHeaderTitle();

        private partial class EzChangelogHeaderTitle : OverlayTitle
        {
            public EzChangelogHeaderTitle()
            {
                Title = PageTitleStrings.MainChangelogControllerDefault;
                Description = NamedOverlayComponentStrings.ChangelogDescription;
                Icon = OsuIcon.ChangelogB;
            }
        }
    }

    public partial class EzChangelogReleaseContent : ChangelogContent
    {
        private readonly APIChangelogBuild build;
        private readonly string body;
        private readonly Bindable<Language> language = new Bindable<Language>();

        public EzChangelogReleaseContent(APIChangelogBuild build, string body, Bindable<Language> languageSource)
        {
            this.build = build;
            this.body = body;
            language.BindTo(languageSource);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING };
            language.BindValueChanged(onLanguageChanged, true);
        }

        private void onLanguageChanged(ValueChangedEvent<Language> _) => Schedule(rebuild);

        private void rebuild()
        {
            while (InternalChildren.Count > 0)
                RemoveInternal(InternalChildren[0], true);

            var locale = EzContentLocaleExtensions.FromGameLanguage(language.Value);
            var parsed = EzChangelogParser.Parse(body, locale);

            var headerBuild = new APIChangelogBuild
            {
                Id = build.Id,
                Version = build.Version,
                DisplayVersion = build.DisplayVersion,
                CreatedAt = build.CreatedAt,
                UpdateStream = build.UpdateStream,
                Versions = build.Versions,
                ChangelogEntries = new List<APIChangelogEntry>(),
            };

            Add(new ChangelogSingleBuild.ChangelogBuildWithNavigation(headerBuild) { SelectBuild = SelectBuild });

            Add(new EzChangelogMarkdownContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = parsed.Markdown,
                Margin = new MarginPadding { Top = 20 },
            });
        }
    }

    public partial class EzChangelogMarkdownContainer : ChangelogMarkdownContainer
    {
        public override OsuMarkdownTextFlowContainer CreateTextFlow() => new EzChangelogMarkdownTextFlowContainer();

        private partial class EzChangelogMarkdownTextFlowContainer : OsuMarkdownTextFlowContainer
        {
            protected override void AddImage(LinkInline linkInline) => AddDrawable(new EzChangelogMarkdownImage(linkInline));
        }

        private partial class EzChangelogMarkdownImage : MarkdownImage, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public EzChangelogMarkdownImage(LinkInline linkInline)
                : base(resolveUrl(linkInline.Url ?? string.Empty))
            {
                TooltipText = linkInline.Title ?? string.Empty;
            }

            private static string resolveUrl(string url)
            {
                if (url.Contains("githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return url;

                return $"https://osu.ppy.sh/media-url?url={url}";
            }
        }
    }
}
