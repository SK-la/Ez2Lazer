// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ListExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Lists;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Objects
{
    /// <summary>
    /// A HitObject describes an object in a Beatmap.
    /// <para>
    /// HitObjects may contain more properties for which you should be checking through the IHas* types.
    /// </para>
    /// </summary>
    public class HitObject
    {
        /// <summary>
        /// A small adjustment to the start time of control points to account for rounding/precision errors.
        /// </summary>
        private const double control_point_leniency = 1;

        /// <summary>
        /// Invoked after <see cref="ApplyDefaults"/> has completed on this <see cref="HitObject"/>.
        /// </summary>
        // TODO: This has no implicit unbind flow. Currently, if a Playfield manages HitObjects it will leave a bound event on this and cause the
        // playfield to remain in memory.
        public event Action<HitObject> DefaultsApplied;

        // 只读属性（private set）而不是 public readonly 字段：Clone() 需要给副本换上自己的 bindable，
        // 而外部调用方依然无法替换引用——对外保证与 readonly 字段一致。
        public Bindable<double> StartTimeBindable { get; private set; } = new BindableDouble();

        /// <summary>
        /// The time at which the HitObject starts.
        /// </summary>
        public virtual double StartTime
        {
            get => StartTimeBindable.Value;
            set => StartTimeBindable.Value = value;
        }

        public BindableList<HitSampleInfo> SamplesBindable { get; private set; } = new BindableList<HitSampleInfo>();

        /// <summary>
        /// The samples to be played when this hit object is hit.
        /// <para>
        /// In the case of <see cref="IHasRepeats"/> types, this is the sample of the curve body
        /// and can be treated as the default samples for the hit object.
        /// </para>
        /// </summary>
        public IList<HitSampleInfo> Samples
        {
            get => SamplesBindable;
            set
            {
                SamplesBindable.Clear();
                SamplesBindable.AddRange(value);
            }
        }

        /// <summary>
        /// Any samples which may be used by this hit object that are non-standard.
        /// This is used only to preload these samples ahead of time.
        /// </summary>
        public virtual IList<HitSampleInfo> AuxiliarySamples => ImmutableList<HitSampleInfo>.Empty;

        /// <summary>
        /// Whether this <see cref="HitObject"/> is in Kiai time.
        /// </summary>
        [JsonIgnore]
        public bool Kiai { get; private set; }

        /// <summary>
        /// The hit windows for this <see cref="HitObject"/>.
        /// </summary>
        [JsonIgnore]
        public HitWindows HitWindows { get; set; }

        // 只读属性（private set）而不是 public readonly 字段：Clone() 需要给副本换上自己的列表，
        // 而外部调用方依然无法替换引用。
        private List<HitObject> nestedHitObjects = new List<HitObject>();

        /// <summary>
        /// 构建中的嵌套对象列表。<see cref="ApplyDefaults"/> 重建嵌套对象期间非 null，<see cref="AddNested"/>
        /// 会写入它；构建完成后才整体发布给 <see cref="nestedHitObjects"/>，因此读线程不会看到构建中的列表。
        /// 写入方由 <see cref="ApplyDefaults"/> 的对象锁串行化，所以这里不需要是线程局部的。
        /// </summary>
        private List<HitObject> nestedHitObjectsUnderConstruction;

        [JsonIgnore]
        public SlimReadOnlyListWrapper<HitObject> NestedHitObjects => nestedHitObjects.AsSlimReadOnly();

        /// <summary>
        /// Applies default values to this HitObject.
        /// </summary>
        /// <param name="controlPointInfo">The control points.</param>
        /// <param name="difficulty">The difficulty settings to use.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void ApplyDefaults(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty, CancellationToken cancellationToken = default)
        {
            // 同一个 HitObject 可能被多个消费者并发 ApplyDefaults：当源对象已是目标类型时，转换产物会与源共享
            // 实例，于是进图、后台难度计算、分析等各自转换时都在重建同一批对象。并发重建会在同一个 List 上竞争
            // Add/增长（增长的实现会替换底层数组），留下没写过的 null 槽位，只读的枚举方随即便会读到 null。
            // 这里按对象串行化写入方，列表再用整体替换的方式发布，使枚举方永远不会看到中间态。
            lock (this)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ApplyDefaultsToSelf(controlPointInfo, difficulty);

                // 在独立列表里重建，构建期间不动已发布的列表，完成后一次性发布引用。这样只读的枚举方（例如后台
                // 难度计算）要么看到旧的完整列表、要么看到新的完整列表，既不会读到 Clear 留下的 null 槽位，也
                // 不会撞上并发 Add 引发的 "Collection was modified"。
                List<HitObject> rebuiltNestedHitObjects = new List<HitObject>();
                nestedHitObjectsUnderConstruction = rebuiltNestedHitObjects;

                try
                {
                    CreateNestedHitObjects(cancellationToken);
                }
                finally
                {
                    nestedHitObjectsUnderConstruction = null;
                }

                if (this is IHasComboInformation hasCombo)
                {
                    foreach (HitObject hitObject in rebuiltNestedHitObjects)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (hitObject is IHasComboInformation n)
                        {
                            n.ComboIndexBindable.BindTo(hasCombo.ComboIndexBindable);
                            n.ComboIndexWithOffsetsBindable.BindTo(hasCombo.ComboIndexWithOffsetsBindable);
                            n.IndexInCurrentComboBindable.BindTo(hasCombo.IndexInCurrentComboBindable);
                        }
                    }
                }

                rebuiltNestedHitObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));

                // 一次性发布：自此读线程看到的是完整的新列表。
                nestedHitObjects = rebuiltNestedHitObjects;

                foreach (var h in nestedHitObjects)
                    h.ApplyDefaults(controlPointInfo, difficulty, cancellationToken);

                // `ApplyDefaults()` may be called multiple times on a single hitobject.
                // to prevent subscribing to `StartTimeBindable.ValueChanged` multiple times with the same callback,
                // remove the previous subscription (if present) before (re-)registering.
                StartTimeBindable.ValueChanged -= onStartTimeChanged;

                // this callback must be (re-)registered after default application
                // to ensure that the read of `this.GetEndTime()` within `onStartTimeChanged` doesn't return an invalid value
                // if `StartTimeBindable` is changed prior to default application.
                StartTimeBindable.ValueChanged += onStartTimeChanged;

                DefaultsApplied?.Invoke(this);
            }

            void onStartTimeChanged(ValueChangedEvent<double> time)
            {
                double offset = time.NewValue - time.OldValue;

                foreach (var nested in nestedHitObjects)
                    nested.StartTime += offset;
            }
        }

        protected virtual void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            Kiai = controlPointInfo.EffectPointAt(StartTime + control_point_leniency).KiaiMode;

            HitWindows ??= CreateHitWindows();
            HitWindows?.SetDifficulty(difficulty.OverallDifficulty);
        }

        /// <summary>
        /// Creates a copy of this hit object which owns everything <see cref="ApplyDefaults"/> and post-conversion mods write to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The copy shares no bindable with the original: <see cref="StartTimeBindable"/> and <see cref="SamplesBindable"/>
        /// are re-created (the <see cref="HitSampleInfo"/>s themselves are immutable and shared), and
        /// <see cref="NestedHitObjects"/> is emptied with the cached judgement and hit windows dropped, for
        /// <see cref="ApplyDefaults"/> to rebuild.
        /// </para>
        /// <para>
        /// Subclass state is carried over as-is, so a subclass that holds its own bindable-backed property (or its own
        /// nested objects) must override this to reset it — otherwise the copy still writes through to the original.
        /// See <c>ManiaHitObject.Clone</c> and <c>HoldNote.Clone</c>.
        /// </para>
        /// <para>
        /// Used by converters whose output must own its hit objects. Without it a converted beatmap aliases the decoded
        /// source, and an in-place post-conversion mod (column or time rewrites, keysound remapping) writes straight
        /// back into that source.
        /// </para>
        /// </remarks>
        public virtual HitObject Clone()
        {
            var clone = (HitObject)MemberwiseClone();

            clone.StartTimeBindable = new BindableDouble(StartTime);
            clone.SamplesBindable = new BindableList<HitSampleInfo>();
            clone.Samples = Samples;
            clone.nestedHitObjects = new List<HitObject>();
            clone.judgement = null;
            clone.HitWindows = null;

            // Subscribers belong to the original's lifetime; the copy must not announce itself to them.
            clone.DefaultsApplied = null;

            return clone;
        }

        protected virtual void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }

        protected void AddNested(HitObject hitObject) => (nestedHitObjectsUnderConstruction ?? nestedHitObjects).Add(hitObject);

        /// <summary>
        /// The <see cref="Judgement"/> that represents the scoring information for this <see cref="HitObject"/>.
        /// </summary>
        [JsonIgnore]
        public Judgement Judgement => judgement ??= CreateJudgement();

        private Judgement judgement;

        /// <summary>
        /// Should be overridden to create a <see cref="Judgement"/> that represents the scoring information for this <see cref="HitObject"/>.
        /// </summary>
        /// <remarks>
        /// For read access, use <see cref="Judgement"/> to avoid unnecessary allocations.
        /// </remarks>
        [NotNull]
        public virtual Judgement CreateJudgement() => new Judgement();

        /// <summary>
        /// Replaces the cached <see cref="Judgement"/> (e.g. when binding environment-specific judgement ranges on apply).
        /// </summary>
        public void SetJudgement(Judgement judgement) => this.judgement = judgement;

        /// <summary>
        /// Clears the cached <see cref="Judgement"/> so the next access re-invokes <see cref="CreateJudgement"/>.
        /// </summary>
        public void ResetJudgement() => judgement = null;

        /// <summary>
        /// Creates the <see cref="HitWindows"/> for this <see cref="HitObject"/>.
        /// This can be null to indicate that the <see cref="HitObject"/> has no <see cref="HitWindows"/> and timing errors should not be displayed to the user.
        /// <para>
        /// This will only be invoked if <see cref="HitWindows"/> hasn't been set externally (e.g. from a <see cref="BeatmapConverter{T}"/>.
        /// </para>
        /// </summary>
        [NotNull]
        protected virtual HitWindows CreateHitWindows() => new DefaultHitWindows();

        /// <summary>
        /// The maximum offset from the end time of <see cref="HitObject"/> at which this <see cref="HitObject"/> can be judged.
        /// <para>
        /// Defaults to the miss window.
        /// </para>
        /// </summary>
        public virtual double MaximumJudgementOffset => HitWindows?.WindowFor(HitResult.Miss) ?? 0;

        public IList<HitSampleInfo> CreateSlidingSamples()
        {
            var slidingSamples = new List<HitSampleInfo>();

            var normalSample = Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL);
            if (normalSample != null)
                slidingSamples.Add(normalSample.With("sliderslide"));

            var whistleSample = Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_WHISTLE);
            if (whistleSample != null)
                slidingSamples.Add(whistleSample.With("sliderwhistle"));

            return slidingSamples;
        }

        /// <summary>
        /// Create a <see cref="HitSampleInfo"/> based on the sample settings of the first <see cref="HitSampleInfo.HIT_NORMAL"/> sample in <see cref="Samples"/>.
        /// If no sample is available, sane default settings will be used instead.
        /// </summary>
        /// <remarks>
        /// In the case an existing sample exists, all settings apart from the sample name will be inherited. This includes volume, bank and suffix.
        /// </remarks>
        /// <param name="sampleName">The name of the sample.</param>
        /// <returns>A populated <see cref="HitSampleInfo"/>.</returns>
        public HitSampleInfo CreateHitSampleInfo(string sampleName = HitSampleInfo.HIT_NORMAL)
        {
            // As per stable, all non-normal "addition" samples should use the same bank.
            if (sampleName != HitSampleInfo.HIT_NORMAL)
            {
                if (Samples.FirstOrDefault(s => s.Name != HitSampleInfo.HIT_NORMAL) is HitSampleInfo existingAddition)
                    return existingAddition.With(newName: sampleName);
            }

            // Fall back to using the normal sample bank otherwise.
            if (Samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL) is HitSampleInfo existingNormal)
                return existingNormal.With(newName: sampleName, newEditorAutoBank: true);

            return new HitSampleInfo(sampleName);
        }

        public override string ToString() => $"{GetType().ReadableName()} @ {StartTime}";
    }

    public static class HitObjectExtensions
    {
        /// <summary>
        /// Returns the end time of this object.
        /// </summary>
        /// <remarks>
        /// This returns the <see cref="IHasDuration.EndTime"/> where available, falling back to <see cref="HitObject.StartTime"/> otherwise.
        /// </remarks>
        /// <param name="hitObject">The object.</param>
        /// <returns>The end time of this object.</returns>
        public static double GetEndTime(this HitObject hitObject) => (hitObject as IHasDuration)?.EndTime ?? hitObject.StartTime;
    }
}
