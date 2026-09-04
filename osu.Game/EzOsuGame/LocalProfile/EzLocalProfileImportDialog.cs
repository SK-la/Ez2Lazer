// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileImportDialog : PopupDialog
    {
        private const float content_width = 420;
        private const float row_height = 36;
        private const float list_max_height = 180;

        private readonly Dictionary<string, BindableBool> selections = new Dictionary<string, BindableBool>(StringComparer.Ordinal);
        private readonly BindableBool replaceMode = new BindableBool();

        public EzLocalProfileImportDialog(
            IReadOnlyList<EzLocalProfileUsernameCount> usernameCounts,
            IReadOnlyCollection<string> previouslyIncluded,
            Action<IReadOnlyList<string>, bool> onConfirm)
        {
            HeaderText = EzSettingsProfile.LOCAL_PROFILE_IMPORT_HEADER;
            BodyText = EzSettingsProfile.LOCAL_PROFILE_IMPORT_BODY;
            Icon = FontAwesome.Solid.User;

            var previous = new HashSet<string>(previouslyIncluded, StringComparer.Ordinal);
            bool usePrevious = previous.Count > 0 && usernameCounts.Any(c => previous.Contains(c.Username));

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
            };

            foreach (var entry in usernameCounts)
            {
                bool selected = !usePrevious || previous.Contains(entry.Username);
                var bindable = new BindableBool(selected);
                selections[entry.Username] = bindable;

                flow.Add(new OsuCheckbox
                {
                    LabelText = $"{entry.Username}  ({entry.ScoreCount})",
                    Current = { BindTarget = bindable },
                });
            }

            float listHeight = Math.Clamp(
                usernameCounts.Count * row_height + Math.Max(0, usernameCounts.Count - 1) * 4,
                row_height,
                list_max_height);

            MainContent.Child = new FillFlowContainer
            {
                Margin = new MarginPadding { Top = 12 },
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = content_width,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = listHeight,
                        Child = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = flow,
                        }
                    },
                    new OsuCheckbox
                    {
                        LabelText = EzSettingsProfile.LOCAL_PROFILE_IMPORT_REPLACE,
                        Current = { BindTarget = replaceMode },
                    },
                }
            };

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_IMPORT_CONFIRM,
                    Action = () =>
                    {
                        var chosen = selections.Where(kv => kv.Value.Value).Select(kv => kv.Key).ToList();
                        onConfirm(chosen, replaceMode.Value);
                    }
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsProfile.LOCAL_PROFILE_IMPORT_CANCEL,
                }
            };
        }
    }
}
