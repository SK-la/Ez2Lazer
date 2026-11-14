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
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    /// <summary>
    /// 垂直分组导航轨。底色与场景库一致，按钮为 Ez 渐变开关态。
    /// 右侧侧栏分组轨与左侧二级场景栏共用同一套外观与选中逻辑。
    /// </summary>
    public abstract partial class EzVerticalRail : CompositeDrawable
    {
        public const float WIDTH = 56;

        private const float dual_line_extra_height = 14;

        /// <summary>
        /// Raised only when the user picks an item by pressing its button. Programmatic changes to
        /// <see cref="SelectedIndex"/> (group rebuilds, external sync) do not raise it, so a rail that
        /// mirrors an external bindable does not write the first item back on load.
        /// </summary>
        protected event Action<int>? SelectionChanged;

        public readonly BindableInt SelectedIndex = new BindableInt();

        private readonly List<Item> items = new List<Item>();
        private FillFlowContainer buttonFlow = null!;

        protected EzVerticalRail()
        {
            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background6,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = buttonFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, EzUIStyleV1.SCENE_BAR_BUTTON_SPACING),
                        Padding = new MarginPadding(EzUIStyleV1.SCENE_BAR_GAP),
                    },
                },
            };

            // 子类可能在构造期就设好了项目（早于 BDL），此时按钮树才建立起来。
            rebuildButtons();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SelectedIndex.BindValueChanged(_ => updateActive(), true);
        }

        public void SetItems(IReadOnlyList<(LocalisableString Title, bool Visible)> next) =>
            SetItems(next.Select(i => (i.Title, (LocalisableString?)null, i.Visible)).ToList());

        public void SetItems(IReadOnlyList<(LocalisableString Primary, LocalisableString? Secondary, bool Visible)> next)
        {
            items.Clear();

            foreach (var (primary, secondary, visible) in next)
                items.Add(new Item(primary, secondary, visible));

            if (buttonFlow != null)
                rebuildButtons();
        }

        public void SetItemVisible(int index, bool visible)
        {
            if (index < 0 || index >= items.Count)
                return;

            items[index].Visible = visible;
            updateButtonVisibility();
            normalizeSelection();
        }

        private void rebuildButtons()
        {
            buttonFlow.Clear();

            for (int i = 0; i < items.Count; i++)
            {
                int index = i;

                var button = new RailButton(items[i].Primary, items[i].Secondary, () =>
                {
                    SelectedIndex.Value = index;
                    SelectionChanged?.Invoke(index);
                })
                {
                    Alpha = items[i].Visible ? 1 : 0,
                    AlwaysPresent = items[i].Visible,
                };

                if (!items[i].Visible)
                    button.Hide();

                buttonFlow.Add(button);
            }

            updateButtonVisibility();
            normalizeSelection();
        }

        private void updateButtonVisibility()
        {
            int i = 0;

            foreach (var child in buttonFlow)
            {
                if (child is not RailButton button)
                    continue;

                if (items[i].Visible)
                    button.Show();
                else
                    button.Hide();

                i++;
            }
        }

        private void normalizeSelection()
        {
            if (items.Count == 0)
                return;

            if (SelectedIndex.Value < 0 || SelectedIndex.Value >= items.Count || !items[SelectedIndex.Value].Visible)
            {
                int firstVisible = items.FindIndex(i => i.Visible);
                SelectedIndex.Value = firstVisible >= 0 ? firstVisible : 0;
            }

            updateActive();
        }

        private void updateActive()
        {
            int i = 0;

            foreach (var child in buttonFlow)
            {
                if (child is RailButton button)
                    button.Active = i == SelectedIndex.Value;

                i++;
            }
        }

        private sealed class Item
        {
            public readonly LocalisableString Primary;
            public readonly LocalisableString? Secondary;

            public bool Visible;

            public Item(LocalisableString primary, LocalisableString? secondary, bool visible)
            {
                Primary = primary;
                Secondary = secondary;
                Visible = visible;
            }
        }

        private partial class RailButton : EzButton
        {
            public bool Active
            {
                get => Accented;
                set => Accented = value;
            }

            public RailButton(LocalisableString primary, LocalisableString? secondary, Action action)
            {
                Action = action;
                RelativeSizeAxes = Axes.X;

                if (secondary == null)
                {
                    Text = primary;
                    Height = EzUIStyleV1.SceneBarControlHeight();
                    TextSize = 12;
                    TextMargin = new MarginPadding(4);
                    return;
                }

                Height = EzUIStyleV1.SceneBarControlHeight() + dual_line_extra_height;
                TextSize = 0.001f;

                Add(new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 1),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = primary,
                            Font = OsuFont.TorusAlternate.With(size: 10),
                        },
                        new OsuSpriteText
                        {
                            Text = secondary.Value,
                            Font = OsuFont.TorusAlternate.With(size: 9),
                        },
                    },
                });
            }
        }
    }
}
