// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyNotePiece : LegacyManiaColumnElement
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;

        private Drawable noteAnimation = null!;
        private ISkinSource skin = null!;

        private float? widthForNoteHeightScale;

        public LegacyNotePiece()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            this.skin = skin;

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

        protected override void OnTintDisplaySettingsChanged()
        {
            reloadAnimation();
        }

        private void reloadAnimation()
        {
            if (directionContainer == null || skin == null)
                return;

            directionContainer.Child = noteAnimation = GetAnimation(skin) ?? Empty();
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
                               ?? $"mania-note{ResolveNoteTextureFileIndex(partForLookup(lookup))}{suffix}";

            return skin.GetAnimation(noteImage, WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);
        }

        private static ManiaNoteDisplayColourPart partForLookup(LegacyManiaSkinConfigurationLookups lookup) => lookup switch
        {
            LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage => ManiaNoteDisplayColourPart.HoldHead,
            LegacyManiaSkinConfigurationLookups.HoldNoteTailImage => ManiaNoteDisplayColourPart.HoldBody,
            _ => ManiaNoteDisplayColourPart.Note,
        };
    }
}
