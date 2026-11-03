// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Tests.NonVisual.EzOsuGame
{
    /// <summary>
    /// 锁定舞台判定线的核心不变量：舞台板原点就是判定线，任意缩放与命中位置下两者都不分离。
    /// </summary>
    [TestFixture]
    public sealed class EzStagePlacementTest
    {
        // 覆盖常见舞台高度、命中位置（含全局判定的极端值）与 Body 尺寸。
        private static readonly float[] draw_heights = { 480, 720, 768, 1080 };
        private static readonly float[] hit_positions = { 0, 110, 180, 300, 500 };
        private static readonly float[] body_heights = { 540, 720, 1080 };
        private static readonly float[] ratios = { EzStagePlateJudgementLine.DEFAULT_RATIO, 0.3f, 0.5f };

        // 4K..18K 的舞台板宽度，缩放系数由此推出。
        private static readonly float[] stage_widths = { 304, 412, 624, 1248, 1404 };

        // 自制舞台可用非 412 的设计宽度。
        private static readonly float[] design_widths = { EzStagePlacement.DEFAULT_DESIGN_WIDTH, 304, 1024 };

        [Test]
        public void TestStageScaleUsesLayoutDesignWidth([ValueSource(nameof(stage_widths))] float stageWidth,
                                                        [ValueSource(nameof(design_widths))] float designWidth)
        {
            Assert.That(EzStagePlacement.GetStageScale(stageWidth, designWidth), Is.EqualTo(stageWidth / designWidth).Within(0.0001f));
        }

        [Test]
        public void TestStageScaleFallsBackWhenDesignWidthInvalid()
        {
            Assert.That(EzStagePlacement.GetStageScale(624, 0), Is.EqualTo(624 / EzStagePlacement.DEFAULT_DESIGN_WIDTH));
            Assert.That(EzStagePlacement.GetStageScale(624, -412), Is.EqualTo(624 / EzStagePlacement.DEFAULT_DESIGN_WIDTH));
        }

        [Test]
        public void TestJudgementLineOffsetMatchesRatio([ValueSource(nameof(body_heights))] float bodyHeight,
                                                        [ValueSource(nameof(ratios))] float ratio)
        {
            // 图上判定线到 Body 底边的距离应等于 ratio * bodyHeight；bodyHeight/2 是中心到底边的距离。
            float offset = EzStagePlacement.GetJudgementLineOffset(bodyHeight, ratio);

            Assert.That(bodyHeight / 2 - offset, Is.EqualTo(ratio * bodyHeight).Within(0.001f));
        }

        [Test]
        public void TestJudgementLineStaysOnHitLine([ValueSource(nameof(draw_heights))] float drawHeight,
                                                    [ValueSource(nameof(hit_positions))] float hitPosition,
                                                    [ValueSource(nameof(body_heights))] float bodyHeight,
                                                    [ValueSource(nameof(ratios))] float ratio,
                                                    [ValueSource(nameof(stage_widths))] float stageWidth)
        {
            foreach (ScrollingDirection direction in new[] { ScrollingDirection.Down, ScrollingDirection.Up })
            {
                float scale = EzStagePlacement.GetStageScale(stageWidth, EzStagePlacement.DEFAULT_DESIGN_WIDTH);
                float hitLineY = EzStagePlacement.GetHitLineOffset(drawHeight, hitPosition, direction);

                // 图上判定线在板内的位置（Body 中心为 0，向下为正），由美术尺寸独立给出。
                float artJudgementLine = bodyHeight / 2 - ratio * bodyHeight;

                // CreateStage 把板子上移，使原点落在判定线上；EzStageBottom 再把原点放到游戏判定线上并缩放。
                float plateY = -EzStagePlacement.GetJudgementLineOffset(bodyHeight, ratio);
                float spriteY = hitLineY;

                float onScreen = spriteY + (plateY + artJudgementLine) * scale;

                Assert.That(onScreen, Is.EqualTo(hitLineY).Within(0.001f), $"scale={scale} direction={direction}");
            }
        }

        [Test]
        public void TestScaleDoesNotMoveJudgementLine([ValueSource(nameof(body_heights))] float bodyHeight,
                                                       [ValueSource(nameof(ratios))] float ratio)
        {
            const float draw_height = 768;
            const float hit_position = 180;

            float hitLineY = EzStagePlacement.GetHitLineOffset(draw_height, hit_position, ScrollingDirection.Down);
            float artJudgementLine = bodyHeight / 2 - ratio * bodyHeight;
            float plateY = -EzStagePlacement.GetJudgementLineOffset(bodyHeight, ratio);

            float? reference = null;

            foreach (float stageWidth in stage_widths)
            {
                float scale = EzStagePlacement.GetStageScale(stageWidth, EzStagePlacement.DEFAULT_DESIGN_WIDTH);
                float onScreen = hitLineY + (plateY + artJudgementLine) * scale;

                reference ??= onScreen;
                Assert.That(onScreen, Is.EqualTo(reference.Value).Within(0.001f), $"scale={scale}");
            }
        }

        [Test]
        public void TestHitLineOffsetDirection()
        {
            const float draw_height = 768;
            const float hit_position = 180;

            float down = EzStagePlacement.GetHitLineOffset(draw_height, hit_position, ScrollingDirection.Down);
            float up = EzStagePlacement.GetHitLineOffset(draw_height, hit_position, ScrollingDirection.Up);

            Assert.That(down, Is.EqualTo(draw_height / 2 - hit_position));
            Assert.That(up, Is.EqualTo(-down));
        }
    }
}
