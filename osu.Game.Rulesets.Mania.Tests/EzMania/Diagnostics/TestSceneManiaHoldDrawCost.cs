// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Diagnostics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning.Default;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

#if DEBUG
namespace osu.Game.Rulesets.Mania.Tests.EzMania.Diagnostics
{
    /// <summary>
    /// LN 消融场景：默认滚动；可冻时钟、模拟按住。
    /// HUD / runtime 每秒打 <c>[LN-ABLATION]</c>。
    /// </summary>
    [TestFixture]
    public partial class TestSceneManiaHoldDrawCost : ManiaInputTestScene
    {
        private const int column_count = 4;
        private const double spawn_lead_ms = 200;
        private const double log_interval_ms = 1000;
        private const double long_hold_duration = 4000;

        // 密度口径与 ManiaDenseScrollWorkload 一致：每列 KPS × 驻留。
        private double perColumnKps = 20;
        private double dwellMs = 700;
        private bool longHolds;

        private int HoldsPerColumn => Math.Max(1, (int)Math.Round(perColumnKps * dwellMs / 1000.0));
        private double HoldSpacing => 1000.0 / perColumnKps;
        private double HoldDuration => longHolds ? long_hold_duration : dwellMs * 0.85;

        [Cached(typeof(IReadOnlyList<Mod>))]
        private IReadOnlyList<Mod> mods { get; set; } = Array.Empty<Mod>();

        [Cached]
        private readonly StageDefinition stage = new StageDefinition(column_count);

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock();
        private readonly FramedClock playfieldClock;
        private readonly List<Column> columns = new List<Column>();
        private readonly List<ScrollingTestContainer> scrollContainers = new List<ScrollingTestContainer>();
        private bool freezeClock;
        private bool simulateHolding;

        private OsuSpriteText? summaryText;
        private bool restoreDiagnostics;
        private double nextLogAt;
        private long lastHoldUpdate;
        private long lastTickScan;
        private long lastTickUpdates;
        private long lastBodyFbo;
        private string lastLoggedVariant = string.Empty;

        public TestSceneManiaHoldDrawCost()
            : base(column_count)
        {
            playfieldClock = new FramedClock(manualClock);
        }

        [BackgroundDependencyLoader]
        private void load(SkinManager skins)
        {
            skins.CurrentSkinInfo.Value = skins.GetSkin(TrianglesSkin.CreateInfo()).SkinInfo;

            var columnFlow = new FillFlowContainer
            {
                Name = "columns",
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Clock = playfieldClock,
            };

            for (int i = 0; i < column_count; i++)
                columnFlow.Add(createColumn(ManiaAction.Key1 + i, i));

            Children = new Drawable[]
            {
                columnFlow,
                summaryText = new OsuSpriteText
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Font = OsuFont.GetFont(size: 16),
                    Margin = new MarginPadding(12),
                    Text = "waiting for holds…",
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            restoreDiagnostics = EzJudgmentDiagnostics.Enabled;
            EzJudgmentDiagnostics.Enabled = true;

            // Column.load() 会按皮肤把 AccentColour 刷成黑；测 LN 体必须能看见。
            foreach (var column in columns)
                column.AccentColour.Value = Color4.OrangeRed;

            spawnHolds();
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            EzJudgmentDiagnostics.Enabled = true;
            ManiaHoldAblation.Reset();
            freezeClock = false;
            simulateHolding = false;
            ManiaJudgeHotPathTrace.Clear();
            resetSampleWindow();
            spawnHolds();
        });

        protected override void Dispose(bool isDisposing)
        {
            ManiaHoldAblation.Reset();
            EzJudgmentDiagnostics.Enabled = restoreDiagnostics;
            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            if (!freezeClock)
                manualClock.CurrentTime += Clock.ElapsedFrameTime;

            playfieldClock.ProcessFrame();

            if (simulateHolding)
            {
                foreach (var hold in this.ChildrenOfType<DrawableHoldNote>())
                    hold.TryBeginHoldPress(playfieldClock.CurrentTime);
            }

            if (Time.Current < nextLogAt)
                return;

            nextLogAt = Time.Current + log_interval_ms;
            logSample("idle");
        }

