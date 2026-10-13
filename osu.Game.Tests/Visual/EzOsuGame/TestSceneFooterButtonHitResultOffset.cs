// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneFooterButtonHitResultOffset : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly BindableDouble current = new BindableDouble
        {
            MinValue = -200,
            MaxValue = 200,
            Precision = 1,
        };

        private FooterButtonHitResultOffset offsetButton = null!;

        private ScreenFooterButton nub => offsetButton.ChildrenOfType<ScreenFooterButton>().Single(button => button != offsetButton);

        private float centreX => (offsetButton.DrawWidth - nub.DrawWidth) / 2;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create slider", () =>
            {
                current.Value = 0;

                Child = offsetButton = new FooterButtonHitResultOffset(current)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            });

            AddUntilStep("slider loaded", () => offsetButton.IsLoaded && nub.IsLoaded);
        }

        [Test]
        public void TestDragAndReset()
        {
            AddAssert("slider starts centred", () => Math.Abs(nub.X - centreX) < 1);

            pressSlider();

            AddStep("drag right", () => InputManager.MoveMouseTo(nub.ScreenSpaceDrawQuad.Centre + new Vector2(60, 0)));

            AddUntilStep("offset increased", () => current.Value > 100);
            AddAssert("slider moved right", () => nub.X > centreX + 40);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddStep("click slider", () =>
            {
                InputManager.MoveMouseTo(nub);
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("offset reset", () => current.Value == 0);
            AddAssert("slider back to centre", () => Math.Abs(nub.X - centreX) < 1);
        }

        [Test]
        public void TestClampedToRange()
        {
            pressSlider();

            AddStep("drag far right", () => InputManager.MoveMouseTo(nub.ScreenSpaceDrawQuad.Centre + new Vector2(500, 0)));
            AddUntilStep("offset at maximum", () => current.Value == current.MaxValue);
            AddAssert("slider inside slot", () => nub.X + nub.DrawWidth <= offsetButton.DrawWidth);

            AddStep("drag far left", () => InputManager.MoveMouseTo(nub.ScreenSpaceDrawQuad.Centre - new Vector2(1000, 0)));
            AddUntilStep("offset at minimum", () => current.Value == current.MinValue);
            AddAssert("slider inside slot", () => nub.X >= 0);

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        [Test]
        public void TestClickOnTrackResets()
        {
            AddStep("set offset", () => current.Value = 120);
            AddUntilStep("slider moved right", () => nub.X > centreX + 10);

            AddStep("click track", () =>
            {
                InputManager.MoveMouseTo(offsetButton, new Vector2(-100, 0));
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("offset reset", () => current.Value == 0);
        }

        private void pressSlider()
        {
            AddStep("press slider", () =>
            {
                InputManager.MoveMouseTo(nub);
                InputManager.PressButton(MouseButton.Left);
            });
        }
    }
}
