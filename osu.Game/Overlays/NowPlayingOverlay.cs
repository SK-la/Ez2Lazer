// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Music;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class NowPlayingOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent
    {
        public const double TRACK_DRAG_SEEK_DEBOUNCE = 40;

        public IconUsage Icon => OsuIcon.Music;
        public LocalisableString Title => NowPlayingStrings.HeaderTitle;
        public LocalisableString Description => NowPlayingStrings.HeaderDescription;

        private const float player_width = 400;
        private const float player_height = 130;
        private const float transition_length = 500;
        private const float progress_height = 10;
        private const float bottom_black_area_height = 55;
        private const float margin = 10;
        private const float side_button_spacing = IconButton.DEFAULT_BUTTON_SIZE + 5;
        private const string favourites_collection_name = @"Favourites";

        private Drawable background = null!;
        private ProgressBar progressBar = null!;

        private IconButton prevButton = null!;
        private IconButton playButton = null!;
        private IconButton nextButton = null!;
        private MusicIconButton shuffleButton = null!;
        private MusicIconButton favouriteButton = null!;
        private MusicIconButton jumpButton = null!;
        private IconButton playlistButton = null!;

        private MarqueeContainer title = null!, artist = null!;

        private PlaylistOverlay? playlist;

        private Container dragContainer = null!;
        private Container playerContainer = null!;
        private Container playlistContainer = null!;

        protected override double PopInOutSampleBalance => OsuGameBase.SFX_STEREO_STRENGTH * 0.75f;

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

        private Bindable<bool> allowTrackControl = null!;
        private readonly BindableBool shuffle = new BindableBool(true);
        private bool setInFavourites;

        private static readonly FontUsage title_font = OsuFont.GetFont(size: 25, italics: true);
        private static readonly FontUsage artist_font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold, italics: true);

        public NowPlayingOverlay()
        {
            Width = player_width;
            Margin = new MarginPadding(margin);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                dragContainer = new DragContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Children = new Drawable[]
                    {
                        playerContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = player_height,
                            Masking = true,
                            CornerRadius = 5,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = Color4.Black.Opacity(40),
                                Radius = 5,
                            },
                            Children = new[]
                            {
                                background = Empty(),
                                title = new MarqueeContainer
                                {
                                    Origin = Anchor.BottomCentre,
                                    Anchor = Anchor.TopCentre,
                                    Position = new Vector2(0, 40),
                                    Colour = Color4.White,
                                    CreateContent = () => new OsuSpriteText
                                    {
                                        Font = title_font,
                                        Text = @"Nothing to play",
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    },
                                    NonOverflowingContentAnchor = Anchor.Centre,
                                    Padding = new MarginPadding { Horizontal = 15 },
                                },
                                artist = new MarqueeContainer
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Position = new Vector2(0, 45),
                                    Colour = Color4.White,
                                    CreateContent = () => new OsuSpriteText
                                    {
                                        Font = artist_font,
                                        Text = @"Nothing to play",
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    },
                                    NonOverflowingContentAnchor = Anchor.Centre,
                                    Padding = new MarginPadding { Horizontal = 15 },
                                },
                                new Container
                                {
                                    Padding = new MarginPadding { Bottom = progress_height },
                                    Height = bottom_black_area_height,
                                    RelativeSizeAxes = Axes.X,
                                    Origin = Anchor.BottomCentre,
                                    Anchor = Anchor.BottomCentre,
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
                                        shuffleButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.Centre,
                                            Position = new Vector2(bottom_black_area_height / 2, 0),
                                            Action = shuffle.Toggle,
                                            Icon = FontAwesome.Solid.Random,
                                        },
                                        favouriteButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.Centre,
                                            Position = new Vector2(bottom_black_area_height / 2 + side_button_spacing, 0),
                                            Action = addCurrentSetToFavourites,
                                            Icon = FontAwesome.Regular.Heart,
                                            TooltipText = @"Add to Favourites",
                                        },
                                        jumpButton = new MusicIconButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.Centre,
                                            Position = new Vector2(-(bottom_black_area_height / 2 + side_button_spacing), 0),
                                            Action = jumpToCurrentBeatmap,
                                            Icon = FontAwesome.Solid.AngleDoubleRight,
                                            TooltipText = CommonStrings.GoToBeatmap,
                                        },
                                        playlistButton = new MusicIconButton
                                        {
                                            Origin = Anchor.Centre,
                                            Anchor = Anchor.CentreRight,
                                            Position = new Vector2(-bottom_black_area_height / 2, 0),
                                            Icon = FontAwesome.Solid.Bars,
                                            Action = togglePlaylist
                                        },
                                    }
                                },
                                progressBar = new HoverableProgressBar
                                {
                                    Origin = Anchor.BottomCentre,
                                    Anchor = Anchor.BottomCentre,
                                    Height = progress_height / 2,
                                    FillColour = colours.Yellow,
                                    BackgroundColour = colours.YellowDarker.Opacity(0.5f),
                                    OnSeek = onSeek,
                                    OnCommit = onCommit,
                                }
                            },
                        },
                        playlistContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Y = player_height + margin,
                        }
                    }
                },
            };
        }

        private double? lastSeekGameTime;
        private double? lastSeekAudioTargetTime;

        private void onSeek(double progress)
        {
            if (!musicController.IsPlaying || lastSeekGameTime == null || Time.Current - lastSeekGameTime > TRACK_DRAG_SEEK_DEBOUNCE)
            {
                musicController.SeekTo(progress);
                lastSeekGameTime = Time.Current;
                lastSeekAudioTargetTime = progress;
            }
        }

        private void onCommit(double progress)
        {
            // Avoid a second seek to the same location, which could occur when using keyboard navigation from `OnSeek` and subsequent `OnCommit` calls.
            if (progress != lastSeekAudioTargetTime)
                musicController.SeekTo(progress);

            lastSeekGameTime = null;
            lastSeekAudioTargetTime = null;
        }

        private void togglePlaylist()
        {
            if (playlist == null)
            {
                LoadComponentAsync(playlist = new PlaylistOverlay
                {
                    RelativeSizeAxes = Axes.Both,
                }, _ =>
                {
                    playlistContainer.Add(playlist);

                    playlist.State.BindValueChanged(s => playlistButton.FadeColour(s.NewValue == Visibility.Visible ? colours.Yellow : Color4.White, 200, Easing.OutQuint), true);

                    togglePlaylist();
                });

                return;
            }

            if (!beatmap.Disabled)
                playlist.ToggleVisibility();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindDisabledChanged(_ => Scheduler.AddOnce(updateEnabledStates));

            allowTrackControl = musicController.AllowTrackControl.GetBoundCopy();
            allowTrackControl.BindValueChanged(_ => Scheduler.AddOnce(updateEnabledStates), true);

            shuffle.BindTo(musicController.Shuffle);
            shuffle.BindValueChanged(s => shuffleButton.FadeColour(s.NewValue ? colours.Yellow : Color4.White, 200, Easing.OutQuint), true);

            musicController.TrackChanged += trackChanged;
            trackChanged(beatmap.Value);
            Scheduler.AddOnce(updateFavouriteButtonState);
        }

        protected override void PopIn()
        {
            this.FadeIn(transition_length, Easing.OutQuint);
            dragContainer.ScaleTo(1, transition_length, Easing.OutElasticHalf);
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.FadeOut(transition_length, Easing.OutQuint);
            dragContainer.ScaleTo(0.9f, transition_length, Easing.OutQuint);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            playlistContainer.Height = MathF.Min(Parent!.DrawHeight - margin * 3 - player_height, PlaylistOverlay.PLAYLIST_HEIGHT);

            float height = player_height;

            if (playlist != null)
            {
                height += playlist.DrawHeight;
                if (playlist.State.Value == Visibility.Visible)
                    height += margin;
            }

            Height = dragContainer.Height = height;
        }

        protected override void Update()
        {
            base.Update();

            if (pendingBeatmapSwitch != null)
            {
                pendingBeatmapSwitch();
                pendingBeatmapSwitch = null;
            }

            var track = musicController.CurrentTrack;

            if (!progressBar.Seeking)
            {
                if (!track.IsDummyDevice)
                {
                    progressBar.EndTime = track.Length;
                    progressBar.CurrentTime = track.CurrentTime;

                    playButton.Icon = track.IsRunning ? FontAwesome.Regular.PauseCircle : FontAwesome.Regular.PlayCircle;
                }
                else
                {
                    progressBar.CurrentTime = 0;
                    progressBar.EndTime = 1;
                    playButton.Icon = FontAwesome.Regular.PlayCircle;
                }
            }
        }

        private Action? pendingBeatmapSwitch;

        private CancellationTokenSource? backgroundLoadCancellation;

        private WorkingBeatmap? currentBeatmap;

        private void trackChanged(WorkingBeatmap beatmap, TrackChangeDirection direction = TrackChangeDirection.None)
        {
            currentBeatmap = beatmap;

            // avoid using scheduler as our scheduler may not be run for a long time, holding references to beatmaps.
            pendingBeatmapSwitch = delegate
            {
                BeatmapMetadata metadata = beatmap.Metadata;

                title.CreateContent = () => new OsuSpriteText
                {
                    Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                    Font = title_font,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
                artist.CreateContent = () => new OsuSpriteText
                {
                    Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                    Font = artist_font,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                backgroundLoadCancellation?.Cancel();

                LoadComponentAsync(new Background(beatmap) { Depth = float.MaxValue }, newBackground =>
                {
                    if (beatmap != currentBeatmap)
                    {
                        newBackground.Dispose();
                        return;
                    }

                    switch (direction)
                    {
                        case TrackChangeDirection.Next:
                            newBackground.Position = new Vector2(player_width, 0);
                            newBackground.MoveToX(0, 500, Easing.OutCubic);
                            background.MoveToX(-player_width, 500, Easing.OutCubic);
                            break;

                        case TrackChangeDirection.Prev:
                            newBackground.Position = new Vector2(-player_width, 0);
                            newBackground.MoveToX(0, 500, Easing.OutCubic);
                            background.MoveToX(player_width, 500, Easing.OutCubic);
                            break;
                    }

                    background.Expire();
                    background = newBackground;

                    playerContainer.Add(newBackground);
                }, (backgroundLoadCancellation = new CancellationTokenSource()).Token);

                updateFavouriteButtonState();
            };
        }

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
            Hide();
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

            if (beatmapDisabled || trackControlDisabled)
                playlist?.Hide();

            prevButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            nextButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            playlistButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            favouriteButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled && !beatmap.IsDefault;
            jumpButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled && !beatmap.IsDefault && game != null;
            playButton.Enabled.Value = !trackControlDisabled;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (musicController.IsNotNull())
                musicController.TrackChanged -= trackChanged;
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

                // works with AutoSizeAxes above to make buttons autosize with the scale animation.
                Content.AutoSizeAxes = Axes.None;
                Content.Size = new Vector2(DEFAULT_BUTTON_SIZE);
            }
        }

        private partial class Background : BufferedContainer
        {
            private readonly Sprite sprite;
            private readonly WorkingBeatmap beatmap;

            public Background(WorkingBeatmap beatmap)
                : base(cachedFrameBuffer: true)
            {
                this.beatmap = beatmap;

                Depth = float.MaxValue;
                RelativeSizeAxes = Axes.Both;

                Children = new Drawable[]
                {
                    sprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = OsuColour.Gray(150),
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = bottom_black_area_height,
                        Origin = Anchor.BottomCentre,
                        Anchor = Anchor.BottomCentre,
                        Colour = Color4.Black.Opacity(0.5f)
                    }
                };
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures)
            {
                sprite.Texture = beatmap.GetBackground() ?? textures.Get(@"Backgrounds/bg2");
            }
        }

        private partial class DragContainer : Container
        {
            protected override bool OnDragStart(DragStartEvent e)
            {
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                Vector2 change = e.MousePosition - e.MouseDownPosition;

                // Diminish the drag distance as we go further to simulate "rubber band" feeling.
                change *= change.Length <= 0 ? 0 : MathF.Pow(change.Length, 0.7f) / change.Length;

                this.MoveTo(change);
            }

            protected override void OnDragEnd(DragEndEvent e)
            {
                this.MoveTo(Vector2.Zero, 800, Easing.OutElastic);
                base.OnDragEnd(e);
            }
        }

        private partial class HoverableProgressBar : ProgressBar
        {
            public override bool HandleNonPositionalInput => IsHovered;

            public HoverableProgressBar()
                : base(true)
            {
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.ResizeHeightTo(progress_height, 500, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.ResizeHeightTo(progress_height / 2, 500, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}
