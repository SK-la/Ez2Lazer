// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning
{
    /// <summary>
    /// A <see cref="SliderBody"/> which changes its curve depending on the snaking progress.
    /// </summary>
    public abstract partial class SnakingSliderBody : SliderBody, ISliderProgress
    {
        public readonly List<Vector2> CurrentCurve = new List<Vector2>();

        public readonly Bindable<bool> SnakingIn = new Bindable<bool>();
        public readonly Bindable<bool> SnakingOut = new Bindable<bool>();

        public double? SnakedStart { get; private set; }
        public double? SnakedEnd { get; private set; }

        public override float PathRadius
        {
            get => base.PathRadius;
            set
            {
                if (base.PathRadius == value)
                    return;

                base.PathRadius = value;

                // stable 画不出的身不随圆圈缩放变化。重建会遍历每个顶点。
                if (viewportDecided && bodyRejected)
                    return;

                Refresh();
            }
        }

        public override Vector2 PathOffset => snakedPathOffset;

        public override Vector2 PathEndOffset => snakedPathEndOffset;

        /// <summary>
        /// The top-left position of the path when fully snaked.
        /// </summary>
        private Vector2 snakedPosition;

        /// <summary>
        /// The offset of the path from <see cref="snakedPosition"/> when fully snaked.
        /// </summary>
        private Vector2 snakedPathOffset;

        /// <summary>
        /// The offset of the end of path from <see cref="snakedPosition"/> when fully snaked.
        /// </summary>
        private Vector2 snakedPathEndOffset;

        private DrawableSlider drawableSlider = null!;

        /// <summary>
        /// 身是否像 osu!stable 一样栅格化到一张窗口大小的画布上。
        /// </summary>
        /// <remarks>
        /// 每次从滑条读取而非加载时捕获：池化滑条在组件已加载后才从 playfield 得到标志，
        /// 缓存副本会留住赋值前的值。
        /// </remarks>
        private bool LimitToStableViewport => drawableSlider.UseStableViewportBody;

        /// <summary>
        /// stable 是否无法栅格化该身边界，因而什么都不画。
        /// </summary>
        private bool bodyRejected;

        /// <summary>
        /// 上传任何顶点前测得的身边界（场地单位）。
        /// </summary>
        private Vector2 bodySize;

        /// <summary>
        /// 每场地单位的显示像素，已除以本 drawable 自身缩放。
        /// </summary>
        /// <remarks>
        /// stable 在 <c>FieldToDisplay</c> 后比较 6px/32px 接合，因此栅格用游玩区缩放，
        /// 而非进场时缩小滑条的接近动画。
        /// </remarks>
        private float stablePixelsPerUnit = 1;

        /// <summary>
        /// 画布决策是否已做出。需要已布局的 drawable，因此不能在加载时决定。
        /// </summary>
        private bool viewportDecided;

        /// <summary>
        /// 视口接受边界后捕获的、经 stable 绘制期接合后的完整身。
        /// </summary>
        private readonly List<Vector2> fullRaster = new List<Vector2>();

        private bool fullRasterReady;

        /// <summary>
        /// 身被拒绝时代替曲线，使任何线段都到不了 draw node。
        /// </summary>
        private static readonly List<Vector2> rejected_curve = new List<Vector2>();

        [BackgroundDependencyLoader]
        private void load(DrawableHitObject drawableObject)
        {
            drawableSlider = (DrawableSlider)drawableObject;

            Refresh();
        }

        public void UpdateProgress(double completionProgress)
        {
            if (drawableSlider.HitObject == null)
                return;

            Slider slider = drawableSlider.HitObject;

            // stable 只在 preempt 前三分之一蛇行进入身，之后栅格保持。
            // 没有蛇行退出：球不吞身，身不跟随球。
            // Aspire 在 hitobject 上设标志，池化身后才应用，此时已绑定 lazer 设置，此路径不得再读设置。
            if (LimitToStableViewport)
            {
                double revealed = SnakingIn.Value ? Math.Clamp((Time.Current - (slider.StartTime - slider.TimePreempt)) / (slider.TimePreempt / 3), 0, 1) : 1;
                setRange(0, revealed);
                return;
            }

            int span = slider.SpanAt(completionProgress);
            double spanProgress = slider.ProgressAt(completionProgress);

            double start = 0;
            double end = SnakingIn.Value ? Math.Clamp((Time.Current - (slider.StartTime - slider.TimePreempt)) / (slider.TimePreempt / 3), 0, 1) : 1;

            if (span >= slider.SpanCount() - 1)
            {
                if (Math.Min(span, slider.SpanCount() - 1) % 2 == 1)
                {
                    start = 0;
                    end = SnakingOut.Value ? spanProgress : 1;
                }
                else
                {
                    start = SnakingOut.Value ? spanProgress : 0;
                }
            }

            setRange(start, end);
        }

        public void Refresh()
        {
            if (drawableSlider.HitObject == null)
                return;

            IReadOnlyList<Vector2> calculated = drawableSlider.HitObject.Path.CalculatedPath;
            bodySize = boundsOf(calculated);

            // 场地范围已不能成为视口。不要保留顶点列表：这些路径可达 1e5 点，
            // 每次滑条出现再拷贝正是 seek 等待的原因。
            if (LimitToStableViewport && StableViewportBody.IsRejected(bodySize, 1))
            {
                CurrentCurve.Clear();
                viewportDecided = true;
                bodyRejected = true;
                fullRasterReady = false;
                fullRaster.Clear();
                SetVertices(rejected_curve);
                Size = Vector2.One;
                updatePathSize();
                snakedPosition = Vector2.Zero;
                snakedPathOffset = Vector2.Zero;
                snakedPathEndOffset = Vector2.Zero;
                return;
            }

            CurrentCurve.Clear();
            CurrentCurve.AddRange(calculated);

            viewportDecided = false;
            bodyRejected = false;
            fullRasterReady = false;
            fullRaster.Clear();

            // 真实缩放未知前不上传。占位尺寸保持锚点有限。
            // 先上传再清 alpha 就是那一帧闪烁。
            if (LimitToStableViewport)
            {
                SetVertices(rejected_curve);
                Size = Vector2.One;
                updatePathSize();
                snakedPosition = Vector2.Zero;
                snakedPathOffset = Vector2.Zero;
                snakedPathEndOffset = Vector2.Zero;
            }
            else if (CurrentCurve.Count > 0)
            {
                SetVertices(CurrentCurve);

                // 强制身为最终路径尺寸，避免过多 autosize 计算
                Path.AutoSizeAxes = Axes.Both;
                Size = Path.Size;

                updatePathSize();

                snakedPosition = Path.PositionInBoundingBox(Vector2.Zero);
                snakedPathOffset = Path.PositionInBoundingBox(Path.Vertices[0]);
                snakedPathEndOffset = Path.PositionInBoundingBox(Path.Vertices[^1]);
            }

            double lastSnakedStart = SnakedStart ?? 0;
            double lastSnakedEnd = SnakedEnd ?? 0;

            SnakedStart = null;
            SnakedEnd = null;

            setRange(lastSnakedStart, lastSnakedEnd);
        }

        public override void RecyclePath()
        {
            base.RecyclePath();
            updatePathSize();
        }

        private void updatePathSize()
        {
            // Force the path to its final size to avoid excessive framebuffer resizes
            Path.AutoSizeAxes = Axes.None;
            Path.Size = Size;
        }

        /// <summary>
        /// 在游玩区缩放已知后，算出 stable 会栅格化什么。
        /// </summary>
        /// <remarks>
        /// 窗口尺寸取自 drawable 树根而非窗口依赖（此处依赖容器没有窗口）。
        /// 缩放与窗口都以屏幕像素读取，因此结果跟随实际分辨率与游玩区缩放。
        /// </remarks>
        private void decideViewport()
        {
            if (!LimitToStableViewport || viewportDecided || !IsLoaded)
                return;

            // 一场地单位对应的屏幕像素。身的本地单位是场地单位，因为路径按它排布，游玩区再叠唯一缩放。
            // 除掉本 drawable 自身缩放，避免接近动画缩小时放行身、随后又分配完整身。
            float pixelsPerUnit = (ToScreenSpace(Vector2.UnitX) - ToScreenSpace(Vector2.Zero)).Length;
            float ownScale = MathF.Max(Math.Abs(Scale.X), Math.Abs(Scale.Y));

            if (ownScale > 0)
                pixelsPerUnit /= ownScale;

            if (!(pixelsPerUnit > 0) || !float.IsFinite(pixelsPerUnit))
                return;

            stablePixelsPerUnit = pixelsPerUnit;

            Drawable root = this;

            while (root.Parent != null)
                root = root.Parent;

            Vector2 viewportPixels = root.ScreenSpaceDrawQuad.Size;

            if (!(viewportPixels.X > 0) || !(viewportPixels.Y > 0))
                return;

            viewportDecided = true;

            // 从曲线测量。尚未上传时 drawable 尺寸仍是占位。
            bodyRejected = StableViewportBody.IsRejected(bodySize, pixelsPerUnit);

            if (bodyRejected)
            {
                // stable 无法为这些边界打开视口，因此身永不绘制。上传顶点会按场地边界开帧缓冲，
                // 对 1:05 之后的线性网格量级约 1e8 单位。
                SetVertices(rejected_curve);
                Size = Vector2.One;
                updatePathSize();
                snakedPosition = Vector2.Zero;
                snakedPathOffset = Vector2.Zero;
                snakedPathEndOffset = Vector2.Zero;
            }
        }

        private void setRange(double p0, double p1)
        {
            bool wasDecided = viewportDecided;
            decideViewport();

            if (p0 > p1)
                (p0, p1) = (p1, p0);

            // 首次决策改变绘制内容，因此即使范围未动也要再应用一次。
            if (viewportDecided && !wasDecided)
            {
                SnakedStart = null;
                SnakedEnd = null;
            }

            if (SnakedStart == p0 && SnakedEnd == p1) return;

            SnakedStart = p0;
            SnakedEnd = p1;

            if (LimitToStableViewport)
            {
                if (bodyRejected)
                {
                    SetVertices(rejected_curve);
                    return;
                }

                // 缩放尚未可知。保留 Refresh 已定好的几何，而非清空后留下零绘制尺寸。
                if (!viewportDecided)
                    return;

                ensureFullRaster();

                if (p0 <= 0 && p1 >= 1)
                    SetVertices(fullRaster);
                else
                {
                    drawableSlider.HitObject.Path.GetStableRasterPath(CurrentCurve, p0, p1, stablePixelsPerUnit);
                    SetVertices(CurrentCurve);
                }
            }
            else
            {
                drawableSlider.HitObject.Path.GetPathToProgress(CurrentCurve, p0, p1);
                SetVertices(CurrentCurve);
            }

            if (Path.Vertices.Count == 0)
                return;

            // The bounding box of the path expands as it snakes, which in turn shifts the position of the path.
            // Depending on the direction of expansion, it may appear as if the path is expanding towards the position of the slider
            // rather than expanding out from the position of the slider.
            // 为消除该效果，把路径位置移向最终蛇行位置。
            Path.Position = snakedPosition - Path.PositionInBoundingBox(Vector2.Zero);
        }

        /// <summary>
        /// 构建一次完整 stable 栅格，并从中记录蛇行原点。
        /// </summary>
        /// <remarks>
        /// 两块铺满屏幕的网格就是这条折线。它是整条曲线，而非原始边界的窗口角落：
        /// 能装进视口的身会完整绘制。
        /// </remarks>
        private void ensureFullRaster()
        {
            if (fullRasterReady)
                return;

            drawableSlider.HitObject.Path.GetStableRasterPath(fullRaster, 0, 1, stablePixelsPerUnit);

            if (fullRaster.Count == 0)
            {
                fullRasterReady = true;
                return;
            }

            SetVertices(fullRaster);
            Path.AutoSizeAxes = Axes.Both;
            Size = Path.Size;
            updatePathSize();

            // 本地零点是滑条头。stable 在场地空间绘制路径且头在物件位置，
            // 因此锚点是该点，而非栅格保留的第一个顶点。
            snakedPosition = Path.PositionInBoundingBox(Vector2.Zero);
            snakedPathOffset = snakedPosition;
            snakedPathEndOffset = Path.PositionInBoundingBox(fullRaster[^1]);
            fullRasterReady = true;
        }

        private static Vector2 boundsOf(IReadOnlyList<Vector2> vertices)
        {
            if (vertices.Count == 0)
                return Vector2.Zero;

            Vector2 min = vertices[0];
            Vector2 max = vertices[0];

            for (int i = 1; i < vertices.Count; i++)
            {
                min = Vector2.ComponentMin(min, vertices[i]);
                max = Vector2.ComponentMax(max, vertices[i]);
            }

            return max - min;
        }
    }
}
