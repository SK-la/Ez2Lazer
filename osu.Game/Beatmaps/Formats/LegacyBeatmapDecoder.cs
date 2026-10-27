// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Beatmaps.Timing;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit;
using osu.Game.Utils;

namespace osu.Game.Beatmaps.Formats
{
    public class LegacyBeatmapDecoder : LegacyDecoder<Beatmap>
    {
        /// <summary>
        /// An offset which needs to be applied to old beatmaps (v4 and lower) to correct timing changes that were applied at a game client level.
        /// </summary>
        public const int EARLY_VERSION_TIMING_OFFSET = 24;

        /// <summary>
        /// A small adjustment to the start time of sample control points to account for rounding/precision errors.
        /// </summary>
        /// <remarks>
        /// Compare: https://github.com/peppy/osu-stable-reference/blob/master/osu!/GameplayElements/HitObjects/HitObject.cs#L319
        /// </remarks>
        public const double CONTROL_POINT_LENIENCY = 5;

        /// <summary>
        /// The maximum allowed number of keys in mania beatmaps.
        /// </summary>
        public const int MAX_MANIA_KEY_COUNT = 18;

        internal static RulesetStore? RulesetStore;

        private Beatmap beatmap = null!;
        private ConvertHitObjectParser parser = null!;

        private LegacySampleBank defaultSampleBank;
        private int defaultSampleVolume = 100;

        public static void Register()
        {
            AddDecoder<Beatmap>(@"osu file format v", m => new LegacyBeatmapDecoder(Parsing.ParseInt(m.Split('v').Last())));
            SetFallbackDecoder<Beatmap>(() => new LegacyBeatmapDecoder());
        }

        /// <summary>
        /// Whether beatmap or runtime offsets should be applied. Defaults on; only disable for testing purposes.
        /// </summary>
        public bool ApplyOffsets = true;

        /// <summary>
        /// 是否完全跳过 hitobject。
        /// </summary>
        /// <remarks>
        /// 对齐 osu!stable 的仅读头信息。导入用此读元数据，避免大谱面文件完整物化两次；
        /// 难度与游玩始终需要真实物件。
        /// </remarks>
        public bool SkipHitObjects { get; set; }

        private readonly int offset;

        public LegacyBeatmapDecoder(int version = LATEST_VERSION)
            : base(version)
        {
            if (RulesetStore == null)
            {
                Logger.Log($"A {nameof(RulesetStore)} was not provided via {nameof(Decoder)}.{nameof(RegisterDependencies)}; falling back to default {nameof(AssemblyRulesetStore)}.");
                RulesetStore = new AssemblyRulesetStore();
            }

            offset = FormatVersion < 5 ? EARLY_VERSION_TIMING_OFFSET : 0;
        }

        protected override Beatmap CreateTemplateObject()
        {
            var templateBeatmap = base.CreateTemplateObject();
            templateBeatmap.ControlPointInfo = new LegacyControlPointInfo();
            return templateBeatmap;
        }

        protected override void ParseStreamInto(LineBufferedReader stream, bool isPrimaryStream, Beatmap beatmap)
        {
            this.beatmap = beatmap;
            this.beatmap.BeatmapVersion = FormatVersion;
            parser = new ConvertHitObjectParser(getOffsetTime(), FormatVersion);

            ApplyLegacyDefaults(this.beatmap);

            base.ParseStreamInto(stream, isPrimaryStream, beatmap);

            applyDifficultyRestrictions(beatmap.Difficulty, beatmap);

            flushPendingPoints();

            // Objects may be out of order *only* if a user has manually edited an .osu file.
            // Unfortunately there are ranked maps in this state (example: https://osu.ppy.sh/s/594828).
            // OrderBy is used to guarantee that the parsing order of hitobjects with equal start times is maintained (stably-sorted)
            // The parsing order of hitobjects matters in mania difficulty calculation
            this.beatmap.HitObjects = this.beatmap.HitObjects.OrderBy(h => h.StartTime).ToList();

            postProcessBreaks(this.beatmap);

            foreach (var hitObject in this.beatmap.HitObjects)
            {
                applyDefaults(hitObject);
                applySamples(hitObject);
            }
        }

