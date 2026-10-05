// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
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
    /// <summary>
    /// 角逐 HUD 组件基类：解析 <see cref="GameplayClockContainer"/>，并协助绑定 <see cref="EzScoreRaceService"/>。
    /// 服务未注册 DI（实验开关关闭）时 <see cref="ScoreRaceService"/> 为 null，派生类以本地成绩静态展示 ghost。
    /// </summary>
    public abstract partial class EzHUDScoreRaceComponent : CompositeDrawable
    {
        [Resolved(canBeNull: true)]
        protected GameplayState? GameplayState { get; private set; }

        [Resolved(canBeNull: true)]
        protected ScoreProcessor? ScoreProcessor { get; private set; }

        [Resolved(canBeNull: true)]
        protected EzScoreRaceService? ScoreRaceService { get; private set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        /// <summary>角逐服务是否已通过 DI 注册（开关开启且冷启动后生效）。</summary>
        protected bool IsScoreRaceServiceAvailable => ScoreRaceService != null;

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
                Alpha = 0,
            };

            AddInternal(LoadingText);
        }

        /// <summary>
        /// 角逐服务未注册时，从本地 Realm 查询 ghost 候选（静态终值，不构建 timeline）。
        /// </summary>
        protected List<ScoreInfo> QueryStaticGhostScores(EzScoreModFilter modFilter, int maxEntries)
        {
            if (GameplayState == null || !SupportsGhostRace)
                return new List<ScoreInfo>();

            var beatmapInfo = GameplayState.Beatmap.BeatmapInfo;
            var rulesetInfo = GameplayState.Ruleset.RulesetInfo;

            var allLocalScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);

            // 与 EzScoreRaceService 一致：非 Mania 不跑 Mod 过滤。
            var effectiveFilter = EzScoreRaceRulesetSupport.GetGhostTimelineMode(rulesetInfo) == EzScoreRaceGhostTimelineMode.ManiaSession
                ? modFilter
                : EzScoreModFilter.Any;

            return EzLocalScoreQueries.SelectGhostCandidates(
                allLocalScores,
                GameplayState.Mods.ToArray(),
                effectiveFilter,
                maxEntries);
        }

        protected virtual void OnEntriesChangedScheduled()
        {
        }

        /// <summary>
        /// 绑定全局 <see cref="EzScoreRaceService"/> 的 States / ModFilter / MaxEntries。
        /// </summary>
        protected bool TryBindScoreRaceService(
            ref EzScoreRaceService? service,
            ref IBindableDictionary<string, EzScoreRaceState>? stateLookup,
            Bindable<EzScoreModFilter>? modFilter = null,
            BindableNumber<int>? maxEntries = null)
        {
            service ??= ScoreRaceService;

            if (service == null)
                return false;

            modFilter?.BindTo(service.ModFilter);

            maxEntries?.BindTo(service.MaxEntries);

            if (stateLookup == null)
            {
                stateLookup = service.States;
                stateLookup.BindCollectionChanged(onScoreRaceStatesChanged, true);
            }

            return true;
        }

        private void onScoreRaceStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<string, EzScoreRaceState> e)
            => OnScoreRaceStatesChanged();

        /// <summary>States 字典变化时调用（默认转发到 <see cref="OnEntriesChangedScheduled"/>）。</summary>
        protected virtual void OnScoreRaceStatesChanged() => OnEntriesChangedScheduled();
    }
}
