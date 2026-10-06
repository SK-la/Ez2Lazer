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
        private static readonly JsonSerializerOptions json_options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static Document Read(Stream stream)
        {
            var doc = JsonSerializer.Deserialize<Document>(stream, json_options)
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

        public static void Write(string path, Document document)
        {
            validate(document);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(document, json_options));
        }

        public static void Write(
            string path,
            GameplayEnvironment environment,
            int columns,
            IReadOnlyList<ManiaHitObject> hitObjects,
            IReadOnlyList<ReplayFrame> frames)
        {
            Write(path, FromParts(environment, columns, hitObjects, frames));
        }

        public static Document FromParts(
            GameplayEnvironment environment,
            int columns,
            IReadOnlyList<ManiaHitObject> hitObjects,
            IReadOnlyList<ReplayFrame> frames)
        {
            var doc = new Document
            {
                HitMode = environment.ManiaHitMode.ToString(),
                HealthMode = environment.ManiaHealthMode.ToString(),
                JudgePrecedence = environment.JudgePrecedence.ToString(),
                Columns = columns,
                HitObjects = hitObjects.Select(toHitObjectDto).ToList(),
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

            var hitObjects = document.HitObjects.Select(toHitObject).ToList();
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

        private static void validate(Document document)
        {
            if (document.Columns < 1)
                throw new InvalidDataException("ReplayJson columns must be >= 1");

            if (document.HitObjects == null || document.HitObjects.Count == 0)
                throw new InvalidDataException("ReplayJson hitObjects required");

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
