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

        private void initialize()
        {
            if (initialized) return;

            initialized = true;

            NoteSetName = ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName);
            NoteSetName.BindValueChanged(e =>
            {
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();
                loaderStoreCache.Clear();
                textureRatioCache.Clear();
                OnNoteChanged?.Invoke();
            }, true);
            StageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            StageName.BindValueChanged(e =>
            {
                foreach (var loader in loaderStoreCache.Values)
                    loader.Dispose();
                loaderStoreCache.Clear();
                OnStageChanged?.Invoke();
            }, true);
        }

        private bool isHitCom(string componentName)
        {
            return componentName.Contains("flare", StringComparison.InvariantCultureIgnoreCase);
        }

        // private bool isStageCom(string componentName)
        // {
        //     return componentName.Contains("Stage", StringComparison.InvariantCultureIgnoreCase);
        // }

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

            string path = $"note/{NoteSetName.Value}/{component}";

            for (int i = 0; i < 300; i++)
            {
                string frameFile = $"{path}/{i:D3}.png";
                var texture = textureStore.Get(frameFile);

                if (texture == null)
                    break;

                animation.AddFrame(texture);
            }

            if (animation.FrameCount == 0)
            {
                path = $"note/circle/{component}";

                for (int i = 0; i < 60; i++)
                {
                    string frameFile = $"{path}/{i:D3}.png";
                    var texture = textureStore.Get(frameFile);

                    if (texture == null)
                        break;

                    animation.AddFrame(texture);
                }
            }

            return animation;
        }

        public virtual Drawable CreateStage(string component)
        {
            string path = $"Stage/{StageName.Value}/{component}";

            var container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 0,
                AutoSizeAxes = Axes.X,
                Height = 247f,
                Masking = true,
                FillMode = FillMode.Fill
            };

            var bodyTexture = textureStore.Get($"{path}.png");

            if (bodyTexture != null)
            {
                container.Add(new Sprite
                {
                    Texture = bodyTexture,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.None,
                    Width = bodyTexture.Width,
                    Height = bodyTexture.Height,
                    FillMode = FillMode.Fill
                });
                // Logger.Log($"Creating stage component: {path}");
            }

            string grooveLightPath = $"Stage/{StageName.Value}/Stage/GrooveLight";
            var grooveLight = textureStore.Get($"{grooveLightPath}.png");

            if (grooveLight != null)
            {
                var grooveLightSprite = new Sprite
                {
                    Texture = grooveLight,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.None,
                    Width = grooveLight.Width,
                    Height = grooveLight.Height,
                    FillMode = FillMode.Fill,
                    Alpha = 0
                };

                container.Add(grooveLightSprite);

                grooveLightSprite.OnLoadComplete += _ =>
                    grooveLightSprite.Loop(b => b.FadeTo(1f, 300).FadeOut(300));

                Logger.Log($"Creating stage grooveLight {grooveLight}");
            }

            string overObjectPath = $"Stage/{StageName.Value}/Stage/{StageName.Value}_Overobject/{StageName.Value}_Overobject";

            for (int i = 0; i < 120; i++)
            {
                var overObject = textureStore.Get($"{overObjectPath}_{i}.png");

                if (overObject != null)
                {
                    // overObject.ScaleAdjust = 0.00125f;

                    container.Add(new Sprite
                    {
                        Texture = overObject,
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        RelativeSizeAxes = Axes.None,
                        Width = overObject.Width,
                        Height = overObject.Height,
                        FillMode = FillMode.Fill
                    });
                    Logger.Log($"Creating stage overObject {overObjectPath}");
                }
            }

            return container;
        }

        private readonly Dictionary<string, float> textureRatioCache = new Dictionary<string, float>();

        public float GetRatio(string path)
        {
            Texture texture = textureStore.Get($"{path}/000.png") ?? textureStore.Get($"{path}/001.png");

            float ratio = 1.0f;
            if (texture != null)
                ratio = texture.Height / (float)texture.Width;

            texture?.Dispose();

            return ratio;
        }

        public bool IsSquareNote(string component)
        {
            if (textureRatioCache.TryGetValue(component, out float ratio))
                return ratio >= 0.75f;

            if (isHitCom(component))
                return true;

            string path = $"note/{NoteSetName.Value}/{component}";

            ratio = GetRatio(path);
            textureRatioCache[component] = ratio;

            return ratio >= 0.75f;
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
