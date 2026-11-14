// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitTarget : EzNote
    {
        protected override ManiaNoteDisplayColourPart DisplayColourPart => ManiaNoteDisplayColourPart.HitTarget;

        protected override bool ShowSeparators => false;

        /// <summary>
        /// 重载判定用前缀，与 <see cref="EzNoteBase.LoadEzProNoteComponent"/> 里实际加载路径无关。
        /// 贴图：<see cref="ManiaPlayfieldNoteDisplayTint.UseTintBaseTexture"/> 为真→白底；为假→列型/降级查找（与 note 同源）。
        /// 乘色：<see cref="IEzSkinInfo.HitTargetColouringEnabled"/> 与列色开关；靶不吃节拍线时刻色。
        /// </summary>
        protected override string ColorPrefix => EzColumnResourceLookup.NOTE_TEXTURE_AFFIX_A;

        private readonly IBindable<double> hitTargetFloatFixed = new Bindable<double>();
        private readonly IBindable<double> hitTargetAlpha = new Bindable<double>();
        private readonly IBindable<bool> hitTargetColouringEnabled = new Bindable<bool>();

        [Resolved(CanBeNull = true)]
        private ManiaPlayfieldNoteDisplayTint? noteDisplayTint { get; set; }

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// The beat this bobs to. Taking it from the shared tracker (rather than the metadata BPM captured at load)
        /// is what makes it follow every timing section and any live rate change.
        /// </summary>
        private EzBeatmapSpeedTracker speedTracker = null!;

        private double beatPhase;

        private IFrameBasedClock? hostClock;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Depth = 1;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo, GameHost host)
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            // 使用 host 的 update 线程时钟作为独立时间源，从而在暂停时仍能继续动画。
            hostClock = host.UpdateThread.Clock;

            AddInternal(speedTracker = new EzBeatmapSpeedTracker());

            hitTargetFloatFixed.BindTo(ezSkinInfo.HitTargetFloatFixed);
            hitTargetAlpha.BindTo(ezSkinInfo.HitTargetAlpha);
            hitTargetColouringEnabled.BindTo(ezSkinInfo.HitTargetColouringEnabled);

            hitTargetAlpha.BindValueChanged(v => Alpha = (float)v.NewValue, true);
            hitTargetColouringEnabled.BindValueChanged(_ => UpdateColor(), true);
        }

        /// <summary>
        /// 命中靶没有命中时刻；关闭靶着色时乘色为白（贴图仍由 tint 基/列型路径决定，见 <see cref="ColorPrefix"/> 说明）。
        /// </summary>
        protected override void UpdateColor()
        {
            if (!hitTargetColouringEnabled.Value || noteDisplayTint == null)
            {
                Colour = Colour4.White;
                return;
            }

            Colour = noteDisplayTint.GetDisplayColour(ManiaNoteDisplayColourPart.HitTarget, Column, 0);
        }

        protected override void Update()
        {
            base.Update();

            updatePosition();
        }

        private void updatePosition()
        {
            double beatLength = speedTracker.BeatLength.Value;

            if (beatLength <= 0)
                return;

            // 平滑正弦波效果
            // The host clock is real time (it deliberately keeps running while gameplay is paused), so the audible
            // rate has to convert it into song time; the gameplay clock's elapsed time would already carry it.
            beatPhase = hostClock != null
                ? EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, hostClock.ElapsedFrameTime, beatLength, speedTracker.Rate.Value)
                : EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, gameplayClock.ElapsedFrameTime, beatLength);

            double smoothValue = 0.3 * Math.Sin(beatPhase * 2 * Math.PI);
            Y = (float)(smoothValue * hitTargetFloatFixed.Value);
        }
    }
}
