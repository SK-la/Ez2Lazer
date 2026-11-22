// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using Live2DCSharpSDK.Framework.Rendering;
using osuTK;

namespace osu.Game.EzOsuGame.Pets
{
    public sealed class EzPetCubismFrameSnapshot
    {
        private readonly EzPetCubismFrameSnapshotPool pool;
        private EzPetCubismMeshPart?[] parts = [];
        private int referenceCount;

        public float CanvasWidth { get; private set; }
        public float CanvasHeight { get; private set; }
        public int PartCount { get; private set; }

        internal EzPetCubismFrameSnapshot(EzPetCubismFrameSnapshotPool pool)
        {
            this.pool = pool;
        }

        internal EzPetCubismMeshPart AddPart(int textureIndex, float opacity, CubismBlendMode blendMode, int vertexCount, int indexCount)
        {
            if (PartCount == parts.Length)
                Array.Resize(ref parts, Math.Max(4, parts.Length * 2));

            var part = parts[PartCount] ??= new EzPetCubismMeshPart();
            part.SetData(textureIndex, opacity, blendMode, vertexCount, indexCount);
            PartCount++;
            return part;
        }

        internal EzPetCubismMeshPart GetPart(int index) => parts[index]!;

        internal void Acquire()
        {
            int current;

            do
            {
                current = Volatile.Read(ref referenceCount);

                if (current <= 0)
                    throw new InvalidOperationException("Cannot acquire a snapshot after it has returned to its pool.");
            }
            while (Interlocked.CompareExchange(ref referenceCount, current + 1, current) != current);
        }

        internal void Release()
        {
            int remaining = Interlocked.Decrement(ref referenceCount);

            if (remaining < 0)
                throw new InvalidOperationException("Snapshot reference count became negative.");

            if (remaining == 0)
                pool.Return(this);
        }

        internal void ResetForCapture(float canvasWidth, float canvasHeight)
        {
            CanvasWidth = canvasWidth;
            CanvasHeight = canvasHeight;
            PartCount = 0;
            referenceCount = 1;
        }
    }

    public sealed class EzPetCubismMeshPart
    {
        public int TextureIndex { get; private set; }
        public float Opacity { get; private set; }
        public CubismBlendMode BlendMode { get; private set; }
        public Vector2[] Positions { get; private set; } = [];
        public Vector2[] UVs { get; private set; } = [];
        public ushort[] Indices { get; private set; } = [];

        internal void SetData(int textureIndex, float opacity, CubismBlendMode blendMode, int vertexCount, int indexCount)
        {
            TextureIndex = textureIndex;
            Opacity = opacity;
            BlendMode = blendMode;

            if (Positions.Length != vertexCount)
                Positions = new Vector2[vertexCount];

            if (UVs.Length != vertexCount)
                UVs = new Vector2[vertexCount];

            if (Indices.Length != indexCount)
                Indices = new ushort[indexCount];
        }
    }

    internal sealed class EzPetCubismFrameSnapshotPool : IDisposable
    {
        private const int maximum_retained_snapshots = 3;

        private readonly Lock gate = new Lock();
        private readonly Queue<EzPetCubismFrameSnapshot> available = new Queue<EzPetCubismFrameSnapshot>();
        private bool disposed;

        public EzPetCubismFrameSnapshot Rent(float canvasWidth, float canvasHeight)
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                var snapshot = available.Count > 0 ? available.Dequeue() : new EzPetCubismFrameSnapshot(this);
                snapshot.ResetForCapture(canvasWidth, canvasHeight);
                return snapshot;
            }
        }

        internal void Return(EzPetCubismFrameSnapshot snapshot)
        {
            lock (gate)
            {
                if (!disposed && available.Count < maximum_retained_snapshots)
                    available.Enqueue(snapshot);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                disposed = true;
                available.Clear();
            }
        }
    }
}
