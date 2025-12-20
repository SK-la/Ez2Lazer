// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play;

namespace osu.Game.LAsEzExtensions.Select
{
    public partial class DuplicateVirtualTrack : EzPreviewTrackManager
    {
        /// <summary>
        /// Debug-only: if set to a positive number, the instance will throw an exception when reaching the
        /// corresponding checkpoint. Use this to pinpoint which part of the duplicate audio pipeline is (not) running.
        ///
        /// Stages:
        /// 1 = StartPreview() entered
        /// 2 = overrides resolved and applied
        /// 3 = gameplay clock/container present, StopUsingBeatmapClock invoked
        /// 4 = original beatmap.Track stopped
        /// 5 = CreateTrack() entered (prints diagnostics)
        /// 52 = CreateTrack() resolved fileStorePath
        /// 53 = CreateTrack() failed to create new track (null)
        /// 54 = CreateTrack() fell back to using beatmap.Track
        /// 55 = CreateTrack() created a new independent track
        /// 6 = UpdateAfterChildren started preview (base.StartPreview)
        /// </summary>
        public int DebugCrashStage { get; set; }

        public IPreviewOverrideProvider? OverrideProvider { get; set; }
        public PreviewOverrideSettings? PendingOverrides { get; set; }

        private bool startRequested;
        private bool started;
        private IWorkingBeatmap? pendingBeatmap;
        private string? debugSnapshot;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private GameplayClockContainer? gameplayClockContainer { get; set; }

        [Resolved(canBeNull: true)]
        private BeatmapManager? beatmapManager { get; set; }

        public new void StartPreview(IWorkingBeatmap beatmap, bool forceEnhanced = false)
        {
            crashIf(1, $"StartPreview entered. gameplayClock={(gameplayClock != null ? "yes" : "no")}");

            pendingBeatmap = beatmap;
            startRequested = true;

            var overrides = PendingOverrides ?? OverrideProvider?.GetPreviewOverrides(beatmap);

            if (overrides != null)
                ApplyOverrides(overrides);

            crashIf(2, $"Overrides applied. PreviewStart={OverridePreviewStartTime} Duration={OverridePreviewDuration} LoopCount={OverrideLoopCount} LoopInterval={OverrideLoopInterval} ExternalClockStartTime={ExternalClockStartTime}");

            OverrideLooping = overrides?.ForceLooping ?? OverrideLooping;
            ExternalClock = gameplayClock;
            ExternalClockStartTime = overrides?.PreviewStart ?? OverridePreviewStartTime;
            EnableHitSounds = overrides?.EnableHitSounds ?? true;

            if (DebugCrashStage == 999)
            {
                var sb = new StringBuilder();
                sb.AppendLine("[DuplicateVirtualTrack DebugDump]");
                sb.AppendLine($"StartPreview: gameplayClock={(gameplayClock != null ? "yes" : "no")} gameplayClockContainerType={gameplayClockContainer?.GetType().FullName ?? "(null)"}");
                sb.AppendLine($"Overrides: PreviewStart={overrides?.PreviewStart} Duration={overrides?.PreviewDuration} LoopCount={overrides?.LoopCount} LoopInterval={overrides?.LoopInterval} ForceLooping={overrides?.ForceLooping} EnableHitSounds={overrides?.EnableHitSounds}");
                sb.AppendLine($"Applied: OverridePreviewStartTime={OverridePreviewStartTime} OverridePreviewDuration={OverridePreviewDuration} OverrideLoopCount={OverrideLoopCount} OverrideLoopInterval={OverrideLoopInterval} OverrideLooping={OverrideLooping} ExternalClockStartTime={ExternalClockStartTime}");
                sb.AppendLine($"Beatmap: AudioFile={beatmap.BeatmapInfo.Metadata.AudioFile} BeatmapInfoType={beatmap.BeatmapInfo.GetType().FullName} BeatmapSetInfoType={beatmap.BeatmapInfo.BeatmapSet?.GetType().FullName ?? "(null)"}");
                debugSnapshot = sb.ToString();
            }

            // In gameplay, the MasterGameplayClockContainer uses beatmap.Track as its source.
            // If we don't detach it, the full original track will keep playing.
            // Switching to a virtual clock source prevents any audio from playing unless we explicitly do so here.
            if (gameplayClockContainer is MasterGameplayClockContainer master)
            {
                master.StopUsingBeatmapClock();
                crashIf(3, $"StopUsingBeatmapClock invoked. IsRunning={gameplayClock?.IsRunning} CurrentTime={gameplayClock?.CurrentTime}");
            }

            // Even after switching the gameplay clock source, the original beatmap track may already be running.
            // Stop it explicitly so only the duplicated audio plays.
            if (gameplayClock != null)
            {
                beatmap.Track?.Stop();
                crashIf(4, $"Original beatmap.Track stopped. TrackIsRunning={beatmap.Track?.IsRunning}");
            }

            // 不直接开播：等待本 Drawable 完成依赖注入，并在 gameplay 时钟 running 时再开始。
            //（选歌界面无 gameplayClock，则下一帧启动即可）
            started = false;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (started || !startRequested || pendingBeatmap == null)
                return;

            // 当有 gameplay 时钟且第一次进入 running 状态时再启动切片播放，避免准备时间被抢占。
            if (gameplayClock != null && !gameplayClock.IsRunning)
                return;

            started = true;
            crashIf(6, $"Starting preview now. gameplayClockTime={gameplayClock?.CurrentTime} PreviewStart={OverridePreviewStartTime} Duration={OverridePreviewDuration}");
            base.StartPreview(pendingBeatmap, false);
        }

