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

namespace osu.Game.EzOsuGame.Performance
{
    /// <summary>
    /// 极速模式：压制非皮肤相关的每帧开销（背景、故事板、模糊、HUD 特效等）以提高游戏内帧数。
    /// </summary>
    /// <remarks>
    /// 做法是把一组已有的官方与 Ez 设置改写为低开销值，改写前先把原值快照落盘，关闭时按快照还原。
    /// 少数没有对应设置的开销（背景整棵子树的绘制、Kiai 喷泉、列模糊）由各自的调用点查询 <see cref="Active"/> 跳过。
    /// <para>
    /// 压制全程生效，不随进出游玩切换。按局切换会让 <see cref="OsuSetting.GameplayLeaderboard"/> 这类
    /// 被 <c>OnScreenDisplay</c> 追踪的项每张图弹两次提示，而游玩期间 <c>OverlayActivationMode</c> 本来就挡住了
    /// 设置面板，按局切换换不到任何收益。
    /// </para>
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

        private readonly OsuConfigManager osuConfig;
        private readonly Ez2ConfigManager ezConfig;

        private readonly Bindable<bool> enabled;

        /// <summary>
        /// 生效期间持有已应用的覆盖项，让它们持有的 bindable 副本不被回收——
        /// <see cref="Bindable{T}"/> 的绑定表是弱引用的。
        /// </summary>
        private IReadOnlyList<SettingOverride>? appliedOverrides;

        public EzTurboMode(OsuConfigManager osuConfig, Ez2ConfigManager ezConfig)
        {
            this.osuConfig = osuConfig;
            this.ezConfig = ezConfig;

            // 先处理上次异常退出遗留的快照，再开始跟踪开关，避免把被改写过的值当成用户原值。
            restoreFromSnapshot(recovery: true);

            enabled = ezConfig.GetBindable<bool>(Ez2Setting.TurboMode);
            enabled.BindValueChanged(_ => updateState(), true);
        }

        private void updateState()
        {
            if (enabled.Value == active)
                return;

            if (active)
                restore();
            else
                apply();
        }

        private void apply()
        {
            var snapshot = new Dictionary<string, string>();
            var applicable = new List<SettingOverride>();

            foreach (var setting in buildOverrides())
            {
                // 已被别处锁住（例如某个 BeginLease）的项写不进去，整项跳过而不是让它抛。
                if (!setting.TryCaptureCurrent(out string captured))
                {
                    Logger.Log($"[Ez] 极速模式跳过 {setting.Key}：该设置当前被其它逻辑锁定。", level: LogLevel.Debug);
                    continue;
                }

                snapshot[setting.Key] = captured;
                applicable.Add(setting);
            }

            // 原值必须先落盘再改写：否则进程在改写后崩溃就再也找不回用户设置。
            ezConfig.SetValue(Ez2Setting.TurboModeSnapshot, JsonConvert.SerializeObject(snapshot));
            ezConfig.Save();

            // 先置位再改写，让 DimLevel 等改写触发的 UpdateVisuals 能看到已生效的状态。
            active = true;
            appliedOverrides = applicable;

            foreach (var setting in applicable)
                setting.ApplyTurboValue();
        }

