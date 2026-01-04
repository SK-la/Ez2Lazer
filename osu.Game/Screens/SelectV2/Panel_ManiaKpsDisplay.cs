// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Analysis;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK.Graphics;

namespace osu.Game.Screens.SelectV2
{
    public partial class EzKpsDisplay : CompositeDrawable
    {
        private readonly OsuSpriteText kpsText;
        // private readonly LineGraph kpsGraph;

        private readonly Dictionary<string, (double averageKps, double maxKps, List<double> kpsList)> kpsCache = new Dictionary<string, (double, double, List<double>)>();
        private CancellationTokenSource? calculationCancellationSource;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        public event Action<Dictionary<int, int>>? ColumnCountsUpdated;
        public event Action<IBeatmap?>? BeatmapUpdated;

        public EzKpsDisplay()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = kpsText = new OsuSpriteText
            {
                Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                Colour = Color4.CornflowerBlue,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft
            };

            // 如果需要图表显示，可以取消注释
            // InternalChildren = new Drawable[]
            // {
            //     kpsText = new OsuSpriteText
            //     {
            //         Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
            //         Colour = Color4.CornflowerBlue,
            //         Anchor = Anchor.CentreLeft,
            //         Origin = Anchor.CentreLeft
            //     },
            //     kpsGraph = new LineGraph
            //     {
            //         Size = new Vector2(300, 20),
            //         Colour = OsuColour.Gray(0.25f),
            //         Anchor = Anchor.CentreLeft,
            //         Origin = Anchor.CentreLeft,
            //         Margin = new MarginPadding { Left = 100 }
            //     },
            // };
        }

        /// <summary>
        /// 异步计算并显示KPS信息
        /// </summary>
        /// <param name="beatmapInfo">谱面信息</param>
        /// <param name="ruleset">规则集</param>
        /// <param name="mods">模组列表</param>
        public void UpdateKpsAsync(BeatmapInfo beatmapInfo, RulesetInfo ruleset, IReadOnlyList<Mod>? mods)
        {
            if (ruleset.OnlineID != 3) // 只在Mania模式下显示
            {
                Hide();
                return;
            }

            Show();

            // 取消之前的计算
            calculationCancellationSource?.Cancel();
            calculationCancellationSource = new CancellationTokenSource();
            var cancellationToken = calculationCancellationSource.Token;

            // 生成缓存键
            string cacheKey = mods == null
                ? $"{beatmapInfo.Hash}"
                : ManiaBeatmapAnalysisCache.CreateCacheKey(beatmapInfo, ruleset, mods);

            // 检查缓存
            if (kpsCache.TryGetValue(cacheKey, out var cachedResult))
            {
                updateUI(cachedResult, null, null);
                return;
            }

            // 显示计算中状态
            kpsText.Text = "  KPS: calculating...";

            // 异步计算
            Task.Run(() =>
            {
                try
                {
                    var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                    var playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset, mods, cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();

                    // 使用优化后的计算器一次性获取所有数据
                    var (averageKps, maxKps, kpsList, columnCounts) = OptimizedBeatmapCalculator.GetAllDataOptimized(playableBeatmap);
                    var kpsResult = (averageKps, maxKps, kpsList);

                    cancellationToken.ThrowIfCancellationRequested();

                    // 缓存结果
                    kpsCache[cacheKey] = kpsResult;

                    // 在UI线程中更新界面
                    Schedule(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            updateUI(kpsResult, columnCounts, playableBeatmap);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // 计算被取消，忽略
                }
                catch (Exception)
                {
                    // 计算出错，显示默认值
                    Schedule(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            updateUI((0, 0, new List<double>()), null, null);
                        }
                    });
                }
            }, cancellationToken);
        }

        private void updateUI((double averageKps, double maxKps, List<double> kpsList) kpsResult,
                              Dictionary<int, int>? columnCounts,
                              IBeatmap? beatmap)
        {
            var (averageKps, maxKps, _) = kpsResult;

            // 更新KPS文本
            kpsText.Text = averageKps > 0 ? $"  KPS: {averageKps:F1} ({maxKps:F1} Max)" : "  KPS: calculating...";

            // 更新图表（如果启用）
            // kpsGraph.Values = kpsList.Count > 0 ? kpsList.Select(kps => (float)kps).ToArray() : new[] { 0f };

            // 通知外部组件列数据已更新
            if (columnCounts != null)
            {
                ColumnCountsUpdated?.Invoke(columnCounts);
            }

            // 通知外部组件beatmap已更新
            BeatmapUpdated?.Invoke(beatmap);
        }

        /// <summary>
        /// 设置KPS显示值
        /// </summary>
        /// <param name="averageKps">平均KPS</param>
        /// <param name="maxKps">最大KPS</param>
        public void SetKps(double averageKps, double maxKps)
        {
            kpsText.Text = averageKps > 0 ? $"  KPS: {averageKps:F1} ({maxKps:F1} Max)" : "  KPS: calculating...";
        }

        /// <summary>
        /// 清空显示并取消计算
        /// </summary>
        public void Clear()
        {
            calculationCancellationSource?.Cancel();
            kpsText.Text = string.Empty;
            Hide();
        }

        protected override void Dispose(bool isDisposing)
        {
            calculationCancellationSource?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
