// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Ez 分析体系的开关、各规则集选用的分析算法与技能阈值。
    /// </summary>
    public partial class EzAnalysisSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EzSettingsStrings.EZ_ANALYSIS_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig, RulesetStore? rulesetStore)
        {
            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled),
                })
                {
                    Keywords = new[] { "analysis", "ez", "song select", "kps", "kpc" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled),
                })
                {
                    Keywords = new[] { "analysis", "sqlite", "cache", "warmup", "persistent" }
                },
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = EzSettingsStrings.SKILL_LN_CHART_MIN_HOLD_OBJECTS,
                    HintText = EzSettingsStrings.SKILL_LN_CHART_MIN_HOLD_OBJECTS_TOOLTIP,
                    Current = ezConfig.GetBindable<int>(Ez2Setting.SkillLnChartMinHoldObjects),
                    KeyboardStep = 1,
                })
                {
                    Keywords = new[] { "ez", "skill", "ln", "hold", "chartdan", "dan", "ratio", "技能", "占比", "门槛" }
                },
            });

            addAnalysisSystemSettings(ezConfig, rulesetStore);
        }

        /// <summary>
        /// One analysis-system dropdown per ruleset. Each ruleset owns its choice; song select sorting, grouping and
        /// filtering read out under it.
        /// </summary>
        private void addAnalysisSystemSettings(Ez2ConfigManager ezConfig, RulesetStore? rulesetStore)
        {
            var selection = ezConfig.GetBindable<string>(Ez2Setting.AnalysisSystemSelection);
            var catalog = EzMetricVariantReadout.Catalog;
            var items = new List<Drawable>();

            foreach (var ruleset in rulesetStore?.AvailableRulesets ?? Enumerable.Empty<RulesetInfo>())
            {
                var variants = catalog.For(ruleset.OnlineID);

                if (variants.Count <= 1)
                    continue;

                var current = new Bindable<EzMetricVariant>(EzMetricVariantReadout.Selected(ruleset.OnlineID) ?? variants[0]);

                current.BindValueChanged(c =>
                {
                    if (c.NewValue != null && !ReferenceEquals(c.NewValue, c.OldValue))
                        EzMetricSelection.SetPrimary(ruleset.OnlineID, c.NewValue.Id);
                });

                // The song select chip writes the same setting; follow it so both stay one value.
                selection.BindValueChanged(_ =>
                {
                    var selected = EzMetricVariantReadout.Selected(ruleset.OnlineID);

                    if (selected != null && !ReferenceEquals(selected, current.Value))
                        current.Value = selected;
                });

                items.Add(new SettingsItemV2(new AnalysisSystemDropdown
                {
                    Caption = LocalisableString.Interpolate($"{ruleset.Name} · {EzSettingsStrings.ANALYSIS_SYSTEM_SELECTION}"),
                    HintText = EzSettingsStrings.ANALYSIS_SYSTEM_SELECTION_TOOLTIP,
                    Items = variants,
                    Current = current,
                })
                {
                    Keywords = new[] { "analysis", "system", "algorithm", "baseline", "算法", "基线", "分析", ruleset.ShortName }
                });
            }

            if (items.Count > 0)
                AddRange(items);
        }

        /// <summary>Shows each analysis system by its display name instead of its stored id.</summary>
        private partial class AnalysisSystemDropdown : FormDropdown<EzMetricVariant>
        {
            protected override LocalisableString GenerateItemText(EzMetricVariant item) => item.Name;
        }
    }
}
