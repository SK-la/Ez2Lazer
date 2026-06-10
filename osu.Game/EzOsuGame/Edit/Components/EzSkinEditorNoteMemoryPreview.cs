// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// In-memory note preview: loads a skin texture once per asset key, then applies session colour/size without reloading resources.
    /// </summary>
    public partial class EzSkinEditorNoteMemoryPreview : Container
    {
        private readonly Sprite noteSprite;
        private string? loadedTextureKey;

        public EzSkinEditorNoteMemoryPreview()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    noteSprite = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    partLabel = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                        Colour = Color4.White,
                    },
                },
            };
        }

        private readonly OsuSpriteText partLabel;

        public void Apply(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request)
        {
            partLabel.Text = request.Part switch
            {
                EzSkinEditorNotePart.HoldHead => "LN Head",
                EzSkinEditorNotePart.HoldBody => "LN Body",
                EzSkinEditorNotePart.HoldTail => "LN Tail",
                _ => "Note",
            };

            string? textureName = profile.ResolveTextureName(request.UseEzNoteVariants, request.Part, request.VariantId);
            string textureKey = textureName ?? string.Empty;

            if (textureKey != loadedTextureKey)
            {
                loadedTextureKey = textureKey;
                noteSprite.Texture = textureName != null ? skin.GetTexture(textureName) : null;
            }

            noteSprite.Colour = request.NoteColour;
            noteSprite.Size = new Vector2((float)request.Width, (float)request.Height);
        }
    }
}
