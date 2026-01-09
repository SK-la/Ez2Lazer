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
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class EzComHitTimingColumns : HitErrorMeter
    {
        [SettingSource("Minimum Hit Result", "Filter out judgments worse than this")]
        public Bindable<HitResult> MinimumHitResult { get; } = new Bindable<HitResult>(HitResult.Good);

        [SettingSource("Markers Height", "Markers Height")]
        public BindableNumber<float> MarkerHeight { get; } = new BindableNumber<float>(2)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1f,
        };

        [SettingSource("Move Height", "Move Height")]
        public BindableNumber<float> MoveHeight { get; } = new BindableNumber<float>(20)
        {
            MinValue = 1,
            MaxValue = 200,
            Precision = 1f,
        };

        [SettingSource("Background Alpha", "Background Alpha")]
        public BindableNumber<float> BackgroundAlpha { get; } = new BindableNumber<float>(0.2f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.1f,
        };

        [SettingSource("Background Colour", "Background Colour")]
        public BindableColour4 BackgroundColour { get; } = new BindableColour4(Colour4.Gray);

        private double[] floatingAverages = null!;
        private Box[] judgementMarkers = null!;
        private Box backgroundBox = null!;
        private Container[] columns = null!;

        private int keyCount;

        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezSkinConfig)
        {
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            recreateComponents();
        }

        private void recreateComponents()
        {
            ClearInternal();
            keyCount = controller.Triggers.Count;
            floatingAverages = new double[keyCount];
            judgementMarkers = new Box[keyCount];
            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Margin = new MarginPadding(2),
                Children = new Drawable[]
                {
                    backgroundBox = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = BackgroundColour.Value,
                        Alpha = BackgroundAlpha.Value,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(0, 0),
                        Children = columns = Enumerable.Range(0, keyCount).Select(index =>
                        {
                            var column = new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0
                                    }
                                }
                            };
                            var marker = new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
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
            Height = MoveHeight.Value;
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
                Height = height.NewValue;

                foreach (var marker in judgementMarkers)
                {
                    // 按比例调整marker的Y位置
                    marker.Y = marker.Y * (height.NewValue / height.OldValue);
                    marker.Y = Math.Clamp(marker.Y, -height.NewValue / 2, height.NewValue / 2);
                }

                Invalidate(Invalidation.DrawSize);
            }, true);

            // 更新背景透明度
            BackgroundAlpha.BindValueChanged(alpha =>
            {
                backgroundBox.Alpha = alpha.NewValue;
            }, true);

            // 更新背景颜色
            BackgroundColour.BindValueChanged(colour =>
            {
                backgroundBox.Colour = colour.NewValue;
            }, true);
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

            if (judgement.Type > MinimumHitResult.Value)
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
            double missWindow = HitWindows.WindowFor(HitResult.Miss);

            if (missWindow == 0)
                return 0;

            float pos = (float)(value / missWindow) * (MoveHeight.Value / 2);
            return Math.Clamp(pos, -MoveHeight.Value / 2, MoveHeight.Value / 2);
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
