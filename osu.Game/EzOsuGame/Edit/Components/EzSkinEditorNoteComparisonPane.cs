// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// One side of the note comparison (live or snapshot): Note and LN hitbox previews stacked together.
    /// </summary>
    public partial class EzSkinEditorNoteComparisonPane : Container
    {
        private readonly EzSkinEditorNoteDrawablePreview tapPreview;
        private readonly EzSkinEditorNoteDrawablePreview holdPreview;

        public EzSkinEditorNoteComparisonPane()
        {
            RelativeSizeAxes = Axes.Both;

            tapPreview = new EzSkinEditorNoteDrawablePreview();
            holdPreview = new EzSkinEditorNoteDrawablePreview();

            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        tapPreview,
                        holdPreview,
                    },
                },
            };
        }

        public void Apply(ISkin skin, IEzSkinEditorNoteRulesetProfile profile, Func<EzSkinEditorNoteCompareKind, EzSkinEditorNotePreviewRequest> getRequest)
        {
            tapPreview.Apply(skin, profile, getRequest(EzSkinEditorNoteCompareKind.Tap));
            holdPreview.Apply(skin, profile, getRequest(EzSkinEditorNoteCompareKind.Hold));
        }
    }
}
