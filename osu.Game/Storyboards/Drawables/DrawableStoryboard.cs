// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ExternalLibraries;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Storyboards.Drawables
{
    public partial class DrawableStoryboard : Container<DrawableStoryboardLayer>
    {
        [Cached(typeof(Storyboard))]
        public Storyboard Storyboard { get; }

        [Cached(typeof(StoryboardTriggerController))]
        public StoryboardTriggerController TriggerController { get; }

        /// <summary>
        /// Whether the storyboard is considered finished.
        /// </summary>
        public IBindable<bool> HasStoryboardEnded => hasStoryboardEnded;

        private readonly BindableBool hasStoryboardEnded = new BindableBool(true);

        protected override Container<DrawableStoryboardLayer> Content { get; }

        protected override Vector2 DrawScale => autoVideSize ? Vector2.One : new Vector2((Parent?.DrawHeight ?? 0) / 480);

        public override bool RemoveCompletedTransforms => false;

        private double? lastEventEndTime;

        [Cached(typeof(IReadOnlyList<Mod>))]
        public IReadOnlyList<Mod> Mods { get; }

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private DependencyContainer dependencies = null!;

        private BindableNumber<double> health = null!;
        private readonly BindableBool passing = new BindableBool(true);

        private readonly bool autoVideSize;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public DrawableStoryboard(Storyboard storyboard, IReadOnlyList<Mod>? mods = null)
        {
            Storyboard = storyboard;
            Mods = mods ?? Array.Empty<Mod>();

            bool onlyHasVideoElements = Storyboard.Layers.SelectMany(l => l.Elements).All(e => e is StoryboardVideo);

            var autoVideSizeEnable = GlobalConfigStore.EzConfig.GetBindable<bool>(Ez2Setting.StoryboardAutoVideoSize);
            autoVideSize = onlyHasVideoElements && autoVideSizeEnable.Value;

            if (autoVideSize)
            {
                RelativeSizeAxes = Axes.Both;
                Size = Vector2.One;
            }
            else
            {
                Size = new Vector2(640, 480);
                Width = Height * (storyboard.Beatmap.WidescreenStoryboard || onlyHasVideoElements ? 16 / 9f : 4 / 3f);
            }

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            AddInternal(Content = new Container<DrawableStoryboardLayer>
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
            AddInternal(TriggerController = new StoryboardTriggerController
            {
                Passing = passing,
            });
        }

        [BackgroundDependencyLoader]
        private void load(IGameplayClock? clock, CancellationToken? cancellationToken, GameplayState? gameplayState)
        {
            if (clock != null)
                Clock = clock;

            dependencies.CacheAs(typeof(TextureStore),
                new TextureStore(host.Renderer, host.CreateTextureLoaderStore(
                    CreateResourceLookupStore()
                ), false, scaleAdjust: 1));

            foreach (var layer in Storyboard.Layers)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                Add(layer.CreateDrawable());
            }

            lastEventEndTime = Storyboard.LatestEventTime;

            health = gameplayState?.HealthProcessor.Health.GetBoundCopy() ?? new BindableDouble(1);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            health.BindValueChanged(val =>
            {
                // TODO: this is very arbitrary and doesn't work how it is historically supposed to.
                // - For taiko ruleset, this will cause the first half of a perfect play to be "failing".
                // - For all cases, it can flip-flop states too often (on stable it only updated at end of combo).
                // - Also, in stable a different condition was used for non-break-time passing state (local combo performance).
                passing.Value = val.NewValue >= 0.5;
            }, true);
            passing.BindValueChanged(_ => updateLayerVisibility(), true);
        }

        protected virtual IResourceStore<byte[]> CreateResourceLookupStore() => new StoryboardResourceLookupStore(Storyboard, realm, host);

        protected override void Update()
        {
            base.Update();
            hasStoryboardEnded.Value = lastEventEndTime == null || Time.Current >= lastEventEndTime;
        }

        public DrawableStoryboardLayer OverlayLayer => Children.Single(layer => layer.Name == "Overlay");

        private void updateLayerVisibility()
        {
            foreach (var layer in Children)
                layer.Enabled = passing.Value ? layer.Layer.VisibleWhenPassing : layer.Layer.VisibleWhenFailing;
        }

        public class StoryboardResourceLookupStore : IResourceStore<byte[]>
        {
            private readonly IResourceStore<byte[]> realmFileStore;
            private readonly Storyboard storyboard;
            private readonly RealmAccess realm;
            private IResourceStore<byte[]>? externalFileStore;
            private bool externalStoreResolved;

            public StoryboardResourceLookupStore(Storyboard storyboard, RealmAccess realm, GameHost host)
            {
                realmFileStore = new RealmFileStore(realm, host.Storage).Store;
                this.storyboard = storyboard;
                this.realm = realm;
            }

            public void Dispose()
            {
                realmFileStore.Dispose();
                externalFileStore?.Dispose();
            }

            public byte[] Get(string name)
            {
                using var stream = GetStream(name);
                if (stream == null)
                    return null!;

                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = new CancellationToken())
                => Task.FromResult(Get(name));

            public Stream? GetStream(string name)
            {
                string? storagePath = storyboard.GetStoragePathFromStoryboardPath(name);

                if (!string.IsNullOrEmpty(storagePath))
                {
                    Stream? realmStream = realmFileStore.GetStream(storagePath);
                    if (realmStream != null)
                        return realmStream;
                }

                IResourceStore<byte[]>? external = getExternalFileStore();
                if (external == null)
                    return null;

                if (!string.IsNullOrEmpty(storagePath))
                {
                    Stream? mapped = external.GetStream(storagePath);
                    if (mapped != null)
                        return mapped;
                }

                // Fall back to relative / basename lookup used by ExternalBeatmapFileStore.
                return external.GetStream(name);
            }

            public IEnumerable<string> GetAvailableResources() =>
                realmFileStore.GetAvailableResources();

            private IResourceStore<byte[]>? getExternalFileStore()
            {
                if (externalStoreResolved)
                    return externalFileStore;

                externalStoreResolved = true;

                if (storyboard.BeatmapInfo.BeatmapSet is not BeatmapSetInfo beatmapSet)
                    return null;

                if (!beatmapSet.IsExternallyHosted && !ExternalBeatmapPathEncoding.IsExternalSetHash(beatmapSet.Hash))
                    return null;

                if (!ExternalBeatmapPathEncoding.TryResolveContentRoot(beatmapSet, out string contentRoot))
                {
                    if (ExternalBeatmapPathEncoding.IsExternalSetHash(beatmapSet.Hash))
                        ExternalBeatmapPathEncoding.TryPopulateExternalHosting(beatmapSet);

                    if (!ExternalBeatmapPathEncoding.TryResolveContentRoot(beatmapSet, out contentRoot))
                        return null;
                }

                return externalFileStore = new ExternalBeatmapFileStore(
                    contentRoot,
                    ExternalBeatmapFileMappingsBuilder.Build(storyboard.BeatmapInfo, realm));
            }
        }
    }
}
