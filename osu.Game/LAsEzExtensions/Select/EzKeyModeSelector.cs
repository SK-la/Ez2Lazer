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
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.LAsEzExtensions.Select
{
    public partial class EzKeyModeSelector : CompositeDrawable
    {
        private Bindable<string> keyModeId = new Bindable<string>();
        private readonly BindableBool isMultiSelectMode = new BindableBool();
        private readonly Dictionary<int, HashSet<string>> modeSelections = new Dictionary<int, HashSet<string>>();

        private ShearedButton labelButton = null!;
        private ShearedCsModeTabControl tabControl = null!;
        private ShearedToggleButton multiSelectButton = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public IBindable<string> Current => tabControl.Current;

        public EzKeyModeFilter EzKeyModeFilter { get; } = new EzKeyModeFilter();

        public EzKeyModeSelector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            CornerRadius = 8;
            Masking = true;
            Shear = OsuGame.SHEAR;
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
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            labelButton = new ShearedButton(50, 30)
                            {
                                Text = "Keys",
                                TextSize = 16,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Shear = new Vector2(0),
                                TooltipText = "Clear selection",
                            },
                            tabControl = new ShearedCsModeTabControl
                            {
                                RelativeSizeAxes = Axes.X,
                                Shear = new Vector2(0),
                            },
                            multiSelectButton = new ShearedToggleButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Shear = new Vector2(0),
                                Text = "K +",
                                Height = 30f,
                                TooltipText = "Enable multi-select",
                            }
                        }
                    }
                }
            };

            multiSelectButton.Active.BindTo(isMultiSelectMode);

            labelButton.Action = () => EzKeyModeFilter.SetSelection(new HashSet<string>());

            keyModeId = ezConfig.GetBindable<string>(Ez2Setting.EzSelectCsMode);
            keyModeId.BindValueChanged(onSelectorChanged, true);

            isMultiSelectMode.BindValueChanged(_ => updateValue(), true);
            ruleset.BindValueChanged(onRulesetChanged, true);
            EzKeyModeFilter.SelectionChanged += updateValue;

            tabControl.Current.BindTarget = keyModeId;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            EzKeyModeFilter.SelectionChanged -= updateValue;
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> e)
        {
            tabControl.UpdateForRuleset(e.NewValue.OnlineID);
            labelButton.Text = e.NewValue.OnlineID == 3 ? "Keys" : "CS";
            updateValue();
        }

        private void onSelectorChanged(ValueChangedEvent<string> e)
        {
            var modes = parseModeIds(e.NewValue);
            EzKeyModeFilter.SetSelection(modes);
            tabControl.UpdateTabItemUI(modes);
        }

        private void updateValue()
        {
            int currentRulesetId = ruleset.Value.OnlineID;

            if (!modeSelections.ContainsKey(currentRulesetId))
                modeSelections[currentRulesetId] = new HashSet<string>();

            HashSet<string> selectedModes = EzKeyModeFilter.SelectedModeIds;

            if (selectedModes.Count == 0)
            {
                keyModeId.Value = "";
            }
            else
            {
                if (isMultiSelectMode.Value)
                {
                    keyModeId.Value = string.Join(",", selectedModes.OrderBy(x => x));
                }
                else
                {
                    keyModeId.Value = selectedModes.First();
                }
            }

            modeSelections[currentRulesetId] = selectedModes;
            tabControl.UpdateForRuleset(currentRulesetId);
            tabControl.UpdateTabItemUI(selectedModes);
            tabControl.IsMultiSelectMode = isMultiSelectMode.Value;
        }

        private HashSet<string> parseModeIds(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new HashSet<string>();

            return new HashSet<string>(value.Split(','));
        }

        public partial class ShearedCsModeTabControl : OsuTabControl<string>
        {
            private HashSet<string> currentSelection = new HashSet<string>();
            private int currentRulesetId = -1;

            public bool IsMultiSelectMode { get; set; }

            public Action<HashSet<string>>? SetCurrentSelections;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ShearedCsModeTabControl()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Shear = OsuGame.SHEAR;
                Masking = true;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                TabContainer.Anchor = Anchor.CentreLeft;
                TabContainer.Origin = Anchor.CentreLeft;
                // TabContainer.Shear = OsuGame.SHEAR;
                TabContainer.RelativeSizeAxes = Axes.X;
                TabContainer.AutoSizeAxes = Axes.Y;
                TabContainer.Spacing = new Vector2(0f);
            }

            public void UpdateForRuleset(int rulesetId)
            {
                if (currentRulesetId == rulesetId && Items.Any())
                    return;

                currentRulesetId = rulesetId;

                var keyModes = CsItemIds.GetModesForRuleset(rulesetId)
                                        .Select(m => m.Id)
                                        .ToList();

                TabContainer.Clear();
                Items = keyModes;

                Schedule(() =>
                {
                    int count =  keyModes.Count;

                    if (count > 0)
                    {
                        float totalWidth = DrawWidth;
                        float spacing = 2f;
                        float itemWidth = (totalWidth - (count * spacing)) / count;
                        foreach (var tab in TabContainer.Children.Cast<ShearedCsModeTabItem>())
                            tab.Width = itemWidth;
                    }
                });

                UpdateTabItemUI(currentSelection);
            }

            public void UpdateTabItemUI(HashSet<string> selectedModes)
            {
                currentSelection = new HashSet<string>(selectedModes);

                foreach (var tabItem in TabContainer.Children.Cast<ShearedCsModeTabItem>())
                {
                    bool isSelected = selectedModes.Contains(tabItem.Value);
                    tabItem.UpdateButton(isSelected);
                }
            }

            protected override Dropdown<string> CreateDropdown() => null!;
            // protected override bool AddEnumEntriesAutomatically => false;

            protected override TabItem<string> CreateTabItem(string value)
            {
                var tabItem = new ShearedCsModeTabItem(value);
                tabItem.Clicked += onTabItemClicked;
                return tabItem;
            }

            private void onTabItemClicked(string mode)
            {
                var newSelection = new HashSet<string>(currentSelection);

                if (!newSelection.Remove(mode))
                {
                    if (IsMultiSelectMode)
                    {
                        newSelection.Add(mode);
                    }
                    else
                    {
                        newSelection.Clear();
                        newSelection.Add(mode);
                    }
                }

                currentSelection = newSelection;
                Current.Value = newSelection.Count == 0 ? "" : string.Join(",", newSelection.OrderBy(x => x));
                UpdateTabItemUI(newSelection);

                SetCurrentSelections?.Invoke(newSelection);
            }

            public partial class ShearedCsModeTabItem : TabItem<string>
            {
                private readonly OsuSpriteText text;
                private readonly Box background;
                private OverlayColourProvider colourProvider = null!;

                public event Action<string>? Clicked;

                public ShearedCsModeTabItem(string value)
                    : base(value)
                {
                    // Shear = OsuGame.SHEAR;
                    CornerRadius = ShearedButton.CORNER_RADIUS;
                    Masking = true;
                    // Width = 40;
                    AutoSizeAxes = Axes.Y;
                    Margin = new MarginPadding { Left = 4 };

                    var modeInfo = CsItemIds.GetById(value);

                    InternalChildren = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
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
                        },
                    };
                }

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider colourProvider)
                {
                    this.colourProvider = colourProvider;
                    background.Colour = colourProvider.Background5;
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    if (Width > 40) Width = 40;
                    // if (Width < 30) Width = 30;
                }

                public void UpdateButton(bool isSelected)
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
                            background.FadeColour(colourProvider.Background5, 150, Easing.OutQuint);
                            text.FadeColour(Colour4.White, 150, Easing.OutQuint);
                        }
                    }
                }

                protected override void OnActivated() => Schedule(updateColours);
                protected override void OnDeactivated() => Schedule(updateColours);

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
