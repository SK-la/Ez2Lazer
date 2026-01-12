// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    public partial class TestSceneSRTune : OsuTestScene
    {
        private FillFlowContainer listContainer = null!;
        private Dictionary<string, double> last = new Dictionary<string, double>();
        private Dictionary<string, double> current = new Dictionary<string, double>();

        // exposed tuning parameters (initialized from Tunables defaults)
        private double ln_weight = SRCalculatorTunable.Tunables.FinalLNToNotesFactor; // 0.0 - 1.0
        private double ln_len_cap = SRCalculatorTunable.Tunables.FinalLNLenCap; // ms, 100 - 2000
        private double totalnotes_offset = SRCalculatorTunable.Tunables.TotalNotesOffset; // 0 - 500
        private double pbar_ln_coeff = SRCalculatorTunable.Tunables.PBarLnMultiplier; // 0.0 - 0.02
        private double jack_multiplier = SRCalculatorTunable.Tunables.JackPenaltyMultiplier; // 10 - 40
        private double final_scale = SRCalculatorTunable.Tunables.FinalScale; // 0.9 - 1.05

        private IBeatmap[] sampleBeatmaps = Array.Empty<IBeatmap>();

        // storage for last/two-setting snapshots
        private (double lnWeight, double lnLenCap, double offset, double pbarCoeff, double jackMult, double scale) lastSettings;

        public TestSceneSRTune()
        {
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(6f),
                Padding = new MarginPadding(10),
                Children = new[]
                {
                    // control row is exposed as test steps rather than placed into the scene hierarchy
                    listContainer = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(4f),
                        AutoSizeAxes = Axes.Y
                    }
                }
            };

            loadSampleBeatmaps();

            // register test steps for sliders and button controls (appear in the test steps UI)
            AddSliderStep("LN Weight", 0, 1, SRCalculatorTunable.Tunables.FinalLNToNotesFactor, v => SRCalculatorTunable.Tunables.FinalLNToNotesFactor = v);
            AddSliderStep("LN Len Cap", 100, 2000, SRCalculatorTunable.Tunables.FinalLNLenCap, v => SRCalculatorTunable.Tunables.FinalLNLenCap = v);
            AddSliderStep("Offset", 0, 500, SRCalculatorTunable.Tunables.TotalNotesOffset, v => SRCalculatorTunable.Tunables.TotalNotesOffset = v);
            AddSliderStep("pBar LN coeff", 0, 0.02, SRCalculatorTunable.Tunables.PBarLnMultiplier, v => SRCalculatorTunable.Tunables.PBarLnMultiplier = v);
            AddSliderStep("Jack mult", 10, 40, SRCalculatorTunable.Tunables.JackPenaltyMultiplier, v => SRCalculatorTunable.Tunables.JackPenaltyMultiplier = v);
            AddSliderStep("Final scale", 0.9, 1.05, SRCalculatorTunable.Tunables.FinalScale, v => SRCalculatorTunable.Tunables.FinalScale = v);

            // save current settings to "last" snapshot
            AddStep("Save as last settings", () =>
            {
                lastSettings = (SRCalculatorTunable.Tunables.FinalLNToNotesFactor, SRCalculatorTunable.Tunables.FinalLNLenCap, SRCalculatorTunable.Tunables.TotalNotesOffset, SRCalculatorTunable.Tunables.PBarLnMultiplier, SRCalculatorTunable.Tunables.JackPenaltyMultiplier, SRCalculatorTunable.Tunables.FinalScale);
                // also snapshot last SR values from current
                foreach (var k in current.Keys)
                    last[k] = current[k];
            });

            // update every 50ms
            Scheduler.AddDelayed(updateAllSR, 50, true);
        }

        private IBeatmap[] createSyntheticSamples()
        {
            var list = new List<IBeatmap>();

            for (int i = 0; i < 8; i++)
            {
                var bm = new ManiaBeatmap(new StageDefinition(4));
                bm.BeatmapInfo = new BeatmapInfo { Metadata = new BeatmapMetadata { Title = $"Sample {i + 1}" } };

                for (int t = 0; t < 2000; t += 250)
                {
                    // alternate between simple notes and hold notes to exercise LN codepaths
                    if (i % 2 == 0)
                    {
                        var note = new Note { StartTime = t + i * 10, Column = i % 4 };
                        bm.HitObjects.Add(note);
                    }
                    else
                    {
                        var hold = new HoldNote { StartTime = t + i * 10, Column = i % 4 };
                        hold.Duration = 400 + i * 50;
                        bm.HitObjects.Add(hold);
                    }
                }

                list.Add(bm);
            }

            return list.ToArray();
        }

        private Drawable buildControlRow()
        {
            // Controls are exposed via test steps (AddSliderStep / AddStep) instead of being placed in the scene.
            return new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
        }

        private void loadSampleBeatmaps()
        {
            // try load canonical test beatmap resource first
            try
            {
                var resourcePath = @"Resources/Testing/Beatmaps/4869637.osu";
                var fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "osu.Game.Rulesets.Mania.Tests", resourcePath);
                if (!System.IO.File.Exists(fullPath))
                    fullPath = System.IO.Path.Combine(Environment.CurrentDirectory, resourcePath);

                // prefer a set of deterministic half-hold beatmaps for side-by-side tuning
                sampleBeatmaps = HalfHoldBeatmapSets.CreateTen(4, 32);
            }
            catch
            {
                sampleBeatmaps = createSyntheticSamples();
            }

            foreach (var bm in sampleBeatmaps)
            {
                string id = ((BeatmapInfo)bm.BeatmapInfo).Metadata.Title!;
                last[id] = 0;
                current[id] = 0;

                var row = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10f),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText { Text = id, Width = 180, Font = new FontUsage(size:20) },
                        new OsuSpriteText { Text = "Last: 0", Name = "last_" + id, Font = new FontUsage(size:20) },
                        new OsuSpriteText { Text = "Cur: 0", Name = "cur_" + id, Font = new FontUsage(size:20) },
                        // place holders for settings display under the SRs
                        new OsuSpriteText { Text = "Last settings: -", Name = "last_set_" + id, Font = new FontUsage(size:20) },
                        new OsuSpriteText { Text = "Cur settings: -", Name = "cur_set_" + id, Font = new FontUsage(size:20) }
                    }
                };

                listContainer.Add(row);
            }
        }

        private void updateAllSR()
        {
            // read slider values
            var sliders = this.ChildrenOfType<BasicSliderBar<double>>();

            foreach (var s in sliders)
            {
                // decide by Width positions we set earlier
                if (s.Width == 0.28f) ln_weight = s.Current.Value;
                else if (s.Width == 0.2f) ln_len_cap = s.Current.Value;
                else if (s.Width == 0.16f) totalnotes_offset = s.Current.Value;
                else if (s.Width == 0.22f) pbar_ln_coeff = s.Current.Value;
                else if (s.Width == 0.18f) jack_multiplier = s.Current.Value;
                else if (s.Width == 0.12f) final_scale = s.Current.Value;
            }

            // propagate slider values into SRCalculatorTunable.Tunables
            TunableSyncFromSliders();

            for (int i = 0; i < sampleBeatmaps.Length; i++)
            {
                var bm = sampleBeatmaps[i];
                string id = bm.BeatmapInfo.Metadata.Title;

                double sr = SRCalculatorTunable.CalculateSR(bm);
                current[id] = sr;

                var row = listContainer.Children[i] as FillFlowContainer;
                var curText = row?.Children[2] as SpriteText;
                if (row?.Children[1] is SpriteText lastText) lastText.Text = $"Last: {last[id]:F2}";
                if (curText != null) curText.Text = $"Cur: {sr:F2}";

                // update settings display
                var lastSetText = row?.Children[3] as SpriteText;
                var curSetText = row?.Children[4] as SpriteText;
                if (lastSetText != null)
                    lastSetText.Text = $"Last settings: ln_w={lastSettings.lnWeight:F3}, ln_cap={lastSettings.lnLenCap:F0}, off={lastSettings.offset:F0}, pbar={lastSettings.pbarCoeff:F4}, jack={lastSettings.jackMult:F1}, scale={lastSettings.scale:F3}";
                if (curSetText != null)
                    curSetText.Text = $"Cur settings: ln_w={SRCalculatorTunable.Tunables.FinalLNToNotesFactor:F3}, ln_cap={SRCalculatorTunable.Tunables.FinalLNLenCap:F0}, off={SRCalculatorTunable.Tunables.TotalNotesOffset:F0}, pbar={SRCalculatorTunable.Tunables.PBarLnMultiplier:F4}, jack={SRCalculatorTunable.Tunables.JackPenaltyMultiplier:F1}, scale={SRCalculatorTunable.Tunables.FinalScale:F3}";
            }
        }

        private void TunableSyncFromSliders()
        {
            var sliders = this.ChildrenOfType<BasicSliderBar<double>>();

            foreach (var s in sliders)
            {
                if (s.Width == 0.28f) SRCalculatorTunable.Tunables.FinalLNToNotesFactor = s.Current.Value;
                else if (s.Width == 0.2f) SRCalculatorTunable.Tunables.FinalLNLenCap = s.Current.Value;
                else if (s.Width == 0.16f) SRCalculatorTunable.Tunables.TotalNotesOffset = s.Current.Value;
                else if (s.Width == 0.22f) SRCalculatorTunable.Tunables.PBarLnMultiplier = s.Current.Value;
                else if (s.Width == 0.18f) SRCalculatorTunable.Tunables.JackPenaltyMultiplier = s.Current.Value;
                else if (s.Width == 0.12f) SRCalculatorTunable.Tunables.FinalScale = s.Current.Value;
            }
        }

        // isolated compute method that uses exposed parameters
        private double HotComputeSR(IBeatmap beatmap, double lnWeight, int lnLenCap, int offset, double pbarCoeff, double jackMult, double scale)
        {
            var mania = (ManiaBeatmap)beatmap;
            int headCount = 0;
            var lnLens = new List<int>();

            foreach (var ho in mania.HitObjects)
            {
                headCount++;
                int tail = (int)ho.GetEndTime();
                int len = Math.Max(0, Math.Min(tail - (int)ho.StartTime, lnLenCap));
                if (len > 0) lnLens.Add(len);
            }

            double baseDifficulty = headCount * 0.1;

            double lnContribution = 0;

            foreach (int len in lnLens)
            {
                // combine ln weight and pbar coefficient
                lnContribution += lnWeight * (len / 200.0) * 0.5 * (1.0 + pbarCoeff * 100.0);
            }

            double totalNotes = headCount + lnContribution;

            // simplistic tail-based penalty: more LN tails -> slightly reduce sr, scaled by jackMult
            double tailPenalty = 1.0 + lnLens.Count * 0.01 * (jackMult / 35.0);

            double sr = baseDifficulty * (totalNotes / (totalNotes + offset)) / tailPenalty;
            sr *= scale;
            return Math.Max(0, sr);
        }
    }
}
