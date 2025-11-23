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
using osu.Framework.Logging;
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
using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;
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
        private EzSkinSettingsManager ezSkinSettings { get; set; } = null!;

        private Bindable<double> columnDim = null!;
        private Bindable<double> columnBlur = null!;
        private readonly Box dimBox;
        private readonly FrostedGlassContainer acrylicEffect;

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
                // TODO: 需要修改framework着色器-容器实现真正效果。动态根据实际的游戏中BackgroundScreen背景或故事版绘制局部虚化效果
                // 注意，如果是真正的毛玻璃效果，此处就不应该在此处试图获取外部的背景内容，因为存在循环依赖问题。
                acrylicEffect = new FrostedGlassContainer
                {
                    Name = "Acrylic backdrop effect",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both, //使用Both即可实现局部遮罩，因为此处父容器就是局部面板
                    BlurRotation = 50f,
                    Depth = 1, // 在背景层
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Red,
                        Alpha = 0.01f,
                    }
                },
                dimBox = new Box //通过叠加覆盖实现暗化效果
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
                        new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageForeground))
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
                // bool isSpecial = ezSkinSettings.IsSpecialColumn(definition.Columns, i);

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

            AddInternal(judgementPooler = new JudgementPooler<DrawableManiaJudgement>(Enum.GetValues<HitResult>().Where(r => hitWindows.IsHitResultAllowed(r))));

            RegisterPool<BarLine, DrawableBarLine>(50, 200);
        }

        [Pure]
        protected virtual Column CreateColumn(int index, bool isSpecial) => new Column(index, isSpecial);

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            currentSkin = skin;

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();

            columnDim = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnDim);
            columnBlur = ezSkinSettings.GetBindable<double>(EzSkinSetting.ColumnBlur);

            // 应用暗化效果
            columnDim.BindValueChanged(v =>
            {
                dimBox.Alpha = (float)v.NewValue;
            }, true);

            // 应用虚化效果
            columnBlur.BindValueChanged(v =>
            {
                acrylicEffect.BlurRotation = (float)v.NewValue;
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
            judgements.Add(judgementPooler.Get(result.Type, j => j.Apply(result, judgedObject))!);
        }

        protected override void Update()
        {
            // Due to masking differences, it is not possible to get the width of the columns container automatically
            // While masking on effectively only the Y-axis, so we need to set the width of the bar line container manually
            barLineContainer.Width = columnFlow.Width;
        }
    }
}
