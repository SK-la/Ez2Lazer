// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Background.Pixiv;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.Tests.EzOsuGame.Background.Pixiv
{
    [TestFixture]
    public class PixivAiFilterTest
    {
        [Test]
        public void TestIllustAiTypeClassification()
        {
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_UNSPECIFIED)), Is.False);
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_HUMAN)), Is.False);
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_AI)), Is.True);
        }

        [Test]
        public void TestUnknownUsesTagFallback()
        {
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_UNSPECIFIED, tags: new[] { "オリジナル" })), Is.False);
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_UNSPECIFIED, tags: new[] { "AI生成", "女の子" })), Is.True);
        }

        [Test]
        public void TestMislabelledHumanStillCaughtByAiTag()
        {
            Assert.That(PixivAiFilter.IsAiGenerated(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_HUMAN, tags: new[] { "AI生成" })), Is.True);
        }

        [Test]
        public void TestContentFilterAcceptsHumanMarkedFollowFeedEntries()
        {
            using var host = new CleanRunHeadlessGameHost();
            var filters = new PixivFilterService(new Ez2ConfigManager(host.Storage));

            Assert.That(filters.PassesContentFilter(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_HUMAN)), Is.True);
            Assert.That(filters.PassesContentFilter(create(illustAiType: PixivConstants.ILLUST_AI_TYPE_AI)), Is.False);

            host.Exit();
        }

        private static PixivIllustInfo create(int illustAiType, string[]? tags = null)
            => new PixivIllustInfo("artist", 1, 0, "https://example.test/a.jpg", 2, tags ?? new[] { "オリジナル" }, "illust", 1920, 1080, illustAiType);
    }
}
