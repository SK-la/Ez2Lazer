// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Dialog;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Confirmation for <c>EzLocalProfileService.ExcludeUsernamesAsync</c>.
    /// Lists the affected players so the names are visible before they stop counting towards the archive.
    /// </summary>
    public partial class EzLocalProfileDeleteConfirmDialog : PopupDialog
    {
        public EzLocalProfileDeleteConfirmDialog(IReadOnlyList<string> usernames, Action onConfirm)
        {
            ArgumentNullException.ThrowIfNull(usernames);
            ArgumentNullException.ThrowIfNull(onConfirm);

            Icon = FontAwesome.Solid.UserSlash;
            HeaderText = EzSettingsProfile.LOCAL_PROFILE_DELETE_CONFIRM_HEADER;
            BodyText = LocalisableString.Format(
                EzSettingsProfile.LOCAL_PROFILE_DELETE_CONFIRM_BODY.ToString(),
                string.Join("\n", usernames));

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogDangerousButton
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_DELETE_CONFIRM,
                    Action = onConfirm,
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_IMPORT_CANCEL,
                },
            };
        }
    }
}