        private void restore()
        {
            // 先复位再写回，理由同 apply()。
            active = false;
            appliedOverrides = null;

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

                foreach (var setting in buildOverrides())
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
        /// 压制清单。<c>lockWhileActive</c> 为 true 的项在生效期间会被 <see cref="Bindable{T}.Disabled"/> 置灰，
        /// 使设置面板显示为不可改；只有确认除设置面板外没有运行时写入方的项才能置灰，
        /// 否则那些写入会撞上 <see cref="Bindable{T}.Value"/> 的 disabled 检查而抛异常。
        /// </summary>
        private IReadOnlyList<SettingOverride> buildOverrides() => new List<SettingOverride>
        {
            // 直接跳过 GameplayDrawableStoryboard 与视频层的创建，不只是隐藏。
            osu(OsuSetting.ShowStoryboard, false),
            // 关掉背景的模糊 pass；配合 DimLevel = 1 让 UserDimContainer 整棵子树不参与绘制。
            osu(OsuSetting.BlurLevel, 0.0),
            osu(OsuSetting.DimLevel, 1.0),
            osu(OsuSetting.LightenDuringBreaks, false),
            osu(OsuSetting.HitLighting, false),
            osu(OsuSetting.StarFountains, false),
            osu(OsuSetting.KeyOverlay, false),
            osu(OsuSetting.FloatingComments, false),
            osu(OsuSetting.SeasonalBackgroundMode, SeasonalBackgroundMode.Never),
            osu(OsuSetting.SongSelectBackgroundBlur, false),
            osu(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin),

            // HUDOverlay 的 ToggleInGameLeaderboard 快捷键直接写这个 bindable，置灰会让游玩中按下时抛异常。
            osu(OsuSetting.GameplayLeaderboard, false, lockWhileActive: false),
            // 0 同时让 ParallaxContainer 不再产生位移。OsuGame 的一次性配置迁移会写它，同样不能置灰。
            osu(OsuSetting.MenuParallaxScale, 0f, lockWhileActive: false),

            // 每帧一次全屏毛玻璃 pass。
            ez(Ez2Setting.AcrylicUiEnabled, false),
            // 服务实例仍在，但会 no-op，不再做元数据查询与 timeline 构建。
            ez(Ez2Setting.EzScoreRaceServiceEnabled, false),
        };

        private SettingOverride osu<TValue>(OsuSetting lookup, TValue turboValue, bool lockWhileActive = true) =>
            new SettingOverride<OsuSetting, TValue>(osuConfig, lookup, turboValue, lockWhileActive);

        private SettingOverride ez<TValue>(Ez2Setting lookup, TValue turboValue, bool lockWhileActive = true) =>
            new SettingOverride<Ez2Setting, TValue>(ezConfig, lookup, turboValue, lockWhileActive);

        public void Dispose()
        {
            if (active)
                restore();
        }

        private abstract class SettingOverride
        {
            public abstract string Key { get; }

            /// <returns>是否取到原值；该设置已被别处锁定时为 false。</returns>
            public abstract bool TryCaptureCurrent(out string value);

            public abstract void ApplyTurboValue();

            /// <returns>是否成功写回。</returns>
            public abstract bool RestoreFrom(string serialised);
        }

        private sealed class SettingOverride<TLookup, TValue> : SettingOverride
            where TLookup : struct, Enum
        {
            private readonly Bindable<TValue> bindable;
            private readonly TLookup lookup;
            private readonly TValue turboValue;
            private readonly bool lockWhileActive;

            public SettingOverride(ConfigManager<TLookup> config, TLookup lookup, TValue turboValue, bool lockWhileActive)
            {
                bindable = config.GetBindable<TValue>(lookup);
                this.lookup = lookup;
                this.turboValue = turboValue;
                this.lockWhileActive = lockWhileActive;
            }

            public override string Key => $"{typeof(TLookup).Name}.{lookup}";

            public override bool TryCaptureCurrent(out string value)
            {
                if (bindable.Disabled)
                {
                    value = string.Empty;
                    return false;
                }

                value = serialise(bindable.Value);
                return true;
            }

            public override void ApplyTurboValue()
            {
                // 顺序不能颠倒：置灰后 Bindable.Value 的 setter 会抛异常。
                bindable.Value = turboValue;

                if (lockWhileActive)
                    bindable.Disabled = true;
            }

            public override bool RestoreFrom(string serialised)
            {
                // 同样不能颠倒：先解锁才写得回去。异常退出后重开时本来就没锁，这里是空操作。
                bindable.Disabled = false;

                if (!tryDeserialise(serialised, out TValue value))
                {
                    Logger.Log($"[Ez] 极速模式无法还原 {Key}（快照值 \"{serialised}\"）。", level: LogLevel.Important);
                    return false;
                }

                bindable.Value = value;
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
