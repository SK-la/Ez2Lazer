// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Shows column insert/remove controls next to the column under the pointer.
    /// </summary>
    /// <remarks>
    /// Raises intent only; the owner decides how to mutate the column set. The controls keep their target column while
    /// hovered so that clicking them cannot move them out from under the pointer.
    /// </remarks>
    public partial class ExtensionColumnInsertOverlay : CompositeDrawable
    {
        private const float button_size = 20;
        private const float spacing = 4;
        private const float edge_margin = 6;

        private readonly ExtensionColumnSet columnSet;
        private readonly ExtensionArea area;

        private IconButton insertButton = null!;
        private IconButton removeButton = null!;
        private FillFlowContainer buttons = null!;

        private DrawableExtensionColumn? hoveredColumn;

        /// <summary>
        /// Whether the controls are available. Missing means always available.
        /// </summary>
        /// <remarks>
        /// Bound to the editor's column-edit switch by the owner, so the controls are absent unless column-edit mode is on.
        /// </remarks>
        public Func<bool>? Enabled { get; set; }

        /// <summary>
        /// Raised with the index the new column should be inserted at.
        /// </summary>
        public Action<int>? InsertRequested { get; set; }

        /// <summary>
        /// Raised with the column the user asked to remove.
        /// </summary>
        public Action<ExtensionColumn>? RemoveRequested { get; set; }

        public ExtensionColumnInsertOverlay(ExtensionColumnSet columnSet, ExtensionArea area)
        {
            this.columnSet = columnSet;
            this.area = area;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = buttons = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, spacing),
                Alpha = 0,
                Children = new Drawable[]
                {
                    insertButton = new IconButton
                    {
                        Icon = FontAwesome.Solid.Plus,
                        Size = new Vector2(button_size),
                        TooltipText = EzEditorStrings.EXTENSION_COLUMN_INSERT,
                        Action = () => InsertRequested?.Invoke(insertIndex()),
                    },
                    removeButton = new IconButton
                    {
                        Icon = FontAwesome.Solid.Minus,
                        Size = new Vector2(button_size),
                        TooltipText = EzEditorStrings.EXTENSION_COLUMN_REMOVE,
                        Action = () =>
                        {
                            if (hoveredColumn != null)
                                RemoveRequested?.Invoke(hoveredColumn.Column);
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Whether one of the controls occupies <paramref name="screenSpacePosition"/>.
        /// </summary>
        /// <remarks>
        /// Hidden controls do not own their position, otherwise they would block the dock's blank space even while
        /// column-edit mode is off.
        /// </remarks>
        public bool OwnsInputAt(Vector2 screenSpacePosition)
            => (Enabled?.Invoke() ?? true) && (insertButton.Contains(screenSpacePosition) || removeButton.Contains(screenSpacePosition));

        protected override void Update()
        {
            base.Update();

            // A removed column takes its drawable away; drop the stale reference before it is measured.
            if (hoveredColumn?.Parent == null)
                hoveredColumn = null;

            var mouse = GetContainingInputManager().CurrentState.Mouse.Position;

            // Keep the target while the pointer sits on the controls, otherwise the controls would chase the pointer away.
            if (!insertButton.Contains(mouse) && !removeButton.Contains(mouse))
                hoveredColumn = area.DrawableColumnAtOrNearest(mouse);

            bool show = (Enabled?.Invoke() ?? true) && (hoveredColumn != null || (columnSet.Columns.Count == 0 && ReceivePositionalInputAt(mouse)));

            buttons.Alpha = show ? 1 : 0;
            removeButton.Alpha = hoveredColumn != null ? 1 : 0;

            if (!show)
                return;

            float stackHeight = button_size * 2 + spacing;
            Vector2 anchor = hoveredColumn != null
                ? ToLocalSpace(new Vector2(hoveredColumn.ScreenSpaceDrawQuad.AABBFloat.Right, hoveredColumn.ScreenSpaceDrawQuad.AABBFloat.Centre.Y))
                : new Vector2(edge_margin, DrawHeight * 0.5f);

            buttons.Position = new Vector2(
                Math.Clamp(anchor.X - button_size / 2, 0, Math.Max(0, DrawWidth - button_size)),
                Math.Clamp(anchor.Y - stackHeight / 2, edge_margin, Math.Max(edge_margin, DrawHeight - stackHeight - edge_margin)));
        }

        private int insertIndex() => hoveredColumn == null ? 0 : columnSet.Columns.IndexOf(hoveredColumn.Column) + 1;
    }
}
