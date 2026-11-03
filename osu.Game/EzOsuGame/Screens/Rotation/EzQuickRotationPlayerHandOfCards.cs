// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Input.Events;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Hand;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    /// <summary>
    /// Routes card hover by mouse X position so overlapping arc-layout cards all receive preview hover.
    /// </summary>
    public partial class EzQuickRotationPlayerHandOfCards : PlayerHandOfCards
    {
        private static readonly MethodInfo? card_clicked_method = typeof(PlayerHandOfCards).GetMethod("cardClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo? card_dragged_method = typeof(PlayerHandOfCards).GetMethod("cardDragged", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? allow_selection_field = typeof(PlayerHandOfCards).GetField("allowSelection", BindingFlags.Instance | BindingFlags.NonPublic);

        protected override HandCard CreateHandCard(RankedPlayCard card) => new EzQuickRotationPlayerHandCard(card, Flipped)
        {
            Clicked = invokeCardClicked,
            Dragged = invokeCardDragged,
            AllowSelection = ((BindableBool)allow_selection_field!.GetValue(this)!).GetBoundCopy(),
            PlayAction = PlayCardAction,
        };

        private void invokeCardClicked(PlayerHandCard handCard) => card_clicked_method?.Invoke(this, new object[] { handCard });

        private void invokeCardDragged(PlayerHandCard handCard, Vector2 screenSpacePosition) => card_dragged_method?.Invoke(this, new object[] { handCard, screenSpacePosition });

        protected override void Update()
        {
            base.Update();

            if (Contracted || SelectionMode == HandSelectionMode.Disabled)
                return;

            var cards = GetCardsInDisplayOrder();

            if (cards.Count == 0)
                return;

            var inputManager = GetContainingInputManager();

            if (inputManager == null)
                return;

            var screenSpaceMouse = inputManager.CurrentState.Mouse.Position;

            if (!ReceivePositionalInputAt(screenSpaceMouse))
            {
                clearHover(cards);
                return;
            }

            int targetIndex = cardIndexAtScreenPosition(cards, screenSpaceMouse);

            for (int i = 0; i < cards.Count; i++)
                cards[i].CardHovered = i == targetIndex;
        }

        private void clearHover(IReadOnlyList<HandCard> cards)
        {
            foreach (var card in cards)
                card.CardHovered = false;
        }

        private int cardIndexAtScreenPosition(IReadOnlyList<HandCard> cards, Vector2 screenSpacePosition)
        {
            var position = ToLocalSpace(screenSpacePosition) - DrawSize / 2;
            int activeIndex = GetActiveCardIndex(cards);

            int minIndex = 0;
            float minDistance = float.MaxValue;

            for (int i = 0; i < cards.Count; i++)
            {
                float distance = MathF.Abs(GetCardX(i, activeIndex) - position.X);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    minIndex = i;
                }
            }

            return minIndex;
        }

        public partial class EzQuickRotationPlayerHandCard : PlayerHandCard
        {
            public EzQuickRotationPlayerHandCard(RankedPlayCard card, bool flipped)
                : base(card, flipped)
            {
            }

            protected override bool OnHover(HoverEvent e) => true;

            protected override void OnHoverLost(HoverLostEvent e)
            {
            }
        }
    }
}
