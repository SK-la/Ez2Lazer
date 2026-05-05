// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Decoder for BMS/BME/BML/PMS file formats.
    /// </summary>
    public class BMSBeatmapDecoder : Decoder<Beatmap>
    {
        private const string BMS_LOG_PREFIX = "[BMS]";

        protected override Beatmap CreateTemplateObject() => new BMSBeatmap();

        // BMS channel definitions
        private const int channel_bgm = 1;
        private const int channel_measure_length = 2;
        private const int channel_bpm_change = 3;
        private const int channel_bga_base = 4;
        private const int channel_bga_poor = 6;
        private const int channel_bga_layer = 7;
        private const int channel_extended_bpm = 8;
        private const int channel_stop = 9;

        // 1P visible notes: 11-19 (1-7 keys + scratch)
        // 1P long notes: 51-59
        // 1P invisible notes: 31-39
        // 2P visible notes: 21-29
        // 2P long notes: 61-69

        private readonly Dictionary<string, string> wavDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> bmpDefinitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> bpmDefinitions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double> stopDefinitions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<int, double> measureLengths = new Dictionary<int, double>();
        private readonly List<BMSEvent> events = new List<BMSEvent>();

        private double initialBpm = 130;
        private int totalKeys = 7;
        private bool hasLongNotes;
        private int lnType = 1; // 1 = LN, 2 = CN, 3 = HCN
        private int eventSequence;

        // Metadata
        private string title = string.Empty;
        private string subtitle = string.Empty;
        private string artist = string.Empty;
        private string subartist = string.Empty;
        private string genre = string.Empty;
        private string stageFile = string.Empty;
        private string banner = string.Empty;
        private int playLevel;
        private int difficulty;
        private int rank = 2; // Judge rank (0=very hard, 1=hard, 2=normal, 3=easy)
        private double total = 100;

        public static void Register()
        {
            // Register for various BMS file magic strings
            AddDecoder<Beatmap>("*----------------------", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#PLAYER", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#GENRE", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#TITLE", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#ARTIST", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#BPM", _ => new BMSBeatmapDecoder());
            AddDecoder<Beatmap>("#PLAYLEVEL", _ => new BMSBeatmapDecoder());
        }

        protected override void ParseStreamInto(LineBufferedReader stream, Beatmap beatmap)
        {
            // Reset state
            wavDefinitions.Clear();
            bmpDefinitions.Clear();
            bpmDefinitions.Clear();
            stopDefinitions.Clear();
            measureLengths.Clear();
            events.Clear();
            eventSequence = 0;

            Logger.Log($"{BMS_LOG_PREFIX} Starting BMS beatmap decoding");

            string? line;

            while ((line = stream.ReadLine()) != null)
            {
                line = line.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith('*'))
                    continue;

                if (line.StartsWith('#'))
                    parseLine(line);
            }

            // Build the beatmap
            buildBeatmap(beatmap);
        }

        private void parseLine(string line)
        {
            // Match header commands like #TITLE, #ARTIST, #WAV01/#WAVAA, etc.
            // BMS uses base36 (0-9, A-Z) indices, but many headers have no index.
            if (line.StartsWith('#'))
            {
                int spaceIndex = line.IndexOfAny(new[] { ' ', '\t' }, 1);

                if (spaceIndex > 1)
                {
                    string header = line.Substring(1, spaceIndex - 1).Trim();
                    string value = line.Substring(spaceIndex + 1).Trim();

                    string upperHeader = header.ToUpperInvariant();
                    string command = upperHeader;
                    string index = string.Empty;

                    if (upperHeader.StartsWith("WAV", StringComparison.Ordinal) && upperHeader.Length > 3)
                    {
                        command = "WAV";
                        index = upperHeader.Substring(3);
                    }
                    else if (upperHeader.StartsWith("BMP", StringComparison.Ordinal) && upperHeader.Length > 3)
                    {
                        command = "BMP";
                        index = upperHeader.Substring(3);
                    }
                    else if (upperHeader.StartsWith("STOP", StringComparison.Ordinal) && upperHeader.Length > 4)
                    {
                        command = "STOP";
                        index = upperHeader.Substring(4);
                    }
                    else if (upperHeader.StartsWith("BPM", StringComparison.Ordinal) && upperHeader.Length > 3)
                    {
                        command = "BPM";
                        index = upperHeader.Substring(3);
                    }

                    parseHeader(command, index, value);
                    return;
                }
            }

            // Match channel data like #00111:01020304
            var channelMatch = Regex.Match(line, @"^#(\d{3})(\d{2}):(.+)$");

            if (channelMatch.Success)
            {
                int measure = int.Parse(channelMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                int channel = int.Parse(channelMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                string data = channelMatch.Groups[3].Value;

                parseChannelData(measure, channel, data);
            }
        }

        private void parseHeader(string command, string index, string value)
        {
            switch (command)
            {
                case "TITLE":
                    title = value;
                    break;

                case "SUBTITLE":
                    subtitle = value;
                    break;

                case "ARTIST":
                    artist = value;
                    break;

                case "SUBARTIST":
                    subartist = value;
                    break;

                case "GENRE":
                    genre = value;
                    break;

                case "BPM":
                    if (string.IsNullOrEmpty(index))
                    {
                        // Initial BPM
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm))
                            initialBpm = bpm;
                    }
                    else
                    {
                        // Extended BPM definition
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm))
                            bpmDefinitions[index] = bpm;
                    }

                    break;

                case "WAV":
                    if (!string.IsNullOrEmpty(index))
                        wavDefinitions[index] = value;
                    break;

                case "BMP":
                    if (!string.IsNullOrEmpty(index))
                        bmpDefinitions[index] = value;
                    break;

                case "STOP":
                    if (!string.IsNullOrEmpty(index) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double stopValue))
                        stopDefinitions[index] = stopValue;
                    break;

                case "STAGEFILE":
                    stageFile = value;
                    break;

                case "BANNER":
                    banner = value;
                    break;

                case "PLAYLEVEL":
                    int.TryParse(value, out playLevel);
                    break;

                case "DIFFICULTY":
                    int.TryParse(value, out difficulty);
                    break;

                case "RANK":
                    int.TryParse(value, out rank);
                    break;

                case "TOTAL":
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out total);
                    break;

                case "LNTYPE":
                    int.TryParse(value, out lnType);
                    hasLongNotes = true;
                    break;

                case "LNOBJ":
                    hasLongNotes = true;
                    break;

                case "PLAYER":
                    // 1=SP, 2=DP, 3=double play
                    break;
            }
        }

        private void parseChannelData(int measure, int channel, string data)
        {
            // Measure length change
            if (channel == channel_measure_length)
            {
                if (double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out double length))
                    measureLengths[measure] = length;
                return;
            }

            // Parse the object data (every 2 characters = 1 object)
            int objectCount = data.Length / 2;

            for (int i = 0; i < objectCount; i++)
            {
                string objKey = data.Substring(i * 2, 2);

                if (objKey == "00")
                    continue;

                double position = (double)i / objectCount;

                events.Add(new BMSEvent
                {
                    Sequence = eventSequence++,
                    Measure = measure,
                    Channel = channel,
                    Position = position,
                    Value = objKey
                });
            }
        }

        private void buildBeatmap(Beatmap beatmap)
        {
            // Log for debugging
            Logger.Log($"[BMS] Loaded {wavDefinitions.Count} WAV definitions, {bmpDefinitions.Count} BMP definitions", LoggingTarget.Runtime, LogLevel.Debug);

            // Set metadata
            beatmap.BeatmapInfo.Metadata.Title = title;
            beatmap.BeatmapInfo.Metadata.TitleUnicode = title;
            beatmap.BeatmapInfo.Metadata.Artist = artist;
            beatmap.BeatmapInfo.Metadata.ArtistUnicode = artist;
            beatmap.BeatmapInfo.Metadata.Author.Username = "Ez2Lazer BMSDecoder";
            beatmap.BeatmapInfo.Metadata.Source = "BMS Import";
            beatmap.BeatmapInfo.Metadata.Tags = $"bms {genre}";
            beatmap.BeatmapInfo.Ruleset = new BMSRuleset().RulesetInfo;
            beatmap.BeatmapInfo.DifficultyName = getDifficultyName();

            if (!string.IsNullOrEmpty(stageFile))
                beatmap.BeatmapInfo.Metadata.BackgroundFile = stageFile;

            // Calculate chart timeline once and re-use for all generated objects/events.
            var timeline = buildTimeline();
            var timingPoints = createTimingPointsFromTimeline(timeline);
            foreach (var tp in timingPoints)
                beatmap.ControlPointInfo.Add(tp.Time, tp.ControlPoint);

            // Create hit objects
            var hitObjects = createHitObjects(timeline.EventTimes);
            beatmap.HitObjects.AddRange(hitObjects);

            // Store background (non-note) sound events
            if (beatmap is BMSBeatmap bmsBeatmap)
            {
                bmsBeatmap.TotalColumns = totalKeys;
                var backgroundEvents = createBackgroundSoundEvents(timeline.EventTimes);
                bmsBeatmap.BackgroundSoundEvents.AddRange(backgroundEvents);

                if (backgroundEvents.Count > 0)
                {
                    beatmap.BeatmapInfo.Metadata.AudioFile = backgroundEvents[0].Filename;
                    beatmap.BeatmapInfo.Metadata.PreviewTime = (int)Math.Max(0, backgroundEvents[0].Time);
                }
                else if (wavDefinitions.Count > 0)
                {
                    beatmap.BeatmapInfo.Metadata.AudioFile = wavDefinitions.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).First();
                }

                Logger.Log($"[BMS] Background sound events: {backgroundEvents.Count}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            // Set difficulty
            beatmap.Difficulty.CircleSize = totalKeys;
            beatmap.Difficulty.OverallDifficulty = mapRankToOD(rank);
            beatmap.Difficulty.DrainRate = 7;
        }

        private string getDifficultyName()
        {
            string name = difficulty switch
            {
                1 => "Beginner",
                2 => "Normal",
                3 => "Hyper",
                4 => "Another",
                5 => "Insane",
                _ => $"Level {playLevel}"
            };

            if (!string.IsNullOrEmpty(subtitle))
                name = $"{subtitle} [{name}]";

            return name;
        }

        private float mapRankToOD(int bmsRank)
        {
            // BMS RANK: 0=very hard, 1=hard, 2=normal, 3=easy
            // Map to OD: higher OD = tighter windows
            return bmsRank switch
            {
                0 => 9f,
                1 => 8f,
                2 => 7f,
                3 => 5f,
                _ => 7f
            };
        }

        private TimelineData buildTimeline()
        {
            var timingPoints = new List<TimingPointData>
            {
                new TimingPointData
                {
                    Time = 0,
                    ControlPoint = new TimingControlPoint { BeatLength = 60000 / initialBpm }
                }
            };

            var sortedEvents = events
                               .OrderBy(e => e.Measure)
                               .ThenBy(e => e.Position)
                               .ThenBy(e => e.Sequence)
                               .ToList();

            var eventTimes = new Dictionary<BMSEvent, double>();

            double currentTime = 0;
            double currentBpm = initialBpm;
            int currentMeasure = 0;
            double currentPositionInMeasure = 0;

            foreach (var evt in sortedEvents.OrderBy(e => e.Measure).ThenBy(e => e.Position).ThenBy(e => e.Sequence))
            {
                double targetPositionInMeasure = Math.Clamp(evt.Position, 0, 1);
                advanceTimeline(ref currentTime, ref currentMeasure, ref currentPositionInMeasure, evt.Measure, targetPositionInMeasure, currentBpm);
                eventTimes[evt] = currentTime;

                if (tryGetBpmChange(evt, out double bpm) && bpm > 0)
                {
                    currentBpm = bpm;
                    timingPoints.Add(new TimingPointData
                    {
                        Time = currentTime,
                        ControlPoint = new TimingControlPoint { BeatLength = 60000 / bpm }
                    });
                }

                if (evt.Channel == channel_stop && stopDefinitions.TryGetValue(evt.Value, out double stopValue) && stopValue > 0)
                {
                    // In BMS, STOP units are based on 1/192 measure (== 1/48 beat at 4/4).
                    currentTime += stopValue * (60000 / currentBpm) / 48.0;
                }
            }

            return new TimelineData
            {
                TimingPoints = timingPoints.OrderBy(t => t.Time).ToList(),
                EventTimes = eventTimes
            };
        }

        private List<TimingPointData> createTimingPointsFromTimeline(TimelineData timeline) => timeline.TimingPoints;

        private void advanceTimeline(ref double currentTime, ref int currentMeasure, ref double currentPositionInMeasure, int targetMeasure, double targetPositionInMeasure, double bpm)
        {
            if (targetMeasure < currentMeasure || targetMeasure == currentMeasure && targetPositionInMeasure < currentPositionInMeasure)
                return;

            while (currentMeasure < targetMeasure)
            {
                double remainingPortion = 1 - currentPositionInMeasure;
                currentTime += beatLengthForMeasure(currentMeasure, bpm) * remainingPortion;
                currentMeasure++;
                currentPositionInMeasure = 0;
            }

            if (targetPositionInMeasure > currentPositionInMeasure)
                currentTime += beatLengthForMeasure(currentMeasure, bpm) * (targetPositionInMeasure - currentPositionInMeasure);

            currentPositionInMeasure = targetPositionInMeasure;
        }

        private bool tryGetBpmChange(BMSEvent evt, out double bpm)
        {
            bpm = 0;

            if (evt.Channel == channel_bpm_change)
            {
                bpm = Convert.ToInt32(evt.Value, 16);
                return bpm > 0;
            }

            if (evt.Channel == channel_extended_bpm && bpmDefinitions.TryGetValue(evt.Value, out bpm))
                return bpm > 0;

            return false;
        }

        private double beatLengthForMeasure(int measure, double bpm)
        {
            double measureLength = measureLengths.GetValueOrDefault(measure, 1.0);
            double beatsPerMeasure = 4.0 * measureLength;
            double msPerBeat = 60000 / bpm;
            return beatsPerMeasure * msPerBeat;
        }

        private List<BMSHitObject> createHitObjects(Dictionary<BMSEvent, double> eventTimes)
        {
            var result = new List<BMSHitObject>();
            var activeHolds = new Dictionary<int, BMSEvent>(); // For LN processing

            var noteEvents = events
                             .Where(e => isNoteChannel(e.Channel))
                             .OrderBy(e => e.Measure)
                             .ThenBy(e => e.Position)
                             .ToList();

            Logger.Log($"{BMS_LOG_PREFIX} Found {noteEvents.Count} note events", LoggingTarget.Runtime, LogLevel.Debug);

            foreach (var evt in noteEvents)
            {
                int column = getColumn(evt.Channel);
                bool isScratch = isScratchChannel(evt.Channel);
                bool isLongNote = isLongNoteChannel(evt.Channel);
                double time = eventTimes[evt];

                // Get keysound sample
                var samples = new List<HitSampleInfo>();

                if (wavDefinitions.TryGetValue(evt.Value, out string? wavFile))
                {
                    samples.Add(new ConvertHitObjectParser.FileHitSampleInfo(wavFile, 100));
                }

                if (isLongNote)
                {
                    // LN processing: pair start and end
                    if (activeHolds.TryGetValue(column, out var startEvent))
                    {
                        double startTime = eventTimes[startEvent];

                        result.Add(new BMSHoldNote
                        {
                            Column = column,
                            StartTime = startTime,
                            Duration = time - startTime,
                            IsScratch = isScratch,
                            Samples = samples,
                        });

                        activeHolds.Remove(column);
                    }
                    else
                    {
                        activeHolds[column] = evt;
                    }
                }
                else
                {
                    result.Add(new BMSNote
                    {
                        Column = column,
                        StartTime = time,
                        IsScratch = isScratch,
                        Samples = samples,
                    });
                }
            }

            // Update total keys based on used columns
            if (result.Count > 0)
            {
                totalKeys = result.Max(h => h.Column) + 1;

                // Log first note's samples for debugging
                var firstNote = result.First();
                string sampleInfo = firstNote.Samples.Count > 0
                    ? string.Join(", ", firstNote.Samples.Select(s => s is ConvertHitObjectParser.FileHitSampleInfo fs ? fs.Filename : s.Name))
                    : "(no samples)";
                Logger.Log($"[BMS] Created {result.Count} hit objects, first note samples: {sampleInfo}", LoggingTarget.Runtime, LogLevel.Debug);
            }

            return result;
        }

        private List<BmsBackgroundSoundEvent> createBackgroundSoundEvents(Dictionary<BMSEvent, double> eventTimes)
        {
            var result = new List<BmsBackgroundSoundEvent>();

            var backgroundEvents = events
                                   .Where(e => isBackgroundSoundChannel(e.Channel))
                                   .OrderBy(e => e.Measure)
                                   .ThenBy(e => e.Position)
                                   .ToList();

            foreach (var evt in backgroundEvents)
            {
                if (!wavDefinitions.TryGetValue(evt.Value, out string? wavFile))
                    continue;

                double time = eventTimes[evt];
                result.Add(new BmsBackgroundSoundEvent(time, wavFile));
            }

            return result;
        }

        private bool isNoteChannel(int channel)
        {
            // 1P visible: 11-19, 1P LN: 51-59
            // 2P visible: 21-29, 2P LN: 61-69
            return channel >= 11 && channel <= 19 ||
                   channel >= 21 && channel <= 29 ||
                   channel >= 51 && channel <= 59 ||
                   channel >= 61 && channel <= 69;
        }

        private bool isBackgroundSoundChannel(int channel)
        {
            if (isNoteChannel(channel))
                return false;

            if (channel == channel_measure_length ||
                channel == channel_bpm_change ||
                channel == channel_extended_bpm ||
                channel == channel_stop)
                return false;

            if (channel == channel_bga_base ||
                channel == channel_bga_poor ||
                channel == channel_bga_layer)
                return false;

            return true;
        }

        private bool isLongNoteChannel(int channel)
        {
            return channel >= 51 && channel <= 59 ||
                   channel >= 61 && channel <= 69;
        }

        private bool isScratchChannel(int channel)
        {
            // Channel 16/56 = 1P scratch, 26/66 = 2P scratch
            return channel == 16 || channel == 26 || channel == 56 || channel == 66;
        }

        private int getColumn(int channel)
        {
            // Map BMS channels to column indices
            // 1P: 11=0, 12=1, 13=2, 14=3, 15=4, 16=scratch, 18=5, 19=6
            // Standard 7-key mapping: 16, 11, 12, 13, 14, 15, 18, 19

            return channel switch
            {
                // 1P side (channels 11-19, 51-59)
                16 or 56 => 0, // Scratch
                11 or 51 => 1, // Key 1
                12 or 52 => 2, // Key 2
                13 or 53 => 3, // Key 3
                14 or 54 => 4, // Key 4
                15 or 55 => 5, // Key 5
                18 or 58 => 6, // Key 6
                19 or 59 => 7, // Key 7

                // 2P side (channels 21-29, 61-69)
                26 or 66 => 8, // Scratch 2
                21 or 61 => 9, // Key 8
                22 or 62 => 10, // Key 9
                23 or 63 => 11, // Key 10
                24 or 64 => 12, // Key 11
                25 or 65 => 13, // Key 12
                28 or 68 => 14, // Key 13
                29 or 69 => 15, // Key 14

                _ => 0
            };
        }

        private class BMSEvent
        {
            public int Sequence { get; set; }
            public int Measure { get; set; }
            public int Channel { get; set; }
            public double Position { get; set; }
            public string Value { get; set; } = string.Empty;
        }

        private class TimelineData
        {
            public List<TimingPointData> TimingPoints { get; set; } = new List<TimingPointData>();
            public Dictionary<BMSEvent, double> EventTimes { get; set; } = new Dictionary<BMSEvent, double>();
        }

        private class TimingPointData
        {
            public double Time { get; set; }
            public TimingControlPoint ControlPoint { get; set; } = new TimingControlPoint();
        }
    }
}
