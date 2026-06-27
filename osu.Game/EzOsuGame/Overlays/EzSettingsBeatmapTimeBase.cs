// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 「谱面时基」相关设置子页。
    /// 当前只包含 <see cref="Ez2Setting.BeatmapClockTimeBase"/>（Audio / Beatmap）。
    ///
    /// 多人 / 旁观 / 撮合上下文下，时基下拉框会变灰禁用，并显示红色 data note 提示「多人模式禁用」。
    /// </summary>
    public partial class EzSettingsBeatmapTimeBase : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.BEATMAP_CLOCK_TIME_BASE;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private IMultiplayerClient? multiplayerClient { get; set; }

        [Resolved]
        private ISpectatorClient? spectatorClient { get; set; }

        [Resolved]
        private IMatchmakingClient? matchmakingClient { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            var dropdown = new FormEnumDropdown<EzBeatmapClockTimeBase>
            {
                Caption = EzSettingsStrings.BEATMAP_CLOCK_TIME_BASE,
                HintText = EzSettingsStrings.BEATMAP_CLOCK_TIME_BASE_TOOLTIP,
                Current = ezConfig.GetBindable<EzBeatmapClockTimeBase>(Ez2Setting.BeatmapClockTimeBase),
            };

            Add(new SettingsItemV2(dropdown)
            {
                Keywords = new[] { "ez", "beatmap", "clock", "time base", "experimental" }
            });

            // multiplayer / spectate / matchmaking 上下文 → 强制锁定时基为 Audio。
            bool isMultiplayer = multiplayerClient != null || spectatorClient != null || matchmakingClient != null;

            if (isMultiplayer)
            {
                // 把当前 Bindable 强制写为 Audio。
                ezConfig.SetValue(Ez2Setting.BeatmapClockTimeBase, EzBeatmapClockTimeBase.Audio);

                // 下拉框直接禁用。
                dropdown.Current.Disabled = true;

                Add(new EzMultiplayerDisableNote());
            }
        }

        /// <summary>
        /// 多人模式下显示的红色「data note」提示。
        /// </summary>
        private partial class EzMultiplayerDisableNote : FillFlowContainer
        {
            public EzMultiplayerDisableNote()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }
        }
    }
}
