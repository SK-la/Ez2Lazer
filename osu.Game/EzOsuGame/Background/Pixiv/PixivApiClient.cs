// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Game.EzOsuGame.Configuration;
using WebRequest = osu.Framework.IO.Network.WebRequest;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivApiClient
    {
        private readonly PixivAuthService authService;
        private readonly PixivFilterService filters;
        private readonly Ez2ConfigManager ezConfig;
        private string? accessToken;

        public PixivApiClient(PixivAuthService authService, PixivFilterService filters, Ez2ConfigManager ezConfig)
        {
            this.authService = authService;
            this.filters = filters;
            this.ezConfig = ezConfig;
        }

        public void SetAccessToken(string token) => accessToken = token;

        public bool TryFetchFollowFeedPage(string url, List<PixivIllustInfo> output, out string? nextUrl, out string? error)
        {
            nextUrl = null;
            error = null;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                error = "Pixiv access token is not set.";
                return false;
            }

            var stats = new FeedCollectStats();

            if (!tryFetchIllusts(url, output, ref stats, out nextUrl, out error))
                return false;

            if (stats.Parsed > 0 && stats.Accepted == 0)
            {
                Logger.Log($"[Pixiv] Active filters: {filters.DescribeActiveFilterConfig()}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                Logger.Log($"[Pixiv] Follow feed page filtered all items: {stats.DescribeRejections()}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
            }

            return true;
        }

        public bool TryGetUserAccount(out string? account, out string? error)
        {
            account = null;
            error = null;

            if (!authService.TryRefreshAccessToken(out string? token, out error))
                return false;

            SetAccessToken(token!);

            if (!tryProbeAuthenticatedApi(accessToken!, out error))
                return false;

            account = authService.LoadAccountName();

            if (string.IsNullOrWhiteSpace(account))
                account = "?";

            return true;
        }

        private bool tryProbeAuthenticatedApi(string token, out string? error)
        {
            error = null;

            try
            {
                using var request = createApiRequest(PixivConstants.ILLUST_FOLLOW_INITIAL_URL, token);
                request.Perform();

                if (request.ResponseStatusCode == HttpStatusCode.OK)
                    return true;

                error = request.GetResponseString() ?? "Pixiv login verification failed.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool tryFetchIllusts(string url, List<PixivIllustInfo> output, ref FeedCollectStats stats, out string? nextUrl, out string? error)
        {
            nextUrl = null;
            error = null;

            try
            {
                using var request = createApiRequest(url, accessToken!);
                request.Perform();

                if (request.ResponseStatusCode != HttpStatusCode.OK)
                {
                    error = request.GetResponseString() ?? "Failed to load Pixiv follow feed.";
                    return false;
                }

                var json = JObject.Parse(request.GetResponseString() ?? string.Empty);
                nextUrl = PixivJsonHelper.ResolveNextUrl(json);
                nextUrl = resolveApiUrl(nextUrl);

                var illusts = PixivJsonHelper.ExtractIllustTokens(json);
                stats.SeenIllustCount += illusts.Count;

                if (!stats.LoggedShape && illusts.Count == 0)
                {
                    stats.LoggedShape = true;
                    Logger.Log($"[Pixiv] Follow feed page had 0 illust entries. JSON keys: {PixivJsonHelper.DescribeTopLevelKeys(json)}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);
                }

                foreach (var token in illusts)
                {
                    stats.Raw++;

                    if (!tryParseIllust(token, filters.LandscapeOnly, out PixivIllustInfo info))
                        continue;

                    stats.Parsed++;

                    if (!filters.TryGetContentFilterRejection(info, out string? rejection))
                    {
                        stats.RecordRejection(rejection);
                        continue;
                    }

                    if (output.Any(existing => existing.IllustId == info.IllustId))
                        continue;

                    stats.Accepted++;
                    output.Add(info);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool tryParseIllust(JToken token, bool landscapeOnly, out PixivIllustInfo info)
        {
            info = default;

            JToken data = PixivJsonHelper.UnwrapIllust(token);

            JToken? visible = PixivJsonHelper.Field(data, "visible");
            if (visible?.Type == JTokenType.Boolean && !visible.Value<bool>())
                return false;

            JToken? user = PixivJsonHelper.Field(data, "user");

            string? rawAccount = user?["account"]?.ToString();
            string? rawUserName = user?["name"]?.ToString();

            if (string.IsNullOrWhiteSpace(rawAccount))
                rawAccount = rawUserName;

            if (string.IsNullOrWhiteSpace(rawAccount))
                rawAccount = PixivJsonHelper.StringValue(data, "user_name");

            if (string.IsNullOrWhiteSpace(rawUserName))
                rawUserName = PixivJsonHelper.StringValue(data, "user_name");

            if (string.IsNullOrWhiteSpace(rawAccount))
            {
                long userId = PixivJsonHelper.LongValue(user ?? data, "id");

                if (userId > 0)
                    rawAccount = $"user_{userId}";
            }

            string account = PixivAccountNormalizer.Normalize(rawAccount);
            string userName = PixivAccountNormalizer.Normalize(rawUserName);

            if (string.IsNullOrWhiteSpace(userName))
                userName = account;
            long illustId = PixivJsonHelper.LongValue(data, "id");

            if (string.IsNullOrWhiteSpace(account) || illustId <= 0)
                return false;

            if (!PixivIllustPageSelector.TrySelectDisplayPage(data, landscapeOnly, out int width, out int height))
                return false;

            int page = PixivIllustPageSelector.display_page;
            string? imageUrl = getImageUrl(data, page);

            if (string.IsNullOrWhiteSpace(imageUrl))
                return false;

            int sanityLevel = PixivJsonHelper.IntValue(data, "sanity_level");
            int xRestrict = PixivJsonHelper.IntValue(data, "x_restrict");
            string[] tags = extractTags(data);
            string illustType = PixivJsonHelper.StringValue(data, "type") ?? "illust";
            int illustAiType = PixivJsonHelper.IntValue(data, "illust_ai_type");

            info = new PixivIllustInfo(account, illustId, page, imageUrl, sanityLevel, tags, illustType, width, height, illustAiType, xRestrict, userName);
            return true;
        }

        private static string[] extractTags(JToken token)
        {
            if (PixivJsonHelper.Field(token, "tags") is not JArray tagArray)
                return Array.Empty<string>();

            return tagArray
                   .Select(t => t["name"]?.ToString())
                   .Where(name => !string.IsNullOrWhiteSpace(name))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToArray()!;
        }

        private static string? getImageUrl(JToken token, int page)
        {
            if (page == 0)
            {
                JToken? metaSinglePage = PixivJsonHelper.Field(token, "meta_single_page");
                string? original = metaSinglePage != null ? PixivJsonHelper.StringValue(metaSinglePage, "original_image_url") : null;
                if (!string.IsNullOrWhiteSpace(original))
                    return original;

                JToken? imageUrls = PixivJsonHelper.Field(token, "image_urls");
                return PixivJsonHelper.StringValue(imageUrls ?? new JObject(), "large")
                       ?? PixivJsonHelper.StringValue(imageUrls ?? new JObject(), "square_medium")
                       ?? PixivJsonHelper.StringValue(imageUrls ?? new JObject(), "medium");
            }

            var pages = PixivJsonHelper.Field(token, "meta_pages") as JArray;
            if (pages == null || page >= pages.Count)
                return null;

            JToken? pageUrls = PixivJsonHelper.Field(pages[page], "image_urls");
            return PixivJsonHelper.StringValue(pageUrls ?? new JObject(), "large")
                   ?? PixivJsonHelper.StringValue(pageUrls ?? new JObject(), "square_medium")
                   ?? PixivJsonHelper.StringValue(pageUrls ?? new JObject(), "medium");
        }

        public string ResolveApiUrl(string url) => resolveApiUrl(url);

        public string GetInitialFollowFeedUrl() => PixivApiProxy.GetInitialFollowFeedUrl(ezConfig.Get<string>(Ez2Setting.PixivApiProxyBaseUrl));

        private string resolveApiUrl(string? url)
            => PixivApiProxy.RewriteApiUrl(url ?? string.Empty, ezConfig.Get<string>(Ez2Setting.PixivApiProxyBaseUrl));

        private WebRequest createApiRequest(string url, string token)
        {
            var request = new WebRequest(resolveApiUrl(url))
            {
                Method = HttpMethod.Get,
            };

            PixivRequestHeaders.ApplyAppApiHeaders(request, token);
            PixivWebRequest.ConfigureApi(request);
            return request;
        }

        private struct FeedCollectStats
        {
            public int Raw;
            public int Parsed;
            public int Accepted;
            public int SeenIllustCount;
            public bool LoggedShape;
            public int RejectedAccount;
            public int RejectedTag;
            public int RejectedR18;
            public int RejectedType;
            public int RejectedDimensions;
            public int RejectedLandscape;
            public int RejectedAi;
            public int RejectedAiTag;

            public void RecordRejection(string? reason)
            {
                switch (reason)
                {
                    case "account":
                        RejectedAccount++;
                        break;

                    case "tag":
                        RejectedTag++;
                        break;

                    case "r18":
                        RejectedR18++;
                        break;

                    case "type":
                        RejectedType++;
                        break;

                    case "dimensions":
                        RejectedDimensions++;
                        break;

                    case "landscape":
                        RejectedLandscape++;
                        break;

                    case "ai":
                        RejectedAi++;
                        break;

                    case "ai_tag":
                        RejectedAiTag++;
                        break;
                }
            }

            public string DescribeRejections()
            {
                var parts = new List<string>();

                if (RejectedAccount > 0) parts.Add($"account={RejectedAccount}");
                if (RejectedTag > 0) parts.Add($"tag={RejectedTag}");
                if (RejectedR18 > 0) parts.Add($"r18={RejectedR18}");
                if (RejectedType > 0) parts.Add($"type={RejectedType}");
                if (RejectedDimensions > 0) parts.Add($"dimensions={RejectedDimensions}");
                if (RejectedLandscape > 0) parts.Add($"landscape={RejectedLandscape}");
                if (RejectedAi > 0) parts.Add($"ai={RejectedAi}");
                if (RejectedAiTag > 0) parts.Add($"ai_tag={RejectedAiTag}");

                return parts.Count > 0 ? string.Join(", ", parts) : "unknown";
            }
        }
    }
}