        [Test]
        public void TestInteractiveAblation()
        {
            AddStep("spawn dense holds", spawnHolds);
            AddStep("density 10 KPS / 300 ms", () => setDensity(10, 300));
            AddStep("density 10 KPS / 500 ms", () => setDensity(10, 500));
            AddStep("density 10 KPS / 700 ms", () => setDensity(10, 700));
            AddStep("density 15 KPS / 500 ms", () => setDensity(15, 500));
            AddStep("density 20 KPS / 300 ms", () => setDensity(20, 300));
            AddStep("density 20 KPS / 500 ms", () => setDensity(20, 500));
            AddStep("density 20 KPS / 700 ms", () => setDensity(20, 700));
            AddToggleStep("long hold body (4s)", v =>
            {
                longHolds = v;
                spawnHolds();
            });
            AddToggleStep("freeze clock", v => freezeClock = v);
            AddToggleStep("simulate holding", v => simulateHolding = v);
            AddToggleStep("disable subtraction stroke", toggleSubtractionStroke);
            AddToggleStep("disable hold tick scan", toggleHoldTickScan);
            AddToggleStep("force 16th ticks (respawn)", toggleForceTicks);
            AddToggleStep("disable tick generation (respawn)", toggleDisableTickGeneration);
            AddToggleStep("enqueue hold head/tail", toggleEnqueueHoldEnds);
            AddStep("log sample now", () => logSample("manual"));
            AddStep("clear holds", clearHolds);
        }

        [Test]
        public void TestHoldEndsStayOutOfNonPositionalQueue()
        {
            AddStep("spawn dense holds", spawnHolds);
            AddUntilStep("holds loaded", () => this.ChildrenOfType<DrawableHoldNote>().Count() == column_count * HoldsPerColumn);
            AddAssert("parent hold still accepts non-positional input", () =>
                this.ChildrenOfType<DrawableHoldNote>().All(h => h.HandleNonPositionalInput));
            AddAssert("head excluded from non-positional input", () =>
                this.ChildrenOfType<DrawableHoldNoteHead>().Any()
                && this.ChildrenOfType<DrawableHoldNoteHead>().All(h => !h.HandleNonPositionalInput));
            AddAssert("tail excluded from non-positional input", () =>
                this.ChildrenOfType<DrawableHoldNoteTail>().Any()
                && this.ChildrenOfType<DrawableHoldNoteTail>().All(t => !t.HandleNonPositionalInput));
        }

        [Test]
        public void TestEnqueueHoldEndsPutsHeadAndTailBack()
        {
            AddStep("enable hold-end enqueue", () => ManiaHoldAblation.EnqueueHoldEnds = true);
            AddStep("spawn dense holds", spawnHolds);
            AddUntilStep("holds loaded", () => this.ChildrenOfType<DrawableHoldNote>().Count() == column_count * HoldsPerColumn);
            AddAssert("head requeued", () =>
                this.ChildrenOfType<DrawableHoldNoteHead>().Any()
                && this.ChildrenOfType<DrawableHoldNoteHead>().All(h => h.HandleNonPositionalInput));
            AddAssert("tail requeued", () =>
                this.ChildrenOfType<DrawableHoldNoteTail>().Any()
                && this.ChildrenOfType<DrawableHoldNoteTail>().All(t => t.HandleNonPositionalInput));
        }

        [Test]
        public void TestForcedTicksAppearUntilGenerationDisabled()
        {
            AddStep("force 16th ticks", () => ManiaHoldAblation.ForceHoldTickGeneration = true);
            AddStep("spawn dense holds", spawnHolds);
            AddUntilStep("ticks present", () => this.ChildrenOfType<DrawableHoldNoteTick>().Any());

            AddStep("disable tick generation", () =>
            {
                ManiaHoldAblation.DisableHoldTickGeneration = true;
                spawnHolds();
            });
            AddUntilStep("ticks gone", () => !this.ChildrenOfType<DrawableHoldNoteTick>().Any());
        }

        private void setDensity(int kps, double dwell)
        {
            perColumnKps = kps;
            dwellMs = dwell;

            foreach (var container in scrollContainers)
                container.TimeRange = dwell;

            spawnHolds();
            logSample($"density={kps}kps/{dwell}ms");
        }

        private void toggleSubtractionStroke(bool disabled)
        {
            ManiaHoldAblation.DisableSubtractionStroke = disabled;

            foreach (var body in this.ChildrenOfType<DefaultBodyPiece>())
                body.Recycle();

            logSample("stroke=" + (disabled ? "off" : "on"));
        }

        private void toggleHoldTickScan(bool disabled)
        {
            ManiaHoldAblation.DisableHoldTickScan = disabled;
            logSample("tickScan=" + (disabled ? "off" : "on"));
        }

        private void toggleForceTicks(bool force)
        {
            ManiaHoldAblation.ForceHoldTickGeneration = force;
            spawnHolds();
            logSample("forceTicks=" + force);
        }

        private void toggleDisableTickGeneration(bool disabled)
        {
            ManiaHoldAblation.DisableHoldTickGeneration = disabled;
            spawnHolds();
            logSample("tickGen=" + (disabled ? "off" : "on"));
        }

        private void toggleEnqueueHoldEnds(bool enqueue)
        {
            ManiaHoldAblation.EnqueueHoldEnds = enqueue;
            logSample("enqueueEnds=" + enqueue);
        }

