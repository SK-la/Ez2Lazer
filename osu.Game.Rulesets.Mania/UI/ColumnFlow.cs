// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// A <see cref="Drawable"/> which flows its contents according to the <see cref="Column"/>s in a <see cref="Stage"/>.
    /// Content can be added to individual columns via <see cref="SetContentForColumn"/>.
    /// </summary>
    /// <typeparam name="TContent">The type of content in each column.</typeparam>
    public partial class ColumnFlow<TContent> : CompositeDrawable
        where TContent : Drawable
    {
        /// <summary>
        /// All contents added to this <see cref="ColumnFlow{TContent}"/>.
        /// </summary>
        public TContent[] Content { get; }

        private readonly FillFlowContainer<Container<TContent>> columns;
        private readonly StageDefinition stageDefinition;

        public new bool Masking
        {
            get => base.Masking;
            set => base.Masking = value;
        }

        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);

        public ColumnFlow(StageDefinition stageDefinition)
        {
            this.stageDefinition = stageDefinition;
            Content = new TContent[stageDefinition.Columns];

            AutoSizeAxes = Axes.X;

            Masking = true;

            InternalChild = columns = new FillFlowContainer<Container<TContent>>
            {
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
            };

            for (int i = 0; i < stageDefinition.Columns; i++)
                columns.Add(new Container<TContent> { RelativeSizeAxes = Axes.Y });

            AddLayout(layout);
        }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private readonly Bindable<ManiaMobileLayout> mobileLayout = new Bindable<ManiaMobileLayout>();
        private readonly Bindable<double> columnWidthBindable = new Bindable<double>();
        private readonly Bindable<double> specialFactorBindable = new Bindable<double>();
        private readonly Bindable<EzColumnWidthStyle> ezColumnWidthStyle = new Bindable<EzColumnWidthStyle>();

        [BackgroundDependencyLoader]
        private void load(ManiaRulesetConfigManager? rulesetConfig)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.MobileLayout, mobileLayout);

            ezSkinConfig.BindWith(Ez2Setting.ColumnWidthStyle, ezColumnWidthStyle);
            ezSkinConfig.BindWith(Ez2Setting.ColumnWidth, columnWidthBindable);
            ezSkinConfig.BindWith(Ez2Setting.SpecialFactor, specialFactorBindable);
            ezColumnWidthStyle.BindValueChanged(v => updateColumnSize());
            columnWidthBindable.BindValueChanged(v => updateColumnSize());
            specialFactorBindable.BindValueChanged(v => updateColumnSize());

            mobileLayout.BindValueChanged(_ => invalidateLayout());
            skin.SourceChanged += invalidateLayout;
        }

        protected override void Update()
        {
            base.Update();

            if (!layout.IsValid)
            {
                updateColumnSize();
                layout.Validate();
            }
        }

        /// <summary>
        /// Sets the content of one of the columns of this <see cref="ColumnFlow{TContent}"/>.
        /// </summary>
        /// <param name="column">The index of the column to set the content of.</param>
        /// <param name="content">The content.</param>
        public void SetContentForColumn(int column, TContent content)
        {
            Content[column] = columns[column].Child = content;
        }

        private void invalidateLayout() => layout.Invalidate();

        private void updateColumnSize()
        {
            float mobileAdjust = 1f;

            if (RuntimeInfo.IsMobile && mobileLayout.Value == ManiaMobileLayout.LandscapeExpandedColumns)
            {
                // GridContainer+CellContainer containing this stage (gets split up for dual stages).
                Vector2? containingCell = this.FindClosestParent<Stage>()?.Parent?.DrawSize;

                // Will be null in tests.
                if (containingCell != null && containingCell.Value.X >= containingCell.Value.Y)
                {
                    float aspectRatio = containingCell.Value.X / containingCell.Value.Y;

                    // 2.83 is a mostly arbitrary scale-up (170 / 60, based on original implementation for argon)
                    mobileAdjust = 2.83f * Math.Min(1, 7f / stageDefinition.Columns);
                    // 1.92 is a "reference" mobile screen aspect ratio for phones.
                    // We should scale it back for cases like tablets which aren't so extreme.
                    mobileAdjust *= aspectRatio / 1.92f;
                }
            }

            for (int i = 0; i < stageDefinition.Columns; i++)
            {
                float leftSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                            new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.LeftColumnSpacing, i))
                                        ?.Value ?? Stage.COLUMN_SPACING;

                float rightSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                             new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.RightColumnSpacing, i))
                                         ?.Value ?? Stage.COLUMN_SPACING;

                columns[i].Margin = new MarginPadding { Left = leftSpacing, Right = rightSpacing };

                float? width = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                       new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, i))
                                   ?.Value;

                if (width == 0)
                {
                    columns[i].Width = 0;
                    columns[i].Margin = new MarginPadding { Left = 0, Right = 0 };
                    continue;
                }

                bool isSpecialColumn =
                    ezSkinConfig.IsSpecialColumn(stageDefinition.Columns, i);
                float ezWidth = (float)columnWidthBindable.Value * (isSpecialColumn ? (float)specialFactorBindable.Value : 1);

                switch (ezColumnWidthStyle.Value)
                {
                    case EzColumnWidthStyle.EzStyleProOnly:
                        var skinInfo = skinManager.CurrentSkinInfo.Value;
                        if (skinInfo.Value.Name.Contains("Ez Style Pro"))
                            width = ezWidth;
                        break;

                    case EzColumnWidthStyle.GlobalWidth:
                        width = ezWidth;
                        break;

                    case EzColumnWidthStyle.GlobalTotalWidth:
                        width = ezWidth * 10 / stageDefinition.Columns;
                        break;
                }

                // only used by default skin (legacy skins get defaults set in LegacyManiaSkinConfiguration)
                width ??= isSpecialColumn ? Column.SPECIAL_COLUMN_WIDTH : Column.COLUMN_WIDTH;

                columns[i].Width = width.Value * mobileAdjust;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= invalidateLayout;
        }
    }
}
