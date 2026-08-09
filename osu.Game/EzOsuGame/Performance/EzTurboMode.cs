// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Performance
{
    /// <summary>
    /// 极速模式：压制非皮肤相关的每帧开销（背景、故事板、模糊、HUD 特效、后台分析等）以提高游戏内帧数。
    /// </summary>
    /// <remarks>
    /// 做法是把一组已有的官方与 Ez 设置改写为低开销值，改写前先把原值快照落盘，关闭时按快照还原。
    /// 少数没有对应设置的开销（背景整棵子树的绘制、Kiai 喷泉、视差）由各自的调用点查询 <see cref="Active"/> 跳过。
    /// <para>
    /// 刻意不做的事：不降低任何线程频率（update / draw / audio），也不把 <c>FrameSync</c> 改成 VSync。
    /// 提高帧数的目的是降低延迟，而降频与锁帧都与该目的相反；音频线程更是判定时间的锚点
    /// （<c>TrackBass.CurrentTime</c> 只在音频线程每帧刷新），降它会直接影响判定与键音时机。
    /// </para>
    /// </remarks>
    public class EzTurboMode : IDisposable
    {
        private static volatile bool active;

        /// <summary>
        /// 压制当前是否生效。供每帧热路径以零成本查询，因此是静态的。
        /// </summary>
        public static bool Active => active;

        /// <summary>
        /// 压制是否会在接下来的一局游玩中生效。
        /// </summary>
        /// <remarks>
        /// 「仅游玩中生效」模式下 <see cref="Active"/> 要等 <c>UserPlayingState</c> 转入游玩才置位，
        /// 而那晚于 <c>Player</c> 建树。因此建树期决定要不要构造某个组件时必须直接看总开关。
        /// </remarks>
        public static bool ActiveForGameplay => active || GlobalConfigStore.EzConfig.Get<bool>(Ez2Setting.TurboMode);

        private readonly OsuConfigManager osuConfig;
        private readonly Ez2ConfigManager ezConfig;

        private readonly Bindable<bool> enabled;
        private readonly Bindable<bool> gameplayOnly;
        private readonly IBindable<LocalUserPlayingState> playingState;

        /// <summary>
        /// 当前生效的压制是否包含「仅全局模式」那一组；用于在子开关变化时判断需不需要重建压制。
        /// </summary>
        private bool appliedGlobalOverrides;

        public EzTurboMode(OsuConfigManager osuConfig, Ez2ConfigManager ezConfig, IBindable<LocalUserPlayingState> playingState)
        {
            this.osuConfig = osuConfig;
            this.ezConfig = ezConfig;

            // 先处理上次异常退出遗留的快照，再开始跟踪开关，避免把被改写过的值当成用户原值。
            restoreFromSnapshot(recovery: true);

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode);
            gameplayOnly = ezConfig.GetBindable<bool>(Ez2Setting.TurboModeGameplayOnly);
            this.playingState = playingState.GetBoundCopy();

            enabled.BindValueChanged(_ => updateState());
            gameplayOnly.BindValueChanged(_ => updateState());
            this.playingState.BindValueChanged(_ => updateState(), true);
        }

        private void updateState()
        {
            bool globalMode = enabled.Value && !gameplayOnly.Value;
            bool shouldBeActive = enabled.Value && (globalMode || playingState.Value != LocalUserPlayingState.NotPlaying);

            if (shouldBeActive == active && globalMode == appliedGlobalOverrides)
                return;

            if (active)
                restore();

            if (shouldBeActive)
                apply(globalMode);
        }

        private void apply(bool includeGlobalOverrides)
        {
            var overrides = buildOverrides(includeGlobalOverrides);

            var snapshot = new Dictionary<string, string>();

            foreach (var setting in overrides)
                snapshot[setting.Key] = setting.CaptureCurrent();

            // 原值必须先落盘再改写：否则进程在改写后崩溃就再也找不回用户设置。
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot, JsonConvert.SerializeObject(snapshot));
            ezConfig.Save();

            // 先置位再改写，让 DimLevel 等改写触发的 UpdateVisuals 能看到已生效的状态。
            active = true;
            appliedGlobalOverrides = includeGlobalOverrides;

            foreach (var setting in overrides)
                setting.ApplyTurboValue();
        }

        private void restore()
        {
            // 先复位再写回，理由同 apply()。
            active = false;
            appliedGlobalOverrides = false;

            restoreFromSnapshot(recovery: false);
        }

        /// <summary>
        /// 按快照里记录的键逐项写回。按键还原（而不是按当前压制清单还原）使得
        /// 上次异常退出、且期间压制清单已改动的情况也能正确恢复。
        /// </summary>
        private void restoreFromSnapshot(bool recovery)
        {
            string raw = ezConfig.Get<string>(Ez2Setting.TurboModeSnapshot);

            if (string.IsNullOrEmpty(raw))
                return;

            Dictionary<string, string>? snapshot = null;

            try
            {
                snapshot = JsonConvert.DeserializeObject<Dictionary<string, string>>(raw);
            }
            catch (Exception e)
            {
                Logger.Log($"[Ez] 极速模式快照无法解析，已丢弃：{e.Message}", level: LogLevel.Important);
            }

            if (snapshot?.Count > 0)
            {
                var byKey = new Dictionary<string, SettingOverride>();

                foreach (var setting in buildOverrides(includeGlobalOverrides: true))
                    byKey[setting.Key] = setting;

                int restored = 0;

                foreach ((string key, string value) in snapshot)
                {
                    if (byKey.TryGetValue(key, out var setting) && setting.RestoreFrom(value))
                        restored++;
                }

                if (recovery)
                    Logger.Log($"[Ez] 极速模式上次未正常退出，已还原 {restored}/{snapshot.Count} 项设置。");
            }

            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot, string.Empty);
            ezConfig.Save();
        }

        /// <summary>
        /// 压制清单。<paramref name="includeGlobalOverrides"/> 为 false 时只保留游玩中真正会生效的项，
        /// 避免为了一局游戏去动选歌与主菜单的外观。
        /// </summary>
        private IReadOnlyList<SettingOverride> buildOverrides(bool includeGlobalOverrides)
        {
            var overrides = new List<SettingOverride>
            {
                // 直接跳过 GameplayDrawableStoryboard 与视频层的创建，不只是隐藏。
                osu(OsuSetting.ShowStoryboard, false),
                // 关掉背景的模糊 pass；配合 DimLevel = 1 让 UserDimContainer 整棵子树不参与绘制。
                osu(OsuSetting.BlurLevel, 0.0),
                osu(OsuSetting.DimLevel, 1.0),
                osu(OsuSetting.LightenDuringBreaks, false),
                osu(OsuSetting.HitLighting, false),
                osu(OsuSetting.StarFountains, false),
                osu(OsuSetting.GameplayLeaderboard, false),
                osu(OsuSetting.KeyOverlay, false),
                osu(OsuSetting.FloatingComments, false),
                // 0 同时让 ParallaxContainer 不再产生位移。
                osu(OsuSetting.MenuParallaxScale, 0f),

                // 每帧一次全屏毛玻璃 pass。
                ez(Ez2Setting.AcrylicUiEnabled, false),
                ez(Ez2Setting.ColumnBlur, 0.0),
                // 服务实例仍在，但会 no-op，不再做元数据查询与 timeline 构建。
                ez(Ez2Setting.EzScoreRaceServiceEnabled, false),
            };

            if (includeGlobalOverrides)
            {
                overrides.AddRange(new[]
                {
                    osu(OsuSetting.SeasonalBackgroundMode, SeasonalBackgroundMode.Never),
                    osu(OsuSetting.SongSelectBackgroundBlur, false),
                    osu(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin),

                    // 选歌时的即时计算与 SQLite 读写；游玩中本来就不跑，只有全局模式才有意义。
                    ez(Ez2Setting.EzAnalysisRecEnabled, false),
                    ez(Ez2Setting.EzAnalysisSqliteEnabled, false),
                });
            }

            return overrides;
        }

        private SettingOverride osu<TValue>(OsuSetting lookup, TValue turboValue) =>
            new SettingOverride<OsuSetting, TValue>(osuConfig, lookup, turboValue);

        private SettingOverride ez<TValue>(Ez2Setting lookup, TValue turboValue) =>
            new SettingOverride<Ez2Setting, TValue>(ezConfig, lookup, turboValue);

        public void Dispose()
        {
            if (active)
                restore();
        }

        private abstract class SettingOverride
        {
            public abstract string Key { get; }

            public abstract string CaptureCurrent();

            public abstract void ApplyTurboValue();

            /// <returns>是否成功写回。</returns>
            public abstract bool RestoreFrom(string serialised);
        }

        private sealed class SettingOverride<TLookup, TValue> : SettingOverride
            where TLookup : struct, Enum
        {
            private readonly ConfigManager<TLookup> config;
            private readonly TLookup lookup;
            private readonly TValue turboValue;

            public SettingOverride(ConfigManager<TLookup> config, TLookup lookup, TValue turboValue)
            {
                this.config = config;
                this.lookup = lookup;
                this.turboValue = turboValue;
            }

            public override string Key => $"{typeof(TLookup).Name}.{lookup}";

            public override string CaptureCurrent() => serialise(config.Get<TValue>(lookup));

            public override void ApplyTurboValue() => config.SetValue(lookup, turboValue);

            public override bool RestoreFrom(string serialised)
            {
                if (!tryDeserialise(serialised, out TValue value))
                {
                    Logger.Log($"[Ez] 极速模式无法还原 {Key}（快照值 \"{serialised}\"）。", level: LogLevel.Important);
                    return false;
                }

                config.SetValue(lookup, value);
                return true;
            }

            private static string serialise(TValue value) =>
                value is Enum ? value.ToString()! : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            private static bool tryDeserialise(string serialised, out TValue value)
            {
                try
                {
                    value = typeof(TValue).IsEnum
                        ? (TValue)Enum.Parse(typeof(TValue), serialised)
                        : (TValue)Convert.ChangeType(serialised, typeof(TValue), CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception)
                {
                    value = default!;
                    return false;
                }
            }
        }
    }
}
