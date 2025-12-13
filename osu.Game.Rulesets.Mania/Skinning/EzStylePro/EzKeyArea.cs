// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;
using osu.Game.LAsEzExtensions;
using osu.Game.LAsEzExtensions.Configuration;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzKeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
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
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        private Bindable<string> stageName = null!;
        private Bindable<double> hitPositonBindable = null!;

        private double bpm;
        private double beatInterval;
        private int keyMode;
        private int columnIndex;

        public EzKeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
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

            stageName = ezSkinConfig.GetBindable<string>(EzSkinSetting.StageName);
            hitPositonBindable = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);

            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
            beatInterval = 60000 / bpm * 64;

            bool isFreeSize = free_size_stages.Contains(stageName.Value);

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

            stageName.BindValueChanged(_ => loadAnimation(), true);
            hitPositonBindable.BindValueChanged(_ => OnConfigChanged(), true);
            ezSkinConfig.OnNoteSizeChanged += OnConfigChanged;
        }

        protected virtual string KeySuffix
        {
            get
            {
                var typeList = ezSkinConfig.GetColumnTypes(keyMode);

                switch (typeList[columnIndex])
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
            float actualPanelWidth = factory.GetNoteSize(keyMode, columnIndex, true).Value.X;
            float baseWidth = 410f / keyMode;
            float scale = actualPanelWidth / baseWidth;

            sprite.Scale = new Vector2(2f, 2 * scale);

            sprite.Y = 768f - (float)hitPositonBindable.Value + 2f;
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

        private static readonly HashSet<string> free_size_stages = new HashSet<string>
        {
            "AZURE_EXPRESSION",
            "Celeste_Lumiere",
            "EC_Wheel",
            "EVOLVE",
            "Fortress3_Gear",
            "Fortress3_Modern",
            "GC",
            "NIGHT_FALL",
            "TANOc2",
            "TECHNIKA",
        };

        public enum EzEnumGameThemeNameForFreeSize
        {
            // ReSharper disable InconsistentNaming
            AZURE_EXPRESSION,
            Celeste_Lumiere,
            EC_Wheel,
            EVOLVE,
            Fortress3_Gear,
            Fortress3_Modern,
            GC,
            NIGHT_FALL,
            TANOc2,
            TECHNIKA,
        }
    }
}
