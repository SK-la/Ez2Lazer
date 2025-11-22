// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
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
    public partial class EzKeyArea : EzNoteBase, IKeyBindingHandler<ManiaAction>
    {
        private Container? container;
        private TextureAnimation? upSprite;
        private TextureAnimation? downSprite;
        protected virtual bool IsKeyPress => true;
        protected override bool UseColorization => true;

        private Bindable<string> stageName = null!;
        private Bindable<double> hitPositonBindable = null!;

        public EzKeyArea()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            container = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Fill,
            };

            Column.TopLevelContainer.Add(container);

            stageName = EZSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            hitPositonBindable = EZSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);

            loadAnimation();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            stageName.BindValueChanged(_ => loadAnimation(), true);
            hitPositonBindable.BindValueChanged(_ => OnConfigChanged(), true);
        }

        protected virtual string KeyBasicSuffix
        {
            get
            {
                if (EZSkinConfig.IsSpecialColumn(StageDefinition.Columns, Column.Index))
                    return "02";

                int logicalIndex = 0;

                for (int i = 0; i < Column.Index; i++)
                {
                    if (EZSkinConfig.IsSpecialColumn(StageDefinition.Columns, i))
                        logicalIndex++;
                }

                return logicalIndex % 2 == 0 ? "00" : "01";
            }
        }

        private void loadAnimation()
        {
            if (StageDefinition.Columns == 14 && Column.Index == 13) return;

            ClearInternal();

            upSprite = Factory.CreateStageKeys("keybase");
            downSprite = Factory.CreateStageKeys("keypress");
            downSprite.Alpha = 0;

            if (container != null)
            {
                container.Children = new[]
                {
                    upSprite,
                    downSprite,
                };
                OnConfigChanged();
            }
        }

        private void OnConfigChanged()
        {
            if (container != null)
                container.Y = 768f - (float)hitPositonBindable.Value + 4f;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
            {
                upSprite.FadeTo(0);
                downSprite.FadeTo(1);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == Column.Action.Value)
            {
                upSprite?.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(1);
                downSprite?.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(0);
            }
        }
    }
}
