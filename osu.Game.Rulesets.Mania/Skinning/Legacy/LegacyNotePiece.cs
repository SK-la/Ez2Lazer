// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyNotePiece : LegacyManiaColumnElement
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Bindable<bool> timingBasedNoteColouring = new Bindable<bool>();

        private Container directionContainer = null!;

        private Drawable noteAnimation = null!;
        private ISkinSource skin = null!;
        private int timingColourTextureKeyMode;

        private float? widthForNoteHeightScale;

        public LegacyNotePiece()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, ManiaRulesetConfigManager? rulesetConfig, ManiaPlayfield? playfield)
        {
            this.skin = skin;
            timingColourTextureKeyMode = playfield?.TotalColumns ?? Column.KeyMode;
            rulesetConfig?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, timingBasedNoteColouring);
            widthForNoteHeightScale = skin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale))?.Value;

            InternalChild = directionContainer = new Container
            {
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            reloadAnimation();
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            timingBasedNoteColouring.BindValueChanged(_ => reloadAnimation());
        }

        protected override void Update()
        {
            base.Update();

            Texture? texture = null;

            if (noteAnimation is Sprite sprite)
                texture = sprite.Texture;
            else if (noteAnimation is TextureAnimation textureAnimation && textureAnimation.FrameCount > 0)
                texture = textureAnimation.CurrentFrame;

            if (texture != null)
            {
                float noteHeight = widthForNoteHeightScale ?? DrawWidth;
                noteAnimation.Scale = Vector2.Divide(new Vector2(DrawWidth, noteHeight), texture.DisplayWidth);
            }
        }

        protected virtual void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                directionContainer.Anchor = Anchor.TopCentre;
                directionContainer.Scale = new Vector2(1, -1);
            }
            else
            {
                directionContainer.Anchor = Anchor.BottomCentre;
                directionContainer.Scale = Vector2.One;
            }
        }

        protected virtual Drawable? GetAnimation(ISkinSource skin) => GetAnimationFromLookup(skin, LegacyManiaSkinConfigurationLookups.NoteImage);

        private void reloadAnimation()
        {
            if (directionContainer == null)
                return;

            directionContainer.Child = noteAnimation = GetAnimation(skin) ?? Empty();
        }

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
            => GetAnimationFromLookup(skin, lookup, lookup);

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, bool useTimingColourTexture)
            => GetAnimationFromLookup(skin, lookup, lookup, useTimingColourTexture);

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, LegacyManiaSkinConfigurationLookups timingColourTextureGroupLookup)
            => GetAnimationFromLookup(skin, lookup, timingColourTextureGroupLookup, timingBasedNoteColouring.Value);

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup, LegacyManiaSkinConfigurationLookups timingColourTextureGroupLookup, bool useTimingColourTexture)
        {
            string noteImage = GetTextureNameForLookup(skin, lookup);
            ISkin? source = findAnimationProvider(skin, noteImage);

            if (source == null)
                return null;

            if (useTimingColourTexture && lookup != LegacyManiaSkinConfigurationLookups.HoldNoteTailImage)
            {
                string timingColourTextureImage = LegacyTextureLoaderStore.CreateManiaTimingColourTextureName(CreateTimingColourTextureGroupName(timingColourTextureKeyMode, timingColourTextureGroupLookup), noteImage);
                var timingColourTextureAnimation = source.GetAnimation(timingColourTextureImage, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);

                if (timingColourTextureAnimation != null)
                    return timingColourTextureAnimation;
            }

            return source.GetAnimation(noteImage, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);
        }

        private static ISkin? findAnimationProvider(ISkin skin, string componentName)
        {
            if (skin is ISkinSource source)
                return source.FindProvider(s => hasAnimation(s, componentName));

            return hasAnimation(skin, componentName) ? skin : null;
        }

        private static bool hasAnimation(ISkin skin, string componentName)
            => skin.GetTexture($"{componentName}-0", WrapMode.ClampToEdge, WrapMode.ClampToEdge) != null
               || skin.GetTexture(componentName, WrapMode.ClampToEdge, WrapMode.ClampToEdge) != null;

        protected string GetTextureNameForLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
        {
            string suffix = lookup switch
            {
                LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage => "H",
                LegacyManiaSkinConfigurationLookups.HoldNoteTailImage => "T",
                _ => string.Empty
            };

            return GetColumnSkinConfig<string>(skin, lookup)?.Value
                   ?? $"mania-note{FallbackColumnIndex}{suffix}";
        }

        public static string CreateTimingColourTextureGroupName(int keyMode, LegacyManiaSkinConfigurationLookups lookup)
            => $"keys-{keyMode}-{lookup}";
    }
}
