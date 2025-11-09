// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.LAsEzExtensions;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzKeyArea : LegacyManiaColumnElement, IKeyBindingHandler<ManiaAction>
    {
        private Drawable container = null!;
        private Drawable upSprite = null!;
        private Drawable downSprite = null!;
        protected virtual bool IsKeyPress => true;
        protected virtual bool UseColorization => true;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzKeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            OnSkinChanged();
        }

        protected virtual string KeyBasicSuffix
        {
            get
            {
                if (ezSkinConfig.IsSpecialColumn(stageDefinition.Columns, column.Index))
                    return "02";

                int logicalIndex = 0;

                for (int i = 0; i < column.Index; i++)
                {
                    if (ezSkinConfig.IsSpecialColumn(stageDefinition.Columns, i))
                        logicalIndex++;
                }

                return logicalIndex % 2 == 0 ? "00" : "01";
            }
        }

        private void loadAnimation()
        {
            ClearInternal();

            upSprite = factory.CreateStage("keybase");
            downSprite = factory.CreateStage("keypress");
            downSprite.Alpha = 0;

            container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new[]
                {
                    upSprite,
                    downSprite,
                }
            };

            OnConfigChanged();
            AddInternal(container);
        }

        private void OnConfigChanged()
        {
        }

        private void OnSkinChanged() => loadAnimation();

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                upSprite.FadeTo(0);
                downSprite.FadeTo(1);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                upSprite.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(1);
                downSprite.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(0);
            }
        }
    }
}
