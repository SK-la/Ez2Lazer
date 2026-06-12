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

        public PixivApiClient(PixivAuthService authService)
        {
            this.authService = authService;
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

                    if (isRestricted(token))
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

            string? account = token["user"]?["account"]?.ToString();
            long illustId = token["id"]?.Value<long>() ?? 0;
            if (string.IsNullOrWhiteSpace(account) || illustId <= 0)
                return false;

            int pageCount = token["page_count"]?.Value<int>() ?? 1;
            int page = pageCount <= 1 ? 0 : RNG.Next(0, pageCount);

            string? imageUrl = getImageUrl(token, page);
            if (string.IsNullOrWhiteSpace(imageUrl))
                return false;

            info = new PixivIllustInfo(account, illustId, page, imageUrl);
            return true;
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

        private static bool isRestricted(JToken token)
        {
            int sanityLevel = token["sanity_level"]?.Value<int>() ?? 0;
            if (sanityLevel >= 4)
                return true;

            var tags = token["tags"] as JArray;
            if (tags == null)
                return false;

            return tags.Any(t =>
            {
                string name = t["name"]?.ToString() ?? t.ToString();
                return name.Contains("R-18", StringComparison.OrdinalIgnoreCase);
            });
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
