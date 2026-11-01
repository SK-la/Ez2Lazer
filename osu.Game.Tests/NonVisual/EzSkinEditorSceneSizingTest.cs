// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.Edit.Components;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorSceneSizingTest
    {
        private const float toolbar_height = 86;

        private const float left_rail = EzVerticalRail.WIDTH;

        // 16:9 宽度受限、4:3 高度受限，两种布局分支各取一个。
        [TestCase(1920, 1080)]
        [TestCase(1024, 768)]
        public void TestSceneMatchesWindowAspectRatio(int width, int height)
        {
            var rect = EzSkinEditorSceneSizing.ComputeSceneRect(new Vector2(width, height), toolbar_height, left_rail, 56);

            // 场景在屏幕上的实际宽高比等于窗口宽高比（相对坐标下即两轴比例相等），且不越界。
            Assert.That(rect.Width * width / (rect.Height * height), Is.EqualTo((float)width / height).Within(0.01f));
            Assert.That(rect.Left, Is.GreaterThanOrEqualTo(left_rail / width - 1e-4f));
            Assert.That(rect.Top, Is.GreaterThanOrEqualTo(0));
            Assert.That(rect.Bottom, Is.LessThanOrEqualTo(1f + 1e-4f));
            Assert.That(rect.Right, Is.LessThanOrEqualTo(1f + 1e-4f));
        }

        [Test]
        public void TestSceneStaysBetweenRailsWhenSidebarReserveChanges()
        {
            // 侧栏固定会改变预留宽度，但场景必须始终落在左右栏之间，不能越过任一栏。
            var window = new Vector2(1920, 1080);

            var unpinned = EzSkinEditorSceneSizing.ComputeSceneRect(window, toolbar_height, left_rail, 0);
            var pinned = EzSkinEditorSceneSizing.ComputeSceneRect(window, toolbar_height, left_rail, 326);

            Assert.That(unpinned.Left, Is.GreaterThanOrEqualTo(left_rail / 1920f - 1e-4f));
            Assert.That(pinned.Right, Is.LessThanOrEqualTo(1f - 326 / 1920f + 1e-4f));
        }

        [Test]
        public void TestSceneCentredBetweenRails()
        {
            // 高度受限时（左右留白相等）场景应在左右栏之间水平居中。
            var rect = EzSkinEditorSceneSizing.ComputeSceneRect(new Vector2(1920, 1080), toolbar_height, left_rail, 0);

            float leftSpace = rect.Left - left_rail / 1920f;
            float rightSpace = 1f - rect.Right;

            Assert.That(leftSpace, Is.EqualTo(rightSpace).Within(1e-4f));
        }

        [Test]
        public void TestSceneCentredVertically()
        {
            // 宽度受限时在工具栏以下的可用区域里垂直居中：上、下留白相等。
            var rect = EzSkinEditorSceneSizing.ComputeSceneRect(new Vector2(1920, 1080), toolbar_height, left_rail, 326);

            float topSpace = rect.Top - (toolbar_height + EzSkinEditorSceneSizing.PADDING) / 1080f;
            float bottomSpace = 1f - rect.Bottom - EzSkinEditorSceneSizing.PADDING / 1080f;

            Assert.That(topSpace, Is.EqualTo(bottomSpace).Within(1e-4f));
        }
    }
}
