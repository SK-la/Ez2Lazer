// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.EzMania;
using osu.Game.Skinning;

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
        private readonly int displayColumns;

        public new bool Masking
        {
            get => base.Masking;
            set => base.Masking = value;
        }

        private readonly LayoutValue layout = new LayoutValue(Invalidation.DrawSize);

        public ColumnFlow(StageDefinition stageDefinition)
        {
            this.stageDefinition = stageDefinition;
            displayColumns = ManiaPlayfieldLayoutHelper.GetDisplayColumnCount(stageDefinition);
            Content = new TContent[displayColumns];

            AutoSizeAxes = Axes.X;

            Masking = true;

            InternalChild = columns = new FillFlowContainer<Container<TContent>>
            {
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Direction = FillDirection.Horizontal,
            };

            for (int i = 0; i < displayColumns; i++)
                columns.Add(new Container<TContent> { RelativeSizeAxes = Axes.Y });

            AddLayout(layout);
        }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        private readonly Bindable<ManiaMobileLayout> mobileLayout = new Bindable<ManiaMobileLayout>();
        private readonly Bindable<double> columnWidthBindable = new Bindable<double>();
        private readonly Bindable<double> specialFactorBindable = new Bindable<double>();
        private readonly Bindable<ColumnWidthStyle> ezColumnWidthStyle = new Bindable<ColumnWidthStyle>();
        private Action<int, int, EzColumnType>? onColumnTypeChangedHandler;

        [BackgroundDependencyLoader]
        private void load(ManiaRulesetConfigManager? rulesetConfig)
        {
            rulesetConfig?.BindWith(ManiaRulesetSetting.MobileLayout, mobileLayout);

            ezSkinConfig.BindWith(Ez2Setting.ColumnWidthStyle, ezColumnWidthStyle);
            ezSkinConfig.BindWith(Ez2Setting.ColumnWidth, columnWidthBindable);
            ezSkinConfig.BindWith(Ez2Setting.SpecialFactor, specialFactorBindable);
            ezColumnWidthStyle.BindValueChanged(v => invalidateLayout());
            columnWidthBindable.BindValueChanged(v => invalidateLayout());
            specialFactorBindable.BindValueChanged(v => invalidateLayout());

            onColumnTypeChangedHandler = onColumnTypeChanged;
            ezSkinConfig.ColumnTypeChanged += onColumnTypeChangedHandler;

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
            float mobileAdjust = ManiaPlayfieldLayoutHelper.CalculateMobileAdjust(
                stageDefinition.Columns,
                mobileLayout.Value,
                this.FindClosestParent<Stage>()?.Parent?.DrawSize);

            for (int i = 0; i < displayColumns; i++)
            {
                var columnSize = ManiaPlayfieldLayoutHelper.CalculateColumnSize(
                    i,
                    stageDefinition.Columns,
                    skin,
                    skinManager,
                    ezSkinConfig,
                    mobileAdjust,
                    applyGlobalWidthSettings: true);

                if (columnSize.Width == 0)
                {
                    columns[i].Width = 0;
                    columns[i].Margin = new MarginPadding { Left = 0, Right = 0 };
                    continue;
                }

                columns[i].Width = columnSize.Width;
                columns[i].Margin = columnSize.Margin;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (onColumnTypeChangedHandler != null)
                ezSkinConfig.ColumnTypeChanged -= onColumnTypeChangedHandler;

            if (skin.IsNotNull())
                skin.SourceChanged -= invalidateLayout;
        }

        private void onColumnTypeChanged(int keyMode, int columnIndex, EzColumnType type)
        {
            if (keyMode == stageDefinition.Columns)
                invalidateLayout();
        }
    }
}
