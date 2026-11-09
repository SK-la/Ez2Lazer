// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Dialog;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Asks before an exit that would queue a full Sunny recompute on the next launch.
    /// </summary>
    /// <remarks>
    /// The alternative to quitting is not "keep the new values without paying": the settings edit is already the new
    /// algorithm, and the stored ratings are simply behind it. So the prompt states the cost and offers the way out
    /// that actually avoids it - going back and restoring the previous values, which makes the pending state go away.
    /// </remarks>
    public partial class EzSunnyRecomputeExitDialog : PopupDialog
    {
        public EzSunnyRecomputeExitDialog(Action onConfirm, Action? onCancel = null)
        {
            Icon = FontAwesome.Solid.ExclamationTriangle;
            HeaderText = EzSettingsStrings.SSR_TUNING_CHANGED_EXIT_HEADER;
            BodyText = EzSettingsStrings.SSR_TUNING_CHANGED_EXIT_BODY;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSettingsStrings.SSR_TUNING_CHANGED_EXIT_CONFIRM,
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsStrings.SSR_TUNING_CHANGED_EXIT_CANCEL,
                    Action = onCancel,
                },
            };
        }
    }
}
