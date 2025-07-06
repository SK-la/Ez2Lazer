// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings.Sections.Gameplay;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public class EzManiaHitModeConvertor : Mod, IApplicableAfterBeatmapConversion, IApplicableToDrawableRuleset<ManiaHitObject>
    {
        public override string Name => "Mania Hit Mode";
        public override LocalisableString Description => "LaMod: To Use Setting";
        public override string Acronym => "MHM";

        public override ModType Type => ModType.CustomMod;
        public override double ScoreMultiplier => 1.0;
        public override bool Ranked => false;

        // public bool UserPlayable = false;

        [SettingSource("HitMode", "其他音游的判定模式")]
        public Bindable<MUGHitMode> HitMode { get; } = new Bindable<MUGHitMode>(MUGHitMode.EZ2AC);

        public HitWindows HitWindows { get; set; } = new ManiaHitWindows();

        // public Bindable<MUGHitMode> HitMode = new Bindable<MUGHitMode>();
        // private readonly BindableDouble configColumnWidth = new BindableDouble();
        // private readonly BindableDouble configSpecialFactor = new BindableDouble();

        public EzManiaHitModeConvertor()
        {
            // if (GlobalConfigStore.Config != null)
            //     HitMode.Value = GlobalConfigStore.Config.Get<MUGHitMode>(OsuSetting.HitMode);
            HitMode.ValueChanged += OnHitModeChanged;
        }

        // [BackgroundDependencyLoader]
        // private void load(OsuConfigManager osuConfig, ManiaRulesetConfigManager config)
        // {
        //     osuConfig.BindWith(OsuSetting.HitMode, HitMode);
        //     GlobalManiaConfigStore.Config = config;
        // }

        // [BackgroundDependencyLoader]
        // private void load(ManiaRulesetConfigManager config, OsuConfigManager osuConfig)
        // {
        //     // this.config = config;
        //     // hitMode = osuConfig.Get<Bindable<MUGHitMode>>(OsuSetting.HitMode);
        //     // hitMode = ConfigManager.Get<Bindable<MUGHitMode>>(OsuSetting.HitMode);
        //     // hitMode.BindValueChanged(OnHitModeChanged, true);
        //     osuConfig.BindWith(OsuSetting.HitMode, HitMode);
        //
        //     GlobalManiaConfigStore.Config = config;
        //     config.BindWith(ManiaRulesetSetting.SpecialFactor, configSpecialFactor);
        //     config.BindWith(ManiaRulesetSetting.ColumnWidth, configColumnWidth);
        //     configSpecialFactor.BindValueChanged(_ =>
        //     {
        //         GlobalManiaConfigStore.TriggerRefresh();
        //     }, true);
        //
        //     configColumnWidth.BindValueChanged(_ =>
        //     {
        //         GlobalManiaConfigStore.TriggerRefresh();
        //     }, true);
        //     Debug.WriteLine($"🔥Column Width: {configSpecialFactor}");
        //     Debug.WriteLine($"🔥Special Factor: {configColumnWidth}");
        //
        //     // GlobalManiaConfigStore.OnRefresh -= onSkinChange;
        // }

        private void OnHitModeChanged(ValueChangedEvent<MUGHitMode> e)
        {
            if (e.NewValue == MUGHitMode.Lazer)
            {
                // 使用默认判定区间
                HitWindows = new DefaultHitWindows();
                return;
            }

            try
            {
                var template = HitWindowTemplates.GetTemplate(e.NewValue.ToString());

                // 创建DefaultHitWindows实例并设置自定义值
                var customWindows = new DefaultHitWindows();
                customWindows.SetCustomWindows(
                    template.TemplatePerfect,
                    template.TemplateGreat,
                    template.TemplateGood,
                    template.TemplateOk,
                    template.TemplateMeh,
                    template.TemplateMiss
                );

                HitWindows = customWindows;
                Debug.WriteLine($"设置了{e.NewValue}模式的判定区间");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                // 出错时使用默认区间
                HitWindows = new DefaultHitWindows();
            }
        }

        private bool shouldSkipHitMode()
        {
            return HitMode.Value != MUGHitMode.EZ2AC && HitMode.Value != MUGHitMode.Melody;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (shouldSkipHitMode())
                return;

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            maniaBeatmap.HitObjects = maniaBeatmap.HitObjects.Select(obj =>
            {
                if (obj is HoldNote hold)
                {
                    return HitMode.Value switch
                    {
                        MUGHitMode.EZ2AC => new Ez2AcHoldNote(hold),
                        MUGHitMode.Melody => new NoJudgmentHoldNote(hold),
                        _ => hold
                    };
                }

                return obj;
            }).ToList();
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<ManiaHitObject> drawableRuleset)
        {
            if (shouldSkipHitMode())
                return;

            var maniaRuleset = (DrawableManiaRuleset)drawableRuleset;

            foreach (var stage in maniaRuleset.Playfield.Stages)
            {
                foreach (var column in stage.Columns)
                {
                    configurePools(column, HitMode.Value);
                }
            }
        }

        private void configurePools(Column column, MUGHitMode hitMode)
        {
            switch (hitMode)
            {
                case MUGHitMode.EZ2AC:
                    column.RegisterPool<NoJudgmentNote, DrawableNote>(10, 50);
                    column.RegisterPool<CustomLNHead, DrawableHoldNoteHead>(10, 50);
                    column.RegisterPool<NoMissLNBody, Ez2AcDrawableHoldNoteBody>(10, 50);
                    column.RegisterPool<Ez2AcHoldNoteTail, Ez2AcDrawableHoldNoteTail>(10, 50);
                    break;

                case MUGHitMode.Melody:
                    column.RegisterPool<NoJudgmentNote, DrawableNote>(10, 50);
                    column.RegisterPool<CustomLNHead, DrawableHoldNoteHead>(10, 50);
                    column.RegisterPool<NoMissLNBody, MalodyDrawableHoldNoteBody>(10, 50);
                    column.RegisterPool<NoComboBreakLNTail, MalodyDrawableHoldNoteTail>(10, 50);
                    break;
            }
        }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            if (shouldSkipHitMode())
                return;

            HitWindows = new ManiaHitWindows();
            HitWindows.SetDifficulty(difficulty.OverallDifficulty);
            configureHitWindows(HitMode.Value);
        }

        private void configureHitWindows(MUGHitMode hitMode)
        {
            switch (hitMode)
            {
                case MUGHitMode.EZ2AC:
                    Ez2AcDrawableHoldNoteTail.HitWindows = HitWindows;
                    break;

                case MUGHitMode.Melody:
                    MalodyDrawableHoldNoteTail.HitWindows = HitWindows;
                    break;
            }
        }

        public static class GlobalManiaConfigStore
        {
            public static ManiaRulesetConfigManager? Config { get; set; }
            private static BeatInterval beatInterval { get; } = new BeatInterval();
            public static event Action? OnRefresh;

            private static Timer? refreshTimer;

            public static void TriggerRefresh()
            {
                refreshTimer?.Dispose();

                // 动态计算间隔
                double interval = beatInterval.GetCurrentQuarterBeatInterval();

                refreshTimer = new Timer(_ =>
                {
                    OnRefresh?.Invoke();

                    // 再次动态计算间隔并递归调用刷新
                    TriggerRefresh();
                }, null, (int)interval, Timeout.Infinite);
            }
        }

        // public bool IsHitResultAllowed(HitResult result)
        // {
        //     return result switch
        //     {
        //         HitResult.Perfect => true,
        //         HitResult.Great => true,
        //         HitResult.Good => true,
        //         HitResult.Ok => true,
        //         HitResult.Meh => true,
        //         HitResult.Miss => true,
        //         HitResult.IgnoreHit => true,
        //         HitResult.IgnoreMiss => true,
        //         _ => false,
        //     };
        // }
    }

    public class BeatInterval
    {
        [Resolved]
        private IBeatmap? beatmap { get; set; }

        public double GetCurrentQuarterBeatInterval()
        {
            if (beatmap != null)
            {
                double bpm = beatmap.BeatmapInfo.BPM;

                return 4 * 60000 / bpm;
            }

            return 100;
        }
    }

    public class HitWindowTemplate
    {
        public double TemplatePerfect { get; set; }
        public double TemplateGreat { get; set; }
        public double TemplateGood { get; set; }
        public double TemplateOk { get; set; }
        public double TemplateMeh { get; set; }
        public double TemplateMiss { get; set; }
    }

    public static class HitWindowTemplates
    {
        private static readonly Dictionary<string, HitWindowTemplate> templates = new Dictionary<string, HitWindowTemplate>
        {
            ["EZ2AC"] = new HitWindowTemplate
            {
                TemplatePerfect = 22,
                TemplateGreat = 32,
                TemplateGood = 64,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            },
            ["IIDX"] = new HitWindowTemplate
            {
                TemplatePerfect = 20,
                TemplateGreat = 40,
                TemplateGood = 60,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            },
            ["Melody"] = new HitWindowTemplate
            {
                TemplatePerfect = 20,
                TemplateGreat = 40,
                TemplateGood = 60,
                TemplateOk = 80,
                TemplateMeh = 100,
                TemplateMiss = 120
            }
        };

        public static HitWindowTemplate GetTemplate(string mode)
        {
            return templates.TryGetValue(mode, out var template)
                ? template
                : throw new InvalidOperationException($"Hit window template for mode '{mode}' is not defined.");
        }
    }
}
