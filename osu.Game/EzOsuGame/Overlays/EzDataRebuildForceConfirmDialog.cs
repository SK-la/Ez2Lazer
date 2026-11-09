// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Dialog;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Confirms the destructive form of one maintenance action. Only that form needs a prompt; the backfill runs
    /// straight from the dialog because it writes missing rows and leaves existing ones alone.
    /// </summary>
    public partial class EzDataRebuildForceConfirmDialog : PopupDialog
    {
        public EzDataRebuildForceConfirmDialog(LocalisableString target, Action onConfirm)
        {
            ArgumentNullException.ThrowIfNull(onConfirm);

            Icon = FontAwesome.Solid.ExclamationTriangle;
            HeaderText = EzSettingsStrings.DATA_REBUILD_DIALOG_HEADER;
            BodyText = LocalisableString.Format(EzSettingsStrings.DATA_REBUILD_DIALOG_BODY.ToString(), target);

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogDangerousButton
                {
                    Text = EzSettingsStrings.DATA_REBUILD_DIALOG_FORCE,
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsStrings.CANCEL_BUTTON,
                },
            };
        }
    }
}
