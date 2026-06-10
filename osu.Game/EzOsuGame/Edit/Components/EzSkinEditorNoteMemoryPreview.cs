// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// In-memory note preview: skin texture inside a coloured hitbox border sized by session width/height.
    /// </summary>
    public partial class EzSkinEditorNoteMemoryPreview : Container
    {
        private const float hitbox_border_thickness = 2;

        private readonly Container hitboxContainer;
        private readonly Box[] hitboxBorderEdges;
        private readonly Container tapContent;
        private readonly Sprite tapSprite;
        private readonly FillFlowContainer holdContent;
        private readonly Sprite holdHeadSprite;
        private readonly Sprite holdBodySprite;
        private readonly Sprite holdTailSprite;

        private string? loadedTextureKey;

        public EzSkinEditorNoteMemoryPreview()
        {
            RelativeSizeAxes = Axes.Both;

            hitboxBorderEdges = new[]
            {
                new Box { RelativeSizeAxes = Axes.X, Height = hitbox_border_thickness },
                new Box { Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, RelativeSizeAxes = Axes.X, Height = hitbox_border_thickness },
                new Box { RelativeSizeAxes = Axes.Y, Width = hitbox_border_thickness },
                new Box { Anchor = Anchor.TopRight, Origin = Anchor.TopRight, RelativeSizeAxes = Axes.Y, Width = hitbox_border_thickness },
            };

            InternalChild = hitboxContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Children = new Drawable[]
                {
                    hitboxBorderEdges[0],
                    hitboxBorderEdges[1],
                    hitboxBorderEdges[2],
                    hitboxBorderEdges[3],
                    tapContent = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(4),
                        Child = tapSprite = new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                    },
                    holdContent = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Padding = new MarginPadding(4),
                        Children = new Drawable[]
                        {
                            holdHeadSprite = new Sprite { RelativeSizeAxes = Axes.X, Height = 24 },
                            holdBodySprite = new Sprite { RelativeSizeAxes = Axes.X, Height = 80 },
                            holdTailSprite = new Sprite { RelativeSizeAxes = Axes.X, Height = 24 },
                        },
                    },
                },
            };
        }

        public void Apply(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request)
        {
            bool isHold = request.CompareKind == EzSkinEditorNoteCompareKind.Hold;
            tapContent.Alpha = isHold ? 0 : 1;
            holdContent.Alpha = isHold ? 1 : 0;

            hitboxContainer.Size = new Vector2((float)request.Width, (float)request.Height);

            foreach (var edge in hitboxBorderEdges)
                edge.Colour = request.NoteColour;

            string textureKey = $"{request.CompareKind}:{request.UseEzNoteVariants}:{request.VariantId}";

            if (textureKey != loadedTextureKey)
            {
                loadedTextureKey = textureKey;

                if (isHold)
                {
                    holdHeadSprite.Texture = loadTexture(skin, profile, request, EzSkinEditorNotePart.HoldHead);
                    holdBodySprite.Texture = loadTexture(skin, profile, request, EzSkinEditorNotePart.HoldBody);
                    holdTailSprite.Texture = loadTexture(skin, profile, request, EzSkinEditorNotePart.HoldTail);
                }
                else
                {
                    tapSprite.Texture = loadTexture(skin, profile, request, EzSkinEditorNotePart.Note);
                }
            }

            var tint = request.NoteColour;

            if (isHold)
            {
                float bodyHeight = Math.Max(16, (float)request.Height - holdHeadSprite.Height - holdTailSprite.Height - 12);
                holdBodySprite.Height = bodyHeight;
                holdHeadSprite.Colour = tint;
                holdBodySprite.Colour = tint;
                holdTailSprite.Colour = tint;
                scaleSpriteWidth(holdHeadSprite, (float)request.Width);
                scaleSpriteWidth(holdBodySprite, (float)request.Width);
                scaleSpriteWidth(holdTailSprite, (float)request.Width);
            }
            else
            {
                tapSprite.Colour = tint;
                fitSpriteToHitbox(tapSprite);
            }
        }

        private static Texture? loadTexture(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request, EzSkinEditorNotePart part)
        {
            string? textureName = profile.ResolveTextureName(request.UseEzNoteVariants, part, request.VariantId);
            return textureName != null ? skin.GetTexture(textureName) : null;
        }

        private void fitSpriteToHitbox(Sprite sprite)
        {
            if (sprite.Texture == null)
                return;

            float maxWidth = Math.Max(1, hitboxContainer.Width - 8);
            float maxHeight = Math.Max(1, hitboxContainer.Height - 8);
            float scale = Math.Min(maxWidth / sprite.Texture.DisplayWidth, maxHeight / sprite.Texture.DisplayHeight);
            sprite.Size = sprite.Texture.DisplaySize * Math.Max(0.05f, scale);
        }

        private static void scaleSpriteWidth(Sprite sprite, float width)
        {
            if (sprite.Texture == null)
                return;

            float targetWidth = Math.Max(1, width - 8);
            float scale = targetWidth / sprite.Texture.DisplayWidth;
            sprite.Size = sprite.Texture.DisplaySize * Math.Max(0.05f, scale);
        }
    }
}
