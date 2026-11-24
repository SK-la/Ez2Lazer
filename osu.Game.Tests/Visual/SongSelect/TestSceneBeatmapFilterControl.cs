// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK.Input;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapFilterControl : SongSelectComponentsTestScene
    {
        private FilterControl filterControl = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        protected override Anchor ComponentAnchor => Anchor.TopRight;
        protected override float InitialRelativeWidth => 0.7f;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = filterControl = new FilterControl
                {
                    State = { Value = Visibility.Visible },
                    RelativeSizeAxes = Axes.X,
                },
            };
        });

        [Test]
        public void TestSortDirectionButtonUpdatesPersistedCriteria()
        {
            AddUntilStep("filter control loaded", () => filterControl.IsLoaded);
            AddStep("reset sort direction", () => ezConfig.SetValue(Ez2Setting.SongSelectSortDirection, SortDirection.Ascending));
            AddStep("click sort direction", () =>
            {
                var button = filterControl.ChildrenOfType<ShearedButton>().Single(control => control.Name == "Song select sort direction");
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("sort direction persisted as descending", () => ezConfig.Get<SortDirection>(Ez2Setting.SongSelectSortDirection) == SortDirection.Descending);
            AddAssert("filter criteria uses persisted sort direction", () => filterControl.CreateCriteria().SortDirection == SortDirection.Descending);
            AddStep("restore sort direction", () => ezConfig.SetValue(Ez2Setting.SongSelectSortDirection, SortDirection.Ascending));
        }

        [Test]
        public void TestSearch()
        {
            AddStep("search for text", () => filterControl.Search("test search"));
        }
    }
}
