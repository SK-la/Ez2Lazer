// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.EzOsuGame.Layout
{
    public partial class EzLayoutThemeImportDialog : PopupDialog
    {
        private readonly Bindable<string> themeSuffix = new Bindable<string>(string.Empty);

        public EzLayoutThemeImportDialog(Action<string> onConfirm)
        {
            HeaderText = EzEditorStrings.LAYOUT_IMPORT_THEME_HEADER;
            BodyText = EzEditorStrings.LAYOUT_IMPORT_THEME_BODY;

            var textBox = new OsuTextBox
            {
                RelativeSizeAxes = Axes.X,
                PlaceholderText = EzEditorStrings.LAYOUT_IMPORT_THEME_PLACEHOLDER,
            };

            textBox.Current.BindTarget = themeSuffix;

            MainContent.Child = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 12 },
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    textBox,
                },
            };

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzEditorStrings.LAYOUT_IMPORT_THEME_CONFIRM,
                    Action = () => onConfirm(themeSuffix.Value.Trim()),
                },
                new PopupDialogCancelButton(),
            };
        }
    }
}
