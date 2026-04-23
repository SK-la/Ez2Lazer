// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    public abstract partial class DrawableManiaHitObject : DrawableHitObject<ManiaHitObject>
    {
        private const int cleanup_interval = 64;

        private static readonly Dictionary<string, SampleTriggerMarker> last_sample_triggers = new Dictionary<string, SampleTriggerMarker>();
        private static int playInvocationCount;

        /// <summary>
        /// The <see cref="ManiaAction"/> which causes this <see cref="DrawableManiaHitObject{TObject}"/> to be hit.
        /// </summary>
        protected readonly IBindable<ManiaAction> Action = new Bindable<ManiaAction>();

        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        [Resolved(canBeNull: true)]
        private ManiaPlayfield playfield { get; set; }

        protected override float SamplePlaybackPosition
        {
            get
            {
                if (playfield == null)
                    return base.SamplePlaybackPosition;

                return (float)HitObject.Column / playfield.TotalColumns;
            }
        }

        /// <summary>
        /// Whether this <see cref="DrawableManiaHitObject"/> can be hit, given a time value.
        /// If non-null, judgements will be ignored whilst the function returns false.
        /// </summary>
        public Func<DrawableHitObject, double, bool> CheckHittable;

        protected DrawableManiaHitObject(ManiaHitObject hitObject)
            : base(hitObject)
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader(true)]
        private void load([CanBeNull] IBindable<ManiaAction> action, [NotNull] IScrollingInfo scrollingInfo)
        {
            if (action != null)
                Action.BindTo(action);

            Direction.BindTo(scrollingInfo.Direction);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected virtual void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            Anchor = Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        // 在同一时间点上，note 不叠加播放重复的音效
        public override void PlaySamples()
        {
            if (Samples == null)
                return;

            if (++playInvocationCount % cleanup_interval == 0)
                cleanupStaleSampleMarkers();

            string sampleSetKey = getSampleSetKey();
            double hitObjectTime = HitObject.StartTime;
            double triggerTime = Time.Current;
            IClock clock = Clock;

            if (last_sample_triggers.TryGetValue(sampleSetKey, out var marker))
            {
                if (marker.ClockReference.TryGetTarget(out var markerClock) && ReferenceEquals(markerClock, clock))
                {
                    bool rewound = triggerTime < marker.TriggerTime;

                    if (!rewound && Math.Abs(marker.HitObjectTime - hitObjectTime) < 0.01)
                        return;
                }
            }

            last_sample_triggers[sampleSetKey] = new SampleTriggerMarker(new WeakReference<IClock>(clock), hitObjectTime, triggerTime);

            base.PlaySamples();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Miss:
                    this.FadeOut(150, Easing.In);
                    break;

                case ArmedState.Hit:
                    this.FadeOut();
                    break;
            }
        }

        /// <summary>
        /// Causes this <see cref="DrawableManiaHitObject"/> to get missed, disregarding all conditions in implementations of <see cref="DrawableHitObject.CheckForResult"/>.
        /// </summary>
        public virtual void MissForcefully() => ApplyMinResult();

        private string getSampleSetKey()
            => string.Join("|", HitObject.Samples.Cast<ISampleInfo>().Select(sample => string.Join(",", sample.LookupNames)));

        // 清理掉已经失效的 sample trigger marker，避免字典无限增长
        private static void cleanupStaleSampleMarkers()
        {
            if (last_sample_triggers.Count == 0)
                return;

            List<string> staleKeys = null;

            foreach ((string key, var marker) in last_sample_triggers)
            {
                if (marker.ClockReference.TryGetTarget(out _))
                    continue;

                staleKeys ??= new List<string>();
                staleKeys.Add(key);
            }

            if (staleKeys == null)
                return;

            foreach (string key in staleKeys)
                last_sample_triggers.Remove(key);
        }

        private readonly record struct SampleTriggerMarker(WeakReference<IClock> ClockReference, double HitObjectTime, double TriggerTime);
    }

    public abstract partial class DrawableManiaHitObject<TObject> : DrawableManiaHitObject
        where TObject : ManiaHitObject
    {
        public new TObject HitObject => (TObject)base.HitObject;

        protected DrawableManiaHitObject(TObject hitObject)
            : base(hitObject)
        {
        }
    }
}
