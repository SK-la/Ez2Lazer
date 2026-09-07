// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Track-mode skills body: key-count chips + SSR skill bars from Realm.
    /// </summary>
    public partial class EzLocalProfileTrackSkillsBody : FillFlowContainer
    {
        private readonly string username;

        private readonly BindableInt selectedKeyCount = new BindableInt();
        private FillFlowContainer keyChipFlow = null!;
        private FillFlowContainer skillBarsFlow = null!;
        private OsuSpriteText emptyHint = null!;

        [Resolved]
        private EzSkillProvider skillProvider { get; set; } = null!;

        public EzLocalProfileTrackSkillsBody(string username)
        {
            this.username = username;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 14);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                keyChipFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(8),
                },
                emptyHint = new OsuSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Font = OsuFont.GetFont(size: 14),
                    Alpha = 0,
                },
                skillBarsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedKeyCount.BindValueChanged(_ => refreshSkillBars(), false);
            rebuild();
        }

        private void rebuild()
        {
            keyChipFlow.Clear();
            skillBarsFlow.Clear();

            var keyCounts = skillProvider.GetPlayerSsrKeyCounts(username);

            if (keyCounts.Count == 0)
            {
                emptyHint.Text = EzSettingsProfile.LOCAL_PROFILE_TRACK_EMPTY;
                emptyHint.Show();
                return;
            }

            emptyHint.Hide();

            foreach (int key in keyCounts)
            {
                keyChipFlow.Add(new KeyChip(key, selectedKeyCount)
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                });
            }

            if (!keyCounts.Contains(selectedKeyCount.Value))
                selectedKeyCount.Value = keyCounts[0];
            else
                refreshSkillBars();
        }

        private void refreshSkillBars()
        {
            skillBarsFlow.Clear();

            int keyCount = selectedKeyCount.Value;
            if (keyCount <= 0)
                return;

            var skills = skillProvider.GetPlayerSsr(username, keyCount);
            var definitions = skillProvider.Registry.GetSystem(EzSkillSystems.PLAYER_SSR)?.Skills
                              ?? Array.Empty<EzSkillDefinition>();

            double max = skills.Values.DefaultIfEmpty(0).Max();
            if (max <= 0)
                max = 1;

            foreach (var def in definitions)
            {
                skills.TryGetValue(def.SkillId, out double value);
                float ratio = (float)(value / max);
                skillBarsFlow.Add(new SkillBarRow(def.DisplayName, value, ratio, Colour4.FromHex(def.AccentHex)));
            }
        }

        private partial class KeyChip : OsuClickableContainer
        {
            private readonly int keyCount;
            private readonly BindableInt selected;
            private Box background = null!;
            private OsuSpriteText label = null!;

            public KeyChip(int keyCount, BindableInt selected)
            {
                this.keyCount = keyCount;
                this.selected = selected;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5,
                    },
                    new Container
                    {
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Child = label = new OsuSpriteText
                        {
                            Text = $"{keyCount}K",
                            Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        }
                    }
                };

                Action = () => selected.Value = keyCount;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selected.BindValueChanged(_ => updateVisual(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateVisual();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateVisual();
                base.OnHoverLost(e);
            }

            private void updateVisual()
            {
                bool active = selected.Value == keyCount;
                label.Font = label.Font.With(weight: active ? FontWeight.Bold : FontWeight.SemiBold);
                background.FadeTo(active || IsHovered ? 1f : 0.7f, 80);
            }
        }

        private partial class SkillBarRow : Container
        {
            public SkillBarRow(string displayName, double value, float ratio, Colour4 accent)
            {
                RelativeSizeAxes = Axes.X;
                Height = 22;
                Padding = new MarginPadding { Horizontal = 4 };

                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = displayName,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                        Width = 96,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = 104, Right = 56 },
                        Child = new EzLocalProfileRoundedBar(ratio, accent),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = value.ToString("0.00", CultureInfo.InvariantCulture),
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    },
                };
            }
        }
    }
}
