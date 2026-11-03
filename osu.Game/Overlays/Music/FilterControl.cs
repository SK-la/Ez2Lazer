// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Overlays.Music
{
    public partial class FilterControl : Container
    {
        public Action<FilterCriteria>? FilterChanged;

        public readonly FilterTextBox Search;

        public FilterControl()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            Child = Search = new FilterTextBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Search.Current.BindValueChanged(_ => FilterChanged?.Invoke(createCriteria()), true);
        }

        private FilterCriteria createCriteria() => new FilterCriteria
        {
            SearchText = Search.Current.Value ?? string.Empty,
        };

        public partial class FilterTextBox : BasicSearchTextBox
        {
            protected override bool AllowCommit => true;

            [BackgroundDependencyLoader]
            private void load()
            {
                Masking = true;
                CornerRadius = 5;

                BackgroundUnfocused = OsuColour.Gray(0.06f);
                BackgroundFocused = OsuColour.Gray(0.12f);
            }
        }
    }
}
