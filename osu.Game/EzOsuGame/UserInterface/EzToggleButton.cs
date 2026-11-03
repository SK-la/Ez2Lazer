// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.EzOsuGame.UserInterface
{
    public partial class EzToggleButton : EzButton
    {
        private Sample? sampleOff;
        private Sample? sampleOn;

        /// <summary>
        /// Toggle buttons by default play on/off samples in addition to the click sample.
        /// Setting this to <c>false</c> silences the toggle sound.
        /// </summary>
        protected virtual bool PlayToggleSamples => true;

        /// <summary>
        /// Whether this button is currently toggled to an active state.
        /// </summary>
        public BindableBool Active { get; } = new BindableBool();

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleOn = audio.Samples.Get(@"UI/check-on");
            sampleOff = audio.Samples.Get(@"UI/check-off");
        }

        protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds(sampleSet);

        protected override void LoadComplete()
        {
            Active.BindDisabledChanged(disabled => Action = disabled ? null : Active.Toggle, true);
            Active.BindValueChanged(v =>
            {
                Accented = v.NewValue;
                playSample();
            });

            Accented = Active.Value;
            base.LoadComplete();
        }

        private void playSample()
        {
            if (!PlayToggleSamples)
                return;

            if (Active.Value)
                sampleOn?.Play();
            else
                sampleOff?.Play();
        }
    }
}
