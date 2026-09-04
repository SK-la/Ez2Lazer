// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Localisation.HUD;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// Ez 版 Argon 进度条：回放 miss 条上区间着色 + 悬停引线；休息段为条上渐变着色。
    /// </summary>
    public partial class EzArgonSongProgress : SongProgress
    {
        private readonly SongProgressInfo info;
        private readonly ArgonSongProgressGraph graph;
        private readonly ArgonSongProgressBar bar;
        private readonly EzSongProgressRestOverlay restOverlay;
        private readonly EzSongProgressMissOverlay missOverlay;
        private readonly EzSongProgressMarkerLayer markers;
        private readonly Container graphContainer;
        private readonly Container content;

        private const float bar_height = 10;

        [SettingSource(typeof(SongProgressStrings), nameof(SongProgressStrings.ShowGraph), nameof(SongProgressStrings.ShowGraphDescription))]
        public Bindable<bool> ShowGraph { get; } = new BindableBool(true);

        [SettingSource(typeof(SongProgressStrings), nameof(SongProgressStrings.ShowTime), nameof(SongProgressStrings.ShowTimeDescription))]
        public Bindable<bool> ShowTime { get; } = new BindableBool(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SONG_PROGRESS_SHOW_MISS_MARKERS_LABEL), nameof(EzHUDStrings.SONG_PROGRESS_SHOW_MISS_MARKERS_DESCRIPTION))]
        public BindableBool ShowMissMarkers { get; } = new BindableBool(true);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SONG_PROGRESS_SHOW_REST_MARKERS_LABEL), nameof(EzHUDStrings.SONG_PROGRESS_SHOW_REST_MARKERS_DESCRIPTION))]
        public BindableBool ShowRestMarkers { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.UseRelativeSize))]
        public BindableBool UseRelativeSize { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [Resolved]
        private Player? player { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IEzReplaySession? replaySession { get; set; }

        private CancellationTokenSource? missLoadCts;
        private bool missLoadStarted;

        public EzArgonSongProgress()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Masking = true;
            CornerRadius = 5;

            Child = content = new Container
            {
                RelativeSizeAxes = Axes.X,
                Children = new Drawable[]
                {
                    info = new SongProgressInfo
                    {
                        Origin = Anchor.TopLeft,
                        Name = "Info",
                        Anchor = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.X,
                        ShowProgress = false
                    },
                    bar = new ArgonSongProgressBar(bar_height)
                    {
                        Name = "Seek bar",
                        Origin = Anchor.BottomLeft,
                        Anchor = Anchor.BottomLeft,
                        OnSeek = time => player?.Seek(time),
                    },
                    graphContainer = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Masking = true,
                        CornerRadius = 5,
                        Child = graph = new ArgonSongProgressGraph
                        {
                            Name = "Difficulty graph",
                            RelativeSizeAxes = Axes.Both,
                            Blending = BlendingParameters.Additive
                        },
                        RelativeSizeAxes = Axes.X,
                    },
                    restOverlay = new EzSongProgressRestOverlay
                    {
                        Name = "Rest segments",
                        Depth = -1,
                    },
                    missOverlay = new EzSongProgressMissOverlay
                    {
                        Name = "Miss segments",
                        Depth = -2,
                    },
                    markers = new EzSongProgressMarkerLayer
                    {
                        Name = "Miss hover markers",
                    },
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            info.TextColour = Colour4.White;
            info.Font = OsuFont.Torus.With(size: 18, weight: FontWeight.Bold);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Interactive.BindValueChanged(v =>
            {
                bar.Interactive = v.NewValue;
                markers.IsReplay.Value = v.NewValue;
                missOverlay.IsReplay.Value = v.NewValue;
                markers.Alpha = v.NewValue ? 1 : 0;
                onReplayOrMissSettingChanged();
            }, true);

            ShowGraph.BindValueChanged(_ => updateGraphVisibility(), true);
            ShowTime.BindValueChanged(_ => info.FadeTo(ShowTime.Value ? 1 : 0, 200, Easing.In), true);
            AccentColour.BindValueChanged(c =>
            {
                // 不要把 Accent 乘到 miss / 休息着色层。
                info.Colour = c.NewValue;
                bar.Colour = c.NewValue;
                graphContainer.Colour = c.NewValue;
            }, true);

            markers.ShowMissMarkers.BindTo(ShowMissMarkers);
            missOverlay.ShowMissMarkers.BindTo(ShowMissMarkers);
            restOverlay.ShowRestMarkers.BindTo(ShowRestMarkers);
            ShowMissMarkers.BindValueChanged(_ => onReplayOrMissSettingChanged());

            if (gameplayState != null)
                restOverlay.SetBreaks(gameplayState.Beatmap.Breaks);

            float previousWidth = Width;
            UseRelativeSize.BindValueChanged(v => RelativeSizeAxes = v.NewValue ? Axes.X : Axes.None, true);
            Width = previousWidth;
        }

        protected override void UpdateObjects(IEnumerable<HitObject> objects)
        {
            graph.Objects = objects;

            info.StartTime = bar.StartTime = markers.StartTime = restOverlay.StartTime = missOverlay.StartTime = FirstHitTime;
            info.EndTime = bar.EndTime = markers.EndTime = restOverlay.EndTime = missOverlay.EndTime = LastHitTime;
        }

        private void updateGraphVisibility()
        {
            graph.FadeTo(ShowGraph.Value ? 1 : 0, 200, Easing.In);
        }

        private void onReplayOrMissSettingChanged()
        {
            if (!Interactive.Value || !ShowMissMarkers.Value)
            {
                cancelMissLoad();
                markers.ClearMisses();
                missOverlay.ClearMisses();
                missLoadStarted = false;
                return;
            }

            ensureMissMarkersLoaded();
        }

        private void ensureMissMarkersLoaded()
        {
            if (missLoadStarted)
                return;

            var score = player?.Score;
            if (score == null)
                return;

            var existing = score.ScoreInfo.HitEvents;

            if (existing.Count > 0)
            {
                applyMissEvents(existing);
                missLoadStarted = true;
                return;
            }

            if (replaySession == null || gameplayState == null)
                return;

            missLoadStarted = true;
            cancelMissLoad();
            missLoadCts = new CancellationTokenSource();
            var token = missLoadCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    var hitEvents = await replaySession.RunHitEventsAsync(score, gameplayState.Beatmap, ReplayRunPurpose.ForStored, token)
                                                       .ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                        return;

                    Schedule(() =>
                    {
                        if (token.IsCancellationRequested || !Interactive.Value || !ShowMissMarkers.Value)
                            return;

                        applyMissEvents(hitEvents);
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch
                {
                    Schedule(() => missLoadStarted = false);
                }
            }, token);
        }

        private void applyMissEvents(IEnumerable<HitEvent> events)
        {
            var missTimes = events
                            .Where(e => e.Result == HitResult.Miss)
                            .Select(e => e.HitObject.StartTime)
                            .ToList();

            markers.SetMissTimes(missTimes);
            missOverlay.SetMissTimes(missTimes);
        }

        private void cancelMissLoad()
        {
            missLoadCts?.Cancel();
            missLoadCts?.Dispose();
            missLoadCts = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            cancelMissLoad();
            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            float markerSpace = Interactive.Value ? EzSongProgressMarkerLayer.AREA_HEIGHT : 0;
            content.Height = bar.Height + markerSpace + info.Height;
            graphContainer.Height = bar.Height;
            restOverlay.Height = bar.Height;
            missOverlay.Height = bar.Height;
            markers.Y = -bar.Height;

            if (Interactive.Value)
                markers.Expanded.Value = bar.IsHovered || IsHovered;
        }

        protected override void UpdateProgress(double progress, bool isIntro)
        {
            bar.Progress = isIntro ? 0 : progress;
        }
    }
}
