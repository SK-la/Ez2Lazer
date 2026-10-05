// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Visualizer
{
    /// <summary>
    /// Now Playing card bottom controls, shown when the pointer approaches the bar under the visualizer logo.
    /// </summary>
    public partial class EzVisualizerPlaybackBar : CompositeDrawable
    {
        private const float bar_height = 55;
        private const float bar_width = 400;
        private const float hover_padding = 40;
        private const float side_button_spacing = IconButton.DEFAULT_BUTTON_SIZE + 5;
        private const double fade_duration = 200;
        private const string favourites_collection_name = @"Favourites";

        private IconButton prevButton = null!;
        private IconButton playButton = null!;
        private IconButton nextButton = null!;
        private IconButton loopButton = null!;
        private IconButton favouriteButton = null!;
        private IconButton jumpButton = null!;
        private IconButton playlistButton = null!;
        private Drawable controls = null!;

        private readonly Bindable<MusicLoopMode> loopMode = new Bindable<MusicLoopMode>();
        private Bindable<bool> allowTrackControl = null!;
        private bool setInFavourites;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private NowPlayingOverlay? nowPlayingOverlay { get; set; }

        public EzVisualizerPlaybackBar()
        {
            // Fixed hitbox so Alpha=0 controls do not collapse AutoSize / hover area.
            Size = new Vector2(bar_width + hover_padding * 2, bar_height + hover_padding * 2);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                // Always-present full-area target: hover anywhere in the bar region shows controls.
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
                },
                controls = new Container
                {
                    Size = new Vector2(bar_width, bar_height),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    CornerRadius = 5,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.55f),
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer<IconButton>
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5),
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    Children = new[]
                                    {
                                        prevButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Action = () => musicController.PreviousTrack(),
                                            Icon = FontAwesome.Solid.StepBackward,
                                        },
                                        playButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Scale = new Vector2(1.4f),
                                            IconScale = new Vector2(1.4f),
                                            Action = () => musicController.TogglePause(),
                                            Icon = FontAwesome.Regular.PlayCircle,
                                        },
                                        nextButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Action = () => musicController.NextTrack(),
                                            Icon = FontAwesome.Solid.StepForward,
                                        },
                                    }
                                },
                                loopButton = new MusicIconButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.Centre,
                                    Position = new Vector2(bar_height / 2, 0),
                                    Action = () => musicController.CycleLoopMode(),
                                    Icon = FontAwesome.Solid.Random,
                                },
                                favouriteButton = new MusicIconButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.Centre,
                                    Position = new Vector2(bar_height / 2 + side_button_spacing, 0),
                                    Action = addCurrentSetToFavourites,
                                    Icon = FontAwesome.Regular.Heart,
                                    TooltipText = @"Add to Favourites",
                                },
                                jumpButton = new MusicIconButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.Centre,
                                    Position = new Vector2(-(bar_height / 2 + side_button_spacing), 0),
                                    Action = jumpToCurrentBeatmap,
                                    Icon = FontAwesome.Solid.AngleDoubleRight,
                                    TooltipText = CommonStrings.GoToBeatmap,
                                },
                                playlistButton = new MusicIconButton
                                {
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.CentreRight,
                                    Position = new Vector2(-bar_height / 2, 0),
                                    Icon = FontAwesome.Solid.Bars,
                                    Action = () => nowPlayingOverlay?.ToggleVisibility(),
                                },
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindDisabledChanged(_ => Scheduler.AddOnce(updateEnabledStates));

            allowTrackControl = musicController.AllowTrackControl.GetBoundCopy();
            allowTrackControl.BindValueChanged(_ => Scheduler.AddOnce(updateEnabledStates), true);

            loopMode.BindTo(musicController.LoopMode);
            loopMode.BindValueChanged(_ => updateLoopButton(), true);

            musicController.TrackChanged += onTrackChanged;
            Scheduler.AddOnce(updateFavouriteButtonState);
            Scheduler.AddOnce(updateEnabledStates);
        }

        protected override void Update()
        {
            base.Update();

            var track = musicController.CurrentTrack;
            playButton.Icon = !track.IsDummyDevice && track.IsRunning
                ? FontAwesome.Regular.PauseCircle
                : FontAwesome.Regular.PlayCircle;
        }

        protected override bool OnHover(HoverEvent e)
        {
            controls.FadeIn(fade_duration, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            controls.FadeOut(fade_duration, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        private void onTrackChanged(WorkingBeatmap working, TrackChangeDirection direction = TrackChangeDirection.None)
            => Scheduler.AddOnce(updateFavouriteButtonState);

        private void addCurrentSetToFavourites()
        {
            if (beatmap.IsDefault)
                return;

            var set = beatmap.Value.BeatmapSetInfo;
            string[] hashes = set.Beatmaps.Select(b => b.MD5Hash).Distinct().ToArray();

            if (hashes.Length == 0)
                return;

            Task.Run(() =>
            {
                realm.Write(r =>
                {
                    var collection = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == favourites_collection_name);

                    if (collection == null)
                    {
                        collection = new BeatmapCollection(favourites_collection_name);
                        r.Add(collection);
                    }

                    foreach (string hash in hashes)
                    {
                        if (!collection.BeatmapMD5Hashes.Contains(hash))
                            collection.BeatmapMD5Hashes.Add(hash);
                    }

                    collection.LastModified = DateTimeOffset.UtcNow;
                });

                Scheduler.Add(updateFavouriteButtonState);
            });
        }

        private void jumpToCurrentBeatmap()
        {
            if (game == null || beatmap.IsDefault)
                return;

            var working = beatmap.Value;
            var set = working.BeatmapSetInfo;
            var difficulties = set.Beatmaps.ToList();

            if (difficulties.Count == 0)
                return;

            var matchingRuleset = difficulties.Where(b => b.Ruleset.Equals(ruleset.Value)).ToList();

            BeatmapInfo selection;

            if (matchingRuleset.Count > 0)
                selection = matchingRuleset[RNG.Next(matchingRuleset.Count)];
            else if (working.BeatmapInfo.BeatmapSet?.Equals(set) == true || difficulties.Any(b => b.Equals(working.BeatmapInfo)))
                selection = working.BeatmapInfo;
            else
                selection = difficulties[RNG.Next(difficulties.Count)];

            game.PresentBeatmap(set, b => b.ID == selection.ID);
        }

        private void updateFavouriteButtonState()
        {
            if (beatmap.IsDefault)
            {
                setInFavourites = false;
            }
            else
            {
                var set = beatmap.Value.BeatmapSetInfo;
                string[] hashes = set.Beatmaps.Select(b => b.MD5Hash).Distinct().ToArray();

                setInFavourites = hashes.Length > 0 && realm.Run(r =>
                {
                    var collection = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == favourites_collection_name);
                    return collection != null && hashes.All(h => collection.BeatmapMD5Hashes.Contains(h));
                });
            }

            favouriteButton.Icon = setInFavourites ? FontAwesome.Solid.Heart : FontAwesome.Regular.Heart;
            favouriteButton.FadeColour(setInFavourites ? colours.Pink1 : Color4.White, 200, Easing.OutQuint);
            favouriteButton.TooltipText = setInFavourites ? @"In Favourites" : @"Add to Favourites";
        }

        private void updateEnabledStates()
        {
            bool beatmapDisabled = beatmap.Disabled;
            bool trackControlDisabled = !musicController.AllowTrackControl.Value;

            prevButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            nextButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            playlistButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            favouriteButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled && !beatmap.IsDefault;
            jumpButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled && !beatmap.IsDefault && game != null;
            playButton.Enabled.Value = !trackControlDisabled;
            loopButton.Enabled.Value = !trackControlDisabled;
        }

        private void updateLoopButton()
        {
            switch (loopMode.Value)
            {
                case MusicLoopMode.Single:
                    loopButton.Icon = FontAwesome.Solid.Redo;
                    loopButton.FadeColour(colours.Yellow, 200, Easing.OutQuint);
                    loopButton.TooltipText = @"单曲循环";
                    break;

                case MusicLoopMode.Sequential:
                    loopButton.Icon = FontAwesome.Solid.Sync;
                    loopButton.FadeColour(Color4.White, 200, Easing.OutQuint);
                    loopButton.TooltipText = @"顺序循环";
                    break;

                default:
                    loopButton.Icon = FontAwesome.Solid.Random;
                    loopButton.FadeColour(colours.Yellow, 200, Easing.OutQuint);
                    loopButton.TooltipText = @"随机循环";
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (musicController.IsNotNull())
                musicController.TrackChanged -= onTrackChanged;
        }

        private partial class MusicIconButton : IconButton
        {
            public MusicIconButton()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                HoverColour = colours.YellowDark.Opacity(0.6f);
                FlashColour = colours.Yellow;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Content.AutoSizeAxes = Axes.None;
                Content.Size = new Vector2(DEFAULT_BUTTON_SIZE);
            }
        }
    }
}
