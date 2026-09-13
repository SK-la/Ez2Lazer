// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using MinaCalc;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzMinaCalcFacadeTest
    {
        [Test]
        public void Msd_is_stable_and_goal_insensitive()
        {
            var notes = createStreamNotes();

            using var calc = new EzMinaCalcFacade();
            var a = calc.CalculateMsd(notes, 1f);
            var b = calc.CalculateMsd(notes, 1f);

            Assert.That(a.Overall, Is.GreaterThan(0));
            Assert.That(a.Overall, Is.EqualTo(b.Overall).Within(1e-4));
            Assert.That(a.Stream, Is.GreaterThan(0));
        }

        [Test]
        public void Ssr_increases_with_higher_goal()
        {
            var notes = createStreamNotes();

            using var calc = new EzMinaCalcFacade();
            var low = calc.CalculateSsr(notes, 1f, 0.93f);
            var high = calc.CalculateSsr(notes, 1f, 0.99f);

            Assert.That(high.Overall, Is.GreaterThan(low.Overall));
        }

        [Test]
        public void Msd_and_ssr_are_distinct_modes()
        {
            var notes = createStreamNotes();

            using var calc = new EzMinaCalcFacade();
            var msd = calc.CalculateMsd(notes, 1f);
            var ssr = calc.CalculateSsr(notes, 1f, 0.93f);

            // Same notes: modes must not produce identical vectors (proves we did not call one path for both).
            Assert.That(Math.Abs(msd.Overall - ssr.Overall), Is.GreaterThan(0.01));
        }

        [Test]
        public void Engine_version_is_positive()
        {
            Assert.That(EzMinaCalcFacade.EngineVersion, Is.GreaterThan(0));
        }

        [Test]
        public void File_hint_prefers_hash_over_path()
        {
            Assert.That(EzMinaCalcFacade.BuildOsuFileHint("abc123", 6), Is.EqualTo("abc123.osu"));
            Assert.That(EzMinaCalcFacade.BuildOsuFileHint(string.Empty, 4), Is.EqualTo("4k.osu"));
        }

        [Test]
        public void Msd_from_osu_text_rates_7k()
        {
            string chart = buildManiaChart(keyCount: 7, rows: 40);

            using var calc = new EzMinaCalcFacade();
            var msd = calc.CalculateMsdFromOsuText(chart, "7k.osu");

            Assert.That(msd.Overall, Is.GreaterThan(0));
            Assert.That(EzMinaCalcFacade.SupportsOsuTextKeyCount(7), Is.True);
            Assert.That(EzMinaCalcFacade.SupportsNoteArrayKeyCount(7), Is.False);
        }

        [Test]
        public void Note_array_7k_yields_zero_on_current_package()
        {
            var notes = new MinaCalcNote[32];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = new MinaCalcNote
                {
                    Notes = 1u << (i % 7),
                    RowTime = i * 0.1f,
                };
            }

            using var calc = new EzMinaCalcFacade();
            var msd = calc.CalculateMsd(notes, 1f);

            // Documents why MSD backfill must use FromOsuText for non-4K.
            Assert.That(msd.Overall, Is.EqualTo(0));
            Assert.That(msd.Stream, Is.EqualTo(0));
        }

        private static string buildManiaChart(int keyCount, int rows)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("osu file format v14");
            sb.AppendLine();
            sb.AppendLine("[General]");
            sb.AppendLine("AudioFilename: virtual");
            sb.AppendLine("Mode: 3");
            sb.AppendLine();
            sb.AppendLine("[Metadata]");
            sb.AppendLine("Title:t");
            sb.AppendLine("TitleUnicode:t");
            sb.AppendLine("Artist:a");
            sb.AppendLine("ArtistUnicode:a");
            sb.AppendLine("Creator:c");
            sb.AppendLine($"Version:{keyCount}k");
            sb.AppendLine("Source:");
            sb.AppendLine("Tags:");
            sb.AppendLine();
            sb.AppendLine("[Difficulty]");
            sb.AppendLine("HPDrainRate:8");
            sb.AppendLine($"CircleSize:{keyCount}");
            sb.AppendLine("OverallDifficulty:8");
            sb.AppendLine("ApproachRate:5");
            sb.AppendLine("SliderMultiplier:1.4");
            sb.AppendLine("SliderTickRate:1");
            sb.AppendLine();
            sb.AppendLine("[TimingPoints]");
            sb.AppendLine("0,500,4,2,0,100,1,0");
            sb.AppendLine();
            sb.AppendLine("[HitObjects]");

            for (int i = 0; i < rows; i++)
            {
                int col = i % keyCount;
                int x = (int)Math.Floor((col + 0.5) * 512.0 / keyCount);
                sb.AppendLine($"{x},192,{i * 100},1,0,0:0:0:0:");
            }

            return sb.ToString();
        }

        private static MinaCalcNote[] createStreamNotes()
        {
            var notes = new MinaCalcNote[32];

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = new MinaCalcNote
                {
                    Notes = 1u << (i % 4),
                    RowTime = i * 0.1f,
                };
            }

            return notes;
        }
    }

    [TestFixture]
    public class EzSsrAggregatorTest
    {
        [Test]
        public void Aggregate_empty_returns_zero()
        {
            Assert.That(EzSsrAggregator.Aggregate(Array.Empty<double>()), Is.EqualTo(0));
        }

        [Test]
        public void Aggregate_single_value_near_input()
        {
            double rating = EzSsrAggregator.Aggregate(new[] { 20.0 });
            Assert.That(rating, Is.GreaterThan(10));
            Assert.That(rating, Is.LessThan(25));
            // Etterna AggregateSSRs with a single sample sits slightly below the raw SSR after scaler/rounding.
            Assert.That(rating, Is.EqualTo(20 * 1.04).Within(6));
        }

        [Test]
        public void Aggregate_vectors_fills_each_axis()
        {
            var plays = new[]
            {
                new EzSkillsetVector(10, 9, 8, 7, 6, 5, 4, 3),
                new EzSkillsetVector(12, 11, 10, 9, 8, 7, 6, 5),
            };

            var result = EzSsrAggregator.AggregateVectors(plays);
            Assert.That(result.Overall, Is.GreaterThan(0));
            Assert.That(result.Stream, Is.GreaterThan(0));
            Assert.That(result.Technical, Is.GreaterThan(0));
        }
    }

    [TestFixture]
    public class EzSkillRegistryTest
    {
        [Test]
        public void Default_systems_keep_msd_ssr_and_dan_separate()
        {
            var registry = new EzSkillRegistry();

            Assert.That(registry.GetSystem(EzSkillSystems.BEATMAP_MSD), Is.Not.Null);
            Assert.That(registry.GetSystem(EzSkillSystems.PLAYER_SSR), Is.Not.Null);
            Assert.That(registry.GetSystem(EzSkillSystems.DAN), Is.Not.Null);

            Assert.That(registry.AllSkills.Any(s => s.SkillId.StartsWith($"{EzSkillSystems.BEATMAP_MSD}.", StringComparison.Ordinal)));
            Assert.That(registry.AllSkills.Any(s => s.SkillId.StartsWith($"{EzSkillSystems.PLAYER_SSR}.", StringComparison.Ordinal)));
            Assert.That(registry.AllSkills.Any(s => s.SkillId.StartsWith($"{EzSkillSystems.DAN}.", StringComparison.Ordinal)));

            // No skill id is shared across systems.
            var ids = registry.AllSkills.Select(s => s.SkillId).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }
    }

    [TestFixture]
    public class EzMinaNoteConverterTest
    {
        [Test]
        public void Convert_empty_beatmap_returns_empty()
        {
            var beatmap = new Beatmap();
            Assert.That(EzMinaNoteConverter.Convert(beatmap), Is.Empty);
        }
    }
}
