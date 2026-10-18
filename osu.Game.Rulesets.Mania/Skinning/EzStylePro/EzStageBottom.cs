// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzStageBottom : CompositeDrawable
    {
        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);
        private readonly EzStageAnchorContainer sprite;

        private readonly IBindable<double> hitPosition = new Bindable<double>();
        private readonly IBindable<bool> globalHitPosition = new Bindable<bool>();
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private EzStageKeyLayer? keyLayer;
        private Stage? stage;
        private float[] keyWidths = Array.Empty<float>();
        private float[] appliedKeyWidths = Array.Empty<float>();

        private Bindable<string> stageName = null!;
        private Bindable<string> judgementLineName = null!;

        [Resolved(CanBeNull = true)]
        private EzStageKeyState? keyState { get; set; }
        private ISkinSource skinSource = null!;
        private int columnCount;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public EzStageBottom()
        {
            AddLayout(layout);

            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChild = sprite = new EzStageAnchorContainer
            {
                // 零尺寸：缩放以该容器的位置为支点，而这个位置就是判定线。
                // 不设 AutoSize，否则支点会变成子项包围盒的中心，判定线又会随列宽漂移。
                RelativeSizeAxes = Axes.None,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo, StageDefinition stageDefinition, IBeatmap beatmap, ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            columnCount = playableKeyCount(beatmap, stageDefinition);
            skinSource = skin;

            hitPosition.BindTo(ezSkinInfo.HitPosition);
            globalHitPosition.BindTo(ezSkinInfo.HitPositionGlobalEnable);
            direction.BindTo(scrollingInfo.Direction);

            stageName = ezSkinConfig.GetBindable<string>(Ez2Setting.StageName);
            judgementLineName = ezSkinConfig.GetBindable<string>(Ez2Setting.JudgementLineName);
        }

        private static int playableKeyCount(IBeatmap beatmap, StageDefinition stageDefinition)
        {
            int fromCs = (int)Math.Round(beatmap.Difficulty.CircleSize);

            if (beatmap is ManiaBeatmap && stageDefinition.Columns > 0 && stageDefinition.Columns != fromCs)
                return stageDefinition.Columns;

            if (fromCs > 0)
                return fromCs;

            return stageDefinition.Columns;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stageName.BindValueChanged(_ => OnSkinChanged(), true);

            // 判定线贴图名变化只需要换图，不必重建整块舞台板。
            judgementLineName.BindValueChanged(_ => OnSkinChanged());

            // 判定线位置与舞台板缩放原点绑定：任一输入变化都要重算，否则会出现只在启动时对齐的情况。
            hitPosition.BindValueChanged(_ => updatePosition(), true);
            globalHitPosition.BindValueChanged(_ => updatePosition(), true);
            direction.BindValueChanged(_ => updatePosition(), true);

            skinSource.SourceChanged += updatePosition;
            factory.OnNoteSizeChanged += updatePosition;
        }

        private void OnSkinChanged()
        {
            sprite.Clear();
            sprite.Add(factory.CreateStage(columnCount, keyState, out keyLayer));
            resetKeyWidths();
            updatePosition();
        }

        private void resetKeyWidths()
        {
            appliedKeyWidths = Array.Empty<float>();
            keyWidths = Array.Empty<float>();
        }

        // 按键图宽度跟随列宽：列宽来自 ColumnFlow 的实测布局，避免再算一份列宽。
        private void updateKeyWidths()
        {
            if (keyLayer == null || !keyLayer.UsesColumnWidth)
                return;

            stage ??= this.FindClosestParent<Stage>();

            Column[]? columns = stage?.Columns;

            if (columns == null || columns.Length == 0)
                return;

            float scale = sprite.Scale.X;

            if (!(scale > 0))
                return;

            if (keyWidths.Length != columns.Length)
            {
                keyWidths = new float[columns.Length];
                appliedKeyWidths = new float[columns.Length];

                for (int i = 0; i < appliedKeyWidths.Length; i++)
                    appliedKeyWidths[i] = float.NaN;
            }

            bool changed = false;

            for (int i = 0; i < columns.Length; i++)
            {
                // 列宽是屏幕坐标，layout 坐标要按舞台缩放还原。
                keyWidths[i] = columns[i].DrawWidth / scale;

                if (keyWidths[i] != appliedKeyWidths[i])
                    changed = true;
            }

            if (!changed)
                return;

            Array.Copy(keyWidths, appliedKeyWidths, keyWidths.Length);
            keyLayer.SetColumnWidths(keyWidths);
        }

        private void updatePosition()
        {
            // 舞台板原点就是 layout 里的判定线，所以这里只需把原点放到游戏判定线上再缩放。
            // 缩放围绕零尺寸容器的支点进行，列宽改变不会移动判定线。
            float hitPositionValue = ManiaPlayfieldLayoutHelper.GetHitPosition(skinSource, globalHitPosition.Value, hitPosition.Value);

            sprite.Scale = new Vector2(EzStagePlacement.GetStageScale(DrawWidth, factory.StageDesignWidth));
            sprite.Y = EzStagePlacement.GetHitLineOffset(DrawHeight, hitPositionValue, direction.Value);
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid)
            {
                updatePosition();
                layout.Validate();
            }

            updateKeyWidths();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            factory.OnNoteSizeChanged -= updatePosition;

            if (skinSource.IsNotNull())
                skinSource.SourceChanged -= updatePosition;
        }
    }
}
