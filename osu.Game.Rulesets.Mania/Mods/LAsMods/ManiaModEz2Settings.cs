// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// #pragma warning disable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModEz2Settings : Mod, IApplicableToDifficulty, IApplicableToBeatmap //, IApplicableToSample //, IStoryboardElement
    {
        public override string Name => "Ez2 Settings";
        public override string Acronym => "ES";
        public override LocalisableString Description => "LaMod: Remove Scratch, Panel.";
        public override ModType Type => ModType.LA_Mod;
        public override IconUsage? Icon => FontAwesome.Solid.Tools;

        public override bool Ranked => false;
        public override double ScoreMultiplier => 1;

        [SettingSource("No (EZ)Scratch", "免盘. For: 6-9k L-S; 12\\14\\16k LR-S ")]
        public BindableBool NoScratch { get; } = new BindableBool();

        [SettingSource("No (EZ)Panel", "免脚踏. For: 7\\14\\18k")]
        public BindableBool NoPanel { get; } = new BindableBool();

        [SettingSource("Healthy (EZ)Scratch", "优化盘子密度 Move the fast Scratch to the other columns")]
        public BindableBool HealthScratch { get; } = new BindableBool(true);

        [SettingSource("Scratch MAX Beat Space", "盘子最大间隔, MAX 1/? Beat", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> MaxBeat { get; } = new BindableDouble(3)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        // [SettingSource("Global Speed Regulation", "全局调速，开局调速有暂停，全局屏蔽倒计时.")]
        // public BindableBool GlobalScrollSpeed { get; } = new BindableBool();

        public ManiaModEz2Settings()
        {
            NoScratch.ValueChanged += OnSettingChanged;
            HealthScratch.ValueChanged += OnHealthScratchChanged;
        }

        private void OnSettingChanged(ValueChangedEvent<bool> e)
        {
            if (e.NewValue)
            {
                HealthScratch.Value = false;
            }
        }

        private void OnHealthScratchChanged(ValueChangedEvent<bool> e)
        {
            if (e.NewValue)
            {
                NoScratch.Value = false;
            }
        }

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                var settings = new List<(LocalisableString setting, LocalisableString value)>();

                if (NoScratch.Value)
                    settings.Add((new LocalisableString("No Scratch"), new LocalisableString("Enabled")));

                if (NoPanel.Value)
                    settings.Add((new LocalisableString("No Panel"), new LocalisableString("Enabled")));

                if (HealthScratch.Value)
                    settings.Add((new LocalisableString("Scratch MAX Beat Space"), new LocalisableString($"1/{MaxBeat.Value} Beat")));

                return settings;
            }
        }

        private IBeatmap beatmap = null!;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            this.beatmap = beatmap;
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (HealthScratch.Value)
            {
                NoScratch.Value = false;
            }

            if (HealthScratch.Value && HealthTemplate.TryGetValue(keys, out var moveTargets))
            {
                var notesToMove = maniaBeatmap.HitObjects
                                              .Where(h => h is ManiaHitObject maniaHitObject && moveTargets.Contains(maniaHitObject.Column))
                                              .OrderBy(h => h.StartTime)
                                              .ToList();

                ManiaHitObject? previousNote = null;

                foreach (var note in notesToMove)
                {
                    if (previousNote != null && note.StartTime - previousNote.StartTime <= beatmap.ControlPointInfo.TimingPointAt(note.StartTime).BeatLength / MaxBeat.Value)
                    {
                        bool moved = false;

                        foreach (int targetColumn in MoveTemplate[keys])
                        {
                            int newColumn = targetColumn;
                            note.Column = newColumn % keys;

                            var targetColumnNotes = maniaBeatmap.HitObjects
                                                                .Where(h => h is ManiaHitObject maniaHitObject && maniaHitObject.Column == newColumn)
                                                                .OrderBy(h => h.StartTime)
                                                                .ToList();
                            bool isValid = true;

                            for (int i = 0; i < targetColumnNotes.Count - 1; i++)
                            {
                                var currentNote = targetColumnNotes[i];
                                var nextNote = targetColumnNotes[i + 1];

                                if (nextNote.StartTime - currentNote.StartTime <= beatmap.ControlPointInfo.TimingPointAt(nextNote.StartTime).BeatLength / 4)
                                {
                                    isValid = false;
                                    break;
                                }

                                if (currentNote is HoldNote holdNote && nextNote.StartTime <= holdNote.EndTime)
                                {
                                    isValid = false;
                                    break;
                                }
                            }

                            if (isValid)
                            {
                                moved = true;
                                break;
                            }
                        }

                        if (!moved)
                        {
                            note.Column = previousNote.Column;
                        }
                    }

                    previousNote = note;
                }
            }

            if (NoScratch.Value && NoScratchTemplate.TryGetValue(keys, out var scratchToRemove))
            {
                var scratchNotesToRemove = maniaBeatmap.HitObjects
                                                       .Where(h => h is ManiaHitObject maniaHitObject && scratchToRemove.Contains(maniaHitObject.Column))
                                                       .OrderBy(h => h.StartTime)
                                                       .ToList();

                foreach (var note in scratchNotesToRemove)
                {
                    maniaBeatmap.HitObjects.Remove(note);
                    applySamples(note);
                }
            }

            if (NoPanel.Value && NoPanelTemplate.TryGetValue(keys, out var panelToRemove))
            {
                var panelNotesToRemove = maniaBeatmap.HitObjects
                                                     .Where(h => h is ManiaHitObject maniaHitObject && panelToRemove.Contains(maniaHitObject.Column))
                                                     .OrderBy(h => h.StartTime)
                                                     .ToList();

                foreach (var note in panelNotesToRemove)
                {
                    maniaBeatmap.HitObjects.Remove(note);
                    applySamples(note);
                }
            }
        }

        private void applySamples(HitObject hitObject)
        {
            SampleControlPoint sampleControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.SamplePointAt(hitObject.GetEndTime() + 5)
                                                    ?? SampleControlPoint.DEFAULT;
            hitObject.Samples = hitObject.Samples.Select(o => sampleControlPoint.ApplyTo(o)).ToList();
        }

        public ISample GetSample(ISampleInfo sampleInfo)
        {
            if (sampleInfo is ConvertHitObjectParser.LegacyHitSampleInfo legacySample && legacySample.IsLayered)
                return new SampleVirtual();

            return GetSample(sampleInfo);
        }

        // public class RemovedNoteSample
        // {
        //     public double StartTime { get; set; }
        //     public required string Sample { get; set; }
        // }

        // public void ApplyToSample(IAdjustableAudioComponent sample) { }

        public void ApplyToDifficulty(BeatmapDifficulty difficulty) { }

        public override string ExtendedIconInformation => "";

        public Dictionary<int, List<int>> NoScratchTemplate { get; set; } = new Dictionary<int, List<int>>
        {
            { 16, new List<int> { 0, 15 } },
            { 14, new List<int> { 0, 12 } },
            { 12, new List<int> { 0, 11 } },
            { 9, new List<int> { 0 } },
            { 8, new List<int> { 0 } },
            { 7, new List<int> { 0 } },
            { 6, new List<int> { 0 } },
        };

        public Dictionary<int, List<int>> NoPanelTemplate { get; set; } = new Dictionary<int, List<int>>
        {
            { 18, new List<int> { 6, 11 } },
            { 14, new List<int> { 6 } },
            { 9, new List<int> { 8 } },
            { 7, new List<int> { 6 } },
        };

        public Dictionary<int, List<int>> HealthTemplate { get; set; } = new Dictionary<int, List<int>>
        {
            { 16, new List<int> { 0, 15 } },
            { 14, new List<int> { 0, 6, 12 } },
            { 12, new List<int> { 0, 11 } },
            { 9, new List<int> { 0, 8 } },
            { 8, new List<int> { 0, 8 } },
            { 7, new List<int> { 0 } },
            { 6, new List<int> { 0 } },
        };

        public Dictionary<int, List<int>> MoveTemplate { get; set; } = new Dictionary<int, List<int>>
        {
            { 16, new List<int> { 15, 0, 2, 4, 8, 10, 6, 7, 8, 9, 5, 10 } },
            { 14, new List<int> { 12, 0, 2, 4, 8, 10, 5, 7, 1, 3, 9, 11 } },
            { 12, new List<int> { 11, 0, 2, 4, 8, 10, 5, 6, 7, 1, 3, 9 } },
            { 9, new List<int> { 8, 0, 4, 2, 3, 1, 5, 7, 6 } },
            { 8, new List<int> { 7, 0, 6, 4, 2, 5, 3, 1 } },
            { 7, new List<int> { 6, 4, 2, 5, 3, 1 } },
            { 6, new List<int> { 4, 2, 5, 3, 1 } },
        };

        // public Dictionary<int, List<int>> MoveTemplate { get; set; } = new Dictionary<int, List<int>>
        // {
        //     { 16, new List<int> { 0, 15 } },
        //     { 14, new List<int> { 0, 12 } },
        //     { 12, new List<int> { 0, 11 } },
        //     { 9, new List<int> { 8, 0 } },
        //     { 8, new List<int> { 7, 0 } },
        //     { 7, new List<int> { 6 } },
        //     { 6, new List<int> { 5, 4 } },
        // };
    }
}
// #pragma warning restore
// public void SetTrackBackgroundColor(List<int> trackIndices, Color4 color, List<Ez2ColumnBackground> columnBackgrounds)
// {
//     foreach (int trackIndex in trackIndices)
//     {
//         if (trackIndex >= 0 && trackIndex < columnBackgrounds.Count)
//         {
//             var columnBackground = columnBackgrounds[trackIndex];
//             columnBackground.background.Colour = color;
//         }
//     }
// }
// public void ReadFromDifficulty(IBeatmapDifficultyInfo difficulty)
// {
// }
// string lines = note;
// beatmap.UnhandledEventLines.Add(lines);
// string path = note.Samples.GetHashCode().ToString();
// double time = note.StartTime;
// storyboard.GetLayer("Background").Add(new StoryboardSampleInfo(path, time, 100));
// storyboard.GetLayer("Background").Add();
// applySamples(note);

// private Storyboard storyboard = null!;

// public void ApplyToSample(IAdjustableAudioComponent sample)
// {
//     foreach (var noteSample in removedSamples)
//     {
//         string path = noteSample.Sample.ToString() ?? string.Empty;
//         double time = noteSample.StartTime;
//         storyboard.GetLayer("Background").Add(new StoryboardSampleInfo(path, time, 100));
//     }
// }
// processedTracks.AddRange(panelToRemove);
// setTrackBackgroundColor(panelToRemove, new Color4(0, 0, 0, 0));
