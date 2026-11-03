// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.SkinEditor
{
    internal enum SkinHudSnapAlignKind
    {
        None,
        Edge,
        Center,
        Spacing,
    }

    internal enum SkinHudSnapReferenceKind
    {
        None,
        Container,
        Component,
    }

    internal enum SkinHudSnapEdge
    {
        Min,
        Mid,
        Max,
    }

    internal enum SkinHudSnapGapMode
    {
        /// <summary>
        /// gap = selection edge - target line.
        /// </summary>
        SelectionMinusTarget,

        /// <summary>
        /// gap = target line - selection edge.
        /// </summary>
        TargetMinusSelection,
    }

    internal struct SkinHudSnapAxisSession
    {
        public SkinHudSnapAlignKind Kind;
        public SkinHudSnapEdge SelectionEdge;
        public SkinHudSnapGapMode GapMode;
        public float TargetLine;
        public SkinHudSnapReferenceKind ReferenceKind;
        public int ReferenceComponentIndex;

        /// <summary>
        /// Active spacing target (configured distance, then 0 after intent threshold).
        /// </summary>
        public float SpacingTarget;

        public float SpacingIntent;

        public readonly bool IsActive => Kind != SkinHudSnapAlignKind.None;

        public readonly bool IsComponentEdgeOrCenter =>
            Kind is SkinHudSnapAlignKind.Edge or SkinHudSnapAlignKind.Center
            && ReferenceKind == SkinHudSnapReferenceKind.Component;

        public readonly bool SupportsComponentOrthogonalSpacing(int componentIndex) =>
            IsComponentEdgeOrCenter && ReferenceComponentIndex == componentIndex;

        public void Reset()
        {
            Kind = SkinHudSnapAlignKind.None;
            SelectionEdge = SkinHudSnapEdge.Min;
            GapMode = SkinHudSnapGapMode.SelectionMinusTarget;
            TargetLine = 0;
            ReferenceKind = SkinHudSnapReferenceKind.None;
            ReferenceComponentIndex = -1;
            SpacingTarget = 0;
            SpacingIntent = 0;
        }
    }

    internal partial class SkinHudSnapDragState
    {
        public SkinHudSnapAxisSession X;
        public SkinHudSnapAxisSession Y;

        public void Reset()
        {
            X.Reset();
            Y.Reset();
        }
    }
}