        /// <summary>
        /// Ensures that all <see cref="BeatmapDifficulty"/> settings are within the allowed ranges.
        /// See also: https://github.com/peppy/osu-stable-reference/blob/0e425c0d525ef21353c8293c235cc0621d28338b/osu!/GameplayElements/Beatmaps/Beatmap.cs#L567-L614
        /// </summary>
        private static void applyDifficultyRestrictions(BeatmapDifficulty difficulty, Beatmap beatmap)
        {
            difficulty.DrainRate = Math.Clamp(difficulty.DrainRate, 0, 10);

            // mania uses "circle size" for key count, thus different allowable range
            difficulty.CircleSize = beatmap.BeatmapInfo.Ruleset.OnlineID != 3
                ? Math.Clamp(difficulty.CircleSize, 0, 10)
                : Math.Clamp(difficulty.CircleSize, 1, MAX_MANIA_KEY_COUNT);

            difficulty.OverallDifficulty = Math.Clamp(difficulty.OverallDifficulty, 0, 10);
            difficulty.ApproachRate = Math.Clamp(difficulty.ApproachRate, 0, 10);

            difficulty.SliderMultiplier = Math.Clamp(difficulty.SliderMultiplier, 0.4, 3.6);
            difficulty.SliderTickRate = Math.Clamp(difficulty.SliderTickRate, 0.5, 8);
        }

        /// <summary>
        /// Processes the beatmap such that a new combo is started the first hitobject following each break.
        /// </summary>
        private static void postProcessBreaks(Beatmap beatmap)
        {
            int currentBreak = 0;
            bool forceNewCombo = false;

            foreach (var h in beatmap.HitObjects.OfType<ConvertHitObject>())
            {
                while (currentBreak < beatmap.Breaks.Count && beatmap.Breaks[currentBreak].EndTime < h.StartTime)
                {
                    forceNewCombo = true;
                    currentBreak++;
                }

                h.NewCombo |= forceNewCombo;
                forceNewCombo = false;
            }
        }

