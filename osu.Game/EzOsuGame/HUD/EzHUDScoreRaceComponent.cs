// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 角逐 HUD 组件基类。对齐官方 Spectator HUD 模式：
    /// - 数据由 <see cref="EzScoreRaceService"/> 通过 <c>States</c> 字典提供
    /// - <see cref="EzScoreRaceTimelineScoreProcessor"/> 由各 HUD 组件自己创建/销毁，绑定 ghost state
    /// - 基类只提供时钟解析和 <see cref="GameplayState"/> 引用，不持有 Session
    /// </summary>
    public abstract partial class EzHUDScoreRaceComponent : CompositeDrawable
    {
        [Resolved(canBeNull: true)]
        protected GameplayState? GameplayState { get; private set; }

        [Resolved(canBeNull: true)]
        protected ScoreProcessor? ScoreProcessor { get; private set; }

        [Resolved(canBeNull: true)]
        protected GameplayClockContainer? GameplayClock { get; private set; }

        /// <summary>
        /// 用于派生类判断当前规则集是否支持 ghost 角逐。
        /// </summary>
        protected bool SupportsGhostRace => EzScoreRaceRulesetSupport.SupportsGhostRace(GameplayState?.Ruleset.RulesetInfo);

        private protected OsuSpriteText? LoadingText;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            GameplayClock ??= this.FindClosestParent<GameplayClockContainer>();
            OnSessionReady();
        }

        /// <summary>
        /// 在 <see cref="LoadComplete"/> 完成后调用，组件可在此绑定 <see cref="EzScoreRaceService.States"/>。
        /// </summary>
        protected virtual void OnSessionReady()
        {
        }

        // protected override void Dispose(bool isDisposing)
        // {
        //     base.Dispose(isDisposing);
        // }

        protected double GetCurrentClockTime()
        {
            return GameplayClock?.CurrentTime ?? 0;
        }

        protected long GetLiveDisplayScore(ScoringMode mode = ScoringMode.Standardised) => ScoreProcessor?.GetDisplayScore(mode) ?? 0;

        protected void EnsureLoadingOverlay()
        {
            if (LoadingText != null)
                return;

            LoadingText = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = EzHUDStrings.SCORE_RACE_LOADING_LABEL,
                Font = OsuFont.GetFont(size: 14),
            };

            AddInternal(LoadingText);
        }

        protected virtual void OnEntriesChangedScheduled()
        {
        }
    }
}
