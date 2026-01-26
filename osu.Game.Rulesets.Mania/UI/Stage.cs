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
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.LAsEzExtensions.Configuration;
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
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Play;
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

        [Resolved]
        private Player? player { get; set; }

        [Resolved(canBeNull: true)]
        private IBindable<WorkingBeatmap>? beatmap { get; set; }

        private Bindable<float> uiScale = null!;
        private Bindable<double> osuConfigDim = null!;
        private Bindable<bool> editorShowStoryboard = null!;
        private Bindable<double> columnDim = null!;
        private Bindable<double> columnBlur = null!;
        private readonly Bindable<bool> showBlurStoryboard = new Bindable<bool>();
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = new Bindable<WorkingBeatmap>();

        private readonly Box dimBox;
        private readonly Container backgroundContainer;
        private readonly BackgroundScreenBeatmap.DimmableBackground maniaMaskedDimmable;

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
                backgroundContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = maniaMaskedDimmable = new BackgroundScreenBeatmap.DimmableBackground
                    {
                        RelativeSizeAxes = Axes.None,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                    }
                },
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
                // bool isSpecial = ezSkinConfig.IsSpecialColumn(definition.Columns, i);

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

            skin.SourceChanged += onSkinChanged;
            onSkinChanged();

            ezSkinConfig.KeyMode = Definition.Columns; //确保 KeyMode 已设置正确
            ezSkinConfig.ColumnTotalWidth = DrawWidth; //确保 ColumnTotalWidth 已设置正确

            uiScale = osuConfig.GetBindable<float>(OsuSetting.UIScale);
            osuConfigDim = osuConfig.GetBindable<double>(OsuSetting.DimLevel);
            editorShowStoryboard = osuConfig.GetBindable<bool>(OsuSetting.EditorShowStoryboard);
            editorShowStoryboard.BindValueChanged(_ => loadBackgroundAsync());

            columnDim = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnDim);
            columnDim.BindValueChanged(v =>
            {
                dimBox.Alpha = (float)Math.Max(v.NewValue, osuConfigDim.Value / 2);
            }, true);

            bindWorkingBeatmapSource();
            loadBackgroundAsync();
            columnBlur = ezSkinConfig.GetBindable<double>(Ez2Setting.ColumnBlur);
            columnBlur.BindValueChanged(v => maniaMaskedDimmable.BlurAmount.Value = (float)v.NewValue * 50, true);
        }

        private void bindWorkingBeatmapSource()
        {
            // Prefer the beatmap provided by Player (gameplay). In editor, Player will be missing,
            // but a bindable beatmap is still available via dependency injection.
            workingBeatmap.ValueChanged -= onWorkingBeatmapChanged;
            workingBeatmap.UnbindAll();

            // Rebind to an appropriate upstream source.
            // GetBoundCopy() is used because we may only have access to IBindable<T> (editor), not Bindable<T>.
            IBindable<WorkingBeatmap>? newWorkingBeatmap = null;

            if (player?.Beatmap.Value != null)
                newWorkingBeatmap = player.Beatmap.GetBoundCopy();
            else if (beatmap != null)
                newWorkingBeatmap = beatmap.GetBoundCopy();

            workingBeatmap = newWorkingBeatmap ?? new Bindable<WorkingBeatmap>();

            // Editor may swap DummyWorkingBeatmap -> real beatmap asynchronously.
            // Refresh the background as soon as the bindable updates.
            workingBeatmap.ValueChanged += onWorkingBeatmapChanged;
        }

        private void onWorkingBeatmapChanged(ValueChangedEvent<WorkingBeatmap> _)
            => loadBackgroundAsync();

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

            workingBeatmap.ValueChanged -= onWorkingBeatmapChanged;
            workingBeatmap.UnbindAll();

            base.Dispose(isDisposing);

            if (currentSkin.IsNotNull())
                currentSkin.SourceChanged -= onSkinChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            NewResult += OnNewResult;
        }

        private void updateDimmableAlphaOpen(bool _ = true)
        {
            maniaMaskedDimmable.Alpha = _ ? 1 : 0;
        }

        private void loadBackgroundAsync()
        {
            if (player?.DimmableStoryboard != null)
            {
                showBlurStoryboard.Value = player.DimmableStoryboard.ContentDisplayed &&
                                           !player.DimmableStoryboard.HasStoryboardEnded.Value;

                if (showBlurStoryboard.Value)
                {
                    updateDimmableAlphaOpen(false);
                    return;
                }
            }

            if (workingBeatmap.Value != null)
            {
                updateDimmableAlphaOpen();
                var maskedBackground = new BeatmapBackground(workingBeatmap.Value);
                maskedBackground.FadeInFromZero(500, Easing.OutQuint);
                maniaMaskedDimmable.Background = maskedBackground;

                // Gameplay: bind to player state. Editor: use editor settings + beatmap storyboard metadata.
                if (player != null)
                {
                    maniaMaskedDimmable.StoryboardReplacesBackground.BindTo(player.StoryboardReplacesBackground);
                    maniaMaskedDimmable.IgnoreUserSettings.BindTo(new Bindable<bool>(true));
                    maniaMaskedDimmable.IsBreakTime.BindTo(player.IsBreakTime);
                }
                else
                {
                    maniaMaskedDimmable.StoryboardReplacesBackground.UnbindAll();
                    maniaMaskedDimmable.IgnoreUserSettings.UnbindAll();
                    maniaMaskedDimmable.IsBreakTime.UnbindAll();

                    // We don't have gameplay break tracking in editor, so assume not in break.
                    ((Bindable<bool>)maniaMaskedDimmable.IsBreakTime).Value = false;

                    // Keep behaviour consistent with gameplay mania background screen:
                    // ignore user storyboard setting and drive replacement ourselves.
                    maniaMaskedDimmable.IgnoreUserSettings.Value = true;

                    // Match editor behaviour: only consider storyboard replacement when editor storyboard display is enabled.
                    // (EditorBackgroundScreen also uses EditorShowStoryboard).
                    maniaMaskedDimmable.StoryboardReplacesBackground.Value = editorShowStoryboard.Value
                                                                             && workingBeatmap.Value.Storyboard.ReplacesBackground
                                                                             && workingBeatmap.Value.Storyboard.HasDrawable;
                }
            }
            else
            {
                updateDimmableAlphaOpen(false);
                Logger.Log("Working beatmap is null, cannot load background.", LoggingTarget.Runtime, LogLevel.Error);
            }
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

            if (player != null) maniaMaskedDimmable.Size = player.DrawSize / 0.95f / uiScale.Value;
        }
    }
}
