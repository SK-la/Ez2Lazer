// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.HUD
{
    public abstract partial class EzHUDScoreRaceComponent : CompositeDrawable
    {
        [Resolved]
        protected RealmAccess Realm { get; private set; } = null!;

        [Resolved]
        protected ScoreManager ScoreManager { get; private set; } = null!;

        [Resolved]
        protected BeatmapManager Beatmaps { get; private set; } = null!;

        [Resolved(canBeNull: true)]
        protected GameplayState? GameplayState { get; private set; }

        [Resolved(canBeNull: true)]
        protected ScoreProcessor? ScoreProcessor { get; private set; }

        [Resolved(canBeNull: true)]
        private EzScoreRaceSessionHost? sessionHost { get; set; }

        protected EzScoreRaceSession? Session { get; private set; }
        protected GameplayClockContainer? GameplayClock { get; private set; }

        protected readonly Bindable<EzScoreModFilter> ModFilter = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);
        protected readonly BindableInt MaxEntries = new BindableInt(5) { MinValue = 1, MaxValue = 10 };

        /// <summary>
        /// 为 false 时本组件不覆盖 Session 的 <see cref="EzScoreRaceSession.MaxEntryCount"/>（如 CompareBars）。
        /// </summary>
        protected virtual bool ContributesMaxEntryCount => true;

        private OsuSpriteText? loadingText;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            GameplayClock = this.FindClosestParent<GameplayClockContainer>();

            if (GameplayState == null)
                return;

            bindSessionWhenAvailable();
        }

        private void bindSessionWhenAvailable()
        {
            if (Session != null)
                return;

            Session = sessionHost?.Session;

            if (Session == null)
            {
                Schedule(bindSessionWhenAvailable);
                return;
            }

            ConfigureSession();

            ModFilter.BindValueChanged(_ => reloadSession(), true);
            MaxEntries.BindValueChanged(_ => reloadSession(), true);
            Session.IsReady.BindValueChanged(_ => updateLoadingState(), true);
            Session.EntriesChanged += onEntriesChanged;
            updateLoadingState();
            OnSessionReady();
        }

        /// <summary>
        /// <see cref="Session"/> 可用后调用（可能晚于 <see cref="LoadComplete"/>）。
        /// </summary>
        protected virtual void OnSessionReady()
        {
        }

        protected override void Update()
        {
            base.Update();
            UpdateDisplay();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && Session != null)
                Session.EntriesChanged -= onEntriesChanged;

            base.Dispose(isDisposing);
        }

        protected abstract void UpdateDisplay();

        protected double GetCurrentClockTime()
        {
            GameplayClock ??= this.FindClosestParent<GameplayClockContainer>();
            return GameplayClock?.CurrentTime ?? 0;
        }

        protected long GetLiveDisplayScore(ScoringMode mode = ScoringMode.Standardised)
            => ScoreProcessor?.GetDisplayScore(mode) ?? 0;

        protected void EnsureLoadingOverlay()
        {
            if (loadingText != null)
                return;

            loadingText = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = EzHUDStrings.SCORE_RACE_LOADING_LABEL,
                Font = OsuFont.GetFont(size: 14),
            };

            AddInternal(loadingText);
        }

        protected virtual void ConfigureSession()
        {
            Session?.Configure(ModFilter.Value, getEffectiveMaxEntryCount());
        }

        private void reloadSession()
        {
            if (Session == null)
                return;

            Session.ReloadIfNeeded(ModFilter.Value, getEffectiveMaxEntryCount());
        }

        private int getEffectiveMaxEntryCount()
            => ContributesMaxEntryCount ? MaxEntries.Value : Session?.MaxEntryCount ?? MaxEntries.Value;

        private void updateLoadingState()
        {
            if (loadingText == null)
                return;

            loadingText.Alpha = Session?.IsReady.Value == true ? 0 : 1;
        }

        private void onEntriesChanged() => Schedule(OnEntriesChangedScheduled);

        protected virtual void OnEntriesChangedScheduled()
        {
        }
    }
}
