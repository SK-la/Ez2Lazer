// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens
{
    public partial class EzEditorSectionComponentsSidebar : EditorSidebarSection
    {
        private const float item_width = 106;
        private const float item_height = 124;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private EzResourceProvider textures { get; set; } = null!;

        private FillFlowContainer cardFlow = null!;
        private readonly Bindable<string> componentType = new Bindable<string>("Note");

        public EzEditorSectionComponentsSidebar()
            : base("Ez Pro Skin") { }

        private Bindable<string>? noteSetName;
        private Bindable<string>? stageName;
        private bool ezSkinBindingsBound;

        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();

        [BackgroundDependencyLoader]
        private void load()
        {
            ensureEzSkinBindings();
            refreshEzSkinOptions();

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new SettingsDropdown<string>
                    {
                        LabelText = "组件类型",
                        Current = componentType,
                        Items = new[] { "Note", "Stage" }
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Colour = new Color4(1f, 1f, 1f, 0.1f),
                        Margin = new MarginPadding { Vertical = 4 },
                    },
                    cardFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(6),
                    },
                }
            };

            noteSetName!.BindValueChanged(_ => rebuildVisualLists());
            stageName!.BindValueChanged(_ => rebuildVisualLists());
            componentType.BindValueChanged(_ => rebuildVisualLists());
            rebuildVisualLists();
        }

        private void ensureEzSkinBindings()
        {
            noteSetName ??= ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);

            stageName ??= ezConfig.GetBindable<string>(Ez2Setting.StageName);

            if (ezSkinBindingsBound)
                return;

            noteSetName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.NoteSetName, v.NewValue));
            stageName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.StageName, v.NewValue));
            ezSkinBindingsBound = true;
        }

        private void refreshEzSkinOptions()
        {
            availableNoteSets.Clear();
            availableStageSets.Clear();

            availableNoteSets.AddRange(listSubdirs(EzModifyPath.NOTE_PATH));
            availableStageSets.AddRange(listSubdirs(EzModifyPath.STAGE_PATH));
        }

        private IEnumerable<string> listSubdirs(string relativePath)
        {
            string fullPath = storage.GetFullPath(relativePath);

            if (!Directory.Exists(fullPath))
                return Array.Empty<string>();

            return Directory.GetDirectories(fullPath)
                            .Select(Path.GetFileName)
                            .Where(n => !string.IsNullOrEmpty(n))!;
        }

        private void rebuildVisualLists()
        {
            refreshEzSkinOptions();
            cardFlow.Clear();

            if (componentType.Value == "Stage")
                populateStageCards();
            else
                populateNoteCards();
        }

        private void populateNoteCards()
        {
            foreach (string noteSet in availableNoteSets)
            {
                cardFlow.Add(new VisualTextureItem(
                    noteSet,
                    createNotePreview(noteSet),
                    () => noteSetName!.Value = noteSet,
                    () => noteSetName!.Value == noteSet));
            }
        }

        private void populateStageCards()
        {
            foreach (string stageSet in availableStageSets)
            {
                cardFlow.Add(new VisualTextureItem(
                    stageSet,
                    createStagePreview(stageSet),
                    () => stageName!.Value = stageSet,
                    () => stageName!.Value == stageSet));
            }
        }

        private Texture? createNotePreview(string noteSet)
            => textures.Get($"note/{noteSet}/whitenote/000") ?? textures.Get($"note/{noteSet}/whitenote/001");

        private Texture? createStagePreview(string stageSet)
        {
            // for (int i = 0; i < 6; i++)
            // {
            //     var t = textures.Get($"Stage/{stageSet}/Stage/GrooveLight_{i}");
            //     if (t != null)
            //         return t;
            // }

            return textures.Get($"Stage/{stageSet}/Stage/eightkey/Body", useLargeStore: true);
        }

        private partial class VisualTextureItem : CompositeDrawable
        {
            private readonly string name;
            private readonly Texture? preview;
            private readonly Action onClick;
            private readonly Func<bool> isSelected;

            private Box selectedBox = null!;

            public VisualTextureItem(string name, Texture? preview, Action onClick, Func<bool> isSelected)
            {
                this.name = name;
                this.preview = preview;
                this.onClick = onClick;
                this.isSelected = isSelected;

                Size = new Vector2(item_width, item_height);
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.GreySeaFoamDark,
                    },
                    selectedBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Padding = new MarginPadding(6),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 80,
                                Masking = true,
                                CornerRadius = 4,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = new Color4(0f, 0f, 0f, 0.35f),
                                    },
                                    preview != null
                                        ? new Sprite
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Fit,
                                            Texture = preview,
                                        }
                                        : new OsuSpriteText
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Text = "-",
                                        }
                                }
                            },
                            new OsuSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = name,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateSelectedState();
            }

            protected override void Update()
            {
                base.Update();
                updateSelectedState();
            }

            private void updateSelectedState()
                => selectedBox.Colour = isSelected() ? new Color4(1f, 1f, 1f, 0.2f) : new Color4(1f, 1f, 1f, 0f);

            protected override bool OnClick(ClickEvent e)
            {
                onClick();
                return true;
            }
        }
    }
}
