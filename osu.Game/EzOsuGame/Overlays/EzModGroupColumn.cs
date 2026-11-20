// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Mods.Input;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Input;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzModGroupColumn : ModColumn, IHasTooltip
    {
        public LocalisableString TooltipText => GroupName;
        public string GroupId { get; }

        public string GroupName { get; private set; }

        public Action<EzModLayoutPanel, Vector2>? PanelDragStarted { get; set; }

        public Action<Vector2>? PanelDragged { get; set; }

        public Action? PanelDragEnded { get; set; }

        public Action<EzModGroupColumn, Vector2>? ColumnDragStarted { get; set; }

        public Action<Vector2>? ColumnDragged { get; set; }

        public Action? ColumnDragEnded { get; set; }

        public Action<EzModGroupColumn>? DeleteRequested { get; set; }

        public Action<EzModGroupColumn, string>? RenameRequested { get; set; }

        private readonly Bindable<bool> editing;
        private readonly Key[]? hotkeys;
        private readonly ModType? seedType;

        private OsuTextBox renameBox = null!;
        private OsuClickableContainer deleteButton = null!;
        private readonly TruncatingSpriteText groupTitle;

        public EzModGroupColumn(EzModGroupLayout group, Bindable<bool> editing, Key[]? hotkeys, bool wrapItems = false)
            : base(parseSeed(group.SeedType) ?? ModType.Fun, false, wrapItems)
        {
            GroupId = group.Id;
            GroupName = group.Name;
            this.editing = editing.GetBoundCopy();
            this.hotkeys = hotkeys;
            seedType = parseSeed(group.SeedType);
            HeaderText = group.Name;
            HeaderLabel.Alpha = 0;
            TopLevelContent.Add(groupTitle = new TruncatingSpriteText
            {
                Text = group.Name,
                Font = OsuFont.TorusAlternate.With(size: 17, weight: FontWeight.SemiBold),
                Colour = Colour4.Black,
                Width = WIDTH - 70,
                Position = new Vector2(17, 21),
                Origin = Anchor.CentreLeft,
            });
        }

        public int InsertionIndex(Vector2 screenPosition, string? skipAcronym)
        {
            if (WrapItems)
            {
                var cards = new List<(float X, float Y, float Width, float Height)>();
                var indices = new List<int>();
                int originalIndex = 0;

                foreach (var item in ItemsFlow)
                {
                    if (item is not EzModLayoutPanel panel || string.Equals(panel.Mod.Acronym, skipAcronym, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (panel.IsPresent)
                    {
                        var topLeft = panel.ToSpaceOfOtherDrawable(Vector2.Zero, ItemsFlow);
                        cards.Add((topLeft.X, topLeft.Y, panel.DrawWidth, panel.DrawHeight));
                        indices.Add(originalIndex);
                    }

                    originalIndex++;
                }

                int wrappedIndex = EzModLayoutGeometry.InsertionIndex(cards, ItemsFlow.ToLocalSpace(screenPosition));
                return wrappedIndex < indices.Count ? indices[wrappedIndex] : originalIndex;
            }

            int index = 0;

            foreach (var child in ItemsFlow)
            {
                if (child is not EzModLayoutPanel panel)
                    continue;

                if (skipAcronym != null && string.Equals(panel.Mod.Acronym, skipAcronym, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (screenPosition.Y < panel.ScreenSpaceDrawQuad.Centre.Y)
                    return index;

                index++;
            }

            return index;
        }

        public void SetGroupName(string name)
        {
            GroupName = name;
            HeaderText = name;
            groupTitle.Text = name;
        }

        public void CommitRename()
        {
            if (renameBox.Alpha == 0)
                return;

            RenameRequested?.Invoke(this, renameBox.Text);
            renameBox.Alpha = 0;
        }

        internal void ApplyEzLayoutShearExtras(float shearX)
        {
            if (!IsLoaded)
                return;

            var counter = new Vector2(-shearX, 0);
            renameBox.Shear = counter;
            deleteButton.Shear = counter;
            groupTitle.Shear = counter;
        }

        protected override void Update()
        {
            base.Update();
            groupTitle.Width = Math.Max(1, DrawWidth - 70);
        }

        internal (Vector2 Position, Vector2 Size) InsertionMarker(Vector2 screenPosition, string? skipAcronym)
        {
            int insertion = InsertionIndex(screenPosition, skipAcronym);
            int index = 0;
            EzModLayoutPanel? last = null;

            foreach (var child in ItemsFlow)
            {
                if (child is not EzModLayoutPanel panel || string.Equals(panel.Mod.Acronym, skipAcronym, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (index >= insertion && panel.IsPresent)
                    return (panel.ToScreenSpace(Vector2.Zero), WrapItems ? new Vector2(3, panel.DrawHeight) : new Vector2(panel.DrawWidth, 3));

                if (panel.IsPresent)
                    last = panel;

                index++;
            }

            if (last == null)
                return (ToScreenSpace(new Vector2(12, 48)), new Vector2(Math.Max(1, DrawWidth - 24), 3));

            return WrapItems
                ? (last.ToScreenSpace(new Vector2(last.DrawWidth, 0)), new Vector2(3, last.DrawHeight))
                : (last.ToScreenSpace(new Vector2(0, last.DrawHeight)), new Vector2(last.DrawWidth, 3));
        }

        protected override bool HidesWhenEmpty => !editing.Value;

        protected override ModPanel CreateModPanel(ModState mod)
        {
            var panel = new EzModLayoutPanel(mod);
            panel.Editing.BindTo(editing);
            panel.DragStarted = position => PanelDragStarted?.Invoke(panel, position);
            panel.Dragged = position => PanelDragged?.Invoke(position);
            panel.DragEnded = () => PanelDragEnded?.Invoke();
            return panel;
        }

        protected override IModHotkeyHandler CreateHotkeyHandler(ModSelectHotkeyStyle hotkeyStyle)
        {
            if (hotkeys == null || hotkeys.Length == 0)
                return new NoopModHotkeyHandler();

            return new SequentialModHotkeyHandler(hotkeys);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (editing.Value)
                return false;

            return base.OnKeyDown(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!editing.Value || !headerContains(e.ScreenSpaceMouseDownPosition))
                return false;

            ColumnDragStarted?.Invoke(this, e.ScreenSpaceMousePosition);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            ColumnDragged?.Invoke(e.ScreenSpaceMousePosition);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            base.OnDragEnd(e);
            ColumnDragEnded?.Invoke();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!editing.Value || !headerContains(e.ScreenSpaceMousePosition))
                return base.OnClick(e);

            renameBox.Text = GroupName;
            renameBox.Alpha = 1;
            GetContainingFocusManager()?.ChangeFocus(renameBox);
            return true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AccentColour = seedType is ModType type ? colours.ForModType(type) : colours.GrayC;

            AddInternal(new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 42,
                Children = new Drawable[]
                {
                    renameBox = new OsuTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 32,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Left = 10, Right = 40 },
                        Shear = -OsuGame.SHEAR,
                        Alpha = 0,
                        CommitOnFocusLost = true,
                        PlaceholderText = EzModLayoutStrings.GROUP_NAME_PLACEHOLDER,
                    },
                    deleteButton = new OsuClickableContainer
                    {
                        Size = new Vector2(28),
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 8 },
                        Shear = -OsuGame.SHEAR,
                        Alpha = 0,
                        TooltipText = EzModLayoutStrings.DELETE_GROUP,
                        Action = () => DeleteRequested?.Invoke(this),
                        Child = new SpriteIcon
                        {
                            Icon = FontAwesome.Solid.Times,
                            Size = new Vector2(12),
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Colour = Colour4.Black,
                        },
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            renameBox.OnCommit += (_, _) =>
            {
                if (renameBox.Alpha == 0)
                    return;

                renameBox.Alpha = 0;
                RenameRequested?.Invoke(this, renameBox.Text);
            };

            editing.BindValueChanged(change =>
            {
                deleteButton.Alpha = change.NewValue ? 1 : 0;

                if (!change.NewValue)
                {
                    if (renameBox.Alpha > 0)
                        RenameRequested?.Invoke(this, renameBox.Text);

                    renameBox.Alpha = 0;

                    if (renameBox.HasFocus)
                        GetContainingFocusManager()?.ChangeFocus(null);
                }

                RefreshColumnState();
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            editing.UnbindAll();
            base.Dispose(isDisposing);
        }

        private bool headerContains(Vector2 screenPosition)
        {
            var local = ToLocalSpace(screenPosition);
            return local.Y >= 0 && local.Y <= 42;
        }

        private static ModType? parseSeed(string? seedType)
        {
            if (string.IsNullOrEmpty(seedType) || !Enum.TryParse(seedType, out ModType type))
                return null;

            return type;
        }
    }
}