        protected override Track? CreateTrack(IWorkingBeatmap beatmap, out bool ownsTrack)
        {
            ownsTrack = false;

            // 由 EzPreviewTrackManager 负责外部时钟驱动的切片/循环/间隔（Seek/Stop/Start）。
            // 在 gameplay 场景下必须使用独立 Track 实例：
            // - 原 beatmap.Track 可能仍在播放整首歌，需要 Stop()
            // - 同时也避免对原 Track 的音量/调整影响到切片播放。
            if (gameplayClock != null)
            {
                string audioFile = beatmap.BeatmapInfo.Metadata.AudioFile;
                IBeatmapSetInfo? beatmapSetInfo = beatmap.BeatmapInfo.BeatmapSet;
                var beatmapSet = beatmapSetInfo as BeatmapSetInfo;

                string beatmapInfoType = beatmap.BeatmapInfo.GetType().FullName ?? "(null)";
                string beatmapSetInfoType = beatmapSetInfo?.GetType().FullName ?? "(null)";

                crashIf(5,
                    $"CreateTrack entered. BeatmapInfoType={beatmapInfoType} AudioFile={audioFile} BeatmapSetInfoType={beatmapSetInfoType} BeatmapSetCast={(beatmapSet != null ? "ok" : "null")}");

                if (!string.IsNullOrEmpty(audioFile) && beatmapSet != null)
                {
                    string? rawFileStorePath = beatmapSet.GetPathForFile(audioFile);
                    string? standardisedFileStorePath = rawFileStorePath;

                    // Some storage APIs may return Windows-style separators.
                    if (!string.IsNullOrEmpty(standardisedFileStorePath))
                        standardisedFileStorePath = standardisedFileStorePath.ToStandardisedPath();

                    crashIf(52, $"Resolved fileStorePath={standardisedFileStorePath ?? "(null)"} from AudioFile={audioFile}");

                    if (!string.IsNullOrEmpty(rawFileStorePath) || !string.IsNullOrEmpty(standardisedFileStorePath))
                    {
                        // For robustness, try both raw and standardised paths.
                        // Prefer the one that yields a valid decoded track (Length > 0 for typical beatmap audio).
                        static bool isProbablyValidTrack(Track? t) => t != null && t.Length > 0;

                        static void ensureTrackLengthPopulated(Track track)
                        {
                            if (!track.IsLoaded || track.Length == 0)
                            {
                                // Force length to be populated (see WorkingBeatmap.PrepareTrackForPreview()).
                                track.Seek(track.CurrentTime);
                            }
                        }

                        bool hasBeatmapTrackStore = beatmapManager?.BeatmapTrackStore != null;

                        Track? beatmapStoreRaw = hasBeatmapTrackStore && !string.IsNullOrEmpty(rawFileStorePath)
                            ? beatmapManager!.BeatmapTrackStore.Get(rawFileStorePath)
                            : null;

                        Track? beatmapStoreStandardised = hasBeatmapTrackStore && !string.IsNullOrEmpty(standardisedFileStorePath)
                            ? beatmapManager!.BeatmapTrackStore.Get(standardisedFileStorePath)
                            : null;

                        Track? globalStoreRaw = !string.IsNullOrEmpty(rawFileStorePath)
                            ? AudioManager.Tracks.Get(rawFileStorePath)
                            : null;

                        Track? globalStoreStandardised = !string.IsNullOrEmpty(standardisedFileStorePath)
                            ? AudioManager.Tracks.Get(standardisedFileStorePath)
                            : null;

                        string? chosenPath;
                        Track? newTrack;

                        if (isProbablyValidTrack(beatmapStoreRaw))
                        {
                            newTrack = beatmapStoreRaw;
                            chosenPath = rawFileStorePath;
                        }
                        else if (isProbablyValidTrack(beatmapStoreStandardised))
                        {
                            newTrack = beatmapStoreStandardised;
                            chosenPath = standardisedFileStorePath;
                        }
                        else if (isProbablyValidTrack(globalStoreRaw))
                        {
                            newTrack = globalStoreRaw;
                            chosenPath = rawFileStorePath;
                        }
                        else if (isProbablyValidTrack(globalStoreStandardised))
                        {
                            newTrack = globalStoreStandardised;
                            chosenPath = standardisedFileStorePath;
                        }
                        else
                        {
                            // Nothing looks valid yet (could be lazy-loaded). Fall back to the beatmap store preference order.
                            newTrack = beatmapStoreRaw ?? beatmapStoreStandardised ?? globalStoreRaw ?? globalStoreStandardised;
                            chosenPath = beatmapStoreRaw != null ? rawFileStorePath
                                : beatmapStoreStandardised != null ? standardisedFileStorePath
                                : globalStoreRaw != null ? rawFileStorePath
                                : globalStoreStandardised != null ? standardisedFileStorePath
                                : null;
                        }

                        if (DebugCrashStage == 999)
                        {
                            if (beatmapStoreRaw != null)
                                ensureTrackLengthPopulated(beatmapStoreRaw);
                            if (beatmapStoreStandardised != null)
                                ensureTrackLengthPopulated(beatmapStoreStandardised);
                            if (globalStoreRaw != null)
                                ensureTrackLengthPopulated(globalStoreRaw);
                            if (globalStoreStandardised != null)
                                ensureTrackLengthPopulated(globalStoreStandardised);

                            var sb = new StringBuilder();
                            sb.AppendLine(debugSnapshot ?? "[DuplicateVirtualTrack DebugDump]\nStartPreview: (no snapshot)\n");
                            sb.AppendLine("CreateTrack:");
                            sb.AppendLine($"- gameplayClockIsRunning={gameplayClock.IsRunning} gameplayClockTime={gameplayClock.CurrentTime}");
                            sb.AppendLine($"- audioFile={audioFile}");
                            sb.AppendLine("- beatmapSetCast=ok");
                            sb.AppendLine($"- rawFileStorePath={rawFileStorePath ?? "(null)"}");
                            sb.AppendLine($"- standardisedFileStorePath={standardisedFileStorePath ?? "(null)"}");
                            sb.AppendLine($"- BeatmapTrackStoreInjected={(beatmapManager != null ? "yes" : "no")} BeatmapTrackStoreAvailable={(hasBeatmapTrackStore ? "yes" : "no")}");
                            sb.AppendLine($"- beatmapStoreRaw={(beatmapStoreRaw != null ? $"ok (Type={beatmapStoreRaw.GetType().Name}, IsLoaded={beatmapStoreRaw.IsLoaded}, Name={beatmapStoreRaw.Name}, Length={beatmapStoreRaw.Length})" : "null")}");
                            sb.AppendLine($"- beatmapStoreStandardised={(beatmapStoreStandardised != null ? $"ok (Type={beatmapStoreStandardised.GetType().Name}, IsLoaded={beatmapStoreStandardised.IsLoaded}, Name={beatmapStoreStandardised.Name}, Length={beatmapStoreStandardised.Length})" : "null")}");
                            sb.AppendLine($"- globalStoreRaw={(globalStoreRaw != null ? $"ok (Type={globalStoreRaw.GetType().Name}, IsLoaded={globalStoreRaw.IsLoaded}, Name={globalStoreRaw.Name}, Length={globalStoreRaw.Length})" : "null")}");
                            sb.AppendLine($"- globalStoreStandardised={(globalStoreStandardised != null ? $"ok (Type={globalStoreStandardised.GetType().Name}, IsLoaded={globalStoreStandardised.IsLoaded}, Name={globalStoreStandardised.Name}, Length={globalStoreStandardised.Length})" : "null")}");
                            sb.AppendLine($"- chosen={(newTrack != null ? $"ok (Type={newTrack.GetType().Name}, IsLoaded={newTrack.IsLoaded}, Name={newTrack.Name}, Length={newTrack.Length})" : "null")} chosenPath={chosenPath ?? "(null)"}");
                            sb.AppendLine($"- beatmap.Track={(beatmap.Track != null ? $"ok (Name={beatmap.Track.Name}, Length={beatmap.Track.Length}, IsRunning={beatmap.Track.IsRunning})" : "null")}");
                            sb.AppendLine($"- willOwnTrack={(newTrack != null ? "yes" : "no")}");
                            throw new InvalidOperationException(sb.ToString());
                        }

                        if (newTrack == null)
                            crashIf(53, $"Track store returned null for fileStorePath={standardisedFileStorePath}. BeatmapTrackStore={(beatmapManager?.BeatmapTrackStore != null ? "yes" : "no")}");

                        if (newTrack != null)
                        {
                            ensureTrackLengthPopulated(newTrack);

                            crashIf(55, $"Created independent track from fileStorePath={chosenPath ?? standardisedFileStorePath ?? rawFileStorePath} (Name={newTrack.Name}, Length={newTrack.Length}).");
                            ownsTrack = true;
                            return newTrack;
                        }
                    }
                }

                crashIf(54, $"Falling back to beatmap.Track. AudioFile={audioFile} BeatmapSetCast={(beatmapSet != null ? "ok" : "null")}");
            }

            return beatmap.Track;
        }

        private void crashIf(int stage, string message)
        {
            if (DebugCrashStage == stage)
                throw new InvalidOperationException($"[DuplicateVirtualTrack DebugCrashStage={stage}] {message}");
        }
    }
}
