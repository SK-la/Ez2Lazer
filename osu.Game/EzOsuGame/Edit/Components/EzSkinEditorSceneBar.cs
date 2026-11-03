// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 顶部一级场景栏。只列出一级场景组（选歌 / 游玩）；组内二级场景由左侧
    /// <see cref="EzSkinEditorSceneRail"/> 提供。
    /// </summary>
    public partial class EzSkinEditorSceneBar : CompositeDrawable
    {
        public const float HEIGHT = SkinEditorSceneLibrary.HEIGHT;

        private const float padding = 10;

        public Bindable<EzEditorSceneType> CurrentScene { get; } = new Bindable<EzEditorSceneType>(EzEditorSceneType.Appearance);

        /// <summary>
        /// Raised when the user picks a top-level group. The owner resolves which secondary scene in
        /// that group to show (remembered per group) and writes it back to <see cref="CurrentScene"/>.
        /// </summary>
        public Action<EzEditorSceneGroup>? GroupRequested { get; set; }

        public Action? OpenOfficialHudEditorRequested { get; set; }

        /// <summary>
        /// The active editing layer. Only selects which layout container is edited; visibility is
        /// driven separately by whether the value is <see cref="EzHUDLayer.HudOff"/>.
        /// </summary>
        public Bindable<EzHUDLayer> ActiveLayer { get; } = new Bindable<EzHUDLayer>(EzHUDLayer.EzHud);

        /// <summary>
        /// Ruleset shown by the editor; used to label the ruleset-scoped layer options.
        /// </summary>
        public Bindable<RulesetInfo?> Ruleset { get; } = new Bindable<RulesetInfo?>();

        /// <summary>
        /// Whether the visual component picker window is shown.
        /// </summary>
        public BindableBool VisualPickerOpen { get; } = new BindableBool();

        private EzEditorLayerButton layerButton = null!;
        private EzToggleButton visualPickerButton = null!;

        private readonly Dictionary<EzEditorSceneGroup, GroupTabButton> groupButtons = new Dictionary<EzEditorSceneGroup, GroupTabButton>();

        public EzSkinEditorSceneBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            float gap = EzUIStyleV1.SCENE_BAR_GAP;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background6,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Name = @"Ez Scene library",
                                AutoSizeAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Spacing = new Vector2(EzUIStyleV1.SCENE_BAR_BUTTON_SPACING, 0),
                                Padding = new MarginPadding { Horizontal = padding, Vertical = gap },
                                Direction = FillDirection.Horizontal,
                                Children = buildGroupButtons()
                                           .Prepend(new OsuSpriteText
                                           {
                                               Text = EzEditorStrings.SCENE_LIBRARY_LABEL,
                                               Anchor = Anchor.CentreLeft,
                                               Origin = Anchor.CentreLeft,
                                               Margin = new MarginPadding { Horizontal = 10 },
                                           })
                                           .Append(new EzSceneBarButton
                                           {
                                               Text = EzEditorStrings.SCENE_OFFICIAL_HUD,
                                               Width = 120,
                                               Action = () => OpenOfficialHudEditorRequested?.Invoke(),
                                           })
                                           .ToArray(),
                            },
                            // 右端控件组：HUD 层下拉固定贴最右，之后新增的按钮一律插到它左边。
                            new FillFlowContainer
                            {
                                Name = @"Ez Scene bar controls",
                                AutoSizeAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Spacing = new Vector2(EzUIStyleV1.SCENE_BAR_BUTTON_SPACING, 0),
                                Padding = new MarginPadding { Horizontal = padding, Vertical = gap },
                                Direction = FillDirection.Horizontal,
                                Children = new Drawable[]
                                {
                                    visualPickerButton = new EzToggleButton
                                    {
                                        AutoSizeAxes = Axes.X,
                                        Height = EzUIStyleV1.SceneBarControlHeight(),
                                        Text = EzEditorStrings.TOOLBAR_VISUAL_PICKER,
                                        TextSize = 14,
                                        Active = { BindTarget = VisualPickerOpen },
                                    },
                                    layerButton = new EzEditorLayerButton
                                    {
                                        GetAvailableLayers = () => EzSkinEditorLayerOptions.For(CurrentScene.Value).Select(d => d.Layer).ToList(),
                                        GetDisplayName = layer => EzSkinEditorLayerOptions.GetDisplayName(layer, Ruleset.Value),
                                        ActiveLayer = ActiveLayer,
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            CurrentScene.BindValueChanged(e =>
            {
                updateActiveTab(e.NewValue);
                ActiveLayer.Value = EzSkinEditorLayerOptions.Normalize(e.NewValue, ActiveLayer.Value);
            }, true);

            Ruleset.BindValueChanged(_ => layerButton.RefreshDisplayText(), true);
        }

        private IEnumerable<Drawable> buildGroupButtons() => EzSkinEditorSceneRegistry.Groups.Select(group =>
        {
            var button = new GroupTabButton(group, getGroupTitle(group), () => GroupRequested?.Invoke(group));
            groupButtons[group] = button;
            return button;
        });

        private static LocalisableString getGroupTitle(EzEditorSceneGroup group) => group switch
        {
            EzEditorSceneGroup.SongSelect => EzEditorStrings.SCENE_GROUP_SONG_SELECT,
            _ => EzEditorStrings.SCENE_GROUP_GAMEPLAY,
        };

        private void updateActiveTab(EzEditorSceneType scene)
        {
            var activeGroup = EzSkinEditorSceneRegistry.GroupOf(scene);

            foreach (var (group, button) in groupButtons)
                button.Active = group == activeGroup;
        }

        private partial class GroupTabButton : EzSceneBarButton
        {
            public EzEditorSceneGroup Group { get; }

            public GroupTabButton(EzEditorSceneGroup group, LocalisableString text, Action action)
            {
                Group = group;
                Text = text;
                Action = action;
            }
        }
    }
}
