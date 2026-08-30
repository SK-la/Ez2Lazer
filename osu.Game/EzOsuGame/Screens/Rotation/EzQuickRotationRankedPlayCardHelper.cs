// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Audio;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;

namespace osu.Game.EzOsuGame.Screens.Rotation
{
    internal static class EzQuickRotationRankedPlayCardHelper
    {
        private static readonly FieldInfo? song_preview_container_field = typeof(RankedPlayCard).GetField("songPreviewContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? card_revealed_field = typeof(RankedPlayCard).GetField("cardRevealed", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? load_component_async_method = typeof(CompositeDrawable).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                                                                                   .Single(method => method.Name == "LoadComponentAsync" && method.IsGenericMethodDefinition);

        private static readonly MethodInfo? add_internal_method = typeof(CompositeDrawable).GetMethod("AddInternal", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(Drawable) });

        public static void PresentLocalBeatmap(RankedPlayCard card, APIBeatmap beatmap, BeatmapInfo localBeatmap, BeatmapManager beatmapManager)
        {
            card.SetContent(new EzQuickRotationRankedPlayCardContent(beatmap, localBeatmap));

            if (song_preview_container_field?.GetValue(card) is CompositeDrawable previewContainer)
                loadLocalPreview(previewContainer, beatmap, localBeatmap, beatmapManager);

            if (card_revealed_field?.GetValue(card) is TaskCompletionSource revealed)
                revealed.TrySetResult();
        }

        private static void loadLocalPreview(CompositeDrawable previewContainer, APIBeatmap beatmap, BeatmapInfo localBeatmap, BeatmapManager beatmapManager)
        {
            var containerType = previewContainer.GetType();
            var previewTrackField = containerType.GetField("previewTrack", BindingFlags.Instance | BindingFlags.NonPublic);
            var trackRunningField = containerType.GetField("trackRunning", BindingFlags.Instance | BindingFlags.NonPublic);
            var setupBeatSyncMethod = containerType.GetMethod("setupBeatSyncProvider", BindingFlags.Instance | BindingFlags.NonPublic);

            if (previewTrackField == null)
                return;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(localBeatmap);
            var localTrack = new EzLocalBeatmapPreviewTrack(workingBeatmap);

            previewTrackField.SetValue(previewContainer, localTrack);

            var loadAsync = load_component_async_method!.MakeGenericMethod(typeof(EzLocalBeatmapPreviewTrack));
            loadAsync.Invoke(previewContainer, new object?[]
            {
                localTrack,
                new Action<EzLocalBeatmapPreviewTrack>(track =>
                {
                    add_internal_method?.Invoke(previewContainer, new object[] { track });

                    track.Looping = true;

                    if (trackRunningField?.GetValue(previewContainer) is Bindable<bool> trackRunning)
                    {
                        track.Started += () => trackRunning.Value = true;
                        track.Stopped += () => trackRunning.Value = false;
                    }

                    setupBeatSyncMethod?.Invoke(previewContainer, new object[] { track, beatmap });
                }),
                CancellationToken.None,
                null,
            });
        }
    }
}
