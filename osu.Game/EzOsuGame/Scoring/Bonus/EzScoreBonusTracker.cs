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
    /// 为单个成绩取得附加分（成绩自带 / 进程缓存 / 按需回放补算），并按当前倾向设置取值。
    /// </summary>
    public partial class EzScoreBonusTracker : Component
    {
        public readonly Bindable<EzScoreBonusState> State = new Bindable<EzScoreBonusState>();

        /// <summary>
        /// 当前倾向下的附加分；<see cref="State"/> 非 Available 时为 null。
        /// </summary>
        public readonly Bindable<EzScoreBonusResult?> Current = new Bindable<EzScoreBonusResult?>();

        private readonly ScoreInfo score;
        private readonly Bindable<EzScoreBonusTendency> tendency = new Bindable<EzScoreBonusTendency>(EzScoreBonusTendency.MissToJudge);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private EzScoreBonusSet? bonusSet;

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

        public static bool AppliesTo(ScoreInfo score) => true;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager? ezConfig)
        {
            ezConfig?.BindWith(Ez2Setting.ScoreBonusTendency, tendency);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            tendency.BindValueChanged(_ => updateCurrent());

            if (!AppliesTo(score))
            {
                setBonus(null);
                return;
            }

            if (EzScoreBonusProvider.TryGet(score, out var cached))
            {
                setBonus(cached);
                return;
            }

            if (replaySession == null)
            {
                setBonus(null);
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

                                    setBonus(t.IsCompletedSuccessfully ? t.GetAwaiter().GetResult() : null);
                                }), TaskScheduler.Default);
        }

        private bool isPlaying() => localUserPlayInfo != null && localUserPlayInfo.PlayingState.Value != LocalUserPlayingState.NotPlaying;

        private void setBonus(EzScoreBonusSet? value)
        {
            bonusSet = value;
            State.Value = value == null ? EzScoreBonusState.Unavailable : EzScoreBonusState.Available;
            updateCurrent();
        }

        private void updateCurrent()
        {
            Current.Value = bonusSet?.For(tendency.Value);
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
