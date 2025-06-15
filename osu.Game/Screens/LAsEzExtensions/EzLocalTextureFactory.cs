using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;

namespace osu.Game.Screens.LAsEzExtensions
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

        private bool isHit(string componentName)
        {
            return componentName.Contains("flare", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual TextureAnimation CreateAnimation(string component)
        {
            // string getPath = hostStorage.GetFullPath(path);

            animationType = isHit(component)
                ? EzAnimationType.Hit
                : EzAnimationType.Note;

            TextureAnimation animation;

            if (animationType == EzAnimationType.Note)
            {
                animation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                    DefaultFrameLength = 60,
                    Loop = true
                };
            }
            else // Hit
            {
                animation = new TextureAnimation
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fit,
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

            // if (animationType == EzAnimationType.Hit)
            // {
            //     animation.OnUpdate += _ =>
            //     {
            //         if (animation.CurrentFrameIndex == animation.FrameCount - 1)
            //             animation.Expire();
            //     };
            // }

            // container.Add(animation);
            return animation;
        }

        private readonly Dictionary<string, float> textureRatioCache = new Dictionary<string, float>();

        private float calculateTextureRatio(TextureAnimation animation, string componentName)
        {
            float defaultRatio = 1.0f;

            if (!isHit(componentName) && animation.FrameCount > 0)
            {
                var texture = animation.CurrentFrame;
                if (texture != null)
                    defaultRatio = texture.Height / (float)texture.Width;
            }

            return defaultRatio;
        }

        public float GetTextureRatio(string componentName)
        {
            if (isHit(componentName))
                return 1.0f;

            if (textureRatioCache.TryGetValue(componentName, out float ratio))
                return ratio;

            var animation = CreateAnimation(componentName);
            ratio = calculateTextureRatio(animation, componentName);
            textureRatioCache[componentName] = ratio;
            return ratio;
        }

        public bool IsSquareNote(string componentName)
        {
            float ratio = GetTextureRatio(componentName);
            return ratio >= 0.75f;
        }

        // 皮肤变更时清除缓存
        public void ClearTextureRatioCache()
        {
            textureRatioCache.Clear();
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
                ClearTextureRatioCache();
                OnTextureNameChanged?.Invoke();
            }, true);
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
}
