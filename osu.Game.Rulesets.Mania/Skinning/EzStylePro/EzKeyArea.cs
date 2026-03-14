// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzKeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private IBindable<string> stageNameBindable = null!;
        private IBindable<double> hitPositonBindable = null!;
        private Bindable<Vector2> noteSizeBindable = null!;

        private Container sprite = null!;
        private TextureAnimation? upSprite;
        private TextureAnimation? downSprite;
        protected virtual bool IsKeyPress => true;
        protected virtual bool UseColorization => true;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        private double bpm;
        private double beatInterval;
        private int keyMode;
        private int columnIndex;

        public EzKeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(Column column, IEzSkinInfo ezSkinInfo)
        {
            sprite = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                RelativeSizeAxes = Axes.X,
                FillMode = FillMode.Stretch,
            };

            keyMode = stageDefinition.Columns;
            columnIndex = column.Index;

            stageNameBindable = ezSkinInfo.StageName;
            hitPositonBindable = ezSkinInfo.HitPosition;
            noteSizeBindable = column.EzNoteSizeBindable;

            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
            beatInterval = 60000 / bpm * 64;

            bool isFreeSize = EzProHelper.FREE_SIZE_STAGES.Contains(stageNameBindable.Value);

            if (isFreeSize)
            {
                sprite.RelativeSizeAxes = Axes.None;
                sprite.AutoSizeAxes = Axes.Both;
                sprite.Scale = new Vector2(2f);
                AddInternal(sprite);
            }
            else
            {
                column.TopLevelContainer.Add(sprite);
            }

            loadAnimation();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            stageNameBindable.BindValueChanged(_ => loadAnimation(), true);
            hitPositonBindable.BindValueChanged(_ => OnConfigChanged(), true);
            noteSizeBindable.BindValueChanged(_ => OnConfigChanged());
        }

        protected virtual string KeySuffix
        {
            get
            {
                var type = ezSkinConfig.GetColumnTypeFast(keyMode, columnIndex);

                switch (type)
                {
                    case EzColumnType.A:
                        return "0";

                    case EzColumnType.B:
                        return "1";

                    case EzColumnType.S:
                    case EzColumnType.P:
                    case EzColumnType.E:
                        return "2";

                    default:
                        return "0";
                }
            }
        }

        private void loadAnimation()
        {
            if (keyMode == 14 && columnIndex == 13) return;

            // ClearInternal();
            upSprite?.ClearFrames();
            downSprite?.ClearFrames();

            upSprite = factory.CreateStageKeys("KeyBase", KeySuffix);
            downSprite = factory.CreateStageKeys("KeyPress", KeySuffix);

            // upSprite.DefaultFrameLength = beatInterval;
            // downSprite.DefaultFrameLength = beatInterval;
            downSprite.Alpha = 0;

            // sprite.Clear();
            sprite.Add(upSprite);
            sprite.Add(downSprite);

            OnConfigChanged();
        }

        private void OnConfigChanged()
        {
            float actualPanelWidth = noteSizeBindable.Value.X;
            float baseWidth = 410f / keyMode;
            float newScale = 2f * (actualPanelWidth / baseWidth);
            float newY = 768f - (float)hitPositonBindable.Value + 2f;

            // 只在值实际改变时更新
            if (MathF.Abs(sprite.Scale.X - newScale) > 0.001f)
            {
                sprite.Scale = new Vector2(newScale, newScale);
            }

            if (MathF.Abs(sprite.Y - newY) > 0.001f)
            {
                sprite.Y = newY;
            }
        }

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
                upSprite?.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(1);
                downSprite?.Delay(LegacyHitExplosion.FADE_IN_DURATION).FadeTo(0);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                stageNameBindable.UnbindBindings();
                hitPositonBindable.UnbindBindings();
                noteSizeBindable.UnbindBindings();
            }

            base.Dispose(isDisposing);
        }
    }
}
