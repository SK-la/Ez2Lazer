// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
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

        /// <summary>
        /// 每列按键的按下状态。舞台板上的按键贴图读它，输入仍由各列的 <see cref="EzKeyArea"/> 写入。
        /// </summary>
        [Cached]
        public readonly EzStageKeyState KeyState;

        public const float COLUMN_SPACING = 1;

        public const float HIT_TARGET_POSITION = 110;

        public Column[] Columns => columnFlow.Content;
        private readonly ColumnFlow<Column> columnFlow;

        private readonly JudgementContainer<DrawableManiaJudgement> judgements;
        private readonly JudgementPooler<DrawableManiaJudgement> judgementPooler;

        private readonly ManiaJudgementLine judgementLine;

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

        // private Bindable<double> osuConfigDim = null!;
        private Bindable<double> columnDim = null!;

        private readonly Box dimBox;
        public readonly SkinnableDrawable StageBackground;
        public readonly SkinnableDrawable StageForeground;

        /// <summary>
        /// Whether the stage panel may be shown at all. Stages used by the ruleset editor override this.
        /// </summary>
        protected virtual bool AllowStagePanel => true;

        /// <summary>
        /// Whether the JudgementLine should be shown.
        /// </summary>
        protected virtual bool ShowJudgementLine => false;

        public Stage(int firstColumnIndex, StageDefinition definition, ref ManiaAction columnStartAction)
        {
            this.firstColumnIndex = firstColumnIndex;
            Definition = definition;
            KeyState = new EzStageKeyState(ManiaPlayfieldLayoutHelper.GetDisplayColumnCount(definition));

            Name = "Stage";

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;

            Container columnBackgrounds;
            Container topLevelContainer;

            InternalChildren = new Drawable[]
            {
                new EzStageBackdrop(),
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
                        StageBackground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageBackground), _ => new DefaultStageBackground())
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
                        StageForeground = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.StageForeground))
                        {
                            RelativeSizeAxes = Axes.Both
                        },
                        new HitPositionPaddedContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                judgements = new JudgementContainer<DrawableManiaJudgement>
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                judgementLine = new ManiaJudgementLine
                                {
                                    // Always present so the playfield and the extension columns can show the same line;
                                    // the editor enables it, gameplay keeps it invisible.
                                    Alpha = ShowJudgementLine ? ManiaJudgementLine.LINE_ALPHA : 0,
                                }
                            }
                        },
                        topLevelContainer = new Container { RelativeSizeAxes = Axes.Both }
                    }
                }
            };

            int displayColumns = ManiaPlayfieldLayoutHelper.GetDisplayColumnCount(definition);

            for (int i = 0; i < displayColumns; i++)
            {
                bool isSpecial = definition.IsSpecialColumn(i);

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

            // [Ez] 池按「任一 JudgeMode 下可能出现的判定」预热，不能按当前设置的 ManiaHitWindows 推导：
            // 当局用的 JudgeMode / 血量模式取自冻结环境（回放是成绩里嵌入的模式），池缺项时
            // JudgementPooler.Get 返回 null，该判定会被静默跳过不显示。
            // 但只预热能作为判定图展示的判定：IgnoreHit / IgnoreMiss / ComboBreak 仅供计分，
            // 进池既白建 drawable，又会让缺资源的皮肤回退成默认文字判定（把内部结果名画到屏幕上）。
            AddInternal(judgementPooler = new JudgementPooler<DrawableManiaJudgement>(JudgeModeHelper.AllModeDisplayableHitResults));

            RegisterPool<BarLine, DrawableBarLine>(50, 200);
        }

        [Pure]
        protected virtual Column CreateColumn(int index, bool isSpecial) => new Column(index, isSpecial);

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            currentSkin = skin;

            currentSkin.SourceChanged += onSkinChanged;
            onSkinChanged();

            // osuConfigDim = osuConfig.GetBindable<double>(OsuSetting.DimLevel);

            columnDim = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim);
            columnDim.BindValueChanged(v =>
            {
                // dimBox.Alpha = (float)Math.Max(v.NewValue, osuConfigDim.Value / 2);
                dimBox.Alpha = (float)v.NewValue;
            }, true);

            var stagePanelEnabled = ezSkinConfig.GetBindable<bool>(Ez2Setting.StagePanelEnabled);
            stagePanelEnabled.BindValueChanged(setStagePanelVisible, true);
        }

        private void setStagePanelVisible(ValueChangedEvent<bool> b)
        {
            if (!AllowStagePanel)
            {
                StageBackground.Alpha = 0;
                StageForeground.Alpha = 0;
            }
            else
            {
                if (b.NewValue)
                {
                    StageBackground.Show();
                    StageForeground.Show();
                }
                else
                {
                    StageBackground.Hide();
                    StageForeground.Hide();
                }
            }
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

        public override void Add(HitObject hitObject)
        {
            if (!tryGetColumnForHitObject((ManiaHitObject)hitObject, out var column))
                return;

            column.Add(hitObject);
        }

        public override bool Remove(HitObject hitObject)
        {
            if (!tryGetColumnForHitObject((ManiaHitObject)hitObject, out var column))
                return false;

            return column.Remove(hitObject);
        }

        public override void Add(DrawableHitObject h)
        {
            if (!tryGetColumnForHitObject((ManiaHitObject)h.HitObject, out var column))
                return;

            column.Add(h);
        }

        public override bool Remove(DrawableHitObject h)
        {
            if (!tryGetColumnForHitObject((ManiaHitObject)h.HitObject, out var column))
                return false;

            return column.Remove(h);
        }

        private bool tryGetColumnForHitObject(ManiaHitObject hitObject, out Column column)
        {
            column = null!;

            int localColumn = hitObject.Column - firstColumnIndex;

            if (ManiaPlayfieldLayoutHelper.ShouldSkipBeatmapColumn(Definition, localColumn))
            {
                Logger.Log($"Skipping hit object in column {hitObject.Column} because Ez2Ac mode hides the last column of 14K beatmaps.");
                return false;
            }

            column = Columns[localColumn];
            return true;
        }

        public void Add(BarLine barLine) => base.Add(barLine);

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            // IgnoreHit / IgnoreMiss / ComboBreak 只是供分数统计使用的辅助结果（如 EZ2AC / Malody 的 LN 尾、
            // HoldNote 父物件、tick 等），不是真正打在玩家身上的判定档。池里本就不该有它们（见 Stage 构造），
            // 这里再兜一层：一旦被放进来，缺资源的皮肤会回退成默认文字判定，把内部结果名画到屏幕上。
            if (!result.Type.IsBasic())
                return;

            judgements.Clear(false);

            // 不用 setup 委托：`Apply` 只做字段赋值，真正的动画在下一帧 PrepareForUse 里跑。
            // 但 JudgementContainer.Add 会同步读 JudgedHitObject，所以赋值必须在 Add 之前。
            var drawableJudgement = judgementPooler.Get(result.Type, null);

            if (drawableJudgement == null)
                return;

            drawableJudgement.Apply(result, judgedObject);

            judgements.Add(drawableJudgement);
        }

        protected override void Update()
        {
            // Due to masking differences, it is not possible to get the width of the columns container automatically
            // While masking on effectively only the Y-axis, so we need to set the width of the bar line container manually
            barLineContainer.Width = columnFlow.Width;
        }
    }
}
