// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Mania.UI.Components;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// A single extension column.
    /// </summary>
    public partial class DrawableExtensionColumn : CompositeDrawable
    {
        public const float WIDTH = 60;

        public readonly ExtensionColumn Column;

        /// <summary>
        /// Raised when the user asks to select every note of this column.
        /// </summary>
        public Action? SelectAllRequested { get; set; }

        private readonly ExtensionArea area;

        private readonly IconButton selectAllButton;

        private readonly Container noteArea;
        private readonly Box background;
        private readonly Box mainAudioStrip;
        private readonly OsuSpriteText numberLabel;

        /// <summary>
        /// The scrolling surface this column's notes are drawn on.
        /// </summary>
        public readonly ExtensionLane Lane;

        public DrawableExtensionColumn(ExtensionColumn column, ExtensionArea area)
        {
            Column = column;
            this.area = area;

            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                mainAudioStrip = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Colour = Colour4.CornflowerBlue,
                },
                // Same padded span as a playfield column (the note area is copied from its scrolling container in
                // Update), so both regions share the hit line and the same usable height. The judgement line is a child
                // of that span so it lands on the same edge the playfield draws its own on.
                noteArea = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        Lane = new ExtensionLane(column, area),
                        new ManiaJudgementLine(),
                    },
                },
                numberLabel = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding { Top = 6 },
                },
                selectAllButton = new IconButton
                {
                    Icon = FontAwesome.Solid.CheckDouble,
                    Size = new Vector2(20),
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Margin = new MarginPadding { Bottom = 6 },
                    Alpha = 0.6f,
                    TooltipText = EzEditorStrings.EXTENSION_COLUMN_SELECT_ALL_NOTES,
                    Action = () => SelectAllRequested?.Invoke(),
                },
            };

            column.Number.BindValueChanged(_ => updateNumber(), true);
        }

        protected override void Update()
        {
            base.Update();

            // Copy the playfield column's scrolling container geometry. The playfield sits under its own scaling
            // container while this column does not, so deriving the span independently makes the grid, notes and
            // selection outlines drift apart the further they sit from the hit line.
            var container = area.ReferenceColumn?.HitObjectContainer;

            if (container == null || container.DrawHeight <= 0)
            {
                noteArea.Height = 0;
                return;
            }

            var quad = container.ScreenSpaceDrawQuad.AABBFloat;

            if (quad.Height <= 0)
            {
                noteArea.Height = 0;
                return;
            }

            float top = ToLocalSpace(new Vector2(quad.Centre.X, quad.Top)).Y;
            float bottom = ToLocalSpace(new Vector2(quad.Centre.X, quad.Bottom)).Y;

            noteArea.Y = top;
            noteArea.Height = bottom - top;
        }

        private void updateNumber()
        {
            bool isMainAudio = Column.Number.Value == ExtensionColumnSet.MAIN_AUDIO_TRACK_NUMBER;

            background.Colour = isMainAudio ? Colour4.CornflowerBlue.Opacity(0.25f) : Colour4.Black.Opacity(0.6f);
            mainAudioStrip.Alpha = isMainAudio ? 1 : 0;
            numberLabel.Text = $"#{Column.Number.Value}";
            numberLabel.Colour = isMainAudio ? Colour4.CornflowerBlue : Colour4.White.Opacity(0.75f);
        }

        /// <summary>
        /// The note rendered under <paramref name="screenSpacePosition"/>, if any.
        /// </summary>
        public ExtensionNote? NoteAt(Vector2 screenSpacePosition) => Lane.NoteAt(screenSpacePosition);

        /// <summary>
        /// The time under <paramref name="screenSpacePosition"/>, or <c>null</c> before the column is laid out.
        /// </summary>
        public double? TimeAt(Vector2 screenSpacePosition) => Lane.TimeAt(screenSpacePosition);

        /// <summary>
        /// Whether <paramref name="screenSpacePosition"/> is over a control of this column rather than its note area.
        /// </summary>
        /// <remarks>
        /// The note interaction layer sits above the columns, so it has to step aside for these positions.
        /// </remarks>
        public bool ContainsControl(Vector2 screenSpacePosition) => selectAllButton.Contains(screenSpacePosition);
    }
}
