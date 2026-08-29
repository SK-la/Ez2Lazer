// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Reflection;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    internal static class EzQuickRotationRankedPlayCardHelper
    {
        private static readonly FieldInfo? songPreviewContainerField = typeof(RankedPlayCard).GetField("songPreviewContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? cardRevealedField = typeof(RankedPlayCard).GetField("cardRevealed", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void PresentLocalBeatmap(RankedPlayCard card, APIBeatmap beatmap, BeatmapInfo localBeatmap)
        {
            card.SetContent(new RankedPlayCardContent(beatmap, localBeatmap));

            if (songPreviewContainerField?.GetValue(card) is object previewContainer)
                previewContainer.GetType().GetMethod("LoadPreview")?.Invoke(previewContainer, new object[] { beatmap });

            if (cardRevealedField?.GetValue(card) is TaskCompletionSource revealed)
                revealed.TrySetResult();
        }
    }
}