        private void applyDefaults(HitObject hitObject)
        {
            DifficultyControlPoint difficultyControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.DifficultyPointAt(hitObject.StartTime) ?? DifficultyControlPoint.DEFAULT;

            if (hitObject is IHasGenerateTicks hasGenerateTicks)
                hasGenerateTicks.GenerateTicks = difficultyControlPoint.GenerateTicks;

            if (hitObject is IHasSliderVelocity hasSliderVelocity)
                hasSliderVelocity.SliderVelocityMultiplier = difficultyControlPoint.SliderVelocity;

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        private void applySamples(HitObject hitObject)
        {
            if (hitObject is IHasRepeats hasRepeats)
            {
                SampleControlPoint sampleControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.SamplePointAt(hitObject.StartTime + CONTROL_POINT_LENIENCY + 1)
                                                        ?? SampleControlPoint.DEFAULT;
                hitObject.Samples = hitObject.Samples.Select(sampleControlPoint.ApplyTo).ToList();

                for (int i = 0; i < hasRepeats.NodeSamples.Count; i++)
                {
                    double time = hitObject.StartTime + i * hasRepeats.Duration / hasRepeats.SpanCount() + CONTROL_POINT_LENIENCY;
                    var nodeSamplePoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.SamplePointAt(time) ?? SampleControlPoint.DEFAULT;

                    hasRepeats.NodeSamples[i] = hasRepeats.NodeSamples[i].Select(nodeSamplePoint.ApplyTo).ToList();
                }
            }
            else
            {
                SampleControlPoint sampleControlPoint = (beatmap.ControlPointInfo as LegacyControlPointInfo)?.SamplePointAt(hitObject.GetEndTime() + CONTROL_POINT_LENIENCY)
                                                        ?? SampleControlPoint.DEFAULT;
                hitObject.Samples = hitObject.Samples.Select(sampleControlPoint.ApplyTo).ToList();
            }
        }

        /// <summary>
        /// Some `BeatmapInfo` members have default values that differ from the default values used by stable.
        /// In addition, legacy beatmaps will sometimes not contain some configuration keys, in which case
        /// the legacy default values should be used.
        /// This method's intention is to restore those legacy defaults.
        /// See also: https://osu.ppy.sh/wiki/en/Client/File_formats/Osu_%28file_format%29
        /// </summary>
        internal static void ApplyLegacyDefaults(Beatmap beatmap)
        {
            beatmap.WidescreenStoryboard = false;
            // in a perfect world this would throw if osu! ruleset couldn't be found,
            // but unfortunately there are "legitimate" cases where it's not there (i.e. ruleset test projects),
            // so attempt to trudge on with whatever it is that's in `BeatmapInfo` if the lookup fails.
            beatmap.BeatmapInfo.Ruleset = RulesetStore?.GetRuleset(0) ?? beatmap.BeatmapInfo.Ruleset;
        }

        protected override void ParseLine(Beatmap beatmap, Section section, string line, bool isPrimaryStream)
        {
            switch (section)
            {
                case Section.General:
                    handleGeneral(line);
                    return;

                case Section.Editor:
                    handleEditor(line);
                    return;

                case Section.Metadata:
                    handleMetadata(line);
                    return;

                case Section.Difficulty:
                    handleDifficulty(line);
                    return;

                case Section.Events:
                    handleEvent(line);
                    return;

                case Section.TimingPoints:
                    handleTimingPoint(line);
                    return;

                case Section.HitObjects:
                    handleHitObject(line);
                    return;
            }

            base.ParseLine(beatmap, section, line, isPrimaryStream);
        }

        private void handleGeneral(string line)
        {
            var pair = SplitKeyVal(line);

            var metadata = beatmap.BeatmapInfo.Metadata;

            switch (pair.Key)
            {
                case @"AudioFilename":
                    metadata.AudioFile = pair.Value.ToStandardisedPath();
                    break;

                case @"AudioLeadIn":
                    beatmap.AudioLeadIn = Parsing.ParseInt(pair.Value);
                    break;

                case @"PreviewTime":
                    int time = Parsing.ParseInt(pair.Value);
                    metadata.PreviewTime = time == -1 ? time : getOffsetTime(time);
                    break;

                case @"SampleSet":
                    defaultSampleBank = Enum.Parse<LegacySampleBank>(pair.Value);
                    break;

                case @"SampleVolume":
                    defaultSampleVolume = Parsing.ParseInt(pair.Value);
                    break;

                case @"StackLeniency":
                    beatmap.StackLeniency = Parsing.ParseFloat(pair.Value);
                    break;

                case @"Mode":
                    beatmap.BeatmapInfo.Ruleset = RulesetStore?.GetRuleset(Parsing.ParseInt(pair.Value)) ?? throw new ArgumentException("Ruleset is not available locally.");
                    break;

                case @"LetterboxInBreaks":
                    beatmap.LetterboxInBreaks = Parsing.ParseInt(pair.Value) == 1;
                    break;

                case @"SpecialStyle":
                    beatmap.SpecialStyle = Parsing.ParseInt(pair.Value) == 1;
                    break;

                case @"WidescreenStoryboard":
                    beatmap.WidescreenStoryboard = Parsing.ParseInt(pair.Value) == 1;
                    break;

                case @"EpilepsyWarning":
                    beatmap.EpilepsyWarning = Parsing.ParseInt(pair.Value) == 1;
                    break;

                case @"SamplesMatchPlaybackRate":
                    beatmap.SamplesMatchPlaybackRate = Parsing.ParseInt(pair.Value) == 1;
                    break;

                case @"Countdown":
                    beatmap.Countdown = Enum.Parse<CountdownType>(pair.Value);
                    break;

                case @"CountdownOffset":
                    beatmap.CountdownOffset = Parsing.ParseInt(pair.Value);
                    break;
            }
        }

        private void handleEditor(string line)
        {
            var pair = SplitKeyVal(line);

            switch (pair.Key)
            {
                case @"Bookmarks":
                    beatmap.Bookmarks = pair.Value.Split(',').Select(v =>
                    {
                        bool result = int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int val);
                        return new { result, val };
                    }).Where(p => p.result).Select(p => p.val).ToArray();
                    break;

                case @"VelocityPresets":
                    beatmap.SliderVelocityPresets = pair.Value.Split(',').Select(v =>
                    {
                        bool result = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double val);
                        return new { result, val };
                    }).Where(p => p.result).Select(p => p.val).ToArray();
                    break;

                case @"DistanceSpacing":
                    beatmap.DistanceSpacing = Math.Max(0, Parsing.ParseDouble(pair.Value));
                    break;

                case @"BeatDivisor":
                    beatmap.BeatmapInfo.BeatDivisor = Math.Clamp(Parsing.ParseInt(pair.Value), BindableBeatDivisor.MINIMUM_DIVISOR, BindableBeatDivisor.MAXIMUM_DIVISOR);
                    break;

                case @"GridSize":
                    beatmap.GridSize = Parsing.ParseInt(pair.Value);
                    break;

                case @"TimelineZoom":
                    beatmap.TimelineZoom = Math.Max(0, Parsing.ParseDouble(pair.Value));
                    break;
            }
        }

