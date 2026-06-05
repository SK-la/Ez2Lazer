// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    [TestFixture]
    public partial class TestSceneBeatmapCarouselScrolling : BeatmapCarouselTestScene
    {
        [SetUpSteps]
        public void SetUpSteps()
        {
            RemoveAllBeatmaps();
            CreateCarousel();

            AddBeatmaps(10);
            WaitForDrawablePanels();
        }

        [Test]
        public void TestScrollPositionMaintainedOnRemove_SecondSelected()
        {
            Quad positionBefore = default;

            AddStep("select middle beatmap", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(null, BeatmapSets.ElementAt(BeatmapSets.Count - 2).Beatmaps.First()));

            WaitForScrolling();

            AddStep("save selected screen position", () => positionBefore = Carousel.ChildrenOfType<PanelBeatmap>().FirstOrDefault(p => p.Selected.Value)!.ScreenSpaceDrawQuad);

            RemoveFirstBeatmap();
            WaitForFiltering();

            AddAssert("select screen position unchanged", () => Carousel.ChildrenOfType<PanelBeatmap>().Single(p => p.Selected.Value).ScreenSpaceDrawQuad,
                () => Is.EqualTo(positionBefore).Using<Quad, Quad>((expected, actual)
                    => Precision.AlmostEquals(expected.TopLeft, actual.TopLeft)
                       && Precision.AlmostEquals(expected.TopRight, actual.TopRight)
                       && Precision.AlmostEquals(expected.BottomLeft, actual.BottomLeft)
                       && Precision.AlmostEquals(expected.BottomRight, actual.BottomRight)));
        }

        [Test]
        public void TestScrollPositionMaintainedOnRemove_SecondSelected_WithUserScroll()
        {
            Quad positionBefore = default;

            AddStep("select middle beatmap", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(null, BeatmapSets.ElementAt(BeatmapSets.Count - 2).Beatmaps.First()));
            WaitForScrolling();

            AddStep("override scroll with user scroll", () =>
            {
                InputManager.MoveMouseTo(Scroll.ScreenSpaceDrawQuad.Centre);
                InputManager.ScrollVerticalBy(-1);
            });
            WaitForScrolling();

            AddStep("save selected screen position", () => positionBefore = Carousel.ChildrenOfType<PanelBeatmap>().FirstOrDefault(p => p.Selected.Value)!.ScreenSpaceDrawQuad);

            RemoveFirstBeatmap();
            WaitForFiltering();

            AddAssert("select screen position unchanged", () => Carousel.ChildrenOfType<PanelBeatmap>().Single(p => p.Selected.Value).ScreenSpaceDrawQuad,
                () => Is.EqualTo(positionBefore));
        }

        [Test]
        public void TestScrollPositionMaintainedOnRemove_LastSelected()
        {
            Quad positionBefore = default;

            AddStep("scroll to end", () => Scroll.ScrollToEnd(false));

            AddStep("select last beatmap", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(null, BeatmapSets.Last().Beatmaps.Last()));

            WaitForScrolling();

            AddStep("save selected screen position", () => positionBefore = Carousel.ChildrenOfType<PanelBeatmap>().FirstOrDefault(p => p.Selected.Value)!.ScreenSpaceDrawQuad);

            RemoveFirstBeatmap();
            WaitForFiltering();
            AddAssert("select screen position unchanged", () => Carousel.ChildrenOfType<PanelBeatmap>().Single(p => p.Selected.Value).ScreenSpaceDrawQuad,
                () => Is.EqualTo(positionBefore));
        }

        [Test]
        public void TestScrollToSelectionAfterFilter()
        {
            double centredScroll = 0;
            int filterPresentationCount = 0;

            AddStep("disable filter debounce", () => Carousel.DebounceDelay = 0);
            AddStep("select first beatmap", () => Carousel.CurrentBeatmap = BeatmapSets.First().Beatmaps.First());

            WaitForScrolling();

            // Ez PanelBeatmap is taller than upstream; compare scroll position instead of screen quads.
            AddStep("save centred scroll position", () => centredScroll = Scroll.Current);

            AddStep("scroll to end", () => Scroll.ScrollToEnd());
            WaitForScrolling();

            AddStep("capture filter presentation count", () => filterPresentationCount = NewItemsPresentedInvocationCount);
            ApplyToFilterAndWaitForFilter("search", f => f.SearchText = "Some");
            WaitForFilterPresentation(filterPresentationCount);
            WaitForScrolling();

            AddUntilStep("scroll returned to selection", () => Precision.AlmostEquals(Scroll.Current, centredScroll));
        }

        [Test]
        public void TestScrollToSelectionAfterFilter_WithUserScroll()
        {
            double scrollAfterUserOverride = 0;
            bool? userScrollingAtPresentation = null;
            double? scrollAtPresentation = null;

            AddStep("disable filter debounce", () => Carousel.DebounceDelay = 0);
            AddStep("select first beatmap", () => Carousel.CurrentBeatmap = BeatmapSets.First().Beatmaps.First());
            WaitForScrolling();

            AddStep("override scroll with user scroll", () =>
            {
                InputManager.MoveMouseTo(Scroll.ScreenSpaceDrawQuad.Centre);
                InputManager.ScrollVerticalBy(-1);
            });
            WaitForScrolling();

            AddStep("save scroll position", () => scrollAfterUserOverride = Scroll.Current);

            AddStep("capture state at filter presentation", () =>
            {
                OnNewItemsPresented = _ =>
                {
                    userScrollingAtPresentation = Carousel.UserScrolling;
                    scrollAtPresentation = Scroll.Current;
                };
            });

            ApplyToFilterAndWaitForFilter("search", f => f.SearchText = "Some");

            // performFilter respects UserScrolling when presenting items; debounce must be zero so the default delay
            // does not leave enough frames for selection refresh to auto-scroll before presentation completes.
            AddAssert("user scrolling preserved at filter presentation", () => userScrollingAtPresentation == true);
            AddAssert("scroll position preserved at filter presentation", () => scrollAtPresentation != null && Precision.AlmostEquals(scrollAtPresentation.Value, scrollAfterUserOverride));
        }
    }
}
