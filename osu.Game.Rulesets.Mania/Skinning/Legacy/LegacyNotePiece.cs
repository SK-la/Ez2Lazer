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
    public partial class LegacyNotePiece : LegacyManiaColumnElement, IColumnNote
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;

        private Drawable noteAnimation = null!;
        protected ISkinSource Skin = null!;
        private string? appliedTextureName;

        private float? widthForNoteHeightScale;

        public LegacyNotePiece()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader(true)]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            Skin = skin;

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
            ColumnWatcher.GetOrCreate(Column).Add(this);
        }

        private void reloadAnimation()
        {
            if (directionContainer == null || Skin == null)
                return;

            appliedTextureName = CurrentTextureName();
            directionContainer.Child = noteAnimation = GetAnimation(Skin) ?? Empty();
        }

        private void reloadAnimationIfTextureChanged()
        {
            if (Skin == null || CurrentTextureName() == appliedTextureName)
                return;

            reloadAnimation();
        }

        protected virtual string CurrentTextureName()
            => GetNoteImageName(Skin, LegacyManiaSkinConfigurationLookups.NoteImage);

        protected string GetNoteImageName(ISkin skin, LegacyManiaSkinConfigurationLookups lookup)
        {
            string suffix = lookup switch
            {
                LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage => "H",
                LegacyManiaSkinConfigurationLookups.HoldNoteTailImage => "T",
                _ => string.Empty,
            };

            return GetColumnSkinConfig<string>(skin, lookup)?.Value
                   ?? $"mania-note{ResolveNoteTextureFileIndex(partForLookup(lookup))}{suffix}";
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
            => skin.GetAnimation(GetNoteImageName(skin, lookup), WrapMode.ClampToEdge, WrapMode.ClampToEdge, true, true);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                ColumnWatcher.Remove(Column, this);

            base.Dispose(isDisposing);
        }

        void IColumnNote.ForwardOnNoteSetChanged() => reloadAnimation();

        void IColumnNote.ForwardOnNoteSizeChanged()
        {
        }

        void IColumnNote.ForwardOnColourChanged() => reloadAnimationIfTextureChanged();

        private static ManiaNoteDisplayColourPart partForLookup(LegacyManiaSkinConfigurationLookups lookup) => lookup switch
        {
            LegacyManiaSkinConfigurationLookups.HoldNoteHeadImage => ManiaNoteDisplayColourPart.HoldHead,
            LegacyManiaSkinConfigurationLookups.HoldNoteTailImage => ManiaNoteDisplayColourPart.HoldBody,
            _ => ManiaNoteDisplayColourPart.Note,
        };
    }
}