        private void handleMetadata(string line)
        {
            var pair = SplitKeyVal(line);

            var metadata = beatmap.BeatmapInfo.Metadata;

            switch (pair.Key)
            {
                case @"Title":
                    metadata.Title = pair.Value;
                    break;

                case @"TitleUnicode":
                    metadata.TitleUnicode = pair.Value;
                    break;

                case @"Artist":
                    metadata.Artist = pair.Value;
                    break;

                case @"ArtistUnicode":
                    metadata.ArtistUnicode = pair.Value;
                    break;

                case @"Creator":
                    metadata.Author.Username = pair.Value;
                    break;

                case @"Version":
                    beatmap.BeatmapInfo.DifficultyName = pair.Value;
                    break;

                case @"Source":
                    metadata.Source = pair.Value;
                    break;

                case @"Tags":
                    metadata.Tags = pair.Value;
                    break;

                case @"BeatmapID":
                    beatmap.BeatmapInfo.OnlineID = Parsing.ParseInt(pair.Value);
                    break;

                case @"BeatmapSetID":
                    beatmap.BeatmapInfo.BeatmapSet = new BeatmapSetInfo { OnlineID = Parsing.ParseInt(pair.Value) };
                    break;
            }
        }

        private void handleDifficulty(string line)
        {
            var pair = SplitKeyVal(line);

            var difficulty = beatmap.Difficulty;

            switch (pair.Key)
            {
                case @"HPDrainRate":
                    difficulty.DrainRate = Parsing.ParseFloat(pair.Value);
                    break;

                case @"CircleSize":
                    difficulty.CircleSize = Parsing.ParseFloat(pair.Value);
                    break;

                case @"OverallDifficulty":
                    difficulty.OverallDifficulty = Parsing.ParseFloat(pair.Value);
                    if (!hasApproachRate)
                        difficulty.ApproachRate = difficulty.OverallDifficulty;
                    break;

                case @"ApproachRate":
                    difficulty.ApproachRate = Parsing.ParseFloat(pair.Value);
                    hasApproachRate = true;
                    break;

                case @"SliderMultiplier":
                    difficulty.SliderMultiplier = Parsing.ParseDouble(pair.Value);
                    break;

                case @"SliderTickRate":
                    difficulty.SliderTickRate = Parsing.ParseDouble(pair.Value);
                    break;
            }
        }

        private void handleEvent(string line)
        {
            string[] split = line.Split(',');

            if (Enum.TryParse(split[0], out LegacyEventType type))
            {
                switch (type)
                {
                    case LegacyEventType.Sprite:
                        // Generally, the background is the first thing defined in a beatmap file.
                        // In some older beatmaps, it is not present and replaced by a storyboard-level background instead.
                        // Allow the first sprite (by file order) to act as the background in such cases.
                        if (string.IsNullOrEmpty(beatmap.BeatmapInfo.Metadata.BackgroundFile))
                        {
                            beatmap.BeatmapInfo.Metadata.BackgroundFile = CleanFilename(split[3]);
                        }

                        break;

                    case LegacyEventType.Video:
                        string filename = CleanFilename(split[2]);

                        // Some very old beatmaps had incorrect type specifications for their backgrounds (ie. using 1 for VIDEO
                        // instead of 0 for BACKGROUND). To handle this gracefully, check the file extension against known supported
                        // video extensions and handle similar to a background if it doesn't match.
                        if (!SupportedExtensions.VIDEO_EXTENSIONS.Contains(Path.GetExtension(filename).ToLowerInvariant()))
                        {
                            beatmap.BeatmapInfo.Metadata.BackgroundFile = filename;
                        }

                        break;

                    case LegacyEventType.Background:
                        beatmap.BeatmapInfo.Metadata.BackgroundFile = CleanFilename(split[2]);
                        break;

                    case LegacyEventType.Break:
                        double start = getOffsetTime(Parsing.ParseDouble(split[1]));
                        double end = Math.Max(start, getOffsetTime(Parsing.ParseDouble(split[2])));

                        beatmap.Breaks.Add(new BreakPeriod(start, end));
                        break;
                }
            }
        }

