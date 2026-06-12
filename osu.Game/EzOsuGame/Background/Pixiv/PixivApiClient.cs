// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivApiClient
    {
        private readonly PixivAuthService authService;
        private readonly PixivFilterService filters;
        private string? accessToken;

        public PixivApiClient(PixivAuthService authService, PixivFilterService filters)
        {
            this.authService = authService;
            this.filters = filters;
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

                    if (!tryParseIllust(token, out PixivIllustInfo info))
                        continue;

                    stats.Parsed++;

                    if (!filters.TryGetContentFilterRejection(info, out string? rejection))
                    {
                        stats.RecordRejection(rejection);
                        continue;
                    }

                    if (output.Any(existing => existing.IllustId == info.IllustId && existing.Page == info.Page))
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

        private static bool tryParseIllust(JToken token, out PixivIllustInfo info)
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

            int page = selectDisplayPage(data, out int width, out int height);

            if (page < 0)
                return false;

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

        private static int selectDisplayPage(JToken token, out int width, out int height)
        {
            width = 0;
            height = 0;

            int pageCount = PixivJsonHelper.IntValue(token, "page_count");
            if (pageCount <= 0)
                pageCount = 1;

            var validPages = new List<int>();

            for (int page = 0; page < pageCount; page++)
            {
                (int pageWidth, int pageHeight) = getPageDimensions(token, page);

                if (pageWidth > 0 && pageHeight > 0)
                    validPages.Add(page);
            }

            if (validPages.Count == 0)
                return -1;

            int selectedPage = validPages[RNG.Next(validPages.Count)];
            (width, height) = getPageDimensions(token, selectedPage);
            return selectedPage;
        }

        private static (int width, int height) getPageDimensions(JToken token, int page)
        {
            if (page == 0)
            {
                return (
                    PixivJsonHelper.IntValue(token, "width"),
                    PixivJsonHelper.IntValue(token, "height"));
            }

            var pages = PixivJsonHelper.Field(token, "meta_pages") as JArray;

            if (pages == null || page >= pages.Count)
                return (0, 0);

            JToken pageToken = pages[page];

            return (
                PixivJsonHelper.IntValue(pageToken, "width") > 0 ? PixivJsonHelper.IntValue(pageToken, "width") : PixivJsonHelper.IntValue(token, "width"),
                PixivJsonHelper.IntValue(pageToken, "height") > 0 ? PixivJsonHelper.IntValue(pageToken, "height") : PixivJsonHelper.IntValue(token, "height"));
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

        private static Framework.IO.Network.WebRequest createApiRequest(string url, string token)
        {
            var request = new Framework.IO.Network.WebRequest(url)
            {
                Method = HttpMethod.Get,
            };

            PixivRequestHeaders.ApplyAppApiHeaders(request, token);
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
                if (RejectedAi > 0) parts.Add($"ai={RejectedAi}");
                if (RejectedAiTag > 0) parts.Add($"ai_tag={RejectedAiTag}");

                return parts.Count > 0 ? string.Join(", ", parts) : "unknown";
            }
        }
    }
}
