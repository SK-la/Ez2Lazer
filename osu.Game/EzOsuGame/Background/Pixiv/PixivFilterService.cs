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
        {
            if (!passesAccountRules(illust.Account))
                return false;

            if (!passesTagRules(illust.Tags))
                return false;

            if (!passesR18Rules(illust.SanityLevel, illust.Tags))
                return false;

            return true;
        }

        public bool AllowsCachedAccount(string account) => passesAccountRules(account);

        public bool ShouldSaveToDisk(PixivIllustInfo illust)
        {
            foreach (string prefix in PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivSkipSaveAccountPrefixes)))
            {
                if (illust.Account.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            string[] skipTags = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivSkipSaveTags));

            if (skipTags.Length > 0 && illust.Tags.Any(tag => containsEntry(skipTags, tag)))
                return false;

            return true;
        }

        private bool passesAccountRules(string account)
        {
            string[] whitelist = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountWhitelist));

            if (whitelist.Length > 0 && !whitelist.Any(entry => string.Equals(entry, account, StringComparison.OrdinalIgnoreCase)))
                return false;

            string[] blacklist = PixivFilterListParser.Parse(config.Get<string>(Ez2Setting.PixivAccountBlacklist));

            if (blacklist.Any(entry => string.Equals(entry, account, StringComparison.OrdinalIgnoreCase)))
                return false;

            return true;
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

        private bool passesR18Rules(int sanityLevel, string[] tags)
        {
            if (config.Get<bool>(Ez2Setting.PixivAllowR18))
                return true;

            if (sanityLevel >= 4)
                return false;

            return !tags.Any(tag => tag.Contains("R-18", StringComparison.OrdinalIgnoreCase));
        }

        private static bool containsEntry(string[] entries, string value)
            => entries.Any(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase));
    }
}
