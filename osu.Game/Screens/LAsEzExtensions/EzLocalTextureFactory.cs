using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Screens.LAsEzExtensions
{
    public partial class EzLocalTextureFactory : CompositeDrawable
    {
        public Bindable<string> NoteSetName { get; set; } = new Bindable<string>();
        public Bindable<string> StageName { get; set; } = new Bindable<string>();
        public event Action? OnNoteChanged;
        public event Action? OnStageChanged;

        private bool initialized;
        private readonly EzSkinSettingsManager ezSkinConfig;
        private readonly TextureStore textureStore;
        private readonly Storage hostStorage;
        private readonly Dictionary<string, TextureLoaderStore> loaderStoreCache = new Dictionary<string, TextureLoaderStore>();

        public EzLocalTextureFactory(
            EzSkinSettingsManager ezSkinConfig,
            TextureStore textureStore,
            Storage hostStorage)
        {
            this.ezSkinConfig = ezSkinConfig;
            this.textureStore = textureStore;
            this.hostStorage = hostStorage;

            ensureLoaderExists();
            initialize();
        }

        private void ensureLoaderExists()
        {
            const string path = "EzResources/";

            if (!loaderStoreCache.TryGetValue(path, out var textureLoaderStore))
            {
                var storage = hostStorage.GetStorageForDirectory(path);
                var fileStore = new StorageBackedResourceStore(storage);
                textureLoaderStore = new TextureLoaderStore(fileStore);
                loaderStoreCache[path] = textureLoaderStore;
                textureStore.AddTextureSource(textureLoaderStore);
            }
        }

        private void clearCacheForPrefix(string prefix)
        {
            var keysToRemove = new List<string>();

            foreach (string key in ratioCache.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    keysToRemove.Add(key);
            }

            foreach (string key in keysToRemove)
                ratioCache.Remove(key);
        }

        private void initialize()
        {
            if (initialized) return;

            initialized = true;

            NoteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            NoteSetName.BindValueChanged(e =>
            {
                // foreach (var loader in loaderStoreCache.Values)
                //     loader.Dispose();
                // loaderStoreCache.Clear();
                // ratioCache.Clear();
                clearCacheForPrefix("note/");
                OnNoteChanged?.Invoke();
            }, true);
            StageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            StageName.BindValueChanged(e =>
            {
                // foreach (var loader in loaderStoreCache.Values)
                //     loader.Dispose();
                // loaderStoreCache.Clear();
                clearCacheForPrefix("Stage/");
                OnStageChanged?.Invoke();
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
            {
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();
                loaderStoreCache.Clear();
            }
        }

        private bool isHitCom(string componentName)
        {
            return componentName.Contains("flare", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual TextureAnimation CreateAnimation(string component)
        {
            bool isHit = isHitCom(component);

            TextureAnimation animation = new TextureAnimation
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = isHit ? Axes.None : Axes.Both,
                FillMode = isHit ? FillMode.Fit : FillMode.Stretch,
                Loop = !isHit
            };

            if (!isHit)
                animation.DefaultFrameLength = 1000.0 / 60.0 * 4;

            string[] pathsToTry =
            {
                $"note/{NoteSetName.Value}/{component}",
                $"note/circle/{component}"
            };

            foreach (string basePath in pathsToTry)
            {
                for (int i = 0; i < 300; i++)
                {
                    string frameFile = $"{basePath}/{i:D3}.png";
                    var texture = textureStore.Get(frameFile);

                    if (texture == null)
                        break;

                    animation.AddFrame(texture);
                }

                if (animation.FrameCount > 0)
                    break;
            }

            return animation;
        }

        private bool isStageBody(string componentName)
        {
            return componentName.Contains("Body", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual Drawable CreateStage(string component)
        {
            bool isBody = isStageBody(component);
            string basePath = $"Stage/{StageName.Value}/Stage";

            var container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
            };

            if (isBody)
            {
                container = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    FillMode = FillMode.Fill,
                    Y = 0,
                    AutoSizeAxes = Axes.X,
                    Height = 247f,
                    Masking = true,
                };

                addStageComponent(container, $"{basePath}/fivekey/{component}");
                addStageComponent(container, $"{basePath}/GrooveLight");
                string overObjectPath = $"{basePath}/Stage/{StageName.Value}_OverObject/{StageName.Value}_OverObject";

                var animation = new TextureAnimation
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.None,
                    FillMode = FillMode.Fill,
                    Loop = true,
                    DefaultFrameLength = 1000.0 / 60.0 * 4
                };

                for (int i = 0; i < 120; i++)
                {
                    var texture = textureStore.Get($"{overObjectPath}_{i}.png");
                    if (texture == null)
                        break;

                    animation.AddFrame(texture);
                }

                container.Add(animation);
            }
            else
            {
                string baseKeyPath = $"{basePath}/eightkey/{component}";
                string[] pathsToTry =
                {
                    $"{baseKeyPath}/KeyBase",
                    $"{baseKeyPath}/KeyPress",
                    $"{baseKeyPath}/KeyBase_0",
                    $"{baseKeyPath}/KeyPress_0",
                    $"{baseKeyPath}/2KeyBase_0",
                    $"{baseKeyPath}/2KeyPress_0",
                };

                foreach (string paths in pathsToTry)
                {
                    addStageComponent(container, $"{paths}");
                }
            }

            return container;
        }

        private void addStageComponent(Container container, string basePath, int frameIndex = -1)
        {
            Texture? texture = loadStageTexture(basePath, frameIndex);
            if (texture == null) return;

            container.Add(new Sprite
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.None,
                FillMode = FillMode.Fill,
                Width = texture.Width,
                Height = texture.Height,
                Texture = texture,
                // Blending = BlendingParameters.Additive,
            });

            Logger.Log($"Factory load {basePath}");
        }

        private Texture? loadStageTexture(string basePath, int i = -1)
        {
            string texturePath;

            if (i < 0) { texturePath = $"{basePath}.png"; }
            else
            {
                texturePath = i == 0
                    ? $"{basePath}.png"
                    : $"{basePath}_{i}.png";
            }

            return textureStore.Get(texturePath);
        }

        private readonly Dictionary<string, float> ratioCache = new Dictionary<string, float>();

        public float GetRatio(string component)
        {
            string path = $"note/{NoteSetName.Value}/{component}";

            if (ratioCache.TryGetValue(path, out float cachedRatio))
                return cachedRatio;

            Texture texture = textureStore.Get($"{path}/000.png") ?? textureStore.Get($"{path}/001.png");

            float ratio = texture == null
                ? 1.0f
                : texture.Height / (float)texture.Width;

            ratioCache[path] = ratio;
            return ratio;
        }

        public bool IsSquareNote(string component)
        {
            float ratio = GetRatio(component);
            return ratio >= 0.75f;
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
