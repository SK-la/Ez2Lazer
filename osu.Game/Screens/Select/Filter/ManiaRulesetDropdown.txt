// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osuTK;

namespace osu.Game.Screens.Select.Filter
{
    public partial class ManiaRulesetDropdown : OsuDropdown<SelectManiaRulesetSubset>
    {
        // TODO:多子集切换
        protected virtual bool ShowManageCollectionsItem => true;

        public Action? RequestFilter { private get; set; }

        private readonly BindableList<SelectManiaRulesetSubset> filters = new BindableList<SelectManiaRulesetSubset>();

        public readonly Live<BeatmapCollection>? Collection;

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IDisposable? realmSubscription;

        public ManiaRulesetDropdown()
        {
            Items = Enum.GetValues(typeof(SelectManiaRulesetSubset)).Cast<SelectManiaRulesetSubset>();
            AlwaysShowSearchBar = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(selectionChanged);
        }

        private Live<BeatmapCollection>? lastFiltered;

        private void selectionChanged(ValueChangedEvent<SelectManiaRulesetSubset> filter)
        {
            if (filter.NewValue.IsNull())
                return;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            realmSubscription?.Dispose();
        }

        protected sealed override DropdownHeader CreateHeader() => CreateCollectionHeader();

        protected sealed override DropdownMenu CreateMenu() => CreateCollectionMenu();

        protected virtual CollectionDropdownHeader CreateCollectionHeader() => new CollectionDropdownHeader();

        protected virtual CollectionDropdownMenu CreateCollectionMenu() => new CollectionDropdownMenu();

        public partial class CollectionDropdownHeader : OsuDropdownHeader
        {
            public CollectionDropdownHeader()
            {
                Height = 25;
                Chevron.Size = new Vector2(12);
                Foreground.Padding = new MarginPadding { Top = 4, Bottom = 4, Left = 8, Right = 8 };
            }
        }

        protected partial class CollectionDropdownMenu : OsuDropdownMenu
        {
            public CollectionDropdownMenu()
            {
                MaxHeight = 200;
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new CollectionDropdownDrawableMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };
        }

        protected partial class CollectionDropdownDrawableMenuItem : OsuDropdownMenu.DrawableOsuDropdownMenuItem
        {
            private IconButton addOrRemoveButton = null!;

            private bool beatmapInCollection;

            private readonly Live<BeatmapCollection>? collection;

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

            public CollectionDropdownDrawableMenuItem(MenuItem item)
                : base(item)
            {
                collection = ((DropdownMenuItem<CollectionFilterMenuItem>)item).Value.Collection;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(addOrRemoveButton = new NoFocusChangeIconButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -OsuScrollContainer.SCROLL_BAR_WIDTH,
                    Scale = new Vector2(0.65f),
                    Action = addOrRemove,
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (collection != null)
                {
                    beatmap.BindValueChanged(_ =>
                    {
                        beatmapInCollection = collection.PerformRead(c => c.BeatmapMD5Hashes.Contains(beatmap.Value.BeatmapInfo.MD5Hash));

                        addOrRemoveButton.Enabled.Value = !beatmap.IsDefault;
                        addOrRemoveButton.Icon = beatmapInCollection ? FontAwesome.Solid.MinusSquare : FontAwesome.Solid.PlusSquare;
                        addOrRemoveButton.TooltipText = beatmapInCollection ? "Remove selected beatmap" : "Add selected beatmap";

                        updateButtonVisibility();
                    }, true);
                }

                updateButtonVisibility();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateButtonVisibility();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateButtonVisibility();
                base.OnHoverLost(e);
            }

            protected override void OnSelectChange()
            {
                base.OnSelectChange();
                updateButtonVisibility();
            }

            private void updateButtonVisibility()
            {
                if (collection == null)
                    addOrRemoveButton.Alpha = 0;
                else
                    addOrRemoveButton.Alpha = IsHovered || IsPreSelected || beatmapInCollection ? 1 : 0;
            }

            private void addOrRemove()
            {
                Debug.Assert(collection != null);

                collection.PerformWrite(c =>
                {
                    if (!c.BeatmapMD5Hashes.Remove(beatmap.Value.BeatmapInfo.MD5Hash))
                        c.BeatmapMD5Hashes.Add(beatmap.Value.BeatmapInfo.MD5Hash);
                });
            }

            protected override Drawable CreateContent() => (Content)base.CreateContent();

            private partial class NoFocusChangeIconButton : IconButton
            {
                public override bool ChangeFocusOnClick => false;
            }
        }
    }
}
