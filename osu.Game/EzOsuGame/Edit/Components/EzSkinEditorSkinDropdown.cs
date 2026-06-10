// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using osuTK;
using Realms;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// Skin selector anchored to the scene bar. Uses a popover so the list does not expand inline over the editor.
    /// </summary>
    public partial class EzSkinEditorSkinDropdown : CompositeDrawable, IHasPopover
    {
        public const float DEFAULT_WIDTH = 200;

        private readonly List<Live<SkinInfo>> dropdownItems = new List<Live<SkinInfo>>();

        private SkinPickerButton pickerButton = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private IDisposable? realmSubscription;

        public EzSkinEditorSkinDropdown()
        {
            RelativeSizeAxes = Axes.None;
            Width = DEFAULT_WIDTH;
            Height = SkinEditorSceneLibrary.BUTTON_HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = pickerButton = new SkinPickerButton
            {
                RelativeSizeAxes = Axes.Both,
                Action = this.ShowPopover,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>()
                                                                         .Where(s => !s.DeletePending)
                                                                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            skins.CurrentSkinInfo.BindValueChanged(e => pickerButton.Text = e.NewValue.ToString() ?? string.Empty, true);

            skins.ScriptedSkinsCatalogUpdated += refreshSkinsList;
            refreshSkinsList();
        }

        public Popover GetPopover() => new SkinListPopover(dropdownItems, skins);

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet? changes)
        {
            if (!sender.Any())
                return;

            refreshSkinsList();
        }

        private void refreshSkinsList()
        {
            dropdownItems.Clear();
            dropdownItems.AddRange(skins.GetAllUsableSkins());
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                skins.ScriptedSkinsCatalogUpdated -= refreshSkinsList;
                realmSubscription?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private partial class SkinPickerButton : OsuButton
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? colourProvider, OsuColour colours)
            {
                BackgroundColour = colourProvider?.Background3 ?? colours.Blue3;
                Content.CornerRadius = 5;
            }
        }

        private partial class SkinListPopover : OsuPopover
        {
            private const float body_width = 280;
            private const float body_padding = 10;
            private const float search_height = 32;
            private const float list_height = 200;
            private const float content_spacing = 8;
            private const float body_height = body_padding * 2 + search_height + content_spacing + list_height;

            private readonly IReadOnlyList<Live<SkinInfo>> items;
            private readonly SkinManager skins;

            private OsuTextBox searchBox = null!;
            private FillFlowContainer listFlow = null!;

            public SkinListPopover(IReadOnlyList<Live<SkinInfo>> items, SkinManager skins)
                : base(withPadding: false)
            {
                this.items = items;
                this.skins = skins;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                // Popover.Body defaults to AutoSize and grows with every skin row — lock body size here.
                Width = body_width;
                Height = body_height;

                Content.AutoSizeAxes = Axes.None;
                Content.RelativeSizeAxes = Axes.Both;

                AllowableAnchors = new[]
                {
                    Anchor.BottomRight,
                    Anchor.BottomLeft,
                    Anchor.TopRight,
                    Anchor.TopLeft,
                };

                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(content_spacing),
                    Padding = new MarginPadding(body_padding),
                    Children = new Drawable[]
                    {
                        searchBox = new OsuTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = search_height,
                            PlaceholderText = @"Search skins",
                        },
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = list_height,
                            ScrollbarOverlapsContent = false,
                            Child = listFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(4),
                            },
                        },
                    },
                };

                searchBox.Current.BindValueChanged(_ => rebuildList(searchBox.Text), true);
                rebuildList(string.Empty);
            }

            private void rebuildList(string query)
            {
                listFlow.Clear(false);

                foreach (var skin in items)
                {
                    string name = skin.ToString() ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(query)
                        && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    var captured = skin;

                    listFlow.Add(new SkinSelectButton(name, () =>
                    {
                        if (captured.ID == SkinInfo.RANDOM_SKIN)
                            skins.SelectRandomSkin();
                        else
                            skins.CurrentSkinInfo.Value = captured;

                        this.HidePopover();
                    }));
                }
            }

            private partial class SkinSelectButton : OsuButton
            {
                public SkinSelectButton(LocalisableString text, Action action)
                {
                    Text = text;
                    Action = action;
                    RelativeSizeAxes = Axes.X;
                    Height = 32;
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider? colourProvider, OsuColour colours)
                {
                    BackgroundColour = colourProvider?.Background4 ?? colours.Blue3;
                }
            }
        }
    }
}
