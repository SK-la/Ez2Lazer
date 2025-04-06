// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence comboSprite.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using Texture = osu.Framework.Graphics.Textures.Texture;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComComboSprite : CompositeDrawable, ISerialisableDrawable
    {
        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.SpriteName), nameof(SkinnableComponentStrings.SpriteNameDescription), SettingControlType = typeof(SpriteSelectorControl))]
        public Bindable<string> SpriteName { get; } = new Bindable<string>("default");

        [SettingSource("Animation Type", "The type of animation to apply")]
        public Bindable<AnimationType> Animation { get; } = new Bindable<AnimationType>(AnimationType.Scale);

        [SettingSource("Increase Scale", "The scale factor when the combo increases")]
        public BindableNumber<float> IncreaseScale { get; } = new BindableNumber<float>(0.5f)
        {
            MinValue = 0.1f,
            MaxValue = 5f,
            Precision = 0.05f,
        };

        [SettingSource("Increase Duration", "The scale duration time when the combo increases")]
        public BindableNumber<float> IncreaseDuration { get; } = new BindableNumber<float>(10)
        {
            MinValue = 1,
            MaxValue = 300,
            Precision = 1f,
        };

        [SettingSource("Decrease Duration", "The scale duration time when the combo decrease")]
        public BindableNumber<float> DecreaseDuration { get; } = new BindableNumber<float>(200)
        {
            MinValue = 10,
            MaxValue = 500,
            Precision = 10f,
        };

        [SettingSource("Animation Origin", "The origin point for the animation")]
        public Bindable<OriginOptions> AnimationOrigin { get; } = new Bindable<OriginOptions>(OriginOptions.TopCentre);

        [SettingSource("Alpha", "The alpha value of this box")]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        public Bindable<int> Current { get; } = new Bindable<int>();
        private Sprite comboSprite = null!;
        private readonly Dictionary<string, Texture> textureMap = new Dictionary<string, Texture>();

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public EzComComboSprite(string textureName)
        {
            SpriteName.Value = textureName;
        }

        public EzComComboSprite()
        {
            RelativeSizeAxes = Axes.None;
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.Centre;

            SpriteName.BindValueChanged(name =>
            {
                if (IsLoaded)
                    updateTexture();
            }, true);
        }

        private void updateTexture()
        {
            string lookupName = SpriteName.Value;
            string localPath = Path.Combine("Skins/Ez2/combo", $"{lookupName}.png");

            Texture? texture = null;

            Logger.Log($"Found {localPath}");

            if (File.Exists(localPath))
            {
                using (var stream = File.OpenRead(localPath))
                {
                    texture = Texture.FromStream(renderer, stream);
                    Logger.Log($"Loaded texture from {localPath}");
                }
            }

            texture ??= textures.Get(@"Gameplay/Ez2/combo/default_combo.png");

            comboSprite.Texture = texture;
        }

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            InternalChild = comboSprite = new Sprite
            {
                Origin = Anchor.Centre,
                Anchor = Anchor.Centre
            };

            foreach (var item in SpriteSelectorControl.SPRITE_PATH_MAP)
            {
                var texture = textures.Get(item.Value);

                if (texture != null)
                {
                    textureMap[item.Key] = texture;
                }
            }

            Current.BindTo(scoreProcessor.Combo);
            Current.BindValueChanged(combo =>
            {
                bool wasIncrease = combo.NewValue > combo.OldValue;
                bool wasMiss = combo.OldValue > 1 && combo.NewValue == 0;

                switch (Animation.Value)
                {
                    case AnimationType.Scale:
                        applyScaleAnimation(wasIncrease, wasMiss);
                        break;

                    case AnimationType.Bounce:
                        applyBounceAnimation(wasIncrease, wasMiss);
                        break;
                }
            });

            updateTexture();
        }

        private void applyScaleAnimation(bool wasIncrease, bool wasMiss)
        {
            float newScaleValue = Math.Clamp(comboSprite.Scale.X * (wasIncrease ? IncreaseScale.Value : 0.8f), 0.5f, 3f);
            Vector2 newScale = new Vector2(newScaleValue);

            setSpriteAnchorAndOrigin();

            comboSprite
                .ScaleTo(newScale, IncreaseDuration.Value, Easing.OutQuint)
                .Then()
                .ScaleTo(Vector2.One, DecreaseDuration.Value, Easing.OutQuint);

            if (wasMiss)
                comboSprite.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        private void applyBounceAnimation(bool wasIncrease, bool wasMiss)
        {
            float factor = Math.Clamp(wasIncrease ? -10 * IncreaseScale.Value : 50, -100f, 100f);

            setSpriteAnchorAndOrigin();

            comboSprite
                .MoveToY(factor, IncreaseDuration.Value / 2, Easing.OutBounce)
                .Then()
                .MoveToY(0, DecreaseDuration.Value, Easing.OutBounce);

            if (wasMiss)
                comboSprite.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        private void setSpriteAnchorAndOrigin()
        {
            Anchor originAnchor = Enum.Parse<Anchor>(AnimationOrigin.Value.ToString());
            comboSprite.Anchor = originAnchor;
            comboSprite.Origin = originAnchor;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => comboSprite.Alpha = alpha.NewValue, true);
        }

        public bool UsesFixedAnchor { get; set; }

        public partial class SpriteSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private Storage storage { get; set; } = null!;

            internal static readonly Dictionary<string, string> SPRITE_PATH_MAP = new Dictionary<string, string>();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                const string skin_path = @"Skins\Ez2\combo";
                SPRITE_PATH_MAP.Clear();
                SPRITE_PATH_MAP["default"] = @"Gameplay/Ez2/combo/default_combo.png";

                string fullPath = storage.GetFullPath(skin_path);

                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);

                string[] files = Directory.GetFiles(fullPath, "*.png", SearchOption.TopDirectoryOnly);

                foreach (string file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    SPRITE_PATH_MAP[fileName] = Path.Combine(skin_path, Path.GetFileName(file));
                }

                Items = SPRITE_PATH_MAP.Keys.ToList();

                Current.ValueChanged += e =>
                {
                    if (SPRITE_PATH_MAP.TryGetValue(e.NewValue, out string? path))
                    {
                        if (Current.Value != path)
                            Current.Value = path;
                    }
                };
            }
        }
    }
}
