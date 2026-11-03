// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    /// <summary>
    /// 进图预热 drawable 必须能被每种皮肤成功加载。
    /// </summary>
    /// <remarks>
    /// note 系列的皮肤组件在自己的 BDL 里注入 owner 的 <c>DrawableHitObject</c>，所以预热必须让它们由真正的
    /// <c>DrawableNote</c> / <c>DrawableHoldNote</c> 拥有。少了 owner 会出现两种失败：
    /// 允许注入 null 的组件拿到 null 后在 load 里 NRE（EzStylePro 的 body），严格 DI 的组件直接解析失败（Legacy 的 body）。
    /// </remarks>
    public partial class TestSceneManiaGameplayWarmUp : SkinnableTestScene
    {
        private static readonly ManiaRuleset ruleset = new ManiaRuleset();

        protected override Ruleset CreateRulesetForSkinProvider() => new ManiaRuleset();

        [Test]
        public void TestWarmUpDrawableLoadsForEverySkin()
        {
            AddStep("create warm-up drawables", () => SetContents(skin => createWarmUpDrawable(skin) ?? Drawable.Empty()));

            AddUntilStep("all loaded", () => CreatedDrawables.All(d => d.LoadState >= LoadState.Ready));

            AddAssert("note 与 LN 的 owner 都在", () => CreatedDrawables.All(d =>
                d.ChildrenOfType<DrawableNote>().Any() && d.ChildrenOfType<DrawableHoldNote>().Any()));
        }

        private static Drawable? createWarmUpDrawable(ISkin skin)
        {
            var beatmapInfo = new BeatmapInfo
            {
                Ruleset = ruleset.RulesetInfo,
                Difficulty = new BeatmapDifficulty { CircleSize = 4 },
            };

            return ruleset.CreateWarmUpDrawable(skin, beatmapInfo, Array.Empty<Mod>());
        }
    }
}
