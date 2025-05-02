// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Screens
{
    public partial class EzSkinSettings : CompositeDrawable
    {
        [SettingSource("Column Width", "Column Width")]
        public BindableNumber<float> ColumnWidth { get; } = new BindableNumber<float>(50f)
        {
            MinValue = 9f,
            MaxValue = 90f,
            Precision = 1f,
        };

        [SettingSource("Special Column Width Factor", "Special Column Width Factor")]
        public BindableNumber<float> SpecialFactor { get; } = new BindableNumber<float>(1f)
        {
            MinValue = 0.1f,
            MaxValue = 2f,
            Precision = 0.1f,
        };

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
        }
    }
}
