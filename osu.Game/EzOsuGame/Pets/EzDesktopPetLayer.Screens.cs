// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Select;

namespace osu.Game.EzOsuGame.Pets
{
    public partial class EzDesktopPetLayer
    {
        private void onScreenChanged(IScreen lastScreen, IScreen newScreen) => applyCurrentScreen(newScreen);

        private void applyCurrentScreen(IScreen? screen)
        {
            bool wasGameplay = inGameplay;

            inGameplay = screen is Player;

            if (screen is MainMenu)
                currentScene = PetScene.Menu;
            else if (screen is ISongSelect)
                currentScene = PetScene.SongSelect;
            else if (screen is Player)
                currentScene = PetScene.Gameplay;
            else
                currentScene = PetScene.Other;

            if (wasGameplay && !inGameplay)
                eventHidden = false;

            if (screen is Player player)
            {
                bindPlayer(player);

                if (!wasGameplay)
                    stateMachine.HandleGameplayEnter();
            }
            else
                unbindPlayer();

            if (screen is ISongSelect)
                tryHandleStarRating();

            updateVisibility();
        }

        private void onBeatmapChanged()
        {
            stateMachine.ResetIdleTimer();

            if (game?.ScreenStack.CurrentScreen is ISongSelect)
                tryHandleStarRating();
        }

        private void tryHandleStarRating()
        {
            var info = beatmap.Value?.BeatmapInfo;
            if (info == null)
                return;

            double stars = info.StarRating;

            if (info.ID == lastStarBeatmapId && stars.Equals(lastStarRating))
                return;

            lastStarBeatmapId = info.ID;
            lastStarRating = stars;
            stateMachine.HandleStarRating(stars);
        }

        private void bindPlayer(Player player)
        {
            if (boundPlayer == player && boundScoreProcessor != null)
                return;

            unbindPlayer();
            boundPlayer = player;
            stateMachine.ResetPlaySession();

            if (player.IsLoaded)
                attachScoreProcessor(player);
            else
                player.OnLoadComplete += onPlayerLoadComplete;
        }

        private void onPlayerLoadComplete(Drawable loaded)
        {
            if (boundPlayer == loaded)
                attachScoreProcessor(boundPlayer);
        }

        private void attachScoreProcessor(Player player)
        {
            if (player != boundPlayer || player.GameplayState == null)
                return;

            boundScoreProcessor = player.GameplayState.ScoreProcessor;
            boundScoreProcessor.Combo.ValueChanged += onComboChanged;
            boundScoreProcessor.NewJudgement += onNewJudgement;
        }

        private void unbindPlayer()
        {
            if (boundPlayer != null)
                boundPlayer.OnLoadComplete -= onPlayerLoadComplete;

            if (boundScoreProcessor != null)
            {
                boundScoreProcessor.Combo.ValueChanged -= onComboChanged;
                boundScoreProcessor.NewJudgement -= onNewJudgement;
            }

            boundPlayer = null;
            boundScoreProcessor = null;
        }

        private void onComboChanged(ValueChangedEvent<int> e)
        {
            if (e.NewValue > 0)
                stateMachine.HandleCombo(e.NewValue);
        }

        private void onNewJudgement(JudgementResult result)
        {
            if (result.Type.BreaksCombo())
                stateMachine.HandleMiss();
            else
                stateMachine.NotifyNonMissJudgement();
        }
    }
}
