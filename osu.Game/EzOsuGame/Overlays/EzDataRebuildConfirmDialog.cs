// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Dialog;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzDataRebuildConfirmDialog : PopupDialog
    {
        public EzDataRebuildConfirmDialog(Action onBackfill, Action onForceRebuild)
        {
            ArgumentNullException.ThrowIfNull(onBackfill);
            ArgumentNullException.ThrowIfNull(onForceRebuild);

            Icon = FontAwesome.Solid.ExclamationTriangle;
            HeaderText = EzSettingsStrings.DATA_REBUILD_DIALOG_HEADER;
            BodyText = EzSettingsStrings.DATA_REBUILD_DIALOG_BODY;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSettingsStrings.DATA_REBUILD_DIALOG_BACKFILL,
                    Action = onBackfill,
                },
                new PopupDialogDangerousButton
                {
                    Text = EzSettingsStrings.DATA_REBUILD_DIALOG_FORCE,
                    Action = onForceRebuild,
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsStrings.CANCEL_BUTTON,
                },
            };
        }
    }
}
