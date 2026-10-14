// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets;
using osu.Game.Screens.Footer;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 结算界面底部环境控件组：按 ruleset 选择判定/血量/偏移绑定，按需生成 <see cref="ScreenFooterButton"/>。
    /// Mania 用 Mania 判定/血量/OffsetPlusMania；Osu 用 StdJudgeMode 判定轨与 OffsetPlusNonMania；其余规则集仅 OffsetPlusNonMania。
    /// </summary>
    public class ResultsEnvironmentControls
    {
        public IBindable<EzEnumJudgeMode> JudgeModeBindable => maniaJudgeMode;

        public IBindable<EzEnumJudgeMode> StdJudgeModeBindable => stdJudgeMode;

        public IBindable<EzEnumHealthMode> HealthModeBindable => maniaHealthMode;

        private readonly RulesetInfo? ruleset;

        private readonly Bindable<EzEnumJudgeMode> maniaJudgeMode;
        private readonly Bindable<EzEnumJudgeMode> stdJudgeMode;
        private readonly Bindable<EzEnumHealthMode> maniaHealthMode;
        private readonly Bindable<double> offsetMania;
        private readonly Bindable<double> offsetNonMania;

        public ResultsEnvironmentControls(Ez2ConfigManager ezConfig, RulesetInfo? ruleset)
        {
            this.ruleset = ruleset;

            maniaJudgeMode = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);
            stdJudgeMode = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.StdJudgeMode);
            maniaHealthMode = ezConfig.GetBindable<EzEnumHealthMode>(Ez2Setting.ManiaHealthMode);
            offsetMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusMania);
            offsetNonMania = ezConfig.GetBindable<double>(Ez2Setting.OffsetPlusNonMania);
        }

        public ScreenFooterButton[] CreateFooterButtons()
        {
            if (isMania)
            {
                return new ScreenFooterButton[]
                {
                    new EnumModeButton<EzEnumJudgeMode>(maniaJudgeMode, FontAwesome.Solid.Crosshairs, Strings.HIT_MODE,
                        items: Enum.GetValues<EzEnumJudgeMode>().Where(m => m != EzEnumJudgeMode.StableClassic)),
                    new EnumModeButton<EzEnumHealthMode>(maniaHealthMode, FontAwesome.Solid.Heartbeat, Strings.HEALTH_MODE),
                    new FooterButtonHitResultOffset(offsetMania),
                };
            }

            if (isOsu)
            {
                return new ScreenFooterButton[]
                {
                    new EnumModeButton<EzEnumJudgeMode>(stdJudgeMode, FontAwesome.Solid.Crosshairs, Strings.STD_HIT_MODE,
                        items: new[] { EzEnumJudgeMode.Lazer, EzEnumJudgeMode.StableClassic }),
                    new FooterButtonHitResultOffset(offsetNonMania),
                };
            }

            return new ScreenFooterButton[]
            {
                new FooterButtonHitResultOffset(offsetNonMania),
            };
        }

        private bool isMania => ruleset?.OnlineID == 3 || ruleset?.ShortName == "mania";

        private bool isOsu => ruleset?.ShortName == "osu";

        private static class Strings
        {
            internal static readonly EzLocalizationManager.EzLocalisableString HIT_MODE = new EzLocalizationManager.EzLocalisableString(
                "Mania 判定模式", "Mania hit mode");

            internal static readonly EzLocalizationManager.EzLocalisableString STD_HIT_MODE = new EzLocalizationManager.EzLocalisableString(
                "Osu 判定模式", "Osu hit mode");

            internal static readonly EzLocalizationManager.EzLocalisableString HEALTH_MODE = new EzLocalizationManager.EzLocalisableString(
                "Mania 血量模式", "Mania health mode");
        }
    }
}
