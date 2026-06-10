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
    /// Note comparison pane: ruleset drawable note inside a coloured hitbox sized to the actual note width/height from config or the note-edit session.
    /// </summary>
    public partial class EzSkinEditorNoteDrawablePreview : Container
    {
        private const float hitbox_border_thickness = 2;

        private readonly Container hitboxContainer;
        private readonly Box hitboxFill;
        private readonly Box[] hitboxBorderEdges;
        private readonly Container noteHost;

        private string? loadedDrawableKey;
        private ISkin? pendingSkin;
        private IEzSkinEditorNoteRulesetProfile? pendingProfile;
        private EzSkinEditorNotePreviewRequest? pendingRequest;

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
                    noteHost = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(hitbox_border_thickness),
                    },
                },
            };
        }

        public void Apply(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, EzSkinEditorNotePreviewRequest request)
        {
            pendingSkin = skin;
            pendingProfile = profile;
            pendingRequest = request;

            if (IsLoaded)
                applyInternal();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (pendingSkin != null && pendingProfile != null && pendingRequest != null)
                applyInternal();
        }

        private void applyInternal()
        {
            if (pendingSkin == null || pendingProfile == null || pendingRequest == null)
                return;

            var request = pendingRequest.Value;

            hitboxContainer.Size = new Vector2((float)request.Width, (float)request.Height);
            hitboxFill.Colour = request.NoteColour.Opacity(0.25f);

            foreach (var edge in hitboxBorderEdges)
                edge.Colour = request.NoteColour;

            string drawableKey = $"{request.CompareKind}:{request.UseEzNoteVariants}:{request.VariantId}:{request.Part}:{request.Width:F2}:{request.Height:F2}";

            if (drawableKey == loadedDrawableKey)
                return;

            loadedDrawableKey = drawableKey;
            noteHost.Clear(true);

            var drawable = pendingProfile.CreateDrawableComparisonPreview(pendingSkin, request);

            if (drawable != null)
                noteHost.Child = drawable;
        }
    }
}
