// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.LAsEzExtensions.UserInterface
{
    /// <summary>
    /// A sheared button that cycles through three states (0,1,2).
    /// Provides a BindableInt `State` for external binding.
    /// </summary>
    public partial class ShearedTriStateButton : ShearedButton
    {
        public Bindable<int> State { get; } = new BindableInt();

        public ShearedTriStateButton(float? width = null)
            : base(width)
        {
        }

        protected override void LoadComplete()
        {
            // clicking cycles 0 -> 1 -> 2 -> 0
            Action = () => State.Value = (State.Value + 1) % 3;

            State.BindValueChanged(_ => UpdateState(), true);

            base.LoadComplete();
        }

        protected virtual void UpdateState()
        {
            // Visual mapping for three states:
            // 0 = off, 1 = on (default highlight), 2 = alt-on (distinct beige colour)
            switch (State.Value)
            {
                case 0:
                    DarkerColour = ColourProvider.Background3;
                    LighterColour = ColourProvider.Background1;
                    TextColour = ColourProvider.Content1;
                    break;

                case 1:
                    DarkerColour = ColourProvider.Highlight1;
                    LighterColour = ColourProvider.Colour0;
                    TextColour = ColourProvider.Background6;
                    break;

                case 2:
                default:
                    // 米黄色（beige）用于开启2，保持文字颜色为 Content1 以保证可读性
                    var beige = new osu.Framework.Graphics.Colour4(0.961f, 0.961f, 0.863f, 1f);
                    DarkerColour = beige;
                    LighterColour = beige.Lighten(0.12f);
                    TextColour = ColourProvider.Content1;
                    break;
            }
        }
    }
}
