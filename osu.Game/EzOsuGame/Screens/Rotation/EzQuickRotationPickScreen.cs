// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Footer;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Hand;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    [Cached]
    public partial class EzQuickRotationPickScreen : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>, IPreviewTrackOwner
    {
        private static readonly Color4 header_plate_colour = Color4.Black.Opacity(0.65f);
        private const float background_dim_alpha = 0.25f;
        private const float header_corner_radius = 12f;

        private PlayerHandOfCards playerHand = null!;
        private EzQuickRotationWedgeArea wedgeArea = null!;
        private Container emptyStateContainer = null!;

        private readonly List<EzQuickRotationLocalRankedPlayCard> cards = new List<EzQuickRotationLocalRankedPlayCard>();

        private Sample? cardAddSample;

        [Cached]
        private readonly CardDetailsOverlayContainer cardDetailsOverlay = new CardDetailsOverlayContainer();

        [Cached]
        private readonly SongPreviewParticleContainer particleContainer = new SongPreviewParticleContainer();

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private PreviewTrackManager previewTrackManager { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            cardAddSample = audio.Samples.Get(@"Multiplayer/Matchmaking/Ranked/card-add-1");

            var session = EzQuickRotationCoordinator.Session;
            var initialCandidates = EzQuickRotationPoolBuilder.DrawCandidates(session.CachedPool, session.PlayedBeatmapIds, EzQuickRotationSession.CandidateCount);
            var initialWedgeBeatmap = initialCandidates.Count > 0
                ? beatmaps.GetWorkingBeatmap(initialCandidates[0])
                : beatmaps.GetWorkingBeatmap(null);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black.Opacity(background_dim_alpha),
                },
                cardDetailsOverlay,
                particleContainer,
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new[]
                            {
                                wedgeArea = new EzQuickRotationWedgeArea(initialWedgeBeatmap, session.Ruleset, session.BaseMods),
                                createStageHeader(),
                                new RoundedButton
                                {
                                    Text = EzQuickRotationStrings.PICK_END_SESSION,
                                    Width = ScreenBackButton.BUTTON_WIDTH,
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    Margin = new MarginPadding
                                    {
                                        Left = OsuGame.SCREEN_EDGE_MARGIN,
                                        Bottom = OsuGame.SCREEN_EDGE_MARGIN + ShearedButton.DEFAULT_HEIGHT + 10,
                                    },
                                    Action = () => EzQuickRotationCoordinator.EndSession(game),
                                },
                                emptyStateContainer = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = EzQuickRotationStrings.POOL_EMPTY,
                                            Font = OsuFont.TorusAlternate.With(size: 28, weight: FontWeight.SemiBold),
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                        },
                                    },
                                },
                                playerHand = new PlayerHandOfCards
                                {
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    RelativeSizeAxes = Axes.Both,
                                    Height = 0.55f,
                                    SelectionMode = HandSelectionMode.Single,
                                    PlayCardAction = onPlayButtonClicked,
                                },
                            },
                        },
                    },
                },
            };

            playerHand.SelectionChanged += onCardSelectionChanged;
            loadCandidates(initialCandidates);
        }

        private Drawable createStageHeader()
        {
            var session = EzQuickRotationCoordinator.Session;

            return new Container
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Margin = new MarginPadding { Right = 40 },
                Masking = true,
                CornerRadius = header_corner_radius,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = header_plate_colour,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = 28, Vertical = 18 },
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = EzQuickRotationStrings.PICK_TITLE,
                                Font = OsuFont.TorusAlternate.With(size: 34),
                                Shadow = false,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new OsuSpriteText
                            {
                                Text = EzQuickRotationStrings.PICK_CAPTION,
                                Font = OsuFont.TorusAlternate.With(size: 22, weight: FontWeight.SemiBold),
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new OsuSpriteText
                            {
                                Text = EzQuickRotationStrings.PICK_BASELINE.Format(session.BaselineDifficulty),
                                Font = OsuFont.TorusAlternate.With(size: 18),
                                Alpha = 0.85f,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new OsuSpriteText
                            {
                                Text = EzQuickRotationStrings.PICK_ENTER_RANDOM,
                                Font = OsuFont.TorusAlternate.With(size: 16),
                                Alpha = 0.7f,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                        },
                    },
                },
            };
        }

        private void loadCandidates(IReadOnlyList<BeatmapInfo> candidates)
        {
            var session = EzQuickRotationCoordinator.Session;

            if (candidates.Count == 0)
            {
                emptyStateContainer.FadeIn(300);
                playerHand.SelectionMode = HandSelectionMode.Disabled;
                return;
            }

            const double stagger = 50;
            double delay = 0;

            updateWedgeForBeatmap(candidates[0]);

            foreach (var beatmap in candidates)
            {
                var apiBeatmap = EzQuickRotationApiBeatmapFactory.Create(beatmap, session.Ruleset);
                var card = new EzQuickRotationLocalRankedPlayCard(beatmap, apiBeatmap);
                cards.Add(card);

                double currentDelay = delay;

                playerHand.AddCard(card, handCard =>
                {
                    handCard.Position = playerHand.BottomCardInsertPosition;
                    handCard.DelayMovementOnEntering(currentDelay);
                });

                Scheduler.AddDelayed(() => SamplePlaybackHelper.PlayWithRandomPitch(cardAddSample), delay);
                delay += stagger;
            }

            Scheduler.AddDelayed(() =>
            {
                if (playerHand.GetCardsInDisplayOrder().FirstOrDefault() is { } firstCard)
                    firstCard.TriggerClick();
            }, delay + 100);
        }

        private void onCardSelectionChanged()
        {
            var selectedCard = playerHand.Cards.FirstOrDefault(c => c.Selected)?.Card as EzQuickRotationLocalRankedPlayCard;

            if (selectedCard == null)
                return;

            updateWedgeForBeatmap(selectedCard.SourceBeatmap);
        }

        private void updateWedgeForBeatmap(BeatmapInfo beatmap)
        {
            var session = EzQuickRotationCoordinator.Session;
            var working = beatmaps.GetWorkingBeatmap(beatmap);
            wedgeArea.UpdateSelection(working, session.Ruleset, session.BaseMods);
        }

        private void onPlayButtonClicked()
        {
            var selectedCard = playerHand.Cards.FirstOrDefault(c => c.Selected)?.Card as EzQuickRotationLocalRankedPlayCard;

            if (selectedCard == null)
                return;

            playerHand.SelectionMode = HandSelectionMode.Disabled;
            playerHand.PlayCardAction = null;
            startBeatmap(selectedCard.SourceBeatmap);
        }

        private void startBeatmap(BeatmapInfo beatmap)
        {
            var session = EzQuickRotationCoordinator.Session;
            var balance = EzQuickRotationDifficultyBalancer.Balance(beatmaps, beatmap, session.Ruleset, session.BaselineDifficulty,
                EzQuickRotationSession.DifficultyTolerance);

            var niceBpm = EzQuickRotationDifficultyBalancer.CreateNiceBpmMod(session.Ruleset, balance.Speed);
            var mods = EzQuickRotationDifficultyBalancer.MergeMods(session.BaseMods, niceBpm);

            session.MarkPlayed(beatmap);
            EzQuickRotationGameplayLauncher.Start(this, beatmaps, Beatmap, Ruleset, Mods, beatmap, session.Ruleset, mods);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Select:
                    if (cards.Count == 0)
                        return true;

                    var randomBeatmap = cards[Random.Shared.Next(cards.Count)].SourceBeatmap;
                    startBeatmap(randomBeatmap);
                    return true;

                case GlobalAction.QuickExit:
                    EzQuickRotationCoordinator.EndSession(game);
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            previewTrackManager.StopAnyPlaying(this);
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            playerHand.SelectionChanged -= onCardSelectionChanged;
        }
    }
}
