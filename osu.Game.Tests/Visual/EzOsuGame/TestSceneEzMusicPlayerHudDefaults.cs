// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.HUD.MusicPlayer;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Screens.Menu;
using osu.Game.EzOsuGame.Screens.Visualizer;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.EzOsuGame
{
    public partial class TestSceneEzMusicPlayerHudDefaults : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private EzConfigLayoutStore store = null!;
        private EzLayoutContainer container = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create store", () => store = EzConfigLayoutStore.CreateClientRoot(LocalStorage));
        }

        [Test]
        public void TestEmptyLayoutGetsOneSetOfDefaults()
        {
            loadContainer();
            AddUntilStep("defaults loaded once", () => container.ComponentsLoaded && container.Components.Count == 4);
            AddAssert("one of each default type", uniqueDefaultTypes);
            AddAssert("component list has unique instances", () =>
                container.Components.Count == container.Components.Distinct().Count());
        }

        [Test]
        public void TestDuplicatedJsonIsTrimmedToOneSet()
        {
            AddStep("write duplicated json", () =>
            {
                var layout = new SkinLayoutInfo();
                layout.Update(null, new[]
                {
                    new SerialisedDrawableInfo(EzMusicPlayerHudDefaults.CreateVisualisation()),
                    new SerialisedDrawableInfo(EzMusicPlayerHudDefaults.CreateVisualisation()),
                    new SerialisedDrawableInfo(new EzVisualizerBeatmapLogo { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerBeatmapLogo { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaybackBar { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaybackBar { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaylist { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaylist { UsesFixedAnchor = true }),
                });
                store.Save(EzLayoutFiles.MUSIC_PLAYER, layout);
            });

            loadContainer();
            AddUntilStep("duplicates trimmed", () => container.ComponentsLoaded && container.Components.Count == 4);
            AddAssert("one of each default type", uniqueDefaultTypes);
        }

        [Test]
        public void TestSecondVisualisationInJsonIsRejectedOnLoad()
        {
            AddStep("write one extra visualisation", () =>
            {
                var layout = new SkinLayoutInfo();
                layout.Update(null, new[]
                {
                    new SerialisedDrawableInfo(EzMusicPlayerHudDefaults.CreateVisualisation()),
                    new SerialisedDrawableInfo(EzMusicPlayerHudDefaults.CreateVisualisation()),
                    new SerialisedDrawableInfo(new EzVisualizerBeatmapLogo { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaybackBar { UsesFixedAnchor = true }),
                    new SerialisedDrawableInfo(new EzVisualizerPlaylist { UsesFixedAnchor = true }),
                });
                store.Save(EzLayoutFiles.MUSIC_PLAYER, layout);
            });

            loadContainer();
            AddUntilStep("sole instances loaded", () => container.ComponentsLoaded && container.Components.Count == 4);
            AddAssert("one visualisation remains", () =>
                container.Components.Count(c => c is EzMenuLogoVisualisation) == 1);
        }

        [Test]
        public void TestDefaultTypesAreSoleInstances()
        {
            AddAssert("second visualisation rejected", () =>
                !EzMusicPlayerHudDefaults.CreateVisualisation().CanBePlaced(new ISerialisableDrawable[] { EzMusicPlayerHudDefaults.CreateVisualisation() }));
            AddAssert("second playlist rejected", () =>
                !new EzVisualizerPlaylist().CanBePlaced(new ISerialisableDrawable[] { new EzVisualizerPlaylist() }));
            AddAssert("visualisation allowed in empty container", () =>
                EzMusicPlayerHudDefaults.CreateVisualisation().CanBePlaced(System.Array.Empty<ISerialisableDrawable>()));
        }

        [Test]
        public void TestUndoAfterMoveDoesNotThrow()
        {
            loadContainer();
            AddUntilStep("defaults loaded", () => container.ComponentsLoaded && container.Components.Count == 4);

            SkinEditorChangeHandler handler = null!;

            AddStep("bind change handler", () => handler = new SkinEditorChangeHandler(container));
            AddStep("nudge first component", () =>
            {
                var drawable = (Drawable)container.Components.First();
                handler.BeginChange();
                drawable.X += 40;
                handler.EndChange();
            });
            AddStep("undo", () => handler.RestoreState(-1));
            AddAssert("still four unique components", () =>
                container.Components.Count == 4 && container.Components.Count == container.Components.Distinct().Count());
        }

        private void loadContainer()
        {
            AddStep("load container", () =>
            {
                Clear();
                container = new EzLayoutContainer(store, EzLayoutFiles.MUSIC_PLAYER, null);
                container.SetDependencyDonor(this);
                container.OnComponentsLoaded += _ => EzMusicPlayerHudDefaults.ApplyIfEmpty(container);
                Child = container;
            });
        }

        private bool uniqueDefaultTypes()
        {
            var types = container.Components.Select(c => c.GetType()).ToArray();
            return types.Length == 4
                   && types.Contains(typeof(EzMenuLogoVisualisation))
                   && types.Contains(typeof(EzVisualizerBeatmapLogo))
                   && types.Contains(typeof(EzVisualizerPlaybackBar))
                   && types.Contains(typeof(EzVisualizerPlaylist));
        }
    }
}
