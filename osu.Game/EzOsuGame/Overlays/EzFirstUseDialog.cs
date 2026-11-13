// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 功能第一次生效时的说明。调用方用 <see cref="EzFirstUse"/> 决定要不要弹。
    /// </summary>
    public partial class EzFirstUseDialog : PopupDialog
    {
        public EzFirstUseDialog(LocalisableString header, LocalisableString body, Action onAcknowledge)
        {
            Icon = FontAwesome.Solid.InfoCircle;
            HeaderText = header;
            BodyText = body;

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSkinStrings.FIRST_USE_ACKNOWLEDGE,
                    Action = onAcknowledge,
                },
            };
        }
    }

    /// <summary>
    /// 同一个键只弹出一次说明，确认后再继续后面的动作。
    /// </summary>
    public static class EzFirstUse
    {
        public static void ShowOnce(Storage storage, IDialogOverlay dialogs, string key, LocalisableString header, LocalisableString body, Action? after = null)
        {
            void finish()
            {
                EzFirstUseNotice.MarkSeen(storage, key);
                after?.Invoke();
            }

            if (EzFirstUseNotice.HasSeen(storage, key))
            {
                after?.Invoke();
                return;
            }

            dialogs.Push(new EzFirstUseDialog(header, body, finish));
        }
    }
}
