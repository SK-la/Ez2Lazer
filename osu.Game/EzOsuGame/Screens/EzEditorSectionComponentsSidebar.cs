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
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
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
        private EzResourceStore resources { get; set; } = null!;

        private FillFlowContainer cardFlow = null!;
        private readonly Bindable<EzEditorComponentType> componentType = new Bindable<EzEditorComponentType>(EzEditorComponentType.Note);

        public EzEditorSectionComponentsSidebar()
            : base("Ez Pro Skin") { }

        private Bindable<string>? noteSetName;
        private Bindable<string>? stageName;
        private Bindable<string>? panelName;
        private Bindable<string>? gameThemeName;
        private Bindable<string>? columnLightName;
        private Bindable<string>? judgementLineName;
        private bool ezSkinBindingsBound;

        private readonly List<string> availableNoteSets = new List<string>();
        private readonly List<string> availableStageSets = new List<string>();
        private readonly List<string> availablePanels = new List<string>();
        private readonly List<string> availableJudgementLines = new List<string>();
        private readonly List<string> availableColumnLights = new List<string>();

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
                    new SettingsEnumDropdown<EzEditorComponentType>
                    {
                        LabelText = EzSkinStrings.COMPONENT_TYPE_LABEL,
                        Current = componentType,
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
            panelName!.BindValueChanged(_ => rebuildVisualLists());
            gameThemeName!.BindValueChanged(_ => rebuildVisualLists());
            judgementLineName!.BindValueChanged(_ => rebuildVisualLists());
            columnLightName!.BindValueChanged(_ => rebuildVisualLists());
            componentType.BindValueChanged(_ => rebuildVisualLists());
            EzGameThemeCandidates.Refresh(storage);
            rebuildVisualLists();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EzGameThemeCandidates.Changed += onGameThemeCandidatesChanged;
        }

        protected override void Dispose(bool isDisposing)
        {
            EzGameThemeCandidates.Changed -= onGameThemeCandidatesChanged;
            base.Dispose(isDisposing);
        }

        private void onGameThemeCandidatesChanged() => rebuildVisualLists();

        private void ensureEzSkinBindings()
        {
            noteSetName ??= ezConfig.GetBindable<string>(Ez2Setting.NoteSetName);
            stageName ??= ezConfig.GetBindable<string>(Ez2Setting.StageName);
            panelName ??= ezConfig.GetBindable<string>(Ez2Setting.StageBackground);
            gameThemeName ??= ezConfig.GetBindable<string>(Ez2Setting.GameThemeName);
            columnLightName ??= ezConfig.GetBindable<string>(Ez2Setting.ColumnLightName);
            judgementLineName ??= ezConfig.GetBindable<string>(Ez2Setting.JudgementLineName);

            if (ezSkinBindingsBound)
                return;

            noteSetName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.NoteSetName, v.NewValue));
            stageName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.StageName, v.NewValue));
            panelName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.StageBackground, v.NewValue));
            gameThemeName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.GameThemeName, v.NewValue));
            columnLightName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.ColumnLightName, v.NewValue));
            judgementLineName.BindValueChanged(v => ezConfig.SetValue(Ez2Setting.JudgementLineName, v.NewValue));
            ezSkinBindingsBound = true;
        }

        private void refreshEzSkinOptions()
        {
            availableNoteSets.Clear();
            availableStageSets.Clear();
            availablePanels.Clear();
            availableColumnLights.Clear();

            availableNoteSets.AddRange(listSubdirs(EzModifyPath.NOTE_PATH));
            availableStageSets.AddRange(listSubdirs(EzModifyPath.STAGE_PATH));
            availablePanels.AddRange(EzResourceDiscovery.ListPanelImages(storage));
            availableColumnLights.AddRange(EzResourceDiscovery.ListColumnSets(storage));

            // 空串代表「不覆盖」，与 EzSkinEditorTextureSettingsSection 的下拉口径一致。
            availableJudgementLines.Add(string.Empty);
            availableJudgementLines.AddRange(EzResourceDiscovery.ListJudgementLineCandidates(storage));

            if (!string.IsNullOrEmpty(judgementLineName!.Value) && !availableJudgementLines.Contains(judgementLineName.Value))
                availableJudgementLines.Add(judgementLineName.Value);
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

            switch (componentType.Value)
            {
                case EzEditorComponentType.Stage:
                    populateStageCards();
                    break;

                case EzEditorComponentType.Panel:
                    populatePanelCards();
                    break;

                case EzEditorComponentType.EarlyLate:
                    populateThemeCards(EzHudThemeSlot.EarlyLate, theme => namedPreview($"GameTheme/{theme}/Early"), shownTheme(EzHudThemeSlot.EarlyLate));
                    break;

                case EzEditorComponentType.Judgement:
                    populateThemeCards(EzHudThemeSlot.Judgement, theme => namedPreview($"GameTheme/{theme}/judgement/Kool"), shownTheme(EzHudThemeSlot.Judgement));
                    break;

                case EzEditorComponentType.Combo:
                    populateThemeCards(EzHudThemeSlot.Combo, theme => namedPreview($"GameTheme/{theme}/combo/Title"), shownTheme(EzHudThemeSlot.Combo));
                    break;

                case EzEditorComponentType.JudgementLine:
                    populateJudgementLineCards();
                    break;

                case EzEditorComponentType.ColumnLight:
                    populateColumnLightCards();
                    break;

                default:
                    populateNoteCards();
                    break;
            }
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

        private void populatePanelCards()
        {
            foreach (string panel in availablePanels)
            {
                cardFlow.Add(new VisualTextureItem(
                    panel,
                    resources.Get($"Panel/{panel}", EzTextureUsage.Large),
                    () => panelName!.Value = panel,
                    () => panelName!.Value == panel));
            }
        }

        private void populateThemeCards(EzHudThemeSlot slot, Func<string, Texture?> preview, string shown)
        {
            foreach (string theme in EzGameThemeCandidates.CreateCardItems(shown))
            {
                cardFlow.Add(new VisualTextureItem(
                    theme,
                    preview(theme),
                    () => applyHudTheme(slot, theme),
                    () => shown == theme));
            }
        }

        private string shownTheme(EzHudThemeSlot slot)
        {
            foreach (IEzHudThemeTarget target in hudThemeTargets())
            {
                if (target.Slot == slot)
                    return EzGameThemeNames.Normalize(target.Theme.Value);
            }

            return EzGameThemeNames.Normalize(gameThemeName!.Value);
        }

        private void applyHudTheme(EzHudThemeSlot slot, string theme)
        {
            foreach (IEzHudThemeTarget target in hudThemeTargets())
            {
                if (target.Slot == slot)
                    target.Theme.Value = theme;
            }

            rebuildVisualLists();
        }

        private IEnumerable<IEzHudThemeTarget> hudThemeTargets()
            => this.FindClosestParent<Game.Overlays.SkinEditor.SkinEditor>()?.HudThemeTargets() ?? Enumerable.Empty<IEzHudThemeTarget>();

        private void populateColumnLightCards()
        {
            foreach (string setName in availableColumnLights)
            {
                cardFlow.Add(new VisualTextureItem(
                    setName,
                    firstFolderImage($"ColumnLight/{setName}"),
                    () => columnLightName!.Value = setName,
                    () => columnLightName!.Value == setName));
            }
        }

        private void populateJudgementLineCards()
        {
            foreach (string name in availableJudgementLines)
            {
                cardFlow.Add(new VisualTextureItem(
                    string.IsNullOrEmpty(name) ? EzSkinStrings.JUDGEMENT_LINE_NONE : name,
                    createJudgementLinePreview(name),
                    () => judgementLineName!.Value = name,
                    () => judgementLineName!.Value == name));
            }
        }

        private Texture? createJudgementLinePreview(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            return resources.Get($"JudgementLine/{EzResourceDiscovery.judgement_line_prefix}{name}", EzTextureUsage.Large)
                   ?? resources.Get($"JudgementLine/{name}", EzTextureUsage.Large);
        }

        private Texture? namedPreview(string path)
            => resources.Get(path, EzTextureUsage.Large) ?? firstFolderImage(path);

        private Texture? firstFolderImage(string directory)
        {
            foreach (string key in resources.ListImageKeys(directory))
            {
                Texture? texture = resources.Get(key, EzTextureUsage.Large);

                if (texture != null)
                    return texture;
            }

            foreach (string child in resources.ListSubdirectories(directory))
            {
                foreach (string key in resources.ListImageKeys($"{directory}/{child}"))
                {
                    Texture? texture = resources.Get(key, EzTextureUsage.Large);

                    if (texture != null)
                        return texture;
                }
            }

            return null;
        }

        private Texture? createNotePreview(string noteSet)
            => resources.Get($"note/{noteSet}/whitenote/000", EzTextureUsage.AnimationSafe)
               ?? resources.Get($"note/{noteSet}/whitenote/001", EzTextureUsage.AnimationSafe);

        private Texture? createStagePreview(string stageSet)
        {
            string basePath = $"Stage/{stageSet}/Stage";
            string? keyFolder = EzLocalTextureFactory.ResolveStageKeyFolder(resources.ListSubdirectories(basePath), EzLocalTextureFactory.MAX_STAGE_KEYS);

            if (keyFolder == null)
                return null;

            return resources.Get($"{basePath}/{keyFolder}/Body", EzTextureUsage.Large);
        }

        private partial class VisualTextureItem : CompositeDrawable
        {
            private readonly LocalisableString name;
            private readonly Texture? preview;
            private readonly Action onClick;
            private readonly Func<bool> isSelected;

            private Box selectedBox = null!;

            public VisualTextureItem(LocalisableString name, Texture? preview, Action onClick, Func<bool> isSelected)
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
