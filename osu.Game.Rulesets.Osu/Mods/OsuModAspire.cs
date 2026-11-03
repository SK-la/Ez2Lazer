// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// 按 osu!stable 的方式游玩 aspire 谱面，可旁观自动播放或亲手打。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aspire 谱面依赖 osu!stable 解析器的 quirks。若在普通 lazer 游玩中全局复现，会改变每张图的规则，
    /// 因此改动游玩的部分仅在本 mod 启用时生效：远超谱面结束的滑条塌成零长度而非留下无界物件，
    /// 且路径永不近似。
    /// </para>
    /// <para>
    /// <see cref="Autoplay"/> 在旁观生成回放与亲手游玩之间切换。没有本 mod 时，此类滑条保留字面时长，
    /// 谱面行为与改动前完全一致。
    /// </para>
    /// </remarks>
    public class OsuModAspire : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder, ICreateReplayData, IConditionalReplayGeneration
    {
        public override string Name => "Aspire";

        public override string Acronym => "ASP";

        public override IconUsage? Icon => OsuIcon.ModAutoplay;

        public override ModType Type => ModType.Automation;

        public override LocalisableString Description => "Play aspire maps the way osu!stable loads them.";

        public override bool Ranked => false;

        public override bool ValidForMultiplayer => false;

        public override bool ValidForMultiplayerAsFreeMod => false;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAutoplay),
            typeof(ModCinema),
            typeof(ModRelax),
            typeof(ModAdaptiveSpeed),
        };

        [SettingSource("Autoplay", "Watch a perfect automated play instead of playing the beatmap yourself.")]
        public BindableBool Autoplay { get; } = new BindableBool(true);

        /// <summary>
        /// 在其它转换 mod 之前应用，使替换 <see cref="Slider"/> 的 mod 仍保留限制。
        /// </summary>
        public int ApplyOrder => -1;

        public bool ReplayGenerationEnabled => Autoplay.Value;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            // 滑条的长度、时长、球位置与评分点生成全部按 stable 的口径，规则本身归 StableClassic 判定档。
            // 必须在转换阶段做：这些标志决定嵌套对象怎么生成，之后改不会重建它们。
            OsuStableClassic.ApplyParsedRules(beatmap);

            removeCoincidentSpinners(beatmap);
        }

        /// <summary>
        /// 丢弃与谱面中已有转盘无法区分的转盘。
        /// </summary>
        /// <remarks>
        /// Aspire 谱面常在同一时间戳叠数百个转盘——野外有一张在同一毫秒放了 579 个，结束时间与位置相同。
        /// 它们完全重叠，肉眼只看到一个，但每个都会分配 drawable，同一帧全部创建就会卡死。
        /// 仅考虑零长度转盘：它们不需要转圈；有真实时长的转盘保留。
        /// </remarks>
        private static void removeCoincidentSpinners(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap)
                return;

            var seen = new HashSet<(double startTime, double endTime, Vector2 position, bool newCombo)>();
            var kept = new List<OsuHitObject>(osuBeatmap.HitObjects.Count);
            bool removed = false;

            foreach (var hitObject in osuBeatmap.HitObjects)
            {
                if (hitObject is not Spinner spinner || spinner.EndTime > spinner.StartTime)
                {
                    kept.Add(hitObject);
                    continue;
                }

                if (seen.Add((spinner.StartTime, spinner.EndTime, spinner.Position, spinner.NewCombo)))
                    kept.Add(hitObject);
                else
                    removed = true;
            }

            if (removed)
                osuBeatmap.HitObjects = kept;
        }

        public ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            => new ModReplayData(new OsuAspireAutoGenerator(beatmap, mods).Generate(), new ModCreatedUser { Username = "Aspire" });
    }
}
