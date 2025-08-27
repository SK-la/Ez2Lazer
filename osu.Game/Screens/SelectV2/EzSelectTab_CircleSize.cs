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
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Framework.Logging;

namespace osu.Game.Screens.SelectV2
{
    public partial class CircleSizeSelectorTab : CompositeDrawable
    {
        private ShearedKeyModeTabControl tabControl = null!;
        private ShearedToggleButton multiSelectButton = null!;
        private readonly Bindable<string> keyModeId = new Bindable<string>("All");
        private readonly BindableBool isMultiSelectMode = new BindableBool();

        private readonly Dictionary<int, HashSet<string>> modeSelections = new Dictionary<int, HashSet<string>>();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public IBindable<string> Current => tabControl.Current;

        public MultiSelectEzKeyMode MultiSelect { get; } = new MultiSelectEzKeyMode();

        public CircleSizeSelectorTab()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CornerRadius = 8;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Shear = -OsuGame.SHEAR,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new[]
                        {
                            tabControl = new ShearedKeyModeTabControl
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                            Empty(),
                            multiSelectButton = new ShearedToggleButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "Multi",
                                Height = 30f,
                            }
                        }
                    }
                }
            };

            keyModeId.BindTo(config.GetBindable<string>(OsuSetting.SelectEzMode));
            tabControl.Current.BindTarget = keyModeId;

            multiSelectButton.Active.BindTo(isMultiSelectMode);
            isMultiSelectMode.BindValueChanged(_ => syncSelection(), true);
            MultiSelect.SelectionChanged += syncSelection;
            keyModeId.BindValueChanged(_ => syncSelection(), true);
            ruleset.BindValueChanged(_ => syncSelection(), true);

            tabControl.GetCurrentModeId = () => ruleset.Value.OnlineID;
            tabControl.GetCurrentSelections = () => modeSelections[ruleset.Value.OnlineID];
            tabControl.SetCurrentSelections = s => modeSelections[ruleset.Value.OnlineID] = new HashSet<string>(s);
            tabControl.GetIsMultiSelect = () => isMultiSelectMode.Value;
            tabControl.SetKeyMode = m => keyModeId.Value = m;

            syncSelection();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            MultiSelect.SelectionChanged -= syncSelection;
        }

        private void syncSelection()
        {
            int modeId = ruleset.Value.OnlineID;

            if (!modeSelections.ContainsKey(modeId))
                modeSelections[modeId] = new HashSet<string> { "All" };

            if (isMultiSelectMode.Value)
            {
                if (!MultiSelect.SelectedModeIds.SetEquals(modeSelections[modeId]))
                {
                    MultiSelect.SetSelection(modeSelections[modeId]);
                    keyModeId.Value = string.Join(",", modeSelections[modeId].OrderBy(x => x));
                }
            }
            else
            {
                if (modeSelections[modeId].Count >= 1)
                    keyModeId.Value = modeSelections[modeId].First();
            }

            tabControl.UpdateItemsForRuleset(modeId, modeSelections[modeId]);

            if (!MultiSelect.SelectedModeIds.SetEquals(modeSelections[modeId]))
                MultiSelect.SetSelection(modeSelections[modeId]);
        }

        public partial class ShearedKeyModeTabControl : OsuTabControl<string>
        {
            public Container LabelContainer;

            private readonly Box labelBox;

            // private readonly LayoutValue drawSizeLayout = new LayoutValue(Invalidation.DrawSize);
            public float TabHeight { get; set; } = 30f;
            public float TabSpacing { get; set; } = 5f;

            // public Action<EzSelectMode>? OnTabItemClicked;

            public Func<int>? GetCurrentModeId;
            public Func<HashSet<string>>? GetCurrentSelections;
            public Action<HashSet<string>>? SetCurrentSelections;
            public Func<bool>? GetIsMultiSelect;
            public Action<string>? SetKeyMode;

            private int currentRulesetId = -1;

            public ShearedKeyModeTabControl()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Shear = OsuGame.SHEAR;
                CornerRadius = ShearedButton.CORNER_RADIUS;
                Masking = true;
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Colour = Colour4.Black.Opacity(0.15f),
                    Radius = 8,
                    Offset = new Vector2(0, 2),
                };
                // AddLayout(drawSizeLayout);
                LabelContainer = new Container
                {
                    Depth = float.MaxValue,
                    CornerRadius = ShearedButton.CORNER_RADIUS,
                    Masking = true,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        labelBox = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        new OsuSpriteText
                        {
                            Text = "Keys",
                            Margin = new MarginPadding
                                { Horizontal = 8f, Vertical = 7f },
                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                            Shear = -OsuGame.SHEAR,
                        },
                    }
                };
                AddInternal(LabelContainer);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                labelBox.Colour = colourProvider.Background3;

                TabContainer.Anchor = Anchor.CentreLeft;
                TabContainer.Origin = Anchor.CentreLeft;
                TabContainer.Shear = -OsuGame.SHEAR;
                TabContainer.RelativeSizeAxes = Axes.X;
                // TabContainer.AutoSizeAxes = Axes.Y;
                TabContainer.Height = TabHeight;
                TabContainer.Spacing = new Vector2(0f);
                TabContainer.Margin = new MarginPadding
                {
                    Left = LabelContainer.DrawWidth + 8,
                };

                const int mode_id = 3;
                var keyModes = EzKeyModes.GetModesForRuleset(mode_id)
                                         .OrderBy(m => m.Id == "All" ? -1 : m.KeyCount ?? 0)
                                         .Select(m => m.Id)
                                         .ToList();

                currentRulesetId = mode_id;
                Items = keyModes;
            }

            public void UpdateItemsForRuleset(int modeId, HashSet<string> selectedModes)
            {
                if (!IsLoaded) return;

                var keyModes = EzKeyModes.GetModesForRuleset(modeId)
                                         .OrderBy(m => m.Id == "All" ? -1 : m.KeyCount ?? 0)
                                         .Select(m => m.Id)
                                         .ToList();

                if (currentRulesetId != modeId)
                {
                    currentRulesetId = modeId;
                    TabContainer.Clear();
                    Items = keyModes;
                    Logger.Log($"[UpdateItemsForRuleset] Rebuilding buttons for ruleset {modeId}, keyModes=[{string.Join(",", keyModes)}]");
                }

                RefreshTabItems(selectedModes, keyModes);
            }

            public void RefreshTabItems(HashSet<string> selectedModes, List<string> keyModes)
            {
                foreach (var tabItem in TabContainer.Children.Cast<ShearedKeyModeTabItem>())
                {
                    if (!keyModes.Contains(tabItem.Value))
                        continue;

                    bool isSelected = selectedModes.Contains(tabItem.Value);
                    tabItem.UpdateMultiSelectState(isSelected);
                }
            }

            public void ApplyTabLayout()
            {
                if (IsLoaded)
                {
                    TabContainer.Spacing = new Vector2(TabSpacing, 0);
                    foreach (var tabItem in TabContainer.Children.Cast<ShearedKeyModeTabItem>())
                        tabItem.Height = TabHeight;
                }
            }

            protected override Dropdown<string> CreateDropdown() => null!;
            // protected override bool AddEnumEntriesAutomatically => false;

            protected override TabItem<string> CreateTabItem(string value)
            {
                var tabItem = new ShearedKeyModeTabItem(value);
                tabItem.Clicked += mode =>
                {
                    var selections = GetCurrentSelections?.Invoke();
                    bool isMulti = GetIsMultiSelect?.Invoke() ?? false;
                    if (selections == null) return;

                    handleSelectionChange(mode, selections, isMulti);

                    SetCurrentSelections?.Invoke(selections);
                    SetKeyMode?.Invoke(isMulti
                        ? string.Join(",", selections.OrderBy(x => x))
                        : selections.First());

                    updateButtonsState(tabItem, selections, isMulti);
                };
                return tabItem;
            }

            private void handleSelectionChange(string mode, HashSet<string> selections, bool isMulti)
            {
                if (mode == "All")
                {
                    selections.Clear();
                    selections.Add("All");
                    return;
                }

                if (isMulti)
                    handleMultiSelect(mode, selections);
                else
                    handleSingleSelect(mode, selections);
            }

            private void handleMultiSelect(string mode, HashSet<string> selections)
            {
                if (selections.Contains("All"))
                {
                    selections.Clear();
                    selections.Add(mode);
                }
                else
                {
                    if (!selections.Remove(mode))
                        selections.Add(mode);
                    if (selections.Count == 0)
                        selections.Add("All");
                }
            }

            private void handleSingleSelect(string mode, HashSet<string> selections)
            {
                if (selections.Contains(mode) && selections.Count == 1)
                {
                    selections.Clear();
                    selections.Add("All");
                }
                else
                {
                    selections.Clear();
                    selections.Add(mode);
                }
            }

            private void updateButtonsState(ShearedKeyModeTabItem clickedItem, HashSet<string> selections, bool isMulti)
            {
                bool wasSelected = selections.Contains(clickedItem.Value);
                bool isSelected = selections.Contains(clickedItem.Value);

                if (wasSelected != isSelected)
                    clickedItem.UpdateMultiSelectState(isSelected);

                // 如果是 All 或者切换到 All，需要更新所有按钮
                if (clickedItem.Value == "All" || selections.Contains("All"))
                {
                    foreach (var item in TabContainer.Children.Cast<ShearedKeyModeTabItem>())
                    {
                        if (item != clickedItem)
                            item.UpdateMultiSelectState(selections.Contains(item.Value));
                    }
                }
                // 在单选模式下，需要更新之前选中的按钮
                else if (!isMulti)
                {
                    foreach (var item in TabContainer.Children.Cast<ShearedKeyModeTabItem>())
                    {
                        if (item != clickedItem && item.Active.Value)
                            item.UpdateMultiSelectState(false);
                    }
                }
            }

            public partial class ShearedKeyModeTabItem : TabItem<string>
            {
                private readonly OsuSpriteText text;
                private readonly Box background;

                // private bool isMania;
                private OverlayColourProvider colourProvider = null!;

                public event Action<string>? Clicked;

                public ShearedKeyModeTabItem(string value)
                    : base(value)
                {
                    Shear = OsuGame.SHEAR;
                    CornerRadius = ShearedButton.CORNER_RADIUS;
                    Masking = true;
                    AutoSizeAxes = Axes.Both;

                    Margin = value == "All" ? new MarginPadding { Left = 4 } : new MarginPadding(0);

                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    };

                    var modeInfo = EzKeyModes.GetById(value);
                    text = new OsuSpriteText
                    {
                        Text = modeInfo?.DisplayName ?? value,
                        Margin = new MarginPadding
                            { Horizontal = 10f, Vertical = 7f },
                        Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Shear = -OsuGame.SHEAR,
                        Colour = Colour4.White,
                    };

                    AddInternal(background);
                    AddInternal(text);
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    this.colourProvider = colourProvider;
                    background.Colour = colourProvider.Background6;
                }

                public void UpdateMultiSelectState(bool isSelected)
                {
                    if (Active.Value != isSelected)
                    {
                        Active.Value = isSelected;
                        Schedule(updateColours);
                    }
                }

                private void updateColours()
                {
                    using (BeginDelayedSequence(0))
                    {
                        if (Active.Value)
                        {
                            background.FadeColour(colourProvider.Light4, 150, Easing.OutQuint);
                            text.FadeColour(Colour4.Black, 150, Easing.OutQuint);
                        }
                        else if (IsHovered)
                        {
                            background.FadeColour(colourProvider.Background4, 150, Easing.OutQuint);
                            text.FadeColour(Colour4.White, 150, Easing.OutQuint);
                        }
                        else
                        {
                            background.FadeColour(colourProvider.Background6, 150, Easing.OutQuint);
                            text.FadeColour(Colour4.White, 150, Easing.OutQuint);
                        }
                    }
                }

                protected override void OnActivated()
                {
                    Schedule(updateColours);
                }

                protected override void OnDeactivated()
                {
                    Schedule(updateColours);
                }

                protected override bool OnHover(HoverEvent e)
                {
                    Schedule(updateColours);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    Schedule(updateColours);
                    base.OnHoverLost(e);
                }

                protected override bool OnClick(ClickEvent e)
                {
                    Clicked?.Invoke(Value);
                    return true;
                }
            }
        }
    }
}
