// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Drawable note/LN preview with coloured hitbox border (replaces texture-sprite path for Ez skins).
    /// </summary>
    public partial class EzSkinEditorNoteDrawablePreview : Container
    {
        private const float hitbox_border_thickness = 2;

        private readonly Container hitboxContainer;
        private readonly Box hitboxFill;
        private readonly Box[] hitboxBorderEdges;
        private readonly Container drawableHost;

        private EzSkinEditorNoteCompareKind loadedKind;

        public EzSkinEditorNoteDrawablePreview()
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
                    hitboxFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Transparent,
                    },
                    hitboxBorderEdges[0],
                    hitboxBorderEdges[1],
                    hitboxBorderEdges[2],
                    hitboxBorderEdges[3],
                    drawableHost = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(4),
                    },
                },
            };
        }

        public void Apply(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request)
        {
            hitboxContainer.Size = new Vector2((float)request.Width, (float)request.Height);
            hitboxFill.Colour = request.NoteColour.Opacity(0.25f);

            foreach (var edge in hitboxBorderEdges)
                edge.Colour = request.NoteColour;

            if (request.CompareKind != loadedKind)
            {
                loadedKind = request.CompareKind;
                drawableHost.Clear(true);
            }

            if (drawableHost.Count == 0)
            {
                var drawable = profile.CreateDrawableComparisonPreview(skin, request);

                if (drawable != null)
                    drawableHost.Child = drawable;
            }
        }
    }
}
