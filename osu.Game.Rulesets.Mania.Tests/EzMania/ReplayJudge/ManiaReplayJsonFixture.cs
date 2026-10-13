// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using osu.Framework.IO.Stores;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 测试用高精度回放 JSON（double 绝对时间 + keys bitmask）。
    /// 局内 Drawable≡Session 门禁用此格式；禁止经 Legacy Round / osr。
    /// </summary>
    internal static class ManiaReplayJsonFixture
    {
        /// <summary>设为 1/true 时，parity TestScene 写出 ReplayJsonDump。</summary>
        public const string DUMP_ENV = "EZ_DUMP_REPLAY_JSON";

        private static readonly JsonSerializerOptions json_read_options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly JsonSerializerOptions json_pretty_options = createWriteOptions(indented: true);
        private static readonly JsonSerializerOptions json_compact_options = createWriteOptions(indented: false);

        private static JsonSerializerOptions createWriteOptions(bool indented) => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static bool DumpEnabled
            => parseEnvBool(Environment.GetEnvironmentVariable(DUMP_ENV));

        public static Document Read(Stream stream)
        {
            var doc = JsonSerializer.Deserialize<Document>(stream, json_read_options)
                      ?? throw new InvalidDataException("ReplayJson deserialize returned null");
            validate(doc);
            return doc;
        }

        public static Document ReadResource(string resourcePath)
        {
            using var stream = new DllResourceStore(typeof(ManiaReplayJsonFixture).Assembly).GetStream(resourcePath)
                               ?? throw new FileNotFoundException($"ReplayJson resource missing: {resourcePath}");
            return Read(stream);
        }

        public static void Write(string path, Document document, bool? compact = null)
        {
            validate(document);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            bool useCompact = compact ?? !string.IsNullOrWhiteSpace(document.BeatmapResource);
            var options = useCompact ? json_compact_options : json_pretty_options;
            File.WriteAllText(path, JsonSerializer.Serialize(document, options));
        }

        public static void Write(
            string path,
            GameplayEnvironment environment,
            int columns,
            IReadOnlyList<ManiaHitObject> hitObjects,
            IReadOnlyList<ReplayFrame> frames,
            string? beatmapResource = null,
            bool? compact = null)
        {
            Write(path, FromParts(environment, columns, hitObjects, frames, beatmapResource), compact);
        }

        public static Document FromParts(
            GameplayEnvironment environment,
            int columns,
            IReadOnlyList<ManiaHitObject> hitObjects,
            IReadOnlyList<ReplayFrame> frames,
            string? beatmapResource = null)
        {
            var doc = new Document
            {
                HitMode = environment.ManiaHitMode.ToString(),
                HealthMode = environment.ManiaHealthMode.ToString(),
                JudgePrecedence = environment.JudgePrecedence.ToString(),
                Columns = columns,
                BeatmapResource = beatmapResource,
                FrameSource = string.IsNullOrWhiteSpace(beatmapResource) ? "synthetic-subms" : "osr-decoded",
                // 全谱夹具用 beatmapResource 挂真实 OD/timing；hitObjects 可省略以控体积。
                HitObjects = string.IsNullOrWhiteSpace(beatmapResource)
                    ? hitObjects.Select(toHitObjectDto).ToList()
                    : new List<HitObjectDto>(),
                Frames = frames.OfType<ManiaReplayFrame>().Select(toFrameDto).ToList(),
            };
            validate(doc);
            return doc;
        }

        public static (GameplayEnvironment Environment, int Columns, List<ManiaHitObject> HitObjects, List<ReplayFrame> Frames, Score Score)
            ToParts(Document document)
        {
            validate(document);

            var environment = ReplayJudgeTestConfig.Create(
                Enum.Parse<EzEnumHitMode>(document.HitMode, ignoreCase: true),
                Enum.Parse<EzEnumHealthMode>(document.HealthMode, ignoreCase: true),
                Enum.Parse<EzEnumJudgePrecedence>(document.JudgePrecedence ?? nameof(EzEnumJudgePrecedence.Earliest), ignoreCase: true));

            var hitObjects = (document.HitObjects).Select(toHitObject).ToList();
            var frames = document.Frames.Select(toFrame).Cast<ReplayFrame>().ToList();

            var score = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Mods = Array.Empty<Mod>(),
                },
                Replay = new Replay { Frames = frames },
            };
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, environment);

            return (environment, document.Columns, hitObjects, frames, score);
        }

        /// <summary>
        /// 计划契约名：合成夹具 → Score + 内存谱面信息；全谱仍需 <see cref="Document.BeatmapResource"/> 另载 .osu。
        /// </summary>
        public static (Score Score, List<ManiaHitObject> HitObjects, int Columns, GameplayEnvironment Environment)
            ToScoreAndBeatmap(Document document)
        {
            var (environment, columns, hitObjects, _, score) = ToParts(document);
            return (score, hitObjects, columns, environment);
        }

        private static bool parseEnvBool(string? value)
            => !string.IsNullOrWhiteSpace(value)
               && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                                || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

        private static void validate(Document document)
        {
            if (document.Columns < 1)
                throw new InvalidDataException("ReplayJson columns must be >= 1");

            bool hasBeatmap = !string.IsNullOrWhiteSpace(document.BeatmapResource);
            bool hasHitObjects = document.HitObjects != null && document.HitObjects.Count > 0;

            if (!hasBeatmap && !hasHitObjects)
                throw new InvalidDataException("ReplayJson requires hitObjects or beatmapResource");

            if (document.Frames == null)
                throw new InvalidDataException("ReplayJson frames required");

            if (string.IsNullOrWhiteSpace(document.HitMode) || string.IsNullOrWhiteSpace(document.HealthMode))
                throw new InvalidDataException("ReplayJson hitMode/healthMode required");
        }

        private static HitObjectDto toHitObjectDto(ManiaHitObject obj) => obj switch
        {
            HoldNote hold => new HitObjectDto
            {
                Type = "Hold",
                Time = hold.StartTime,
                End = hold.EndTime,
                Column = hold.Column,
            },
            Note note => new HitObjectDto
            {
                Type = "Note",
                Time = note.StartTime,
                Column = note.Column,
            },
            _ => throw new NotSupportedException($"Unsupported hit object for ReplayJson: {obj.GetType().Name}"),
        };

        private static FrameDto toFrameDto(ManiaReplayFrame frame)
        {
            int keys = 0;

            foreach (var action in frame.Actions)
                keys |= 1 << (int)action;

            return new FrameDto { T = frame.Time, Keys = keys };
        }

        private static ManiaHitObject toHitObject(HitObjectDto dto)
        {
            return dto.Type.ToUpperInvariant() switch
            {
                "NOTE" => new Note { StartTime = dto.Time, Column = dto.Column },
                "HOLD" => new HoldNote
                {
                    StartTime = dto.Time,
                    Duration = (dto.End ?? throw new InvalidDataException("Hold requires end")) - dto.Time,
                    Column = dto.Column,
                },
                _ => throw new InvalidDataException($"Unknown hitObject type: {dto.Type}"),
            };
        }

        private static ManiaReplayFrame toFrame(FrameDto dto)
        {
            var actions = new List<ManiaAction>();
            int keys = dto.Keys;
            var action = ManiaAction.Key1;

            while (keys > 0)
            {
                if ((keys & 1) != 0)
                    actions.Add(action);

                action++;
                keys >>= 1;
            }

            return new ManiaReplayFrame(dto.T, actions.ToArray());
        }

        internal sealed class Document
        {
            public string HitMode { get; set; } = string.Empty;
            public string HealthMode { get; set; } = string.Empty;
            public string? JudgePrecedence { get; set; }
            public int Columns { get; set; } = 4;

            /// <summary>
            /// 可选：嵌入 .osu 资源路径。全谱夹具用此挂真实难度/timing；此时 hitObjects 可空。
            /// </summary>
            public string? BeatmapResource { get; set; }

            /// <summary>
            /// 帧来源标记：<c>synthetic-subms</c>（合成亚毫秒）/ <c>osr-decoded</c>（osr 整数档 double）。
            /// </summary>
            public string? FrameSource { get; set; }

            public List<HitObjectDto> HitObjects { get; set; } = new List<HitObjectDto>();
            public List<FrameDto> Frames { get; set; } = new List<FrameDto>();
        }

        internal sealed class HitObjectDto
        {
            public string Type { get; set; } = string.Empty;
            public double Time { get; set; }
            public double? End { get; set; }
            public int Column { get; set; }
        }

        internal sealed class FrameDto
        {
            public double T { get; set; }
            public int Keys { get; set; }
        }
    }
}
