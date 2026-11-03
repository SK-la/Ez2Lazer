// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Progress notification shared by every entry point that runs <see cref="EzLocalProfileService.ComputeAsync"/>
    /// (the settings dialog and the startup reconcile), so a long analysis reports itself identically wherever it
    /// was started from — the user should never have to guess whether data is being recomputed.
    /// </summary>
    public static class EzLocalProfileComputeNotification
    {
        public static ProgressNotification Create(LocalisableString? text = null) => new ProgressNotification
        {
            Text = text ?? EzSettingsProfile.LOCAL_PROFILE_COMPUTE_STARTED,
            CompletionText = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_DONE,
            State = ProgressNotificationState.Active,
        };

        /// <summary>
        /// Forwards compute progress to a <see cref="ProgressNotification"/> from any thread. Deliberately not
        /// <see cref="Progress{T}"/>: that marshals every tick through the sync context and floods it. The
        /// notification updates its own drawables from whichever thread reports, so it also keeps working while the
        /// settings panel is closed.
        /// </summary>
        public sealed class Forwarder : IProgress<EzLocalProfileComputeProgress>
        {
            /// <summary>
            /// Minimum gap between UI updates. A bulk pass reports hundreds of times; each accepted report sets text
            /// and geometry on the notification, so dropping everything inside this window keeps the update thread
            /// free without the user noticing (they only read whole percentages anyway).
            /// </summary>
            private const int min_report_interval_ms = 100;

            private readonly ProgressNotification notification;
            private readonly System.Diagnostics.Stopwatch sinceLastReport = System.Diagnostics.Stopwatch.StartNew();

            private EzLocalProfileComputePhase lastPhase = (EzLocalProfileComputePhase)(-1);
            private int lastBasisPoints = -1;

            public Forwarder(ProgressNotification notification)
            {
                this.notification = notification;
            }

            public void Report(EzLocalProfileComputeProgress value)
            {
                if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                    return;

                int total = Math.Max(1, value.Total);
                int processed = Math.Clamp(value.Processed, 0, total);

                int basisPoints = (int)(10000L * processed / total);
                bool phaseChanged = value.Phase != lastPhase;
                bool finished = processed >= total;

                if (!phaseChanged && !finished
                    && !(basisPoints / 100 != lastBasisPoints / 100 && sinceLastReport.ElapsedMilliseconds >= min_report_interval_ms))
                    return;

                lastPhase = value.Phase;
                lastBasisPoints = basisPoints;
                sinceLastReport.Restart();

                switch (value.Phase)
                {
                    case EzLocalProfileComputePhase.Saving:
                        notification.Text = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_SAVING;
                        notification.Progress = 0.99f;
                        return;

                    case EzLocalProfileComputePhase.Skills:
                        notification.Text = LocalisableString.Format(
                            EzSettingsProfile.LOCAL_PROFILE_COMPUTE_SKILLS.ToString(),
                            processed,
                            total);
                        // Keep under 100% until the completion step.
                        notification.Progress = Math.Min(0.99f, 0.85f + 0.14f * processed / total);
                        return;

                    default:
                        notification.Text = LocalisableString.Format(
                            EzSettingsProfile.LOCAL_PROFILE_COMPUTE_PROGRESS.ToString(),
                            processed,
                            total);
                        // Cap the analysing phase so Saving / Skills still have visual room.
                        notification.Progress = Math.Min(0.85f, 0.85f * processed / total);
                        return;
                }
            }
        }

        /// <summary>
        /// Complete (or fail) the notification once a compute task ends, and reload the archive so the UI picks the
        /// new numbers up. Safe to call from a continuation.
        /// </summary>
        public static void Finish(
            Task computeTask,
            EzLocalProfileService localProfileService,
            ProgressNotification notification,
            INotificationOverlay? notifications)
        {
            if (notification.State == ProgressNotificationState.Cancelled)
                return;

            if (computeTask.IsFaulted)
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_FAILED });
                return;
            }

            if (computeTask.IsCanceled)
            {
                notification.State = ProgressNotificationState.Cancelled;
                return;
            }

            localProfileService.ReloadFromDisk();
            notification.Progress = 1f;
            notification.CompletionText = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_DONE;
            notification.State = ProgressNotificationState.Completed;
        }
    }
}
