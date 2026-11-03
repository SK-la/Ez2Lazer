// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Ez / Track toggle: label + two Insight-style chips (Background5 → Background6).
    /// </summary>
    public partial class EzLocalProfileAnalysisSystemSelector : CompositeDrawable, IHasCurrentValue<EzLocalProfileAnalysisSystem>
    {
        private readonly BindableWithCurrent<EzLocalProfileAnalysisSystem> current =
            new BindableWithCurrent<EzLocalProfileAnalysisSystem>(EzLocalProfileAnalysisSystem.Ez);

        public Bindable<EzLocalProfileAnalysisSystem> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public EzLocalProfileAnalysisSystemSelector()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = EzSettingsProfile.LOCAL_PROFILE_ANALYSIS_SYSTEM,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        Colour = colours.Content2,
                    },
                    new SystemChip(EzLocalProfileAnalysisSystem.Ez, "Ez", current),
                    new SystemChip(EzLocalProfileAnalysisSystem.Track, "Track", current),
                }
            };
        }

        private partial class SystemChip : OsuClickableContainer
        {
            private readonly EzLocalProfileAnalysisSystem value;
            private readonly LocalisableString label;
            private readonly Bindable<EzLocalProfileAnalysisSystem> current;
            private EzLocalProfileHoverBox background = null!;
            private OsuSpriteText text = null!;

            public SystemChip(EzLocalProfileAnalysisSystem value, LocalisableString label, Bindable<EzLocalProfileAnalysisSystem> current)
            {
                this.value = value;
                this.label = label;
                this.current = current;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 8;
                Action = () => this.current.Value = this.value;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                background = new EzLocalProfileHoverBox();
                background.Configure(colours);

                Children = new Drawable[]
                {
                    background,
                    text = new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Text = label,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        Colour = colours.Content1,
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(_ => refresh(), true);
            }

            private void refresh()
            {
                bool selected = current.Value == value;
                background.SetSelected(selected);
                background.Refresh(IsHovered);
                text.Font = text.Font.With(weight: selected ? FontWeight.Bold : FontWeight.SemiBold);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.Refresh(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.Refresh(false);
                base.OnHoverLost(e);
            }
        }
    }
}
