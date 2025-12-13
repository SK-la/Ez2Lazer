using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class EzComHitTimingColumns : HitErrorMeter
    {
        [SettingSource("Markers Height", "Markers Height")]
        public BindableNumber<float> MarkerHeight { get; } = new BindableNumber<float>(2)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1f,
        };

        [SettingSource("Move Height", "Move Height")]
        public BindableNumber<float> MoveHeight { get; } = new BindableNumber<float>(10)
        {
            MinValue = 1,
            MaxValue = 50,
            Precision = 1f,
        };

        [SettingSource("Background Alpha", "Background Alpha")]
        public BindableNumber<float> BackgroundAlpha { get; } = new BindableNumber<float>(0.2f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.1f,
        };

        private double[] floatingAverages = null!;
        private Box[] judgementMarkers = null!;
        private Container[] columns = null!;

        private int keyCount;

        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        public EzComHitTimingColumns()
        {
            AutoSizeAxes = Axes.None;
        }

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig)
        {
            keyCount = controller.Triggers.Count;
            floatingAverages = new double[keyCount];
            judgementMarkers = new Box[keyCount];
            recreateComponents();
            columnWidth = ezSkinConfig.GetBindable<double>(EzSkinSetting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(EzSkinSetting.SpecialFactor);
        }

        private void recreateComponents()
        {
            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativePositionAxes = Axes.Both,
                Margin = new MarginPadding(2),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(0, 0),
                        Children = columns = Enumerable.Range(0, keyCount).Select(index =>
                        {
                            var column = createColumn();
                            var marker = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativePositionAxes = Axes.Y,
                                RelativeSizeAxes = Axes.X,
                                Width = 1,
                                Height = MarkerHeight.Value,
                                Blending = BlendingParameters.Additive,
                                Colour = Colour4.Gray,
                                Alpha = 0.8f
                            };

                            column.Add(marker);
                            judgementMarkers[index] = marker;
                            return column;
                        }).ToArray()
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            controller.Triggers.BindCollectionChanged((_, __) => recreateComponents(), true);

            columnWidth.BindValueChanged(_ => updateWidth(), true);
            specialFactor.BindValueChanged(_ => updateWidth(), true);
            // 更新标识块高度
            MarkerHeight.BindValueChanged(height =>
            {
                foreach (var marker in judgementMarkers)
                    marker.Height = height.NewValue;
            }, true);

            // 更新背景柱状列高度和标识块移动范围
            MoveHeight.BindValueChanged(height =>
            {
                foreach (var column in columns)
                {
                    var backgroundBox = column.Children.OfType<Box>().FirstOrDefault();

                    if (backgroundBox != null)
                    {
                        backgroundBox.Height = height.NewValue;
                    }
                }

                foreach (var marker in judgementMarkers)
                {
                    marker.Y = Math.Clamp(marker.Y, -height.NewValue / 2, height.NewValue / 2);
                    // 重新计算标识块的相对位置
                    float currentAbsoluteY = marker.Y * height.OldValue;
                    float newRelativeY = currentAbsoluteY / height.NewValue;

                    marker.Y = newRelativeY;

                    marker.MoveToY(newRelativeY, 800, Easing.OutQuint);
                }

                Invalidate(Invalidation.DrawSize);
            }, true);

            // 更新背景透明度
            BackgroundAlpha.BindValueChanged(alpha =>
            {
                foreach (var column in columns)
                {
                    var backgroundBox = column.Children.OfType<Box>().FirstOrDefault();
                    if (backgroundBox != null)
                        backgroundBox.Alpha = alpha.NewValue;
                }
            }, true);
        }

        private Container createColumn()
        {
            return new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both, // 改为相对尺寸
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = Colour4.Gray,
                        Alpha = BackgroundAlpha.Value
                    }
                }
            };
        }

        private void updateWidth()
        {
            if (keyCount <= 0)
                return;

            float totalWidth = 0;

            for (int i = 0; i < keyCount; i++)
            {
                float? widthS = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                        new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, i))
                                    ?.Value;

                float newWidth = widthS ?? (float)columnWidth.Value;

                columns[i].Width = newWidth;
                totalWidth += newWidth;
            }

            Width = totalWidth;
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || !judgement.Type.IsScorable())
                return;

            int columnIndex = -1;

            if (judgement.HitObject is IHasColumn hasColumn)
                columnIndex = hasColumn.Column;

            if (columnIndex < 0 || columnIndex >= keyCount)
                return;

            floatingAverages[columnIndex] = floatingAverages[columnIndex] * 0.9 + judgement.TimeOffset * 0.1;

            const int marker_move_duration = 800;
            var marker = judgementMarkers[columnIndex];

            float targetY = getRelativeJudgementPosition(floatingAverages[columnIndex]);

            marker.Y = targetY;

            marker.MoveToY(targetY, marker_move_duration, Easing.OutQuint);

            marker.Colour = GetColourForHitResult(judgement.Type);
        }

        private float getRelativeJudgementPosition(double value)
        {
            float moveRange = MoveHeight.Value;
            double missWindow = HitWindows.WindowFor(HitResult.Miss);

            if (moveRange == 0 || missWindow == 0)
                return 0;

            float pos = (float)(value / (missWindow * moveRange));
            return Math.Clamp(pos, -moveRange, moveRange);
        }

        public override void Clear()
        {
            foreach (var column in columns)
            {
                column.Clear();
            }
        }
    }
}
