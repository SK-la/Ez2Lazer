// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// A collection of <see cref="Column"/>s.
    /// </summary>
    public partial class Stage : ScrollingPlayfield
    {
        [Cached]
        public readonly StageDefinition Definition;

        public const float COLUMN_SPACING = 1;

        public const float HIT_TARGET_POSITION = 110;

        public Column[] Columns => columnFlow.Content;
        private readonly ColumnFlow<Column> columnFlow;

        private readonly JudgementContainer<DrawableManiaJudgement> judgements;
        private readonly JudgementPooler<DrawableManiaJudgement> judgementPooler;

        private readonly Drawable barLineContainer;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            foreach (var c in Columns)
            {
                if (c.ReceivePositionalInputAt(screenSpacePos))
                    return true;
            }

            return false;
        }

        private readonly int firstColumnIndex;

        private ISkinSource currentSkin = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private OsuConfigManager osuConfig { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IBackdropCaptureSourceProvider? backdropCaptureSourceProvider { get; set; }

        // private Bindable<double> osuConfigDim = null!;
        private Bindable<double> columnDim = null!;
        private Bindable<double> columnBlur = null!;
        private bool blurEnabledByConfig;

        private bool showBlur => GlobalConfigStore.EzConfig.Get<double>(Ez2Setting.ColumnBlur) > 0;

        private readonly Box dimBox;
        private readonly BackdropBlurDrawable? stageBackdropBlur;
        private readonly SkinnableDrawable stageForeground;

        public Stage(int firstColumnIndex, StageDefinition definition, ref ManiaAction columnStartAction)
        {
            this.firstColumnIndex = firstColumnIndex;
            Definition = definition;

            Name = "Stage";

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;

            Container columnBackgrounds;
            Container topLevelContainer;

            InternalChildren = new Drawable[]
            {
                (stageBackdropBlur = showBlur
                    ? new BackdropBlurDrawable
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        EffectEnabled = false,
                        // FrameBufferScale = new Vector2(0.2f),
                        CaptureFrameInterval = 3,
                        MaxCapturesPerSecond = 300,
                    }
                    : null) ?? Empty(),
                dimBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black,
                },
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Children = new Drawable[]
                    {
                        new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageBackground), _ => new DefaultStageBackground())
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        columnBackgrounds = new Container
                        {
                            Name = "Column backgrounds",
                            RelativeSizeAxes = Axes.Both,
                        },
                        new Container
                        {
                            Name = "Barlines mask",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            RelativeSizeAxes = Axes.Y,
                            Width = 1366, // Bar lines should only be masked on the vertical axis
                            BypassAutoSizeAxes = Axes.Both,
                            Masking = true,
                            Child = barLineContainer = new HitPositionPaddedContainer
                            {
                                Name = "Bar lines",
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.Y,
                                Child = HitObjectContainer,
                            }
                        },
                        columnFlow = new ColumnFlow<Column>(definition)
                        {
                            RelativeSizeAxes = Axes.Y,
                        },
                        stageForeground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageForeground))
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        new HitPositionPaddedContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = judgements = new JudgementContainer<DrawableManiaJudgement>
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                        topLevelContainer = new Container { RelativeSizeAxes = Axes.Both }
                    }
                }
            };

            for (int i = 0; i < definition.Columns; i++)
            {
                bool isSpecial = definition.IsSpecialColumn(i);
                // 必须使用全局静态，类型构造时无法获取自动注入
                // bool isSpecial = GlobalConfigStore.EzConfig.IsSpecialColumnFast(definition.Columns, i);

                var action = columnStartAction;
                columnStartAction++;
                var column = CreateColumn(firstColumnIndex + i, isSpecial).With(c =>
                {
                    c.RelativeSizeAxes = Axes.Both;
                    c.Width = 1;
                    c.Action.Value = action;
                });

                topLevelContainer.Add(column.TopLevelContainer.CreateProxy());
                columnBackgrounds.Add(column.BackgroundContainer.CreateProxy());
                columnFlow.SetContentForColumn(i, column);
                AddNested(column);
            }

            var hitWindows = new ManiaHitWindows();

            AddInternal(judgementPooler = new JudgementPooler<DrawableManiaJudgement>(Enum.GetValues<HitResult>().Where(hitWindows.IsHitResultAllowed)));

            RegisterPool<BarLine, DrawableBarLine>(50, 200);
        }

        [Pure]
        protected virtual Column CreateColumn(int index, bool isSpecial) => new Column(index, isSpecial);

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            currentSkin = skin;

            if (stageBackdropBlur != null)
                stageBackdropBlur.CaptureSourceProvider = backdropCaptureSourceProvider;

            currentSkin.SourceChanged += onSkinChanged;
            onSkinChanged();

            // osuConfigDim = osuConfig.GetBindable<double>(OsuSetting.DimLevel);

            columnDim = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim);
            columnDim.BindValueChanged(v =>
            {
                // dimBox.Alpha = (float)Math.Max(v.NewValue, osuConfigDim.Value / 2);
                dimBox.Alpha = (float)v.NewValue;
            }, true);

            columnBlur = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnBlur);
            columnBlur.BindValueChanged(v =>
            {
                float sigma = (float)v.NewValue * 50;
                blurEnabledByConfig = sigma > 0.01f;

                if (stageBackdropBlur != null)
                {
                    stageBackdropBlur.BlurSigma = new Vector2(sigma);
                    updateBackdropBlurState();
                }
            }, true);

            var stagePanelEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.StagePanelEnabled);
            stagePanelEnabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                    stageForeground.Show();
                else
                    stageForeground.Hide();
            }, true);
        }

        private void onSkinChanged()
        {
            float paddingTop = currentSkin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.StagePaddingTop))?.Value ?? 0;
            float paddingBottom = currentSkin.GetConfig<ManiaSkinConfigurationLookup, float>(new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.StagePaddingBottom))?.Value ?? 0;

            Padding = new MarginPadding
            {
                Top = paddingTop,
                Bottom = paddingBottom,
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            // must happen before children are disposed in base call to prevent illegal accesses to the judgement pool.
            NewResult -= OnNewResult;

            // 清理模糊容器的引用，释放 D3D11 渲染目标资源
            if (stageBackdropBlur != null)
            {
                // 先禁用效果，防止清理过程中继续捕获
                stageBackdropBlur.EffectEnabled = false;

                // 断开所有捕获目标
                stageBackdropBlur.CaptureSourceProvider = null;
                stageBackdropBlur.CaptureTarget = null;
                stageBackdropBlur.CaptureTargets.Clear();

                // 强制过期并移除
                stageBackdropBlur.Expire();
            }

            base.Dispose(isDisposing);

            if (currentSkin.IsNotNull())
                currentSkin.SourceChanged -= onSkinChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += OnNewResult;
        }

        public override void Add(HitObject hitObject) => Columns[((ManiaHitObject)hitObject).Column - firstColumnIndex].Add(hitObject);

        public override bool Remove(HitObject hitObject) => Columns[((ManiaHitObject)hitObject).Column - firstColumnIndex].Remove(hitObject);

        public override void Add(DrawableHitObject h) => Columns[((ManiaHitObject)h.HitObject).Column - firstColumnIndex].Add(h);

        public override bool Remove(DrawableHitObject h) => Columns[((ManiaHitObject)h.HitObject).Column - firstColumnIndex].Remove(h);

        public void Add(BarLine barLine) => base.Add(barLine);

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            judgements.Clear(false);

            var drawableJudgement = judgementPooler.Get(result.Type, j => j.Apply(result, judgedObject));
            if (drawableJudgement == null)
                return;

            judgements.Add(drawableJudgement);
        }

        protected override void Update()
        {
            // Due to masking differences, it is not possible to get the width of the columns container automatically
            // While masking on effectively only the Y-axis, so we need to set the width of the bar line container manually
            barLineContainer.Width = columnFlow.Width;
        }

        private void updateBackdropBlurState()
        {
            if (stageBackdropBlur == null)
                return;

            stageBackdropBlur.EffectEnabled = blurEnabledByConfig;
        }
    }
}
