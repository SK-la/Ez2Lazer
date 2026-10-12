// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Screens.Footer;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 结算界面底部 HitMode / HealthMode / HitResult Offset 控件组。
    /// 持有配置绑定，按需生成对应的 <see cref="ScreenFooterButton"/>。
    /// </summary>
    public class ResultsEnvironmentControls
    {
        public IBindable<EzEnumHitMode> HitModeBindable => hitMode;

        public IBindable<EzEnumHealthMode> HealthModeBindable => healthMode;

        private readonly Bindable<EzEnumHitMode> hitMode;
        private readonly Bindable<EzEnumHealthMode> healthMode;
        private readonly Bindable<double> offset;

        public ResultsEnvironmentControls(Ez2ConfigManager ezConfig)
        {
            hitMode = ezConfig.GetBindable<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            healthMode = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            offset = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
        }

        public ScreenFooterButton[] CreateFooterButtons() => new ScreenFooterButton[]
        {
            new EnumModeButton<EzEnumHitMode>(hitMode, FontAwesome.Solid.Crosshairs, Strings.HIT_MODE),
            new EnumModeButton<EzEnumHealthMode>(healthMode, FontAwesome.Solid.Heartbeat, Strings.HEALTH_MODE),
            new FooterButtonHitResultOffset(offset),
        };

        private static class Strings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString(
                "Mania 判定模式", "Mania hit mode");

            internal static readonly EzLocalizationManager.EzLocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString(
                "Mania 血量模式", "Mania health mode");
        }
    }
}
