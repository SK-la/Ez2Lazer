// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Input.Bindings;
using osu.Game.Online;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;

namespace osu.Game.Screens.Ranking
{
    public partial class ReplayDownloadButton : ScreenFooterButton, IKeyBindingHandler<GlobalAction>
    {
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();

        protected new readonly Bindable<DownloadState> State = new Bindable<DownloadState>();

        private ScoreDownloadTracker? downloadTracker;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        private ReplayAvailability replayAvailability
        {
            get
            {
                if (State.Value == DownloadState.LocallyAvailable)
                    return ReplayAvailability.Local;

                if (Score.Value?.HasOnlineReplay == true)
                    return ReplayAvailability.Online;

                return ReplayAvailability.NotAvailable;
            }
        }

        public ReplayDownloadButton(ScoreInfo? score)
        {
            Score.Value = score;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, ScoreModelDownloader scoreDownloader)
        {
            AccentColour = colours.Blue1;

            Action = () =>
            {
                switch (State.Value)
                {
                    case DownloadState.LocallyAvailable:
                        game?.PresentScore(Score.Value!, ScorePresentType.Gameplay);
                        break;

                    case DownloadState.NotDownloaded:
                        scoreDownloader.Download(Score.Value!);
                        break;

                    case DownloadState.Importing:
                    case DownloadState.Downloading:
                        shake();
                        break;
                }
            };

            Score.BindValueChanged(score =>
            {
                // An export may be pending from the last score.
                // Reset this to meet user expectations (a new score which has just been switched to shouldn't export)
                State.ValueChanged -= exportWhenReady;

                downloadTracker?.RemoveAndDisposeImmediately();
                downloadTracker = null;
                State.SetDefault();

                if (score.NewValue != null)
                {
                    Add(downloadTracker = new ScoreDownloadTracker(score.NewValue)
                    {
                        State = { BindTarget = State }
                    });
                }

                updateState();
            }, true);

            State.BindValueChanged(state =>
            {
                updateState();

                AccentColour = state.NewValue switch
                {
                    DownloadState.LocallyAvailable => colours.Lime1,
                    DownloadState.Downloading or DownloadState.Importing => colours.Yellow,
                    _ => colours.Blue1,
                };
            }, true);
        }

        private void shake()
        {
            Content.FinishTransforms();

            Content.MoveToX(-5, 40, Easing.OutSine)
                   .Then()
                   .MoveToX(5, 80, Easing.InOutSine)
                   .Then()
                   .MoveToX(0, 40, Easing.InSine);
        }

        #region Export via hotkey logic (also in SaveFailedScoreButton)

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.SaveReplay:
                    TriggerClick();
                    return true;

                case GlobalAction.ExportReplay:
                    if (State.Value == DownloadState.LocallyAvailable)
                    {
                        State.BindValueChanged(exportWhenReady, true);
                    }
                    else
                    {
                        // A download needs to be performed before we can export this replay.
                        TriggerClick();

                        if (Enabled.Value)
                            State.BindValueChanged(exportWhenReady, true);
                    }

                    return true;
            }

            return false;
        }

        public override void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private void exportWhenReady(ValueChangedEvent<DownloadState> state)
        {
            if (state.NewValue != DownloadState.LocallyAvailable) return;

            scoreManager.Export(Score.Value!);

            State.ValueChanged -= exportWhenReady;
        }

        #endregion

        private void updateState()
        {
            switch (replayAvailability)
            {
                case ReplayAvailability.Local:
                    Icon = FontAwesome.Solid.Play;
                    Text = ReplayStrings.WATCH;
                    TooltipText = ReplayStrings.WATCH;
                    Enabled.Value = true;
                    break;

                case ReplayAvailability.Online:
                    Icon = FontAwesome.Solid.Download;
                    Text = ReplayStrings.DOWNLOAD;
                    TooltipText = ReplayStrings.DOWNLOAD;
                    Enabled.Value = true;
                    break;

                default:
                    Icon = FontAwesome.Solid.Times;
                    Text = ReplayStrings.UNAVAILABLE;
                    TooltipText = ReplayStrings.UNAVAILABLE;
                    Enabled.Value = false;
                    break;
            }
        }

        private enum ReplayAvailability
        {
            Local,
            Online,
            NotAvailable,
        }

        private static class ReplayStrings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString WATCH = new EzLocalizationManager.EzLocalisableString(
                "观看回放", "Watch replay");

            internal static readonly EzLocalizationManager.EzLocalisableString DOWNLOAD = new EzLocalizationManager.EzLocalisableString(
                "下载回放", "Download replay");

            internal static readonly EzLocalizationManager.EzLocalisableString UNAVAILABLE = new EzLocalizationManager.EzLocalisableString(
                "无可用回放", "Replay unavailable");
        }
    }
}
