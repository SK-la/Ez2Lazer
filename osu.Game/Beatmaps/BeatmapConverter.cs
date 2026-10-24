// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Lists;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Converts a Beatmap for another mode.
    /// </summary>
    /// <typeparam name="T">The type of HitObject stored in the Beatmap.</typeparam>
    public abstract class BeatmapConverter<T> : IBeatmapConverter
        where T : HitObject
    {
        private event Action<HitObject, IEnumerable<HitObject>> objectConverted;

        event Action<HitObject, IEnumerable<HitObject>> IBeatmapConverter.ObjectConverted
        {
            add => objectConverted += value;
            remove => objectConverted -= value;
        }

        public IBeatmap Beatmap { get; }

        protected BeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
        {
            Beatmap = beatmap;
        }

        /// <summary>
        /// Whether <see cref="Beatmap"/> can be converted by this <see cref="BeatmapConverter{T}"/>.
        /// </summary>
        public abstract bool CanConvert();

        public IBeatmap Convert(CancellationToken cancellationToken = default)
        {
            // We always operate on a clone of the original beatmap, to not modify it game-wide
            var original = Beatmap.Clone();

            // Shallow clone isn't enough to ensure we don't mutate beatmap info unexpectedly.
            // Can potentially be removed after `Beatmap.Difficulty` doesn't save back to `Beatmap.BeatmapInfo`.
            original.BeatmapInfo = original.BeatmapInfo.Clone();
            original.ControlPointInfo = original.ControlPointInfo.DeepClone();

            // Used in osu!mania conversion.
            original.Breaks = new SortedList<BreakPeriod>(Comparer<BreakPeriod>.Default);
            original.Breaks.AddRange(Beatmap.Breaks);

            return ConvertBeatmap(original, cancellationToken);
        }

        /// <summary>
        /// Performs the conversion of a Beatmap using this Beatmap Converter.
        /// </summary>
        /// <param name="original">The un-converted Beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The converted Beatmap.</returns>
        protected virtual Beatmap<T> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            var beatmap = CreateBeatmap();

            beatmap.BeatmapInfo = original.BeatmapInfo;
            beatmap.ControlPointInfo = original.ControlPointInfo;
            beatmap.HitObjects = convertHitObjects(original.HitObjects, original, cancellationToken).OrderBy(s => s.StartTime).ToList();
            beatmap.Breaks = original.Breaks;
            beatmap.AudioLeadIn = original.AudioLeadIn;
            beatmap.StackLeniency = original.StackLeniency;
            beatmap.SpecialStyle = original.SpecialStyle;
            beatmap.LetterboxInBreaks = original.LetterboxInBreaks;
            beatmap.WidescreenStoryboard = original.WidescreenStoryboard;
            beatmap.EpilepsyWarning = original.EpilepsyWarning;
            beatmap.SamplesMatchPlaybackRate = original.SamplesMatchPlaybackRate;
            beatmap.DistanceSpacing = original.DistanceSpacing;
            beatmap.GridSize = original.GridSize;
            beatmap.TimelineZoom = original.TimelineZoom;
            beatmap.Countdown = original.Countdown;
            beatmap.CountdownOffset = original.CountdownOffset;
            beatmap.Bookmarks = original.Bookmarks;
            beatmap.SliderVelocityPresets = original.SliderVelocityPresets;
            beatmap.BeatmapVersion = original.BeatmapVersion;

            return beatmap;
        }

        private List<T> convertHitObjects(IReadOnlyList<HitObject> hitObjects, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var result = new List<T>(hitObjects.Count);

            foreach (var obj in hitObjects)
            {
                if (obj is T tObj)
                {
                    result.Add(ConvertCompatibleHitObject(tObj));
                    continue;
                }

                var converted = ConvertHitObject(obj, beatmap, cancellationToken);

                if (objectConverted != null)
                {
                    converted = converted.ToList();
                    objectConverted.Invoke(obj, converted);
                }

                foreach (var c in converted)
                {
                    if (c != null)
                        result.Add(c);
                }
            }

            return result;
        }

        /// <summary>
        /// Creates the <see cref="Beatmap{T}"/> that will be returned by this <see cref="BeatmapProcessor"/>.
        /// </summary>
        protected virtual Beatmap<T> CreateBeatmap() => new Beatmap<T>();

        /// <summary>
        /// Handles a source hit object that is already of the target type <typeparamref name="T"/>, which
        /// <c>convertHitObjects</c> would otherwise put into the converted beatmap as-is.
        /// </summary>
        /// <remarks>
        /// The default reuse makes the converted beatmap share hit object instances with its source, so every
        /// in-place write the pipeline performs on a converted object (post-conversion mods, judgement and hit
        /// window binding, <see cref="HitObject.ApplyDefaults"/>) also lands on the source — and, since the same
        /// source object is handed out again by the next conversion, on every other converted beatmap as well.
        /// Override this to return <see cref="HitObject.Clone"/> when the resulting beatmap must own its hit
        /// objects; see <c>ManiaBeatmapConverter</c>.
        /// </remarks>
        protected virtual T ConvertCompatibleHitObject(T obj) => obj;

        /// <summary>
        /// Performs the conversion of a hit object.
        /// This method is generally executed sequentially for all objects in a beatmap.
        /// </summary>
        /// <param name="original">The hit object to convert.</param>
        /// <param name="beatmap">The un-converted Beatmap.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The converted hit object.</returns>
        protected virtual IEnumerable<T> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken) => Enumerable.Empty<T>();
    }
}
