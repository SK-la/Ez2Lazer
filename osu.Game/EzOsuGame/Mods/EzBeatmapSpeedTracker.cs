// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Mods
{
    /// <summary>
    /// Unified source of the effective beat length and playback rate for the current beatmap and mod selection.
    /// </summary>
    /// <remarks>
    /// Consumers (beat-synced HUD components, previews) should read <see cref="BeatLength"/> and <see cref="Rate"/>
    /// instead of re-deriving them: this component owns the whole chain
    /// <list type="bullet">
    /// <item>the beatmap whose timing points apply: while playing, <see cref="GameplayState.Beatmap"/>, so multi-BPM / SV
    /// timing point lists are honoured as the mods converted them; otherwise the loaded beatmap — see
    /// <see cref="ResolveTimingBeatmap"/> for why no conversion is needed either way, and <see cref="NeedsBeatmapLoad"/>
    /// for the still-decoding selection a carousel move produces;</item>
    /// <item>the rate: it is read from the aggregated mod adjustments the audio is actually played through, so it is
    /// what you hear rather than what a difficulty calculation assumes — the gameplay clock's adjustments while playing
    /// (<see cref="GameplayClockExtensions.GetTrueGameplayRate"/>), and the song select music track otherwise (see
    /// <see cref="ResolveTrackRate"/>). Only with neither of those present does it fall back to
    /// <see cref="EzModRate.ResolvePlaybackRate"/>, which reports one scalar for the whole mod list.</item>
    /// </list>
    /// <para>
    /// <see cref="BeatLength"/> follows the live playhead whenever the chart has more than one timing point, so a
    /// multi-BPM chart reports the section being played rather than a single average. With no playhead it reports the
    /// chart's most common beat length, the same value the song select title labels "mostly".
    /// </para>
    /// Add it as a child of the consuming drawable; it draws nothing.
    /// </remarks>
    public partial class EzBeatmapSpeedTracker : CompositeComponent
    {
        /// <summary>
        /// Milliseconds per beat of the beatmap being played, or 0 when unknown.
        /// Follows in-chart timing point changes at the live playhead (gameplay clock while playing, song select music
        /// otherwise); with no playhead it is the chart's most common beat length, and while the selection is still
        /// decoding it is derived from the metadata BPM.
        /// </summary>
        public BindableDouble BeatLength { get; } = new BindableDouble();

        /// <summary>
        /// The rate the audio is actually played at. See the class remarks for how it is resolved.
        /// </summary>
        public BindableDouble Rate { get; } = new BindableDouble(1);

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IBindable<IReadOnlyList<Mod>>? mods { get; set; }

        // Only available while playing, where the mod-applied beatmap comes from.
        [Resolved(canBeNull: true)]
        private GameplayState? gameplayState { get; set; }

        // Only available while playing. Every rate mod registers itself here as an audio adjustment, aggregated across
        // all of them, and FrameStabilityContainer delegates this to the outer gameplay clock.
        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private IFrameStableClock? frameStableClock { get; set; }

        // Song select has no gameplay clock. What is audible there is the music controller's track, which the mod
        // adjustments are applied to (see SongSelect.ApplyModTrackAdjustments), and which carries the playhead.
        [Resolved(canBeNull: true)]
        private MusicController? musicController { get; set; }

        private IBeatmap? timingBeatmap;
        private bool hasVariableTiming;
        private bool awaitingBeatmapLoad;

        private IBindable<double>? rateFrequency;
        private IBindable<double>? rateTempo;
        private bool usesFallbackRate;

        private DrawableTrack? songSelectTrack;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            mods?.BindValueChanged(change => updateMods(change.NewValue));
            beatmap.BindValueChanged(_ => updateBeatmapTiming());

            if (gameplayClock != null)
                attachRateSource(gameplayClock.AdjustmentsFromMods.AggregateFrequency, gameplayClock.AdjustmentsFromMods.AggregateTempo);
            else if (musicController != null)
            {
                musicController.TrackChanged += onMusicControllerTrackChanged;
                attachSongSelectTrack();
            }
            else
                updateFallbackRate(mods?.Value);

            updateBeatmapTiming();
        }

        protected override void Update()
        {
            base.Update();

            if (awaitingBeatmapLoad)
            {
                var working = beatmap.Value;

                // Waiting on the decode of a selection that arrived still-loading (see updateBeatmapTiming). The
                // metadata BPM stands in until the timing points exist; this is the only work the wait costs.
                if (working != null && !working.BeatmapLoaded)
                    return;

                updateBeatmapTiming();
            }

            if (!hasVariableTiming || timingBeatmap == null || livePlayhead is not double songTime)
                return;

            // The only per-frame work, and only for charts whose beat length actually changes: resolve the timing point
            // active right now and publish it when the section changes. Reference/bin-search only, no allocation.
            double liveBeatLength = timingBeatmap.ControlPointInfo.TimingPointAt(songTime).BeatLength;

            if (liveBeatLength > 0 && liveBeatLength != BeatLength.Value)
                BeatLength.Value = liveBeatLength;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (musicController != null)
                musicController.TrackChanged -= onMusicControllerTrackChanged;

            detachRateSource();
        }

        /// <summary>
        /// The song time of whatever is audibly playing right now, or <see langword="null"/> when nothing is.
        /// </summary>
        private double? livePlayhead
        {
            get
            {
                if (gameplayClock != null)
                    return frameStableClock?.CurrentTime ?? gameplayClock.CurrentTime;

                // A paused or stopped song select track has nothing to follow, and its position may be left over from a
                // different beatmap entirely (song selects that own their audio, like BMS, stop the global controller).
                return songSelectTrack?.IsRunning == true ? songSelectTrack.CurrentTime : null;
            }
        }

        /// <summary>
        /// Milliseconds per beat of <paramref name="beatmap"/>, or derived from <paramref name="fallbackBpm"/> when the
        /// beatmap carries no usable timing points. Returns 0 when neither is available.
        /// </summary>
        /// <param name="songTime">The song time to resolve the active timing point at, or null to use the
        /// beatmap's most common beat length (matching the BPM shown on the song select title).</param>
        public static double ResolveBeatLength(IBeatmap? beatmap, double? songTime, double fallbackBpm)
        {
            if (beatmap != null && beatmap.ControlPointInfo.TimingPoints.Count > 0)
            {
                double beatLength = songTime.HasValue
                    ? beatmap.ControlPointInfo.TimingPointAt(songTime.Value).BeatLength
                    : beatmap.GetMostCommonBeatLength();

                if (beatLength > 0)
                    return beatLength;
            }

            return fallbackBpm > 0 ? 60000 / fallbackBpm : 0;
        }

        /// <summary>
        /// Resolves the beatmap whose timing points describe the current selection.
        /// </summary>
        /// <param name="gameplayBeatmap">The already mod-applied beatmap from <see cref="GameplayState"/>, when playing.</param>
        /// <param name="beatmap">The beatmap selection, whose loaded beatmap is used outside gameplay.</param>
        /// <remarks>
        /// Conversion never rewrites timing points, so <c>WorkingBeatmap.GetPlayableBeatmap</c> is deliberately not
        /// used: the converted beatmap shares the source <see cref="ControlPointInfo"/>
        /// (see <see cref="BeatmapConverter{TObject}"/>), no mod touches timing, and running a full synchronous
        /// conversion just to read a beat length would additionally execute every <see cref="IApplicableToBeatmap"/>
        /// mod's side effects, with the current ruleset's mods, from a display-only component.
        /// </remarks>
        public static IBeatmap? ResolveTimingBeatmap(IBeatmap? gameplayBeatmap, WorkingBeatmap? beatmap)
            => gameplayBeatmap ?? beatmap?.Beatmap;

        /// <summary>
        /// Whether reading the timing points of the current selection would have to block on a beatmap decode.
        /// </summary>
        /// <param name="gameplayBeatmap">The already mod-applied beatmap from <see cref="GameplayState"/>, when playing.</param>
        /// <param name="beatmap">The beatmap selection, whose loaded beatmap is used outside gameplay.</param>
        /// <remarks>
        /// <c>WorkingBeatmap.Beatmap</c> resolves its asynchronous load synchronously, so touching it while a freshly
        /// selected beatmap is still decoding stalls the update thread — and song select emits a selection per carousel
        /// move. Callers are expected to stand in with <see cref="BeatmapInfo.BPM"/> and retry while this is true.
        /// An already running gameplay beatmap never needs that, as gameplay only starts once it is loaded.
        /// </remarks>
        public static bool NeedsBeatmapLoad(IBeatmap? gameplayBeatmap, WorkingBeatmap? beatmap)
            => gameplayBeatmap == null && beatmap != null && !beatmap.BeatmapLoaded;

        /// <summary>
        /// The rate an audio component is actually played at, from its aggregated mod adjustments.
        /// </summary>
        /// <param name="frequency">The component's <c>AggregateFrequency</c>.</param>
        /// <param name="tempo">The component's <c>AggregateTempo</c>.</param>
        /// <remarks>
        /// These are the aggregates the audio engine itself plays through, so this covers every
        /// <see cref="IApplicableToTrack"/> mod in both pitch modes (a rate mod lands in the frequency or the tempo
        /// aggregate depending on its pitch setting, and they multiply), at full precision and following the live value
        /// of mods whose speed changes — unlike a single scalar computed from the mod list.
        /// </remarks>
        public static double ResolveTrackRate(IBindable<double>? frequency, IBindable<double>? tempo)
            => (frequency?.Value ?? 1) * (tempo?.Value ?? 1);

        /// <summary>
        /// Advances a beat phase by one frame, returning it wrapped into [0, 1).
        /// </summary>
        /// <param name="phase">The phase to advance, as returned by a previous call.</param>
        /// <param name="elapsedTime">Milliseconds elapsed since the last frame, in the clock the caller animates on.</param>
        /// <param name="beatLength">Milliseconds per beat, as reported by <see cref="BeatLength"/>.</param>
        /// <param name="rate">The multiplier to apply to <paramref name="elapsedTime"/>; see the remarks.</param>
        /// <remarks>
        /// A beat-synced animation should advance a phase like this instead of taking the song time modulo a beat
        /// length: a modulo of the absolute time re-derives the phase from the song's origin, so any change of beat
        /// length makes the animation visibly jump, whereas an accumulated phase carries straight through a section
        /// change. Wrapping every call also keeps the value too small to lose precision in a long song.
        /// <para>
        /// Whether <paramref name="rate"/> should be the audible rate depends on where <paramref name="elapsedTime"/>
        /// comes from, not on this helper: a gameplay clock's elapsed time is already expressed in song time (it
        /// advances at the audible rate), so it needs no multiplier and passing one would double-count it. A
        /// real-time clock — a host or overlay clock that keeps running while gameplay is paused — does need
        /// <see cref="Rate"/> to convert its elapsed time into song time.
        /// </para>
        /// </remarks>
        public static double AdvanceBeatPhase(double phase, double elapsedTime, double beatLength, double rate = 1)
        {
            if (!(beatLength > 0) || !double.IsFinite(beatLength) || !double.IsFinite(elapsedTime) || !double.IsFinite(rate))
                return phase;

            double wrapped = (phase + elapsedTime * rate / beatLength) % 1;

            // A rewind (negative elapsed) would otherwise leave the phase negative.
            return wrapped < 0 ? wrapped + 1 : wrapped;
        }

        private void updateMods(IReadOnlyList<Mod>? selectedMods)
        {
            // Mods never change timing, and both live rate sources follow the mods on their own. Only the fallback,
            // used when there is no audio to read at all, has to be recomputed here.
            if (usesFallbackRate)
                updateFallbackRate(selectedMods);
        }

        private void updateFallbackRate(IReadOnlyList<Mod>? selectedMods)
        {
            usesFallbackRate = true;
            Rate.Value = EzModRate.ResolvePlaybackRate(selectedMods);
        }

        private void updateBeatmapTiming()
        {
            var working = beatmap.Value;

            awaitingBeatmapLoad = NeedsBeatmapLoad(gameplayState?.Beatmap, working);

            if (awaitingBeatmapLoad)
            {
                // Do not touch WorkingBeatmap.Beatmap here: it blocks the update thread until the asynchronous decode
                // finishes, and song select emits one selection per carousel move. The metadata BPM is what the title
                // already shows, and Update() picks the real timing points up as soon as they exist.
                timingBeatmap = null;
                hasVariableTiming = false;
                BeatLength.Value = ResolveBeatLength(null, null, working?.BeatmapInfo.BPM ?? 0);
                return;
            }

            timingBeatmap = EzBeatmapSpeedTracker.ResolveTimingBeatmap(gameplayState?.Beatmap, working);

            hasVariableTiming = hasVariableBeatLength(timingBeatmap?.ControlPointInfo);

            BeatLength.Value = ResolveBeatLength(timingBeatmap, null, working?.BeatmapInfo.BPM ?? 0);
        }

        private void attachRateSource(IBindable<double> frequency, IBindable<double> tempo)
        {
            detachRateSource();

            usesFallbackRate = false;

            rateFrequency = frequency;
            rateTempo = tempo;

            frequency.BindValueChanged(onRateChanged, true);
            tempo.BindValueChanged(onRateChanged, true);
        }

        private void detachRateSource()
        {
            if (rateFrequency != null)
            {
                rateFrequency.ValueChanged -= onRateChanged;
                rateFrequency = null;
            }

            if (rateTempo != null)
            {
                rateTempo.ValueChanged -= onRateChanged;
                rateTempo = null;
            }
        }

        private void onRateChanged(ValueChangedEvent<double> _)
        {
            if (gameplayClock != null)
            {
                // The same single definition of truth every other gameplay component reads (BPM counter, judgement
                // rate, ...), including a reversed clock's sign.
                Rate.Value = gameplayClock.GetTrueGameplayRate();
                return;
            }

            Rate.Value = EzBeatmapSpeedTracker.ResolveTrackRate(rateFrequency, rateTempo);
        }

        private void onMusicControllerTrackChanged(WorkingBeatmap loadedBeatmap, TrackChangeDirection direction) => attachSongSelectTrack();

        private void attachSongSelectTrack()
        {
            var track = musicController?.CurrentTrack;

            if (ReferenceEquals(songSelectTrack, track))
                return;

            songSelectTrack = track;

            if (track != null)
                attachRateSource(track.AggregateFrequency, track.AggregateTempo);
            else
            {
                detachRateSource();
                updateFallbackRate(mods?.Value);
            }
        }

        private static bool hasVariableBeatLength(ControlPointInfo? controlPointInfo)
        {
            var timingPoints = controlPointInfo?.TimingPoints;

            if (timingPoints == null || timingPoints.Count < 2)
                return false;

            double first = timingPoints[0].BeatLength;

            for (int i = 1; i < timingPoints.Count; i++)
            {
                if (timingPoints[i].BeatLength != first)
                    return true;
            }

            return false;
        }
    }
}
