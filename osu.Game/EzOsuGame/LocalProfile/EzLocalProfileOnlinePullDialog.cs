// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOnlinePullDialog : PopupDialog
    {
        private readonly Bindable<EzLocalProfileOnlinePullKind> kind = new Bindable<EzLocalProfileOnlinePullKind>(EzLocalProfileOnlinePullKind.Best);
        private readonly Bindable<EzLocalProfileOnlinePullRulesetChoice> rulesetChoice = new Bindable<EzLocalProfileOnlinePullRulesetChoice>(EzLocalProfileOnlinePullRulesetChoice.Mania);
        private readonly BindableBool resetOffset = new BindableBool();
        private readonly OsuSpriteText offsetHint;

        public EzLocalProfileOnlinePullDialog(
            RulesetStore rulesetStore,
            Func<int, int> getMostPlayedOffset,
            Action<EzLocalProfileOnlinePullRequest> onConfirm)
        {
            HeaderText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_HEADER;
            BodyText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BODY;
            Icon = FontAwesome.Solid.CloudDownloadAlt;

            offsetHint = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Font = OsuFont.GetFont(size: 14),
            };

            void refreshOffsetHint()
            {
                int offset = getMostPlayedOffset((int)rulesetChoice.Value);
                offsetHint.Text = string.Format(
                    EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_OFFSET_HINT.ToString(),
                    rulesetChoice.Value,
                    offset,
                    EzLocalProfileOnlinePullService.DEFAULT_MOST_PLAYED_BATCH);
            }

            rulesetChoice.BindValueChanged(_ => refreshOffsetHint(), true);
            kind.BindValueChanged(_ =>
            {
                offsetHint.Alpha = kind.Value == EzLocalProfileOnlinePullKind.MostPlayed ? 1 : 0.4f;
            }, true);

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new Drawable[]
                {
                    new FormEnumDropdown<EzLocalProfileOnlinePullRulesetChoice>
                    {
                        Caption = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_RULESET,
                        Current = { BindTarget = rulesetChoice },
                    },
                    new FormEnumDropdown<EzLocalProfileOnlinePullKind>
                    {
                        Caption = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_KIND,
                        Current = { BindTarget = kind },
                    },
                    offsetHint,
                    new OsuCheckbox
                    {
                        LabelText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_RESET_OFFSET,
                        Current = { BindTarget = resetOffset },
                    },
                }
            };

            MainContent.Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 260,
                Margin = new MarginPadding { Top = 16 },
                Child = flow,
            };

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_CONFIRM,
                    Action = () =>
                    {
                        var ruleset = rulesetStore.GetRuleset((int)rulesetChoice.Value)
                                      ?? rulesetStore.AvailableRulesets.FirstOrDefault(r => r.OnlineID == (int)rulesetChoice.Value);

                        if (ruleset == null)
                            return;

                        onConfirm(new EzLocalProfileOnlinePullRequest
                        {
                            Kind = kind.Value,
                            Ruleset = ruleset,
                            ResetMostPlayedOffset = resetOffset.Value,
                            MostPlayedBatchSize = EzLocalProfileOnlinePullService.DEFAULT_MOST_PLAYED_BATCH,
                        });
                    }
                },
                new PopupDialogCancelButton
                {
                    Text = EzSettingsStrings.LOCAL_PROFILE_IMPORT_CANCEL,
                }
            };
        }
    }

    public enum EzLocalProfileOnlinePullRulesetChoice
    {
        Osu = 0,
        Taiko = 1,
        Catch = 2,
        Mania = 3,
    }
}