        private void handleTimingPoint(string line)
        {
            string[] split = line.Split(',');

            // Aspire 与损坏导出常写出远超 stable 的 timing 字段：超出 32 位的时间、负拍号分子、未定义采样组。
            // 拒绝单个字段会丢掉整条控制点及其全部 timing 变更，因此宽松解析并钳制。
            double time = getOffsetTime(Parsing.ParseDouble(split[0].Trim(), parseLimit: double.MaxValue));

            // beatLength is allowed to be NaN to handle an edge case in which some beatmaps use NaN slider velocity to disable slider tick generation (see LegacyDifficultyControlPoint).
            // BMS / 损坏导出可能写出超出固定解析上限的 beatLength（如 6E+16）；接受有限 double，赋值时再钳制。
            double beatLength = Parsing.ParseDouble(split[1].Trim(), parseLimit: double.MaxValue, allowNaN: true);

            // stable 的 BeatLengthAt 仅在继承值为负时相乘。NaN 不满足该判断，
            // 因此保留红线拍长，本点只用于抑制 tick。
            double speedMultiplier = stableSpeedMultiplier(beatLength);

            TimeSignature timeSignature = TimeSignature.SimpleQuadruple;

            if (split.Length >= 3 && tryParseLenientInt(split[2], out int numerator) && numerator > 0)
                timeSignature = numerator == 4 ? TimeSignature.SimpleQuadruple : new TimeSignature(numerator);

            LegacySampleBank sampleSet = defaultSampleBank;

            if (split.Length >= 4 && tryParseLenientInt(split[3], out int sampleBank) && Enum.IsDefined(typeof(LegacySampleBank), sampleBank))
                sampleSet = (LegacySampleBank)sampleBank;

            int customSampleBank = 0;
            if (split.Length >= 5 && tryParseLenientInt(split[4], out int parsedCustomSampleBank))
                customSampleBank = Math.Max(0, parsedCustomSampleBank);

            int sampleVolume = defaultSampleVolume;
            if (split.Length >= 6 && tryParseLenientInt(split[5], out int parsedVolume))
                sampleVolume = Math.Clamp(parsedVolume, 0, 100);

            bool timingChange = true;
            if (split.Length >= 7 && split[6].Length > 0)
                timingChange = split[6][0] == '1';

            bool kiaiMode = false;
            bool omitFirstBarSignature = false;

            if (split.Length >= 8 && tryParseLenientInt(split[7], out int effectFlagsValue))
            {
                var effectFlags = (LegacyEffectFlags)effectFlagsValue;
                kiaiMode = effectFlags.HasFlag(LegacyEffectFlags.Kiai);
                omitFirstBarSignature = effectFlags.HasFlag(LegacyEffectFlags.OmitFirstBarLine);
            }

            string stringSampleSet = sampleSet.ToString().ToLowerInvariant();
            if (stringSampleSet == @"none")
                stringSampleSet = HitSampleInfo.BANK_NORMAL;

            if (timingChange)
            {
                if (double.IsNaN(beatLength))
                    throw new InvalidDataException("Beat length cannot be NaN in a timing control point");

                var controlPoint = CreateTimingControlPoint();

                // bindable 下限为 0.6ms。stable 保留写入值；Aspire 从此字段还原。
                controlPoint.ParsedBeatLength = beatLength;
                controlPoint.BeatLength = Math.Clamp(beatLength, controlPoint.BeatLengthBindable.MinValue, controlPoint.BeatLengthBindable.MaxValue);
                controlPoint.TimeSignature = timeSignature;
                controlPoint.OmitFirstBarLine = omitFirstBarSignature;

                addControlPoint(time, controlPoint, true);
            }

            int onlineRulesetID = beatmap.BeatmapInfo.Ruleset.OnlineID;

            addControlPoint(time, new DifficultyControlPoint
            {
                GenerateTicks = !double.IsNaN(beatLength),
                SliderVelocity = speedMultiplier,
            }, timingChange);

            var effectPoint = new EffectControlPoint
            {
                KiaiMode = kiaiMode,
            };

            // osu!taiko and osu!mania use effect points rather than difficulty points for scroll speed adjustments.
            if (onlineRulesetID == 1 || onlineRulesetID == 3)
                effectPoint.ScrollSpeed = speedMultiplier;

            addControlPoint(time, effectPoint, timingChange);

            addControlPoint(time, new LegacySampleControlPoint
            {
                SampleBank = stringSampleSet,
                SampleVolume = sampleVolume,
                CustomSampleBank = customSampleBank,
            }, timingChange);
        }

