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
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyNotePiece : LegacyManiaColumnElement
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private readonly Bindable<bool> divisorNoteColouring = new Bindable<bool>();
        private readonly Bindable<bool> colorSettingsEnabled = new Bindable<bool>();

        private Container directionContainer = null!;

        private Drawable noteAnimation = null!;
        private ISkinSource skin = null!;

        private float? widthForNoteHeightScale;
        private string index = "1";

        public LegacyNotePiece()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo, ManiaRulesetConfigManager? config)
        {
            this.skin = skin;
            config?.BindWith(ManiaRulesetSetting.TimingBasedNoteColouring, divisorNoteColouring);
            colorSettingsEnabled.BindTo(Column.ColorSettingsEnabledBindable);

            widthForNoteHeightScale = skin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.WidthForNoteHeightScale))?.Value;

            InternalChild = directionContainer = new Container
            {
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            updateNoteFileIndex();
            reloadAnimation();
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            divisorNoteColouring.BindValueChanged(_ =>
            {
                updateNoteFileIndex();
                reloadAnimation();
            });

            colorSettingsEnabled.BindValueChanged(_ =>
            {
                updateNoteFileIndex();
                reloadAnimation();
            });
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
            directionContainer.Child = noteAnimation = GetAnimation(skin) ?? Empty();
        }

        protected Drawable? GetAnimationFromLookup(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
        {
            string suffix = string.Empty;

            switch (lookup)
            {
                case LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage:
                    suffix = "H";
                    break;

                case LegacyManiaSkinConfigurationLookups.HoldNoteTailImage:
                    suffix = "T";
                    break;
            }

            string noteImage = GetColumnSkinConfig<string>(skin, lookup)?.Value
                               ?? $"mania-note{index}{suffix}";

            return skin.GetAnimation(noteImage, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);
        }

        private void updateNoteFileIndex()
        {
            index = ManiaNoteTextureColouring.ShouldUseTintBaseNoteTextures(colorSettingsEnabled.Value, divisorNoteColouring.Value)
                ? "1"
                : FallbackColumnIndex;
        }
    }
}
