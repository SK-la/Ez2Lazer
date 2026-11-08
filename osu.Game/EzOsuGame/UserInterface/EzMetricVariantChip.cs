// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.UserInterface
{
    /// <summary>
    /// 点击切换当前规则集的分析系统。
    /// </summary>
    /// <remarks>
    /// 切换列表会根据当前规则集的分析系统数量来决定是否显示。若只有一个系统，则不显示切换列表，显示当前系统的名称。
    /// 若有多个系统，则显示当前系统的名称，点击后会切换到下一个系统。若没有系统，则不显示。
    /// <para />
    /// <see cref="Ez2Setting.AnalysisSystemSelection"/> 关联设置中的当前分析系统。
    /// </remarks>
    public partial class EzMetricVariantChip : OsuClickableContainer
    {
        public Action<EzMetricVariant?>? VariantChanged;

        private readonly OsuSpriteText label;
        private readonly Box background;

        private Bindable<string>? configSelection;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        public EzMetricVariantChip()
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 8;
            // Shear = OsuGame.SHEAR;
            Margin = new MarginPadding { Right = 10 };

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                label = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    // Shear = -OsuGame.SHEAR,
                    Margin = new MarginPadding { Horizontal = 7, Vertical = 1.5f },
                    Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, Ez2ConfigManager ezConfig)
        {
            background.Colour = colours.BlueDarker;
            configSelection = ezConfig.GetBindable<string>(Ez2Setting.AnalysisSystemSelection);

            Action = () =>
            {
                int rulesetOnlineId = ruleset.Value?.OnlineID ?? 0;
                EzMetricSelection.Cycle(rulesetOnlineId, EzMetricVariantReadout.Catalog);
                refresh(rulesetOnlineId);
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ruleset.BindValueChanged(_ => refresh(ruleset.Value?.OnlineID ?? 0), true);

            // The settings dropdown writes the same setting; pick that up so the chip and the panel follow it.
            configSelection?.BindValueChanged(_ => refresh(ruleset.Value?.OnlineID ?? 0));
        }

        /// <summary>Current system for the active ruleset, or null when the ruleset has no analysis.</summary>
        public EzMetricVariant? Current { get; private set; }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            configSelection?.UnbindAll();
        }

        private void refresh(int rulesetOnlineId)
        {
            var previous = Current;
            Current = EzMetricVariantReadout.Selected(rulesetOnlineId);

            if (Current == null)
            {
                Hide();
                return;
            }

            label.Text = Current.Name;
            Show();

            if (previous?.Id != Current.Id)
                VariantChanged?.Invoke(Current);
        }
    }
}