        private void spawnHolds()
        {
            clearHolds();

            foreach (var column in columns)
                column.AccentColour.Value = Color4.OrangeRed;

            double firstHead = playfieldClock.CurrentTime + spawn_lead_ms;

            for (int col = 0; col < columns.Count; col++)
            {
                for (int i = 0; i < HoldsPerColumn; i++)
                {
                    var obj = new HoldNote
                    {
                        Column = col,
                        StartTime = firstHead + i * HoldSpacing,
                        Duration = HoldDuration
                    };
                    obj.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
                    columns[col].Add(new DrawableHoldNote(obj)
                    {
                        AccentColour = { Value = Color4.OrangeRed }
                    });
                }
            }

            resetSampleWindow();
            refreshSummary();
        }

        private void clearHolds()
        {
            foreach (var column in columns)
                column.HitObjectContainer.Clear();
        }

        private void resetSampleWindow()
        {
            lastHoldUpdate = ManiaJudgeHotPathTrace.HoldUpdateCalls;
            lastTickScan = ManiaJudgeHotPathTrace.HoldTickScanVisits;
            lastTickUpdates = ManiaJudgeHotPathTrace.HoldTickUpdates;
            lastBodyFbo = ManiaJudgeHotPathTrace.HoldBodyForceRedraws;
            nextLogAt = Time.Current + log_interval_ms;
        }

        private void logSample(string variant)
        {
            long holdUpdate = ManiaJudgeHotPathTrace.HoldUpdateCalls - lastHoldUpdate;
            long tickScan = ManiaJudgeHotPathTrace.HoldTickScanVisits - lastTickScan;
            long tickUpdates = ManiaJudgeHotPathTrace.HoldTickUpdates - lastTickUpdates;
            long bodyFbo = ManiaJudgeHotPathTrace.HoldBodyForceRedraws - lastBodyFbo;

            lastHoldUpdate = ManiaJudgeHotPathTrace.HoldUpdateCalls;
            lastTickScan = ManiaJudgeHotPathTrace.HoldTickScanVisits;
            lastTickUpdates = ManiaJudgeHotPathTrace.HoldTickUpdates;
            lastBodyFbo = ManiaJudgeHotPathTrace.HoldBodyForceRedraws;

            long fboRedraw = GlobalStatistics.Get<long>("Draw", "FBORedraw").Value;
            double updateMs = host.UpdateThread.Clock.ElapsedFrameTime;
            double drawMs = host.DrawThread.Clock.ElapsedFrameTime;

            int holdCount = this.ChildrenOfType<DrawableHoldNote>().Count();
            int tickCount = this.ChildrenOfType<DrawableHoldNoteTick>().Count();
            int bodyPieces = this.ChildrenOfType<DefaultBodyPiece>().Count();
            int queueHeads = this.ChildrenOfType<DrawableHoldNoteHead>().Count(h => h.HandleNonPositionalInput);
            int queueTails = this.ChildrenOfType<DrawableHoldNoteTail>().Count(t => t.HandleNonPositionalInput);

            string line =
                $"variant={variant} density={perColumnKps:0.##}kps/{dwellMs:0}ms holds={holdCount} holdsPerCol={HoldsPerColumn} " +
                $"holdDur={HoldDuration:0}ms ticks={tickCount} bodies={bodyPieces} " +
                $"queue(head/tail)={queueHeads}/{queueTails} freeze={(freezeClock ? "on" : "off")} holding={(simulateHolding ? "on" : "off")} " +
                $"stroke={(ManiaHoldAblation.DisableSubtractionStroke ? "off" : "on")} " +
                $"tickScan={(ManiaHoldAblation.DisableHoldTickScan ? "off" : "on")} " +
                $"tickGen={(ManiaHoldAblation.DisableHoldTickGeneration ? "off" : ManiaHoldAblation.ForceHoldTickGeneration ? "force" : "default")} " +
                $"updMs={updateMs:F2} drawMs={drawMs:F2} FBORedraw={fboRedraw} " +
                $"ΔHoldUpdate={holdUpdate} ΔTickScan={tickScan} ΔTickUpd={tickUpdates} ΔBodyFbo={bodyFbo}";

            if (summaryText != null)
                summaryText.Text = line.Replace(" ", "\n");

            if (variant != lastLoggedVariant || variant == "idle")
                Logger.Log($"[LN-ABLATION] {line}", LoggingTarget.Runtime, LogLevel.Important);

            lastLoggedVariant = variant;
        }

        private void refreshSummary() => logSample("spawn");

        private Drawable createColumn(ManiaAction action, int index)
        {
            var column = new Column(index, false)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Height = 0.85f,
                AccentColour = { Value = Color4.OrangeRed },
                Action = { Value = action },
            };

            columns.Add(column);

            var scrollContainer = new ScrollingTestContainer(ScrollingDirection.Down)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                TimeRange = dwellMs,
                Child = column
            };

            scrollContainers.Add(scrollContainer);
            return scrollContainer;
        }
    }
}
#endif
