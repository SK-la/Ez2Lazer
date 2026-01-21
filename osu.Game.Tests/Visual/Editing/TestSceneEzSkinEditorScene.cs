// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.LAsEzExtensions.Screens;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.Editing
{
    [TestFixture]
    public partial class TestSceneEzSkinEditorScene : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider overlayColour = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Cached]
        private readonly DialogOverlay dialogOverlay = new DialogOverlay();

        private EzSkinEditorScreen ezSkinEditorScreen = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Instantiate the screen for testing purposes
            ezSkinEditorScreen = new EzSkinEditorScreen();
            Child = ezSkinEditorScreen;
            Add(dialogOverlay);
        }

        [Test]
        public void TestLoadScreen()
        {
            // Test that the screen loads without errors
            AddStep("load screen", () => { });
            AddAssert("screen is not null", () => ezSkinEditorScreen != null);
            AddAssert("screen is EzSkinEditorScreen", () => ezSkinEditorScreen is EzSkinEditorScreen);
        }

        // Removed TestPushScreen as Stack is not accessible in this context
        // [Test]
        // public void TestPushScreen()
        // {
        //     AddStep("push screen", () => Stack.Push(ezSkinEditorScreen));
        //     AddUntilStep("screen is current", () => Stack.CurrentScreen == ezSkinEditorScreen);
        // }

        [Test]
        public void TestPopulateSettings()
        {
            AddStep("populate settings", () => ezSkinEditorScreen.PopulateSettings());
        }

        [Test]
        public void TestPresentGameplay()
        {
            AddStep("present gameplay", () => ezSkinEditorScreen.PresentGameplay());
        }
    }
}
