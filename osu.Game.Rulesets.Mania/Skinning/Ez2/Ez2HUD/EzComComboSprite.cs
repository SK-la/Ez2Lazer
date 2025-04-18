// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence comboSprite.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComComboSprite : SkinnableDrawable, ISerialisableDrawable
    {
        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.SpriteName), nameof(SkinnableComponentStrings.SpriteNameDescription), SettingControlType = typeof(SpriteSelectorControl))]
        public Bindable<string> SpriteName { get; } = new Bindable<string>(string.Empty);

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

        protected override bool ApplySizeRestrictionsToDefault => true;
        public Bindable<int> Current { get; } = new Bindable<int>();
        private Sprite comboSprite = null!;
        private Texture texture = null!;
        private readonly Dictionary<string, Texture> textureMap = new Dictionary<string, Texture>();

        // [Resolved]
        // private ISkinSource source { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        public EzComComboSprite(string textureName, Vector2? maxSize = null, ConfineMode confineMode = ConfineMode.NoScaling)
            : base(new SpriteComponentLookup(textureName, maxSize), confineMode)
        {
            SpriteName.Value = textureName;
        }

        public EzComComboSprite()
            : base(new SpriteComponentLookup(string.Empty), ConfineMode.NoScaling)
        {
            RelativeSizeAxes = Axes.None;
            AutoSizeAxes = Axes.Both;

            SpriteName.BindValueChanged(name =>
            {
                ((SpriteComponentLookup)ComponentLookup).LookupName = name.NewValue ?? string.Empty;
                if (IsLoaded)
                    SkinChanged(CurrentSkin);
            });
        }

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
            foreach (var item in SpriteSelectorControl.SPRITE_PATH_MAP)
            {
                texture = textures.Get(item.Value);

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
        }

        private void applyScaleAnimation(bool wasIncrease, bool wasMiss)
        {
            float newScaleValue = Math.Clamp(comboSprite.Scale.X * (wasIncrease ? IncreaseScale.Value : 0.8f), 0.5f, 3f);
            Vector2 newScale = new Vector2(newScaleValue);

            Anchor originAnchor = Enum.Parse<Anchor>(AnimationOrigin.Value.ToString());
            comboSprite.Anchor = originAnchor;
            comboSprite.Origin = originAnchor;

            comboSprite
                .ScaleTo(newScale, IncreaseDuration.Value, Easing.OutQuint)
                .Then()
                .ScaleTo(Vector2.One, DecreaseDuration.Value, Easing.OutQuint);

            if (wasMiss)
                comboSprite.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        private void applyBounceAnimation(bool wasIncrease, bool wasMiss)
        {
            float factor = 0;

            // 根据 AnimationOrigin 的值设置跳动方向
            switch (AnimationOrigin.Value)
            {
                case OriginOptions.TopCentre:
                    factor = Math.Clamp(wasIncrease ? 10 * IncreaseScale.Value : -50, -100f, 100f); // 向下跳
                    break;

                case OriginOptions.BottomCentre:
                    factor = Math.Clamp(wasIncrease ? -10 * IncreaseScale.Value : 50, -100f, 100f); // 向上跳
                    break;

                case OriginOptions.Centre:
                    factor = Math.Clamp(wasIncrease ? 10 * IncreaseScale.Value : -10 * IncreaseScale.Value, -100f, 100f); // 上下跳
                    break;
            }

            comboSprite
                .MoveToY(factor, IncreaseDuration.Value / 4, Easing.OutBounce)
                .Then()
                .MoveToY(0, DecreaseDuration.Value, Easing.OutBounce);

            if (wasMiss)
                comboSprite.FlashColour(Color4.Red, DecreaseDuration.Value, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => comboSprite.Alpha = alpha.NewValue, true);
        }

        public bool UsesFixedAnchor { get; set; }

        protected override Drawable CreateDefault(ISkinComponentLookup lookup)
        {
            var spriteLookup = (SpriteComponentLookup)lookup;
            texture = textures.Get(spriteLookup.LookupName);

            if (texture == null || SpriteName.Value == string.Empty)
                texture = textures.Get(@"Gameplay/Ez2/combo/default_combo.png");

            if (spriteLookup.MaxSize != null)
                texture = texture.WithMaximumSize(spriteLookup.MaxSize.Value);

            comboSprite = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = texture
            };
            return comboSprite;
        }

        internal class SpriteComponentLookup : ISkinComponentLookup
        {
            public string LookupName { get; set; }
            public Vector2? MaxSize { get; set; }

            public SpriteComponentLookup(string textureName, Vector2? maxSize = null)
            {
                LookupName = textureName;
                MaxSize = maxSize;
            }
        }

        private const string base_path = @"Gameplay/Ez2/combo";

        public partial class SpriteSelectorControl : SettingsDropdown<string>
        {
            [Resolved]
            private TextureStore textures { get; set; } = null!;

            internal static readonly Dictionary<string, string> SPRITE_PATH_MAP = new Dictionary<string, string>();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                var resources = textures.GetAvailableResources();
                SPRITE_PATH_MAP.Clear();

                var matchingResources = resources
                    .Where(r => r.StartsWith(base_path, StringComparison.OrdinalIgnoreCase)
                                && Path.GetExtension(r).Equals(".png", StringComparison.OrdinalIgnoreCase));

                foreach (string? resource in matchingResources)
                {
                    string fileName = Path.GetFileNameWithoutExtension(resource);
                    SPRITE_PATH_MAP[fileName] = resource;
                }

                Items = SPRITE_PATH_MAP.Keys.ToList();

                Current.ValueChanged += e =>
                {
                    if (SPRITE_PATH_MAP.TryGetValue(e.NewValue, out string? path))
                    {
                        if (Current.Value != path)
                            Current.Value = path;

                        // Ensure the sprite is updated when the value changes
                        // SpriteName.Value = e.NewValue;
                    }
                };
            }
        }
    }
}
