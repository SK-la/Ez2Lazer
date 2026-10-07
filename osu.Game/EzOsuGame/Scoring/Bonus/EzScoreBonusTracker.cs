// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring.Bonus
{
    public enum EzScoreBonusState
    {
        Pending,
        Available,
        Unavailable,
    }

    /// <summary>
    /// 为单个成绩取得附加分（成绩自带 / 进程缓存 / 按需回放补算），并按当前倾向设置与谱面星级加权。
    /// </summary>
    public partial class EzScoreBonusTracker : Component
    {
        public readonly Bindable<EzScoreBonusState> State = new Bindable<EzScoreBonusState>();

        /// <summary>
        /// 已加权的附加分；<see cref="State"/> 非 Available 时为 null。
        /// </summary>
        public readonly Bindable<EzScoreBonusResult?> Weighted = new Bindable<EzScoreBonusResult?>();

        private readonly ScoreInfo score;
        private readonly Bindable<EzScoreBonusTendency> tendency = new Bindable<EzScoreBonusTendency>();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private EzScoreBonusResult? raw;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IEzReplaySession? replaySession { get; set; }

        [Resolved]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        public EzScoreBonusTracker(ScoreInfo score)
        {
            this.score = score;
        }

        public static bool AppliesTo(ScoreInfo score) => score.Ruleset.OnlineID == EzScoreBonusCalculator.MANIA_RULESET_ID;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager? ezConfig)
        {
            ezConfig?.BindWith(Ez2Setting.ScoreBonusTendency, tendency);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tendency.BindValueChanged(_ => updateWeighted());

            if (!AppliesTo(score))
            {
                setRaw(null);
                return;
            }

            if (EzScoreBonusProvider.TryGet(score, out var cached))
            {
                setRaw(cached);
                return;
            }

            if (replaySession == null)
            {
                setRaw(null);
                return;
            }

            State.Value = EzScoreBonusState.Pending;
            request();
        }

        private void request()
        {
            EzScoreBonusProvider.GetAsync(score, scoreManager, beatmapManager, replaySession!, isPlaying, cancellation.Token)
                                .ContinueWith(t => Schedule(() =>
                                {
                                    if (cancellation.IsCancellationRequested)
                                        return;

                                    // 共享任务被别的显示方取消时重新排队。
                                    if (t.IsCanceled)
                                    {
                                        request();
                                        return;
                                    }

                                    setRaw(t.IsCompletedSuccessfully ? t.GetAwaiter().GetResult() : null);
                                }), TaskScheduler.Default);
        }

        private bool isPlaying() => localUserPlayInfo != null && localUserPlayInfo.PlayingState.Value != LocalUserPlayingState.NotPlaying;

        private void setRaw(EzScoreBonusResult? value)
        {
            raw = value;
            State.Value = value == null ? EzScoreBonusState.Unavailable : EzScoreBonusState.Available;
            updateWeighted();
        }

        private void updateWeighted()
        {
            Weighted.Value = raw == null ? null : EzScoreBonusWeighting.Resolve(raw.Value, score.BeatmapInfo?.StarRating ?? 0, tendency.Value);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            base.Dispose(isDisposing);
        }

        public static long TotalWithBonus(ScoreInfo score, EzScoreBonusResult bonus) => Math.Max(0, score.TotalScore + bonus.Total);

        public static string FormatSigned(int value)
        {
            if (value > 0)
                return $"+{value:N0}";

            if (value < 0)
                return $"\u2212{Math.Abs(value):N0}";

            return "0";
        }
    }
}
