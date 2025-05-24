// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
// using osu.Framework.Logging;
using osu.Game.Screens;

namespace osu.Game.Skinning.Components
{
    public partial class EzHitFactory : CompositeDrawable
    {
        public Bindable<string> TextureNameBindable { get; } = new Bindable<string>("evolve");
        public string TextureBasePath = @"EzResources/note";

        private readonly TextureStore textureStore;
        private readonly EzSkinSettingsManager ezSkinConfig;

        private const float fps = 60;

        public EzHitFactory(TextureStore textureStore, EzSkinSettingsManager ezSkinConfig, string? customTexturePath = null)
        {
            this.textureStore = textureStore;
            this.ezSkinConfig = ezSkinConfig;

            if (!string.IsNullOrEmpty(customTexturePath))
                TextureBasePath = customTexturePath;

            AutoSizeAxes = Axes.Both;
            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };

            initialize();
        }

        private void initialize()
        {
            TextureNameBindable.Value = ezSkinConfig.Get<string>(EzSkinSetting.NoteSetName);

            ezSkinConfig.GetBindable<string>(EzSkinSetting.NoteSetName).BindValueChanged(e =>
                TextureNameBindable.Value = e.NewValue, true);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        public virtual Drawable CreateAnimation(string component)
        {
            string noteSetName = TextureNameBindable.Value;

            var container = new Container
            {
                AutoSizeAxes = Axes.None, // 关闭自动尺寸
            };

            var animation = new TextureAnimation
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.None, // 关闭相对尺寸
                // Scale = new osuTK.Vector2(2),
                Loop = false
            };

            for (int i = 0;; i++)
            {
                string framePath = $@"{TextureBasePath}/{noteSetName}/{component}/{i:D3}.png";
                var texture = textureStore.Get(framePath);
                // Logger.Log($"EzHitFactory: Try load {framePath}, result: {(texture != null ? "Success" : "Fail")}", LoggingTarget.Runtime, LogLevel.Debug);
                if (texture == null)
                    break;

                animation.AddFrame(texture);
            }

            // Logger.Log($"EzHitFactory: Animation frame count = {animation.FrameCount}", LoggingTarget.Runtime, LogLevel.Debug);Logger.Log($"EzHitFactory: Animation frame count = {animation.FrameCount}", LoggingTarget.Runtime, LogLevel.Debug);
            animation.OnUpdate += _ =>
            {
                var tex = animation.CurrentFrame?.Size;

                if (tex != null)
                {
                    container.Width = tex.Value.X;
                    container.Height = tex.Value.Y;
                    // animation.Y = -tex.Value.Y / 8f;
                }

                if (animation.CurrentFrameIndex == animation.FrameCount - 1)
                    animation.Expire();
            };

            container.Add(animation);
            return container;
        }
    }
}
