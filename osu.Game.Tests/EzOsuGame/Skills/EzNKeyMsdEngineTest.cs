// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzNKeyMsdEngineTest
    {
        [Test]
        public void Engine_reports_the_vendored_build()
        {
            using var engine = new EzNKeyMsdEngine();

            Assert.That(engine.EngineVersion, Is.EqualTo("0.74.0"));
            Assert.That(engine.MinKeyCount, Is.EqualTo(4));
            Assert.That(engine.MaxKeyCount, Is.EqualTo(18));
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(16)]
        [TestCase(18)]
        public void Rates_every_supported_keymode(int keyCount)
        {
            using var engine = new EzNKeyMsdEngine();

            Assert.That(engine.SupportsKeyCount(keyCount), Is.True);

            var msd = engine.CalculateMsd(createStreamNotes(keyCount), keyCount);

            Assert.That(msd.Overall, Is.GreaterThan(0), $"{keyCount}K produced a zero MSD vector.");
            Assert.That(msd.Stream, Is.GreaterThan(0), $"{keyCount}K produced a zero Stream axis.");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(19)]
        [TestCase(32)]
        public void Unsupported_keymodes_return_zero(int keyCount)
        {
            using var engine = new EzNKeyMsdEngine();

            Assert.That(engine.SupportsKeyCount(keyCount), Is.False);
            Assert.That(EzNKeyMsdEngine.IsSupportedKeyCount(keyCount), Is.False);
            Assert.That(engine.CalculateMsd(createStreamNotes(4), keyCount).Overall, Is.EqualTo(0));
            Assert.That(engine.CalculateSsr(createStreamNotes(4), keyCount, 1f, 0.97f).Overall, Is.EqualTo(0));
        }

        [Test]
        public void Too_few_rows_are_unrateable()
        {
            using var engine = new EzNKeyMsdEngine();

            Assert.That(engine.CalculateMsd(Array.Empty<EzCalcNote>(), 4).Overall, Is.EqualTo(0));
            Assert.That(engine.CalculateMsd(new[] { new EzCalcNote(1u, 0f) }, 4).Overall, Is.EqualTo(0));

            // Exactly at the minimum row count is still rateable.
            var atMinimum = createStreamNotes(4, rows: EzNKeyMsdEngine.MIN_RATEABLE_ROWS);
            Assert.That(engine.CalculateMsd(atMinimum, 4).Overall, Is.GreaterThan(0));
        }

        [Test]
        public void Msd_and_ssr_use_different_goals()
        {
            var notes = createStreamNotes(4);

            using var engine = new EzNKeyMsdEngine();
            var msd = engine.CalculateMsd(notes, 4);
            var ssr = engine.CalculateSsr(notes, 4, 1f, 0.99f);

            Assert.That(msd.Overall, Is.GreaterThan(0));
            Assert.That(ssr.Overall, Is.GreaterThan(msd.Overall));
        }

        [Test]
        public void Ssr_goal_is_clamped_to_the_usable_range()
        {
            var notes = createStreamNotes(4);

            using var engine = new EzNKeyMsdEngine();

            var atMax = engine.CalculateSsr(notes, 4, 1f, EzNKeyMsdEngine.MAX_SSR_GOAL);
            var atMin = engine.CalculateSsr(notes, 4, 1f, EzNKeyMsdEngine.MIN_SSR_GOAL);

            Assert.That(engine.CalculateSsr(notes, 4, 1f, 4f).Overall, Is.EqualTo(atMax.Overall).Within(1e-4));
            Assert.That(engine.CalculateSsr(notes, 4, 1f, 0.1f).Overall, Is.EqualTo(atMin.Overall).Within(1e-4));
        }

        [Test]
        public void Higher_rate_increases_difficulty()
        {
            var notes = createStreamNotes(4);

            using var engine = new EzNKeyMsdEngine();
            var normal = engine.CalculateMsd(notes, 4, 1f);
            var fast = engine.CalculateMsd(notes, 4, 1.3f);

            Assert.That(normal.Overall, Is.GreaterThan(0));
            Assert.That(fast.Overall, Is.GreaterThan(normal.Overall));
        }

        [Test]
        public void Repeated_computes_are_stable()
        {
            var notes = createStreamNotes(6);

            using var engine = new EzNKeyMsdEngine();
            var first = engine.CalculateMsd(notes, 6);
            var second = engine.CalculateMsd(notes, 6);

            Assert.That(first.Overall, Is.GreaterThan(0));
            Assert.That(second.Overall, Is.EqualTo(first.Overall).Within(1e-4));
            Assert.That(second.Technical, Is.EqualTo(first.Technical).Within(1e-4));
        }

        [Test]
        public void Stream_chart_matches_the_vendored_engine_baseline()
        {
            // Regression anchor for the vendored 0.74.0 build (Resources/EzSkills/NOTICE.md).
            // The tolerance is loose on purpose: the UI re-reads whatever the engine returns,
            // so this only has to catch an accidental engine swap, not pin exact floats.
            var notes = createStreamNotes(4, rows: 64, rowSeconds: 0.05f);

            using var engine = new EzNKeyMsdEngine();
            var msd = engine.CalculateMsd(notes, 4);

            Assert.That(msd.Overall, Is.EqualTo(13.2229395).Within(0.25));
            Assert.That(msd.Stream, Is.EqualTo(12.41642).Within(0.25));
            Assert.That(msd.Technical, Is.EqualTo(12.248576).Within(0.25));
        }

        [Test]
        public void Chords_up_to_sixteen_columns_are_rateable()
        {
            using var engine = new EzNKeyMsdEngine();

            var msd = engine.CalculateMsd(createChordNotes(rows: 8, width: 16), 18);

            Assert.That(msd.Overall, Is.GreaterThan(0));
        }

        [Test]
        public void Engine_recovers_from_a_chart_the_module_cannot_rate()
        {
            // The vendored module aborts on a chord wider than 16 columns. The engine must
            // discard that instance instead of staying poisoned for every later chart.
            using var engine = new EzNKeyMsdEngine();

            Assert.Throws<EzMsdEngineException>(() => engine.CalculateMsd(createChordNotes(rows: 8, width: 17), 18));

            var msd = engine.CalculateMsd(createStreamNotes(18), 18);

            Assert.That(msd.Overall, Is.GreaterThan(0), "engine did not recover after a trapped compute.");
        }

        [Test]
        public void Disposed_engine_throws_on_use()
        {
            var engine = new EzNKeyMsdEngine();
            engine.Dispose();

            Assert.Throws<ObjectDisposedException>(() => engine.CalculateMsd(createStreamNotes(4), 4));
        }

        /// <summary>One note per row, rotating columns — a plain stream.</summary>
        private static EzCalcNote[] createStreamNotes(int keyCount, int rows = 32, float rowSeconds = 0.1f)
        {
            var notes = new EzCalcNote[rows];

            for (int i = 0; i < rows; i++)
                notes[i] = new EzCalcNote(1u << (i % keyCount), i * rowSeconds);

            return notes;
        }

        /// <summary>Every row holds the same <paramref name="width"/>-column chord.</summary>
        private static EzCalcNote[] createChordNotes(int rows, int width, float rowSeconds = 0.1f)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 32);

            uint mask = width >= 32 ? uint.MaxValue : (1u << width) - 1u;
            var notes = new EzCalcNote[rows];

            for (int i = 0; i < rows; i++)
                notes[i] = new EzCalcNote(mask, i * rowSeconds);

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

        [Test]
        public void Same_time_objects_share_one_row_bitmask()
        {
            var beatmap = new Beatmap();
            beatmap.HitObjects.Add(new TestColumnObject(100, 0));
            beatmap.HitObjects.Add(new TestColumnObject(100, 3));
            beatmap.HitObjects.Add(new TestColumnObject(150, 2));

            var rows = EzMinaNoteConverter.Convert(beatmap);

            Assert.That(rows, Has.Length.EqualTo(2));
            Assert.That(rows[0].Notes, Is.EqualTo((1u << 0) | (1u << 3)));
            Assert.That(rows[0].RowTime, Is.EqualTo(0.1f).Within(1e-6));
            Assert.That(rows[1].Notes, Is.EqualTo(1u << 2));
            Assert.That(rows[1].RowTime, Is.EqualTo(0.15f).Within(1e-6));
        }

        [Test]
        public void Negative_times_are_shifted_to_zero()
        {
            var beatmap = new Beatmap();
            beatmap.HitObjects.Add(new TestColumnObject(-200, 0));
            beatmap.HitObjects.Add(new TestColumnObject(-100, 1));
            beatmap.HitObjects.Add(new TestColumnObject(300, 2));

            var rows = EzMinaNoteConverter.Convert(beatmap);

            Assert.That(rows[0].RowTime, Is.EqualTo(0f).Within(1e-6));
            Assert.That(rows[1].RowTime, Is.EqualTo(0.1f).Within(1e-6));
            Assert.That(rows[2].RowTime, Is.EqualTo(0.5f).Within(1e-6));
        }

        [Test]
        public void Negative_start_chart_is_rateable()
        {
            // Regression: a negative row time used to walk the engine's interval index out of
            // bounds and abort the module.
            var beatmap = new Beatmap { Difficulty = { CircleSize = 4 } };

            for (int i = 0; i < 32; i++)
                beatmap.HitObjects.Add(new TestColumnObject(-1000 + i * 100, i % 4));

            var rows = EzMinaNoteConverter.Convert(beatmap);

            Assert.That(rows.Min(r => r.RowTime), Is.GreaterThanOrEqualTo(0));

            using var engine = new EzNKeyMsdEngine();
            Assert.That(engine.CalculateMsd(rows, 4).Overall, Is.GreaterThan(0));
        }

        [Test]
        public void Resolve_key_count_prefers_circle_size()
        {
            var beatmap = new Beatmap { Difficulty = { CircleSize = 7 } };
            beatmap.HitObjects.Add(new TestColumnObject(0, 0));

            Assert.That(EzMinaNoteConverter.ResolveKeyCount(beatmap), Is.EqualTo(7));
        }

        [Test]
        public void Resolve_key_count_falls_back_to_widest_column()
        {
            var beatmap = new Beatmap { Difficulty = { CircleSize = 0 } };
            beatmap.HitObjects.Add(new TestColumnObject(0, 0));
            beatmap.HitObjects.Add(new TestColumnObject(10, 5));

            Assert.That(EzMinaNoteConverter.ResolveKeyCount(beatmap), Is.EqualTo(6));
        }

        [Test]
        public void Resolve_key_count_defaults_to_four_without_objects()
        {
            var beatmap = new Beatmap { Difficulty = { CircleSize = 0 } };

            Assert.That(EzMinaNoteConverter.ResolveKeyCount(beatmap), Is.EqualTo(4));
        }

        private class TestColumnObject : HitObject, IHasColumn
        {
            public int Column { get; }

            public TestColumnObject(double startTime, int column)
            {
                StartTime = startTime;
                Column = column;
            }
        }
    }
}
