using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Screens
{
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        public Bindable<string> TextureNameBindable { get; set; } = new Bindable<string>("evolve");
        public string BaseTypePath = @"note";
        private EzAnimationType animationType;

        private readonly EzSkinSettingsManager ezSkinConfig;
        private readonly TextureStore textureStore;

        // private readonly Storage hostStorage;
        // private TextureLoaderStore? textureLoaderStore;
        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();

        public EzLocalTextureFactory(
            EzSkinSettingsManager ezSkinConfig,
            TextureStore textureStore,
            Storage hostStorage,
            string? typePath = null)
        {
            this.ezSkinConfig = ezSkinConfig;
            this.textureStore = textureStore;
            // this.hostStorage = hostStorage;

            RelativeSizeAxes = Axes.Both;
            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };

            if (!string.IsNullOrEmpty(typePath))
                BaseTypePath = typePath;
            initialize();

            string path = $"EzResources/{BaseTypePath}/";

            if (!loaderStoreCache.TryGetValue(path, out var textureLoaderStore))
            {
                var storage = hostStorage.GetStorageForDirectory(path);
                var fileStore = new StorageBackedResourceStore(storage);
                textureLoaderStore = new TextureLoaderStore(fileStore);
                loaderStoreCache[path] = textureLoaderStore;
                this.textureStore.AddTextureSource(textureLoaderStore);
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        public event Action? OnTextureNameChanged;

        private bool initialized;

        private void initialize()
        {
            if (initialized) return;

            initialized = true;

            TextureNameBindable = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);

            TextureNameBindable.BindValueChanged(e =>
            {
                OnTextureNameChanged?.Invoke();
            }, true);
        }

        public virtual Drawable CreateAnimation(string component)
        {
            // string getPath = hostStorage.GetFullPath(path);

            string lower = component.ToLowerInvariant();
            if (lower.Contains("flare"))
                animationType = EzAnimationType.Hit;
            else
                animationType = EzAnimationType.Note;

            Container container;
            TextureAnimation animation;

            if (animationType == EzAnimationType.Note)
            {
                container = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                };

                animation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    DefaultFrameLength = 360f,
                    Loop = true
                };
            }
            else // Hit
            {
                container = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.None,
                };

                animation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fit,
                    Loop = false,
                };
            }

            string noteSetName = TextureNameBindable.Value;

            for (int i = 0;; i++)
            {
                string frameFile = $"{noteSetName}/{component}/{i:D3}.png";
                var texture = textureStore.Get(frameFile);

                if (texture == null)
                    break;

                animation.AddFrame(texture);
            }

            // Logger.Log($@"{getPath} Load {animation.FrameCount}", LoggingTarget.Runtime, LogLevel.Debug);

            if (animation.FrameCount == 0)
            {
                Logger.Log($"{noteSetName}/{component} is 0", LoggingTarget.Runtime, LogLevel.Debug);
                return container;
            }

            if (animationType == EzAnimationType.Hit)
            {
                animation.OnUpdate += _ =>
                {
                    if (animation.CurrentFrameIndex == animation.FrameCount - 1)
                        animation.Expire();
                };
            }

            container.Add(animation);
            return container;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            foreach (var loader in loaderStoreCache.Values)
                loader.Dispose();
            loaderStoreCache.Clear();
        }
    }

    public enum EzAnimationType
    {
        Note,
        Hit,
        Stage,
        Key,
        Health,
    }

    // /// <summary>
    // /// 注册公开API
    // /// </summary>
    // public partial class EzLocalTexture : CompositeDrawable, ISerialisableDrawable
    // {
    //     public EzLocalTextureFactory NoteFactory { get; private set; } = null!;
    //
    //     [Resolved]
    //     private GameHost host { get; set; } = null!;
    //
    //     [Resolved]
    //     private IRenderer renderer { get; set; } = null!;
    //
    //     [BackgroundDependencyLoader]
    //     private void load(EzSkinSettingsManager ezSkinConfig)
    //     {
    //         NoteFactory = new EzLocalTextureFactory(
    //             ezSkinConfig,
    //             new TextureStore(renderer),
    //             host.Storage
    //         );
    //
    //         AddInternal(NoteFactory);
    //     }
    //
    //     public bool UsesFixedAnchor { get; set; }
    // }
}
