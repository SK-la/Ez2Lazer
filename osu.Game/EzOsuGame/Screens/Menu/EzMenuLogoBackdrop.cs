// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Material;
using osuTK;

namespace osu.Game.EzOsuGame.Screens.Menu
{
    public partial class EzMenuLogoBackdrop : CompositeDrawable
    {
        public readonly Bindable<bool> ShowDecorations = new Bindable<bool>(true);

        private readonly Box fill;
        private readonly EzMaterialContainer material;

        private Bindable<EzLogoBackdropStyle> style = null!;
        private Bindable<Colour4> solidColour = null!;
        private Bindable<double> blur = null!;
        private Bindable<double> opacity = null!;
        private Bindable<EzMaterialUiMode>? globalMaterialMode;

        // private Colour4 appliedLinked = EzLogoBackdropSampler.DefaultColour;

        public EzMenuLogoBackdrop()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                material = new EzMaterialContainer
                {
                    Depth = 1,
                },
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = EzLogoBackdropSampler.DefaultColour,
                },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(Ez2ConfigManager? ezConfig, EzLogoBackdropSampler? backdropSampler)
        {
            if (ezConfig == null)
                return;

            style = ezConfig.GetBindable<EzLogoBackdropStyle>(Ez2Setting.MenuLogoBackdropStyle);
            solidColour = ezConfig.GetBindable<Colour4>(Ez2Setting.MenuLogoBackdropColour);
            blur = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropBlur);
            opacity = ezConfig.GetBindable<double>(Ez2Setting.MenuLogoBackdropOpacity);
            globalMaterialMode = ezConfig.GetBindable<EzMaterialUiMode>(Ez2Setting.MaterialUiMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            style.BindValueChanged(_ => apply(), true);
            solidColour.BindValueChanged(_ => apply());
            blur.BindValueChanged(_ => apply());
            opacity.BindValueChanged(_ => apply());
            globalMaterialMode?.BindValueChanged(_ => apply());
        }

        // protected override void Update()
        // {
        //     base.Update();
        //
        //     if (sampler == null)
        //         return;
        //
        //     if (sampler.Colour == appliedLinked)
        //         return;
        //
        //     appliedLinked = sampler.Colour;
        //     applyLinkedFill();
        // }

        protected override void Update()
        {
            base.Update();

            updateMaterialVisibility();
        }

        private void apply()
        {
            // 亚克力风格是 logo 自己的设置。全局材质为默认（不干预）时据此保持亚克力，
            // 全局为亚克力/液态玻璃时由全局覆盖。单色与透明风格不涉及材质。
            material.OwnMode = EzMaterialUiMode.Acrylic;
            material.BlurSigma = new Vector2((float)blur.Value);

            bool solidLook = style.Value == EzLogoBackdropStyle.Solid;
            ShowDecorations.Value = solidLook;

            if (solidLook)
            {
                fill.Colour = solidColour.Value;
                fill.Alpha = 1;
            }
            else
            {
                fill.Alpha = 0;
            }

            updateMaterialVisibility();
        }

        /// <summary>
        /// 亚克力风格要申请整屏捕获。logo 是全局缓存的单实例，离开主菜单只是淡出而非移除，
        /// 因此必须按实际可见性释放捕获；否则进过一次主菜单后，后续所有界面
        /// （包括 gameplay 的 note 与数字）都会一直留在离屏缓冲里被重采样。
        /// </summary>
        private void updateMaterialVisibility()
        {
            bool visible = style != null
                           && style.Value == EzLogoBackdropStyle.Acrylic
                           && isEffectivelyVisible;

            if (material.MaterialVisible == visible)
                return;

            material.MaterialVisible = visible;
            material.SyncMaterialCaptureState();
        }

        private bool isEffectivelyVisible
        {
            get
            {
                for (Drawable? d = this; d != null; d = d.Parent)
                {
                    if (!d.IsPresent || d.Alpha <= 0)
                        return false;
                }

                return true;
            }
        }

        // private void applyLinkedFill()
        // {
        //     Colour4 colour = sampler?.Colour ?? EzLogoBackdropSampler.DefaultColour;
        //     appliedLinked = colour;
        //     fill.Colour = colour;
        //     fill.Alpha = (float)opacity.Value;
        // }
    }
}