        private readonly List<ControlPoint> pendingControlPoints = new List<ControlPoint>();
        private readonly HashSet<Type> pendingControlPointTypes = new HashSet<Type>();
        private double pendingControlPointsTime;
        private bool hasApproachRate;

        private void addControlPoint(double time, ControlPoint point, bool timingChange)
        {
            if (time != pendingControlPointsTime)
                flushPendingPoints();

            if (timingChange)
                pendingControlPoints.Insert(0, point);
            else
                pendingControlPoints.Add(point);

            pendingControlPointsTime = time;
        }

        private void flushPendingPoints()
        {
            // Changes from non-timing-points are added to the end of the list (see addControlPoint()) and should override any changes from timing-points (added to the start of the list).
            for (int i = pendingControlPoints.Count - 1; i >= 0; i--)
            {
                var type = pendingControlPoints[i].GetType();
                if (!pendingControlPointTypes.Add(type))
                    continue;

                beatmap.ControlPointInfo.Add(pendingControlPointsTime, pendingControlPoints[i]);
            }

            pendingControlPoints.Clear();
            pendingControlPointTypes.Clear();
        }

        private void handleHitObject(string line)
        {
            if (SkipHitObjects)
                return;

            var obj = parser.Parse(line);
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            beatmap.HitObjects.Add(obj);
        }

        private int getOffsetTime(int time) => time + (ApplyOffsets ? offset : 0);

        /// <summary>
        /// 难度点上存储的滑条速度。10–1000 的钳制仍在
        /// <see cref="LegacyRulesetExtensions.GetPrecisionAdjustedBeatLength"/>，该方法期望此未钳制比值。
        /// </summary>
        /// <remarks>
        /// stable 的 <c>BeatLengthAt</c> 仅在继承值为负时相乘。NaN 非负，
        /// 因此贡献 1，由独立的 <c>GenerateTicks</c> 标志关闭 tick 生成。
        /// </remarks>
        private static double stableSpeedMultiplier(double beatLength)
        {
            if (!(beatLength < 0))
                return 1;

            return 100.0 / -beatLength;
        }

        /// <summary>
        /// 解析 timing 点的辅助字段，且不因该字段失败而丢掉整条控制点。
        /// </summary>
        /// <remarks>
        /// 超出 32 位范围的值钳制而非拒绝：该字段只影响元数据（小节线、采样组、音量），
        /// 丢掉整行也会丢掉它所携带的 timing 与滑条速度变更。
        /// </remarks>
        private static bool tryParseLenientInt(string value, out int result)
        {
            result = 0;

            if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return false;

            if (double.IsNaN(parsed))
                return false;

            result = (int)Math.Clamp(parsed, int.MinValue, int.MaxValue);
            return true;
        }

        private double getOffsetTime() => ApplyOffsets ? offset : 0;

        private double getOffsetTime(double time) => time + (ApplyOffsets ? offset : 0);

        protected virtual TimingControlPoint CreateTimingControlPoint() => new TimingControlPoint();
    }
}
