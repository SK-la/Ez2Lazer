// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osuTK;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;

namespace osu.Game.EzOsuGame.LocalProfile
{
    public partial class EzLocalProfileOnlinePullDialog : PopupDialog
    {
        private readonly Bindable<EzLocalProfileOnlinePullKind> kind = new Bindable<EzLocalProfileOnlinePullKind>(EzLocalProfileOnlinePullKind.Best);
        private readonly Bindable<EzLocalProfileOnlinePullRulesetChoice> rulesetChoice = new Bindable<EzLocalProfileOnlinePullRulesetChoice>(EzLocalProfileOnlinePullRulesetChoice.Osu);
        private readonly BindableBool includeStatsWithoutImport = new BindableBool(true);
        private readonly OsuSpriteText offsetStoredHint;
        private readonly OsuNumberBox offsetInput;

        public EzLocalProfileOnlinePullDialog(
            RulesetStore rulesetStore,
            Func<int, int> getMostPlayedOffset,
            Action<EzLocalProfileOnlinePullRequest> onConfirm)
        {
            HeaderText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_HEADER;
            BodyText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BODY;
            Icon = FontAwesome.Solid.CloudDownloadAlt;

            offsetStoredHint = new OsuSpriteText
            {
                RelativeSizeAxes = Axes.X,
                Font = OsuFont.GetFont(size: 14),
            };

            offsetInput = new OsuNumberBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
                PlaceholderText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_OFFSET_INPUT,
            };

            void syncOffsetFromStore()
            {
                int stored = getMostPlayedOffset((int)rulesetChoice.Value);
                offsetStoredHint.Text = string.Format(
                    EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_OFFSET_HINT.ToString(),
                    stored,
                    EzLocalProfileOnlinePullService.DEFAULT_MOST_PLAYED_BATCH);
                offsetInput.Text = stored.ToString(CultureInfo.InvariantCulture);
            }

            rulesetChoice.BindValueChanged(_ => syncOffsetFromStore(), true);

            var mostPlayedOffsetSection1 = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(4),
                Children = new Drawable[]
                {
                    offsetStoredHint,
                    new OsuSpriteText
                    {
                        Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_OFFSET_INPUT,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    },
                    offsetInput,
                }
            };

            kind.BindValueChanged(k =>
            {
                mostPlayedOffsetSection1.Alpha = k.NewValue == EzLocalProfileOnlinePullKind.MostPlayed ? 1 : 0.35f;
            }, true);

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new[]
                {
                    createLabeledDropdown(EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_RULESET.ToString(), rulesetChoice),
                    createLabeledDropdown(EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_KIND.ToString(), kind),
                    mostPlayedOffsetSection1,
                    new OsuCheckbox
                    {
                        LabelText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_INCLUDE_STATS,
                        Current = { BindTarget = includeStatsWithoutImport },
                    },
                }
            };

            MainContent.Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 320,
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

                        int startOffset = getMostPlayedOffset((int)rulesetChoice.Value);

                        if (int.TryParse(offsetInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int typed)
                            && typed >= 0)
                        {
                            startOffset = typed;
                        }

                        onConfirm(new EzLocalProfileOnlinePullRequest
                        {
                            Kind = kind.Value,
                            Ruleset = ruleset,
                            MostPlayedStartOffset = startOffset,
                            IncludeInStatsWithoutImport = includeStatsWithoutImport.Value,
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

        private static Drawable createLabeledDropdown<T>(string caption, Bindable<T> current)
            where T : struct, Enum
        {
            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(2),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = caption,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                    },
                    new OsuEnumDropdown<T>
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = current },
                    },
                }
            };
        }
    }

    public enum EzLocalProfileOnlinePullRulesetChoice
    {
        [Description("osu!")]
        Osu = 0,

        [Description("osu!taiko")]
        Taiko = 1,

        [Description("osu!catch")]
        Catch = 2,

        [Description("osu!mania")]
        Mania = 3,
    }
}
