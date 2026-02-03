// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Graphics.UserInterface;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.LAsEzExtensions.UserInterface
{
    public partial class ShearedTriStateButton : ShearedButton
    {
        public Bindable<KeySoundPreviewMode> State = new Bindable<KeySoundPreviewMode>();

        public ShearedTriStateButton(float? width = null)
            : base(width)
        {
        }

        protected override void LoadComplete()
        {
            Action = () => State.Value = (KeySoundPreviewMode)(((int)State.Value + 1) % 3);
            Logger.Log(State.Value.ToString());
            State.BindValueChanged(_ => UpdateState(), true);

            base.LoadComplete();
        }

        protected virtual void UpdateState()
        {
            // Visual mapping for three states:
            // 0 = off, 1 = on (default highlight), 2 = alt-on (distinct beige colour)
            switch (State.Value)
            {
                case KeySoundPreviewMode.Off:
                    DarkerColour = ColourProvider.Background3;
                    LighterColour = ColourProvider.Background1;
                    TextColour = ColourProvider.Content1;
                    break;

                case KeySoundPreviewMode.AutoPreview:
                    DarkerColour = ColourProvider.Highlight1;
                    LighterColour = ColourProvider.Colour0;
                    TextColour = ColourProvider.Background6;
                    break;

                case KeySoundPreviewMode.AutoPlayPlus:
                default:
                    DarkerColour = Colour4.PaleGoldenrod;
                    LighterColour = Colour4.LightGoldenrodYellow;
                    TextColour = ColourProvider.Background6;
                    break;
            }
        }
    }
}
