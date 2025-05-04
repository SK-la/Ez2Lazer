using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzColumnHitErrorMeter : HitErrorMeter
    {
        [SettingSource("Icon Height", "Icon Height")]
        public BindableNumber<float> IconHeight { get; } = new BindableNumber<float>(2)
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
        private float keyCount;

        private OsuConfigManager config = null!;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        public EzColumnHitErrorMeter()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;
            recreateComponents();
        }

        private void recreateComponents()
        {
            keyCount = controller.Triggers.Count;
            floatingAverages = new double[(int)keyCount];
            judgementMarkers = new Box[(int)keyCount];

            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Margin = new MarginPadding(2),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(0, 0),
                        Children = columns = Enumerable.Range(0, (int)keyCount).Select(index =>
                        {
                            var column = createColumn();
                            var marker = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativePositionAxes = Axes.Y,
                                Blending = BlendingParameters.Additive,
                                Width = (float)config.Get<double>(OsuSetting.ColumnWidth),
                                Height = IconHeight.Value,
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

            // 更新标识块高度
            IconHeight.BindValueChanged(height =>
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

                    // 更新标识块的移动范围
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
            var backgroundBox = new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Height = MoveHeight.Value,
                Width = (float)config.Get<double>(OsuSetting.ColumnWidth),
                Colour = Colour4.Gray,
                Alpha = 0.2f
            };

            return new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    backgroundBox
                }
            };
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || !judgement.Type.IsScorable())
                return;

            int columnIndex = getColumnIndex(judgement);
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
            return (float)Math.Clamp((value / HitWindows.WindowFor(HitResult.Miss)) * moveRange, -moveRange, moveRange);
        }

        private int getColumnIndex(JudgementResult judgement)
        {
            if (judgement.HitObject is IHasColumn hasColumn)
                return hasColumn.Column;

            return -1;
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
