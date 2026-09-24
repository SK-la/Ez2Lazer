// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDHitTimingColumns : HitErrorMeter
    {
        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.JUDGEMENT_FILTER_LABEL), nameof(EzCommonModStrings.JUDGEMENT_FILTER_DESCRIPTION))]
        public Bindable<EzEnumHitResult> JudgementFilter { get; } = new Bindable<EzEnumHitResult>(EzEnumHitResult.Good);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.JUDGEMENT_FILTER_DIRECTION_LABEL), nameof(EzCommonModStrings.JUDGEMENT_FILTER_DIRECTION_DESCRIPTION))]
        public Bindable<JudgementFilterDirection> FilterDirection { get; } = new Bindable<JudgementFilterDirection>(JudgementFilterDirection.IgnoreBetter);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MARKERS_HEIGHT_LABEL), nameof(EzHUDManiaStrings.MARKERS_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> MarkerHeight { get; } = new BindableNumber<float>(2)
        {
            MinValue = 1,
            MaxValue = 20,
            Precision = 1f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MOVE_HEIGHT_LABEL), nameof(EzHUDManiaStrings.MOVE_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> MoveHeight { get; } = new BindableNumber<float>(20)
        {
            MinValue = 1,
            MaxValue = 200,
            Precision = 1f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.BACKGROUND_ALPHA_LABEL), nameof(EzHUDManiaStrings.BACKGROUND_ALPHA_DESCRIPTION))]
        public BindableNumber<float> BackgroundAlpha { get; } = new BindableNumber<float>(0.2f)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.1f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.BACKGROUND_COLOUR_LABEL), nameof(EzHUDManiaStrings.BACKGROUND_COLOUR_DESCRIPTION))]
        public BindableColour4 BackgroundColour { get; } = new BindableColour4(Colour4.Gray);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.BACKGROUND_GRADIENT_LABEL), nameof(EzHUDManiaStrings.BACKGROUND_GRADIENT_DESCRIPTION))]
        public BindableBool BackgroundGradient { get; } = new BindableBool();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.UNIFIED_MOVEMENT_LABEL), nameof(EzHUDManiaStrings.UNIFIED_MOVEMENT_DESCRIPTION))]
        public BindableBool UnifiedMovement { get; } = new BindableBool();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.STOP_MOVEMENT_LABEL), nameof(EzHUDManiaStrings.STOP_MOVEMENT_DESCRIPTION))]
        public BindableBool StopMovement { get; } = new BindableBool();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_LABEL), nameof(EzHUDManiaStrings.MATCH_PANEL_WIDTH_DESCRIPTION))]
        public BindableBool MatchManiaPanelWidth { get; } = ManiaPlayfieldLayoutHelper.CreateDefaultMatchPanelWidthBindable();

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_LABEL), nameof(EzHUDManiaStrings.MATCH_HIT_POSITION_LAYOUT_DESCRIPTION))]
        public BindableBool MatchManiaHitPositionLayout { get; } = new BindableBool();

        private Container[]? columns;
        private Box[] judgementMarkers = null!;
        private Box? backgroundSolid;
        private Box? backgroundGradientTop;
        private Box? backgroundGradientBottom;

        private double[] floatingAverages = null!;
        private int keyCount;

        private Bindable<double> columnWidth = null!;
        private Bindable<double> specialFactor = null!;
        private Bindable<ColumnWidthStyle> columnWidthStyle = null!;
        private Bindable<bool> hitPositionGlobalEnable = null!;
        private Bindable<double> hitPosition = null!;
        private Ez2ConfigManager ezSkinConfig = null!;

        private Anchor savedAnchor;
        private Anchor savedOrigin;
        private Vector2 savedPosition;
        private bool savedLayout;

        [Resolved]
        private InputCountController controller { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezSkinConfig)
        {
            this.ezSkinConfig = ezSkinConfig;
            columnWidth = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnWidth);
            specialFactor = ezSkinConfig.GetBindable<double>(Ez2Setting.SpecialFactor);
            columnWidthStyle = ezSkinConfig.GetBindable<ColumnWidthStyle>(Ez2Setting.ColumnWidthStyle);
            hitPositionGlobalEnable = ezSkinConfig.GetBindable<bool>(Ez2Setting.HitPositionGlobalEnable);
            hitPosition = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            floatingAverages = Array.Empty<double>();
            judgementMarkers = Array.Empty<Box>();
            columns = Array.Empty<Container>();
        }

        private void recreateComponents()
        {
            ClearInternal();
            keyCount = ManiaEzColumnLayout.GetDisplayColumnCount(controller.Triggers.Count);

            if (keyCount <= 0)
            {
                floatingAverages = Array.Empty<double>();
                judgementMarkers = Array.Empty<Box>();
                columns = Array.Empty<Container>();
                backgroundSolid = null!;
                backgroundGradientTop = null!;
                backgroundGradientBottom = null!;
                return;
            }

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
                    backgroundSolid = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    backgroundGradientTop = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Height = 0.5f,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.BottomCentre,
                    },
                    backgroundGradientBottom = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Height = 0.5f,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.TopCentre,
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
            updateBackgroundAppearance();
        }

        private void updateBackgroundAppearance()
        {
            if (backgroundSolid == null || backgroundGradientTop == null || backgroundGradientBottom == null)
                return;

            var colour = BackgroundColour.Value;
            float alpha = BackgroundAlpha.Value;

            if (BackgroundGradient.Value)
            {
                backgroundSolid.Alpha = 0;

                var centreColour = colour.Opacity(alpha);
                var transparentColour = colour.Opacity(0);

                backgroundGradientTop.Alpha = 1;
                backgroundGradientTop.Colour = ColourInfo.GradientVertical(transparentColour, centreColour);

                backgroundGradientBottom.Alpha = 1;
                backgroundGradientBottom.Colour = ColourInfo.GradientVertical(centreColour, transparentColour);
            }
            else
            {
                backgroundSolid.Colour = colour;
                backgroundSolid.Alpha = alpha;

                backgroundGradientTop.Alpha = 0;
                backgroundGradientBottom.Alpha = 0;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            controller.Triggers.BindCollectionChanged((_, __) => recreateComponents(), true);

            columnWidth.BindValueChanged(_ => updateWidth(), true);
            specialFactor.BindValueChanged(_ => updateWidth(), true);
            columnWidthStyle.BindValueChanged(_ => updateWidth(), true);
            MatchManiaPanelWidth.BindValueChanged(_ => updateWidth(), true);

            hitPositionGlobalEnable.BindValueChanged(_ => updateHitPositionLayout());
            hitPosition.BindValueChanged(_ => updateHitPositionLayout());
            MatchManiaHitPositionLayout.BindValueChanged(_ => updateHitPositionLayout(), true);

            ezSkinConfig.ColumnTypeChanged += (_, __, ___) => updateWidth();
            skin.SourceChanged += onSkinChanged;

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
                    // 在途的缓动会每帧把 Y 写回它自己的插值（目标还是按旧高度算的），必须先清掉，
                    // 否则下面这次按比例赋值会被覆盖，拖动滑块时标记会卡在旧范围里。
                    marker.ClearTransforms(false, nameof(Drawable.Y));

                    // 按比例调整marker的Y位置
                    marker.Y = marker.Y * (height.NewValue / height.OldValue);
                    marker.Y = Math.Clamp(marker.Y, -height.NewValue / 2, height.NewValue / 2);
                }

                Invalidate(Invalidation.DrawSize);
            }, true);

            BackgroundAlpha.BindValueChanged(_ => updateBackgroundAppearance(), true);
            BackgroundColour.BindValueChanged(_ => updateBackgroundAppearance(), true);
            BackgroundGradient.BindValueChanged(_ => updateBackgroundAppearance(), true);

            UnifiedMovement.BindValueChanged(_ => updateAllMarkerPositions());

            StopMovement.BindValueChanged(e =>
            {
                if (e.NewValue)
                {
                    // 收尾到目标而不是停在半途：开启前标记本来就停在最近一次判定的位置（那时缓动是空变换），
                    // 保持这个观感，否则会在「停止移动」时停在一个中间插值上。
                    foreach (var marker in judgementMarkers)
                        marker.FinishTransforms(false, nameof(Drawable.Y));
                }
                else
                    updateAllMarkerPositions();
            }, true);
        }

        private void updateWidth()
        {
            if (keyCount <= 0 || columns == null)
                return;

            int keyMode = controller.Triggers.Count;
            float totalWidth = 0;

            for (int i = 0; i < keyCount; i++)
            {
                var columnSize = ManiaPlayfieldLayoutHelper.CalculateColumnSize(
                    i,
                    keyMode,
                    skin,
                    skinManager,
                    ezSkinConfig,
                    columnWidth.Value,
                    specialFactor.Value,
                    columnWidthStyle.Value,
                    mobileAdjust: 1f,
                    MatchManiaPanelWidth.Value);

                columns[i].Width = columnSize.Width;
                columns[i].Margin = columnSize.Margin;
                totalWidth += columnSize.TotalWidth;
            }

            Width = totalWidth;
        }

        private void onSkinChanged()
        {
            updateWidth();
            updateHitPositionLayout();
        }

        private void updateHitPositionLayout()
        {
            if (MatchManiaHitPositionLayout.Value)
            {
                if (!savedLayout)
                {
                    savedAnchor = Anchor;
                    savedOrigin = Origin;
                    savedPosition = Position;
                    savedLayout = true;
                }

                ManiaPlayfieldLayoutHelper.ApplyHitPositionPlacement(
                    this,
                    ManiaPlayfieldLayoutHelper.GetHitPosition(skin, hitPositionGlobalEnable.Value, hitPosition.Value));
                return;
            }

            if (!savedLayout)
                return;

            Anchor = savedAnchor;
            Origin = savedOrigin;
            Position = savedPosition;
            savedLayout = false;
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || !shouldCountJudgement(judgement.Type))
                return;

            int columnIndex = -1;

            if (judgement.HitObject is IHasColumn hasColumn)
                columnIndex = hasColumn.Column;

            if (columnIndex < 0 || columnIndex >= keyCount)
                return;

            floatingAverages[columnIndex] = floatingAverages[columnIndex] * 0.9 + judgement.TimeOffset * 0.1;

            if (UnifiedMovement.Value)
            {
                float targetY = getRelativeJudgementPosition(getOverallAverage());
                moveAllMarkers(targetY, columnIndex, judgement.Type);
            }
            else
            {
                float targetY = getRelativeJudgementPosition(floatingAverages[columnIndex]);
                moveMarker(judgementMarkers[columnIndex], targetY, judgement.Type);
            }
        }

        private bool shouldCountJudgement(HitResult result)
        {
            if (!result.IsBasic())
                return false;

            int judgementIndex = result.GetIndexForOrderedDisplay();
            int filterIndex = JudgementFilter.Value.ToHitResult().GetIndexForOrderedDisplay();

            return FilterDirection.Value switch
            {
                JudgementFilterDirection.IgnoreBetter => judgementIndex >= filterIndex,
                JudgementFilterDirection.IgnoreWorse => judgementIndex <= filterIndex,
                _ => true,
            };
        }

        private double getOverallAverage()
        {
            if (keyCount <= 0)
                return 0;

            double sum = 0;

            for (int i = 0; i < keyCount; i++)
                sum += floatingAverages[i];

            return sum / keyCount;
        }

        private void updateAllMarkerPositions()
        {
            if (judgementMarkers.Length == 0 || StopMovement.Value)
                return;

            if (UnifiedMovement.Value)
            {
                float targetY = getRelativeJudgementPosition(getOverallAverage());

                foreach (var marker in judgementMarkers)
                    moveMarker(marker, targetY);
            }
            else
            {
                for (int i = 0; i < judgementMarkers.Length; i++)
                    moveMarker(judgementMarkers[i], getRelativeJudgementPosition(floatingAverages[i]));
            }
        }

        private void moveAllMarkers(float targetY, int judgedColumnIndex, HitResult hitResult)
        {
            for (int i = 0; i < judgementMarkers.Length; i++)
            {
                if (i == judgedColumnIndex)
                    moveMarker(judgementMarkers[i], targetY, hitResult);
                else
                    moveMarker(judgementMarkers[i], targetY);
            }
        }

        private void moveMarker(Box marker, float targetY, HitResult? hitResult = null)
        {
            if (hitResult != null)
                marker.Colour = GetColourForHitResult(hitResult.Value);

            if (StopMovement.Value)
                return;

            const int marker_move_duration = 800;

            // 只发缓动，不要先直接赋值。
            // marker 是常驻对象（不像 BarHitErrorMeter 里那条一次性的池化判定线，那边直接赋值是对的）：
            // 先把 Y 钉到目标的话，紧随其后的 MoveToY 就是 start == end 的空变换，缓动等于没有。
            marker.MoveToY(targetY, marker_move_duration, Easing.OutQuint);
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
            if (columns == null)
                return;

            foreach (var column in columns)
                column.Clear();
        }
    }
}
