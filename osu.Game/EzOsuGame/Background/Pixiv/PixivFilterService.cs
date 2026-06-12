// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public class PixivFilterService
    {
        private readonly Ez2ConfigManager config;

        public PixivFilterService(Ez2ConfigManager config)
        {
            this.config = config;
        }

        public bool PassesContentFilter(PixivIllustInfo illust)
            => TryGetContentFilterRejection(illust, out _);

        public bool TryGetContentFilterRejection(PixivIllustInfo illust, out string? reason)
        {
            if (!passesAccountRules(illust.Account))
            {
                reason = "account";
                return false;
            }

            if (!passesTagRules(illust.Tags))
            {
                reason = "tag";
                return false;
            }

            if (!passesR18Rules(illust.SanityLevel, illust.XRestrict, illust.Tags))
            {
                reason = "r18";
                return false;
            }

            if (!isSupportedIllustType(illust.IllustType))
            {
                reason = "type";
                return false;
            }

            if (illust.Width <= 0 || illust.Height <= 0)
            {
                reason = "dimensions";
                return false;
            }

            if (!passesLandscapeRules(illust.Width, illust.Height))
            {
                reason = "landscape";
                return false;
            }

            if (PixivAiFilter.IsAiGenerated(illust))
            {
                reason = illust.IllustAiType == PixivConstants.ILLUST_AI_TYPE_AI ? "ai" : "ai_tag";
                return false;
            }

            reason = null;
            return true;
        }

        public string DescribeActiveFilterConfig()
        {
            int whitelistCount = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountWhitelist)).Length;
            int tagIncludeCount = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivTagInclude)).Length;

            return $"AllowR18={config.Get<bool>(Ez2Setting.PixivAllowR18)}, landscapeOnly={config.Get<bool>(Ez2Setting.PixivLandscapeOnly)}, accountWhitelist={whitelistCount}, tagInclude={tagIncludeCount}";
        }

        public bool LandscapeOnly => config.Get<bool>(Ez2Setting.PixivLandscapeOnly);

        public bool AllowsCachedAccount(string account) => passesAccountRules(PixivAccountNormalizer.Normalize(account));

        public bool AllowsCachedIllust(string account, string userName)
        {
            account = PixivAccountNormalizer.Normalize(account);
            userName = PixivAccountNormalizer.Normalize(userName);

            if (!string.IsNullOrEmpty(account))
                return passesAccountRules(account);

            if (!string.IsNullOrEmpty(userName))
                return passesAccountRules(userName);

            return !HasAccountWhitelist();
        }

        public bool HasAccountWhitelist()
            => PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountWhitelist)).Length > 0;

        private bool passesAccountRules(string account)
        {
            account = PixivAccountNormalizer.Normalize(account);

            if (string.IsNullOrEmpty(account))
                return false;

            string[] whitelist = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountWhitelist));

            if (whitelist.Length > 0 && !whitelist.Any(entry => string.Equals(PixivAccountNormalizer.Normalize(entry), account, StringComparison.OrdinalIgnoreCase)))
                return false;

            string[] blacklist = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountBlacklist));

            if (blacklist.Any(entry => string.Equals(PixivAccountNormalizer.Normalize(entry), account, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
        }

        private static bool isSupportedIllustType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return true;

            return string.Equals(type, "illust", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(type, "manga", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(type, "ugoira", StringComparison.OrdinalIgnoreCase);
        }

        private bool passesTagRules(string[] tags)
        {
            string[] includeTags = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivTagInclude));

            if (includeTags.Length > 0 && !tags.Any(tag => containsEntry(includeTags, tag)))
                return false;

            string[] excludeTags = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivTagExclude));

            if (excludeTags.Length > 0 && tags.Any(tag => containsEntry(excludeTags, tag)))
                return false;

            return true;
        }

        private bool passesLandscapeRules(int width, int height)
        {
            if (!LandscapeOnly)
                return true;

            return width > height;
        }

        private bool passesR18Rules(int sanityLevel, int xRestrict, string[] tags)
        {
            if (config.Get<bool>(Ez2Setting.PixivAllowR18))
                return true;

            if (xRestrict >= 1 || sanityLevel >= 4)
                return false;

            return !tags.Any(tag => tag.Contains("R-18", StringComparison.OrdinalIgnoreCase));
        }

        private static bool containsEntry(string[] entries, string value)
            => entries.Any(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
    }
}
