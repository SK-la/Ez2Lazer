// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.SkinEditor
{
    /// <summary>
    /// Grid-style HUD reference alignment: per-axis edge, center, or spacing constraints.
    /// Component same-axis: adjacent 0-gap edges and center only (no configured spacing).
    /// Configured spacing on an axis applies to container edges only, except orthogonal spacing to a
    /// component which requires edge/center alignment to that same component on the other axis.
    /// </summary>
    internal static class SkinHudSnapHelper
    {
        /// <summary>
        /// Enter/exit threshold for every alignment mode (parent-local px).
        /// </summary>
        public const float THRESHOLD = 5f;

        private readonly struct Bounds
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Top;
            public readonly float Bottom;

            public Bounds(float left, float right, float top, float bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public float HCenter => (Left + Right) * 0.5f;
            public float VCenter => (Top + Bottom) * 0.5f;

            public Bounds Offset(float dx, float dy) => new Bounds(Left + dx, Right + dx, Top + dy, Bottom + dy);
        }

        private readonly struct SnapCandidate
        {
            public readonly SkinHudSnapAlignKind Kind;
            public readonly SkinHudSnapEdge SelectionEdge;
            public readonly SkinHudSnapGapMode GapMode;
            public readonly float TargetLine;
            public readonly SkinHudSnapReferenceKind ReferenceKind;
            public readonly int ReferenceComponentIndex;

            public SnapCandidate(
                SkinHudSnapAlignKind kind,
                SkinHudSnapEdge selectionEdge,
                SkinHudSnapGapMode gapMode,
                float targetLine,
                SkinHudSnapReferenceKind referenceKind,
                int referenceComponentIndex = -1)
            {
                Kind = kind;
                SelectionEdge = selectionEdge;
                GapMode = gapMode;
                TargetLine = targetLine;
                ReferenceKind = referenceKind;
                ReferenceComponentIndex = referenceComponentIndex;
            }
        }

        public static Vector2 ApplySnap(
            SkinnableContainer container,
            IReadOnlyList<Drawable> selection,
            Vector2 rawParentDelta,
            float snapDistance,
            bool usePixelBounds,
            ref SkinHudSnapDragState state)
        {
            if (selection.Count == 0)
                return rawParentDelta;

            var currentBounds = getCombinedBounds(selection, usePixelBounds);
            var proposedBounds = currentBounds.Offset(rawParentDelta.X, rawParentDelta.Y);
            var others = getSnapTargets(container, selection, usePixelBounds);
            var containerSize = container.DrawSize;

            maintainAxis(true, ref state.X, in state.Y, proposedBounds, snapDistance, containerSize, others);
            maintainAxis(false, ref state.Y, in state.X, proposedBounds, snapDistance, containerSize, others);
            maintainAxis(true, ref state.X, in state.Y, proposedBounds, snapDistance, containerSize, others);
            maintainAxis(false, ref state.Y, in state.X, proposedBounds, snapDistance, containerSize, others);

            float adjustX = computeSnapAdjust(true, proposedBounds, state.X);
            var proposedAfterX = proposedBounds.Offset(adjustX, 0);
            float adjustY = computeSnapAdjust(false, proposedAfterX, state.Y);

            return new Vector2(rawParentDelta.X + adjustX, rawParentDelta.Y + adjustY);
        }

        private static void maintainAxis(
            bool isHorizontal,
            ref SkinHudSnapAxisSession session,
            in SkinHudSnapAxisSession orthogonal,
            Bounds proposedBounds,
            float snapDistance,
            Vector2 containerDrawSize,
            IReadOnlyList<Bounds> others)
        {
            if (session.Kind == SkinHudSnapAlignKind.Spacing
                && session.ReferenceKind == SkinHudSnapReferenceKind.Component
                && !orthogonal.SupportsComponentOrthogonalSpacing(session.ReferenceComponentIndex))
            {
                session.Reset();
            }
            else if (session.IsActive)
            {
                if (shouldRelease(isHorizontal, proposedBounds, session))
                    session.Reset();
                else if (session.Kind == SkinHudSnapAlignKind.Spacing)
                    updateSpacingIntent(isHorizontal, proposedBounds, ref session, snapDistance);
            }

            if (!session.IsActive)
                tryCapture(isHorizontal, proposedBounds, snapDistance, orthogonal, containerDrawSize, others, ref session);
        }

        private static void updateSpacingIntent(
            bool isHorizontal,
            Bounds proposedBounds,
            ref SkinHudSnapAxisSession session,
            float snapDistance)
        {
            if (Math.Abs(session.SpacingTarget - snapDistance) > float.Epsilon)
                return;

            float proposedGap = computeGap(isHorizontal, proposedBounds, session);

            if (proposedGap < session.SpacingTarget - float.Epsilon)
                session.SpacingIntent += session.SpacingTarget - proposedGap;
            else if (proposedGap > session.SpacingTarget + float.Epsilon)
                session.SpacingIntent = Math.Max(0, session.SpacingIntent - (proposedGap - session.SpacingTarget));

            if (session.SpacingIntent >= THRESHOLD)
                session.SpacingTarget = 0;
        }

        private static bool shouldRelease(bool isHorizontal, Bounds proposedBounds, SkinHudSnapAxisSession session) =>
            computeAlignmentError(isHorizontal, proposedBounds, session) > THRESHOLD;

        private static float computeAlignmentError(bool isHorizontal, Bounds bounds, SkinHudSnapAxisSession session)
        {
            switch (session.Kind)
            {
                case SkinHudSnapAlignKind.Edge:
                case SkinHudSnapAlignKind.Center:
                    return Math.Abs(getAlignedCoordinate(isHorizontal, bounds, session) - session.TargetLine);

                case SkinHudSnapAlignKind.Spacing:
                    return Math.Abs(computeGap(isHorizontal, bounds, session) - session.SpacingTarget);

                default:
                    return float.MaxValue;
            }
        }

        private static float getAlignedCoordinate(bool isHorizontal, Bounds bounds, SkinHudSnapAxisSession session) =>
            getSelectionEdgeValue(isHorizontal, bounds, session.SelectionEdge);

        private static void tryCapture(
            bool isHorizontal,
            Bounds proposedBounds,
            float snapDistance,
            in SkinHudSnapAxisSession orthogonal,
            Vector2 containerDrawSize,
            IReadOnlyList<Bounds> others,
            ref SkinHudSnapAxisSession session)
        {
            SnapCandidate? best = null;
            float bestError = float.MaxValue;

            foreach (var candidate in enumerateCandidates(isHorizontal, containerDrawSize, others, proposedBounds))
            {
                if (candidate.Kind == SkinHudSnapAlignKind.Spacing)
                {
                    if (candidate.ReferenceKind == SkinHudSnapReferenceKind.Component
                        && !orthogonal.SupportsComponentOrthogonalSpacing(candidate.ReferenceComponentIndex))
                    {
                        continue;
                    }

                    float gap = computeGap(isHorizontal, proposedBounds, candidate);
                    if (gap < 0)
                        continue;

                    float error = Math.Abs(gap - snapDistance);
                    if (error > THRESHOLD)
                        continue;
                }
                else
                {
                    float error = Math.Abs(getSelectionEdgeValue(isHorizontal, proposedBounds, candidate.SelectionEdge) - candidate.TargetLine);
                    if (error > THRESHOLD)
                        continue;

                    if (error < bestError)
                    {
                        bestError = error;
                        best = candidate;
                    }

                    continue;
                }

                float spacingError = Math.Abs(computeGap(isHorizontal, proposedBounds, candidate) - snapDistance);

                if (spacingError < bestError)
                {
                    bestError = spacingError;
                    best = candidate;
                }
            }

            if (best == null)
                return;

            session.Kind = best.Value.Kind;
            session.SelectionEdge = best.Value.SelectionEdge;
            session.GapMode = best.Value.GapMode;
            session.TargetLine = best.Value.TargetLine;
            session.ReferenceKind = best.Value.ReferenceKind;
            session.ReferenceComponentIndex = best.Value.ReferenceComponentIndex;
            session.SpacingIntent = 0;
            session.SpacingTarget = best.Value.Kind == SkinHudSnapAlignKind.Spacing ? snapDistance : 0;
        }

        private static float computeSnapAdjust(bool isHorizontal, Bounds proposedBounds, SkinHudSnapAxisSession session)
        {
            if (!session.IsActive)
                return 0;

            if (session.Kind == SkinHudSnapAlignKind.Center)
            {
                float current = getSelectionEdgeValue(isHorizontal, proposedBounds, SkinHudSnapEdge.Mid);
                return session.TargetLine - current;
            }

            float desiredEdge = session.Kind switch
            {
                SkinHudSnapAlignKind.Edge => computeEdgeForGap(isHorizontal, session.SelectionEdge, session.GapMode, session.TargetLine, 0),
                SkinHudSnapAlignKind.Spacing => computeEdgeForGap(isHorizontal, session.SelectionEdge, session.GapMode, session.TargetLine, session.SpacingTarget),
                _ => getSelectionEdgeValue(isHorizontal, proposedBounds, session.SelectionEdge),
            };

            float proposedEdge = getSelectionEdgeValue(isHorizontal, proposedBounds, session.SelectionEdge);
            return desiredEdge - proposedEdge;
        }

        private static IEnumerable<SnapCandidate> enumerateCandidates(
            bool isHorizontal,
            Vector2 containerDrawSize,
            IReadOnlyList<Bounds> others,
            Bounds selectionBounds)
        {
            if (isHorizontal)
            {
                for (int i = 0; i < others.Count; i++)
                {
                    var other = others[i];
                    if (blocksHorizontalSnap(selectionBounds, other))
                        continue;

                    // Component: adjacent 0-gap edges and center only (no configured spacing on this axis).
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Left, SkinHudSnapReferenceKind.Component, i);
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Right, SkinHudSnapReferenceKind.Component, i);
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Center, SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, other.HCenter, SkinHudSnapReferenceKind.Component, i);
                }

                float width = containerDrawSize.X;
                yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, width, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, width, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Center, SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, width * 0.5f, SkinHudSnapReferenceKind.Container);
            }
            else
            {
                for (int i = 0; i < others.Count; i++)
                {
                    var other = others[i];
                    if (blocksVerticalSnap(selectionBounds, other))
                        continue;

                    yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Top, SkinHudSnapReferenceKind.Component, i);
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Bottom, SkinHudSnapReferenceKind.Component, i);
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Center, SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, other.VCenter, SkinHudSnapReferenceKind.Component, i);

                    // Y spacing to component B; requires X edge/center alignment to the same component.
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Bottom, SkinHudSnapReferenceKind.Component, i);
                    yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Top, SkinHudSnapReferenceKind.Component, i);
                }

                float height = containerDrawSize.Y;
                yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Edge, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, height, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Spacing, SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, height, SkinHudSnapReferenceKind.Container);
                yield return new SnapCandidate(SkinHudSnapAlignKind.Center, SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, height * 0.5f, SkinHudSnapReferenceKind.Container);
            }
        }

        private static bool blocksHorizontalSnap(Bounds selection, Bounds other) =>
            selection.Bottom <= other.Top || selection.Top >= other.Bottom;

        private static bool blocksVerticalSnap(Bounds selection, Bounds other) =>
            selection.Right <= other.Left || selection.Left >= other.Right;

        private static float computeGap(bool isHorizontal, Bounds bounds, SnapCandidate candidate) =>
            computeGap(isHorizontal, bounds, candidate.SelectionEdge, candidate.GapMode, candidate.TargetLine);

        private static float computeGap(bool isHorizontal, Bounds bounds, SkinHudSnapAxisSession session) =>
            computeGap(isHorizontal, bounds, session.SelectionEdge, session.GapMode, session.TargetLine);

        private static float computeGap(bool isHorizontal, Bounds bounds, SkinHudSnapEdge edge, SkinHudSnapGapMode mode, float targetLine)
        {
            float selectionEdge = getSelectionEdgeValue(isHorizontal, bounds, edge);
            return mode == SkinHudSnapGapMode.SelectionMinusTarget
                ? selectionEdge - targetLine
                : targetLine - selectionEdge;
        }

        private static float computeEdgeForGap(bool isHorizontal, SkinHudSnapEdge edge, SkinHudSnapGapMode mode, float targetLine, float gap) =>
            mode == SkinHudSnapGapMode.SelectionMinusTarget
                ? targetLine + gap
                : targetLine - gap;

        private static float getSelectionEdgeValue(bool isHorizontal, Bounds bounds, SkinHudSnapEdge edge)
        {
            if (isHorizontal)
            {
                return edge switch
                {
                    SkinHudSnapEdge.Min => bounds.Left,
                    SkinHudSnapEdge.Max => bounds.Right,
                    _ => bounds.HCenter,
                };
            }

            return edge switch
            {
                SkinHudSnapEdge.Min => bounds.Top,
                SkinHudSnapEdge.Max => bounds.Bottom,
                _ => bounds.VCenter,
            };
        }

        private static Bounds getCombinedBounds(IReadOnlyList<Drawable> drawables, bool usePixelBounds)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            foreach (var drawable in drawables)
            {
                var bounds = toBounds(SkinHudSnapBounds.FromDrawable(drawable, usePixelBounds));
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }

            return new Bounds(minX, maxX, minY, maxY);
        }

        private static Bounds getSnapBounds(Drawable drawable, bool usePixelBounds) =>
            toBounds(SkinHudSnapBounds.FromDrawable(drawable, usePixelBounds));

        private static Bounds toBounds(SkinHudSnapBounds bounds) =>
            new Bounds(bounds.Left, bounds.Right, bounds.Top, bounds.Bottom);

        private static List<Bounds> getSnapTargets(SkinnableContainer container, IReadOnlyList<Drawable> selection, bool usePixelBounds)
        {
            var selectionSet = new HashSet<Drawable>(selection);
            var result = new List<Bounds>();

            foreach (var component in container.Components)
            {
                if (component is not Drawable drawable)
                    continue;

                if (!component.IsEditable || !drawable.IsPresent || selectionSet.Contains(drawable))
                    continue;

                result.Add(getSnapBounds(drawable, usePixelBounds));
            }

            return result;
        }
    }
}
