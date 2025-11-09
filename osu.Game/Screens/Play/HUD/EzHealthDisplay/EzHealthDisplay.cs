
using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD.EzHealthDisplay
{
    public partial class EzHealthDisplay : EzHealthDisplayBase, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource("Bar height")]
        public BindableFloat BarHeight { get; } = new BindableFloat(20)
        {
            MinValue = 0,
            MaxValue = 64,
            Precision = 1
        };

        [SettingSource("Use relative size")]
        public BindableBool UseRelativeSize { get; } = new BindableBool(true);

        private EzHealthDisplayBar mainBar = null!;
        private EzHealthDisplayBar glowBar = null!;

        private ScheduledDelegate? resetMissBarDelegate;

        private bool displayingMiss => resetMissBarDelegate != null;

        private double glowBarValue;
        private double healthBarValue;

        public const float MAIN_PATH_RADIUS = 10f;
        private const float padding = MAIN_PATH_RADIUS * 2;
        private const float glow_path_radius = 40f;

        protected override string[] TextureSuffixes => new[] { "background", "mainbar", "glowbar" };

        public EzHealthDisplay()
        {
            Width = 0.98f;
        }

        protected override void LoadTextures()
        {
            base.LoadTextures();

            Content.Children = new Drawable[]
            {
                new EzHealthDisplayBackground(TextureFactory, TexturePrefix + "background")
                {
                    RelativeSizeAxes = Axes.Both
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(MAIN_PATH_RADIUS - glow_path_radius),
                    Child = glowBar = new EzHealthDisplayBar(TextureFactory, TexturePrefix + "glowbar")
                    {
                        RelativeSizeAxes = Axes.Both,
                        PathRadius = glow_path_radius,
                    }
                },
                mainBar = new EzHealthDisplayBar(TextureFactory, TexturePrefix + "mainbar")
                {
                    RelativeSizeAxes = Axes.Both,
                    PathRadius = MAIN_PATH_RADIUS,
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            HealthProcessor.NewJudgement += onNewJudgement;

            float previousWidth = Width;
            UseRelativeSize.BindValueChanged(v => RelativeSizeAxes = v.NewValue ? Axes.X : Axes.None, true);
            Width = previousWidth;

            BarHeight.BindValueChanged(_ => updateContentSize(), true);
        }

        private void onNewJudgement(JudgementResult result)
        {
            pendingMissAnimation |= !result.IsHit && result.HealthIncrease < 0;
        }

        private bool pendingMissAnimation;

        protected override void Update()
        {
            base.Update();

            healthBarValue = Interpolation.DampContinuously(healthBarValue, Current.Value, 50, Time.Elapsed);
            if (!displayingMiss)
                glowBarValue = Interpolation.DampContinuously(glowBarValue, Current.Value, 50, Time.Elapsed);

            mainBar.Alpha = (float)Interpolation.DampContinuously(mainBar.Alpha, Current.Value > 0 ? 1 : 0, 40, Time.Elapsed);
            glowBar.Alpha = (float)Interpolation.DampContinuously(glowBar.Alpha, glowBarValue > 0 ? 1 : 0, 40, Time.Elapsed);

            updatePathProgress();
            updateContentSize();
        }

        protected override void HealthChanged(bool increase)
        {
            if (Current.Value >= glowBarValue)
                finishMissDisplay();

            if (pendingMissAnimation)
            {
                triggerMissDisplay();
                pendingMissAnimation = false;
            }

            base.HealthChanged(increase);
        }

        protected override void FinishInitialAnimation(double value)
        {
            base.FinishInitialAnimation(value);
            this.TransformTo(nameof(healthBarValue), value, 500, Easing.OutQuint);
            this.TransformTo(nameof(glowBarValue), value, 250, Easing.OutQuint);
        }

        protected override void Flash()
        {
            base.Flash();

            if (!displayingMiss)
            {
                glowBar.Flash();
            }
        }

        private void triggerMissDisplay()
        {
            resetMissBarDelegate?.Cancel();
            resetMissBarDelegate = null;

            this.Delay(500).Schedule(() =>
            {
                this.TransformTo(nameof(glowBarValue), Current.Value, 300, Easing.OutQuint);
                finishMissDisplay();
            }, out resetMissBarDelegate);

            glowBar.SetDamageColour();
        }

        private void finishMissDisplay()
        {
            if (!displayingMiss)
                return;

            if (Current.Value > 0)
            {
                glowBar.ResetColour();
            }

            resetMissBarDelegate?.Cancel();
            resetMissBarDelegate = null;
        }

        private void updateContentSize()
        {
            float usableWidth = DrawWidth - padding;

            if (usableWidth < 0) enforceMinimumWidth();

            Content.Size = new Vector2(DrawWidth, BarHeight.Value + padding);

            void enforceMinimumWidth()
            {
                Axes relativeAxes = RelativeSizeAxes;
                RelativeSizeAxes = Axes.None;
                Width = padding;
                RelativeSizeAxes = relativeAxes;
            }
        }

        private void updatePathProgress()
        {
            mainBar.ProgressRange = new Vector2(0f, (float)healthBarValue);
            glowBar.ProgressRange = new Vector2((float)healthBarValue, (float)Math.Max(glowBarValue, healthBarValue));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            HealthProcessor.NewJudgement -= onNewJudgement;
        }
    }
}
