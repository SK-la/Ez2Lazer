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
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osuTK;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzKeyModeSelector : CompositeDrawable
    {
        private static readonly int[] all_items =
        {
            1, 2, 3,
            4, 5, 6, 7, 8, 9, 10,
            12, 14, 16, 18
        };

        public static List<int> GetModesForRuleset(int rulesetId)
        {
            if (rulesetId == 3)
                return all_items.Where(v => v >= 4).ToList();

            return all_items.Where(v => v <= 12).ToList();
        }

        public static IReadOnlyList<int> GetSelectedModeValues(int rulesetId, IEnumerable<string> selectedModeIds)
        {
            var selected = selectedModeIds as ICollection<string> ?? selectedModeIds.ToList();

            if (selected.Count == 0)
                return Array.Empty<int>();

            return GetModesForRuleset(rulesetId)
                   .Where(m => selected.Contains(m.ToString()))
                   .ToList();
        }

        public void ApplyToFilterCriteria(FilterCriteria criteria)
        {
            int rulesetId = ruleset.Value.OnlineID;
            var modes = GetSelectedModeValues(rulesetId, SelectedModeIds);

            if (modes.Count == 0)
                return;

            if (rulesetId == 3)
            {
                if (criteria.RulesetCriteria != null)
                    criteria.RulesetCriteria.TryParseCustomKeywordCriteria("keys", Operator.Equal, string.Join(",", modes));
            }
            else
            {
                criteria.CircleSize = new FilterCriteria.OptionalRange<float>
                {
                    Min = modes.Min() - 0.5f,
                    Max = modes.Max() + 0.5f,
                    IsLowerInclusive = false,
                    IsUpperInclusive = false
                };
            }
        }

        private readonly Dictionary<int, HashSet<string>> modeSelections = new Dictionary<int, HashSet<string>>();
        private readonly BindableBool isMultiSelectMode = new BindableBool(true);
        private Bindable<string> keyModeId = new Bindable<string>();

        private ShearedButton labelButton = null!;
        private ShearedCsModeTabControl tabControl = null!;
        // private ShearedToggleButton multiSelectButton = null!;

        private int currentRulesetId = -1;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public IBindable<string> Current => keyModeId;

        public HashSet<string> SelectedModeIds { get; } = new HashSet<string>();

        public EzKeyModeSelector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Shear = OsuGame.SHEAR;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            CornerRadius = 2;
            Masking = true;

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
                        // new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            labelButton = new ShearedButton
                            {
                                Text = "Keys",
                                TextSize = 16,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                AutoSizeAxes = Axes.X,
                                Height = 30f,
                                Shear = new Vector2(0),
                                TooltipText = EzSongSelectStrings.CLEAR_SELECTION,
                                Action = () =>
                                {
                                    setSelection(new HashSet<string>());
                                    persistSelection();
                                }
                            },
                            tabControl = new ShearedCsModeTabControl
                            {
                                RelativeSizeAxes = Axes.X,
                                Shear = new Vector2(0),
                            },
                            // multiSelectButton = new ShearedToggleButton
                            // {
                            //     Anchor = Anchor.Centre,
                            //     Origin = Anchor.Centre,
                            //     Shear = new Vector2(0),
                            //     Text = "K +",
                            //     Height = 30f,
                            //     TooltipText = EzSongSelectStrings.MULTI_SELECT_BUTTON_TOOLTIP,
                            // }
                        }
                    }
                }
            };

            // multiSelectButton.Active.BindTo(isMultiSelectMode);

            keyModeId = ezConfig.GetBindable<string>(Ez2Setting.EzSelectCsMode);
            keyModeId.BindValueChanged(onPersistedSelectionChanged, true);

            isMultiSelectMode.BindValueChanged(_ => persistSelection(), true);
            ruleset.BindValueChanged(onRulesetChanged, true);

            tabControl.SelectionChanged = onTabSelectionChanged;
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> e)
        {
            if (currentRulesetId >= 0)
                modeSelections[currentRulesetId] = new HashSet<string>(SelectedModeIds);

            int id = e.NewValue.OnlineID;
            currentRulesetId = id;

            var validIds = getValidModeIdSet(id);

            if (!modeSelections.TryGetValue(id, out var selectionForRuleset))
                selectionForRuleset = parseModeIds(keyModeId.Value);

            selectionForRuleset.IntersectWith(validIds);
            setSelection(selectionForRuleset);
            modeSelections[id] = new HashSet<string>(SelectedModeIds);

            if (id == 1) // Taiko
            {
                Hide();
                SelectedModeIds.Clear();
            }
            else
            {
                Show();
            }

            tabControl.UpdateForRuleset(id);
            labelButton.Text = id == 3 ? "Keys" : "CS";

            persistSelection();
        }

        private void onPersistedSelectionChanged(ValueChangedEvent<string> e)
        {
            var modes = parseModeIds(e.NewValue);
            setSelection(modes);
            syncTabVisuals();
        }

        private void onTabSelectionChanged(HashSet<string> modes)
        {
            setSelection(modes);
            persistSelection();
        }

        private HashSet<string> parseModeIds(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new HashSet<string>();

            return new HashSet<string>(value.Split(','));
        }

        private void setSelection(HashSet<string> modeIds)
        {
            SelectedModeIds.Clear();
            SelectedModeIds.UnionWith(modeIds);
        }

        private void persistSelection()
        {
            int activeRulesetId = ruleset.Value.OnlineID;
            modeSelections[activeRulesetId] = new HashSet<string>(SelectedModeIds);
            keyModeId.Value = formatModeIds(SelectedModeIds);
            syncTabVisuals();
        }

        private void syncTabVisuals()
        {
            int activeRulesetId = ruleset.Value.OnlineID;
            tabControl.UpdateForRuleset(activeRulesetId);
            tabControl.UpdateTabItemUI(SelectedModeIds);
            tabControl.IsMultiSelectMode = isMultiSelectMode.Value;
        }

        private string formatModeIds(HashSet<string> selectedModes)
        {
            if (selectedModes.Count == 0)
                return string.Empty;

            if (isMultiSelectMode.Value)
                return string.Join(",", selectedModes.OrderBy(x => x));

            return selectedModes.First();
        }

        private static HashSet<string> getValidModeIdSet(int rulesetId) =>
            GetModesForRuleset(rulesetId).Select(m => m.ToString()).ToHashSet();

        public partial class ShearedCsModeTabControl : OsuTabControl<string>
        {
            private HashSet<string> displayedSelection = new HashSet<string>();
            private int currentRulesetId = -1;

            public bool IsMultiSelectMode { get; set; }

            public Action<HashSet<string>>? SelectionChanged;

            // [Resolved]
            // private OverlayColourProvider colourProvider { get; set; } = null!;

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

                var keyModes = GetModesForRuleset(rulesetId);

                TabContainer.Clear();
                Items = keyModes.Select(v => v.ToString()).ToList(); // 按钮文字就是数字

                Schedule(() =>
                {
                    int count = keyModes.Count;

                    if (count > 0)
                    {
                        float totalWidth = DrawWidth;
                        float itemWidth = (totalWidth - (count * 2f)) / count;
                        foreach (var tab in TabContainer.Children.Cast<ShearedCsModeTabItem>())
                            tab.Width = itemWidth;
                    }
                });

                UpdateTabItemUI(displayedSelection);
            }

            public void UpdateTabItemUI(HashSet<string> selectedModes)
            {
                displayedSelection = new HashSet<string>(selectedModes);

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
                var newSelection = new HashSet<string>(displayedSelection);

                if (!newSelection.Remove(mode))
                {
                    if (IsMultiSelectMode)
                        newSelection.Add(mode);
                    else
                    {
                        newSelection.Clear();
                        newSelection.Add(mode);
                    }
                }

                SelectionChanged?.Invoke(newSelection);
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
                    // Margin = new MarginPadding { Left = 4 };

                    InternalChildren = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        text = new OsuSpriteText
                        {
                            Text = value,
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
