// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
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
    /// - 基类负责解析 <see cref="Game.Screens.Play.GameplayClockContainer"/> 引用（用于 Processor 的 ReferenceClock）
    /// </summary>
    public abstract partial class EzHUDScoreRaceComponent : CompositeDrawable
    {
        [Resolved(canBeNull: true)]
        protected GameplayState? GameplayState { get; private set; }

        [Resolved(canBeNull: true)]
        protected ScoreProcessor? ScoreProcessor { get; private set; }

        /// <summary>
        /// 已就位的 <see cref="Game.Screens.Play.GameplayClockContainer"/>，可作为 processor 的 ReferenceClock。
        /// 退出 / 重新进入 Player 时该引用会失效，调用方需重新解析。
        /// </summary>
        protected IClock? GameplayClock => GameplayClockContainer;

        public GameplayClockContainer? GameplayClockContainer;

        /// <summary>
        /// 用于派生类判断当前规则集是否支持 ghost 角逐。
        /// </summary>
        protected bool SupportsGhostRace => EzScoreRaceRulesetSupport.SupportsGhostRace(GameplayState?.Ruleset.RulesetInfo);

        private protected OsuSpriteText? LoadingText;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // 延迟查找父时钟：HUD 可能先于 GameplayClockContainer 完成加载。
            Schedule(() =>
            {
                if (GameplayClockContainer == null)
                {
                    GameplayClockContainer = this.FindClosestParent<GameplayClockContainer>();
                    if (GameplayClockContainer != null)
                        OnGameplayClockResolved(GameplayClockContainer);
                }
            });

            OnSessionReady();
        }

        /// <summary>
        /// 当 <see cref="Game.Screens.Play.GameplayClockContainer"/> 已就位时调用，派生类可在此将 clock 注入到自己的 processor。
        /// </summary>
        protected virtual void OnGameplayClockResolved(GameplayClockContainer clock)
        {
        }

        /// <summary>
        /// 在 <see cref="LoadComplete"/> 完成后调用，组件可在此绑定 <see cref="EzScoreRaceService.States"/>。
        /// </summary>
        protected virtual void OnSessionReady()
        {
        }

        protected double GetCurrentClockTime()
        {
            return GameplayClockContainer?.CurrentTime ?? 0;
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
