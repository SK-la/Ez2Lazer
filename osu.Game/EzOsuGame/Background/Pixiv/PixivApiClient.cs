// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using osu.Framework.Utils;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivApiClient
    {
        private readonly PixivAuthService authService;
        private readonly PixivFilterService filters;

        public PixivApiClient(PixivAuthService authService, PixivFilterService filters)
        {
            this.authService = authService;
            this.filters = filters;
        }

        public bool TryGetRandomFollowIllust(out PixivIllustInfo illust, out string? error)
        {
            illust = default;
            error = null;

            if (!tryCollectFollowCandidates(out var candidates, out error))
                return false;

            illust = candidates[RNG.Next(candidates.Count)];
            return true;
        }

        public bool TryGetUncachedFollowIllust(PixivImageStore images, out PixivIllustInfo illust, out string? error)
        {
            illust = default;
            error = null;

            if (!tryCollectFollowCandidates(out var candidates, out error))
                return false;

            var uncached = candidates.Where(c => !images.IsCached(c)).ToList();

            if (uncached.Count == 0)
                return false;

            illust = uncached[RNG.Next(uncached.Count)];
            return true;
        }

        private bool tryCollectFollowCandidates(out List<PixivIllustInfo> candidates, out string? error)
        {
            candidates = new List<PixivIllustInfo>();
            error = null;

            if (!authService.TryRefreshAccessToken(out string? accessToken, out error))
                return false;

            string? nextUrl = $"{PixivConstants.API_BASE_URL}/v1/illust/follow?restrict=public";

            for (int page = 0; page < 3 && nextUrl != null; page++)
            {
                if (!tryFetchIllusts(nextUrl, accessToken!, candidates, out nextUrl, out error))
                    return false;

                if (candidates.Count >= 30)
                    break;
            }

            if (candidates.Count == 0)
            {
                error = "No suitable illustrations found in Pixiv follow feed.";
                return false;
            }

            return true;
        }

        public bool TryGetUserAccount(out string? account, out string? error)
        {
            account = null;
            error = null;

            if (!authService.TryRefreshAccessToken(out string? accessToken, out error))
                return false;

            try
            {
                using var request = createApiRequest($"{PixivConstants.API_BASE_URL}/v2/user/account", accessToken!);
                request.Perform();

                if (request.ResponseStatusCode != HttpStatusCode.OK)
                {
                    error = request.GetResponseString() ?? "Failed to load Pixiv account.";
                    return false;
                }

                account = JObject.Parse(request.GetResponseString() ?? string.Empty)["user"]?["account"]?.ToString();
                return !string.IsNullOrWhiteSpace(account);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool tryFetchIllusts(string url, string accessToken, List<PixivIllustInfo> output, out string? nextUrl, out string? error)
        {
            nextUrl = null;
            error = null;

            try
            {
                using var request = createApiRequest(url, accessToken);
                request.Perform();

                if (request.ResponseStatusCode != HttpStatusCode.OK)
                {
                    error = request.GetResponseString() ?? "Failed to load Pixiv follow feed.";
                    return false;
                }

                var json = JObject.Parse(request.GetResponseString() ?? string.Empty);
                nextUrl = json["next_url"]?.ToString();

                var illusts = json["illusts"] as JArray;
                if (illusts == null)
                    return true;

                foreach (var token in illusts)
                {
                    if (!tryParseIllust(token, out PixivIllustInfo info))
                        continue;

                    if (!filters.PassesContentFilter(info))
                        continue;

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

            string? rawAccount = token["user"]?["account"]?.ToString();

            if (string.IsNullOrWhiteSpace(rawAccount))
                rawAccount = token["user"]?["name"]?.ToString();

            string account = PixivAccountNormalizer.Normalize(rawAccount);
            long illustId = token["id"]?.Value<long>() ?? 0;

            if (string.IsNullOrWhiteSpace(account) || illustId <= 0)
                return false;

            int page = selectLandscapePage(token, out int width, out int height);

            if (page < 0)
                return false;

            string? imageUrl = getImageUrl(token, page);

            if (string.IsNullOrWhiteSpace(imageUrl))
                return false;

            int sanityLevel = token["sanity_level"]?.Value<int>() ?? 0;
            string[] tags = extractTags(token);
            string illustType = token["type"]?.ToString() ?? "illust";
            int illustAiType = token["illust_ai_type"]?.Value<int>() ?? 0;

            info = new PixivIllustInfo(account, illustId, page, imageUrl, sanityLevel, tags, illustType, width, height, illustAiType);
            return true;
        }

        private static int selectLandscapePage(JToken token, out int width, out int height)
        {
            width = 0;
            height = 0;

            int pageCount = token["page_count"]?.Value<int>() ?? 1;
            var landscapePages = new List<int>();

            for (int page = 0; page < pageCount; page++)
            {
                (int pageWidth, int pageHeight) = getPageDimensions(token, page);

                if (pageWidth > pageHeight)
                    landscapePages.Add(page);
            }

            if (landscapePages.Count == 0)
                return -1;

            int selectedPage = landscapePages[RNG.Next(landscapePages.Count)];
            (width, height) = getPageDimensions(token, selectedPage);
            return selectedPage;
        }

        private static (int width, int height) getPageDimensions(JToken token, int page)
        {
            if (page == 0)
            {
                return (
                    token["width"]?.Value<int>() ?? 0,
                    token["height"]?.Value<int>() ?? 0);
            }

            var pages = token["meta_pages"] as JArray;

            if (pages == null || page >= pages.Count)
                return (0, 0);

            JToken pageToken = pages[page];

            return (
                pageToken["width"]?.Value<int>() ?? token["width"]?.Value<int>() ?? 0,
                pageToken["height"]?.Value<int>() ?? token["height"]?.Value<int>() ?? 0);
        }

        private static string[] extractTags(JToken token)
        {
            if (token["tags"] is not JArray tagArray)
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
                string? original = token["meta_single_page"]?["original_image_url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(original))
                    return original;

                return token["image_urls"]?["large"]?.ToString()
                       ?? token["image_urls"]?["square_medium"]?.ToString();
            }

            var pages = token["meta_pages"] as JArray;
            if (pages == null || page >= pages.Count)
                return null;

            return pages[page]["image_urls"]?["large"]?.ToString()
                   ?? pages[page]["image_urls"]?["square_medium"]?.ToString();
        }

        private static Framework.IO.Network.WebRequest createApiRequest(string url, string accessToken)
        {
            var request = new Framework.IO.Network.WebRequest(url)
            {
                Method = HttpMethod.Get,
            };

            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("User-Agent", PixivConstants.USER_AGENT);
            request.AddHeader("App-OS", PixivConstants.APP_OS);
            request.AddHeader("App-OS-Version", PixivConstants.APP_OS_VERSION);
            request.AddHeader("App-Version", PixivConstants.APP_VERSION);
            request.AddHeader("Accept-Language", "zh-CN");

            return request;
        }
    }
}
