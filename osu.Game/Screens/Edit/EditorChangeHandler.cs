// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using osu.Framework.Bindables;
using osu.Framework.Extensions;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Tracks changes to the <see cref="Editor"/>.
    /// </summary>
    public abstract partial class EditorChangeHandler : TransactionalCommitComponent, IEditorChangeHandler
    {
        public readonly Bindable<bool> CanUndo = new Bindable<bool>();
        public readonly Bindable<bool> CanRedo = new Bindable<bool>();

        public event Action? OnStateChange;

        private readonly List<byte[]> savedStates = new List<byte[]>();

        private int currentState = -1;

        /// <summary>
        /// A SHA-2 hash representing the current visible editor state.
        /// </summary>
        public string CurrentStateHash
        {
            get
            {
                ensureStateSaved();

                using (var stream = new MemoryStream(savedStates[currentState]))
                    return stream.ComputeSHA2Hash();
            }
        }

        private bool isRestoring;

        public const int MAX_SAVED_STATES = 50;

        /// <summary>
        /// Marks a snapshot whose bytes carry <see cref="IEditorStateContributor"/> blocks in addition to the beatmap.
        /// </summary>
        /// <remarks>
        /// Snapshots without contributors keep the bare beatmap layout, so handlers that have none are byte-identical
        /// to the format that predates this mechanism.
        /// </remarks>
        private const ushort contributor_header_magic = 0x5A45;

        private const byte contributor_format_version = 1;

        private readonly List<IEditorStateContributor> contributors = new List<IEditorStateContributor>();

        /// <summary>
        /// Adds a contributor whose state is saved and restored together with the beatmap.
        /// </summary>
        /// <remarks>
        /// Registering later than the first snapshot rewrites that snapshot, so the undo step back to it can still describe
        /// the contributor's state.
        /// </remarks>
        public void AddContributor(IEditorStateContributor contributor)
        {
            if (contributors.Contains(contributor))
                return;

            contributors.Add(contributor);

            // A snapshot may already exist if something asked for one during load. Rewriting the current one keeps the
            // first undo step able to describe the contributor's state instead of silently leaving it alone.
            if (savedStates.Count > 0)
                savedStates[currentState] = writeState();
        }

        public void RemoveContributor(IEditorStateContributor contributor)
        {
            if (!contributors.Remove(contributor))
                return;

            if (savedStates.Count > 0)
                savedStates[currentState] = writeState();
        }

        public override void BeginChange()
        {
            ensureStateSaved();

            base.BeginChange();
        }

        private void ensureStateSaved()
        {
            if (savedStates.Count == 0)
                SaveState();
        }

        protected override void UpdateState()
        {
            if (isRestoring)
                return;

            byte[] newState = writeState();

            // if the previous state is binary equal we don't need to push a new one, unless this is the initial state.
            if (savedStates.Count > 0 && newState.SequenceEqual(savedStates[currentState])) return;

            if (currentState < savedStates.Count - 1)
                savedStates.RemoveRange(currentState + 1, savedStates.Count - currentState - 1);

            if (savedStates.Count > MAX_SAVED_STATES)
                savedStates.RemoveAt(0);

            savedStates.Add(newState);

            currentState = savedStates.Count - 1;

            OnStateChange?.Invoke();
            updateBindables();
        }

        /// <summary>
        /// Serialises the beatmap text, followed by every contributor's block when there is at least one contributor.
        /// </summary>
        private byte[] writeState()
        {
            using (var beatmap = new MemoryStream())
            {
                WriteCurrentStateToStream(beatmap);

                if (contributors.Count == 0)
                    return beatmap.ToArray();

                using (var state = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(state, Encoding.UTF8, true))
                    {
                        writer.Write(contributor_header_magic);
                        writer.Write(contributor_format_version);
                        writer.Write(contributors.Count);
                        writer.Write((int)beatmap.Length);
                    }

                    beatmap.Position = 0;
                    beatmap.CopyTo(state);

                    using (var writer = new BinaryWriter(state, Encoding.UTF8, true))
                    {
                        foreach (var contributor in contributors)
                        {
                            using (var block = new MemoryStream())
                            {
                                contributor.Write(block);
                                writer.Write((int)block.Length);
                                block.Position = 0;
                                block.CopyTo(state);
                            }
                        }
                    }

                    return state.ToArray();
                }
            }
        }

        /// <summary>
        /// Splits a snapshot into the beatmap text and the contributor blocks it carries.
        /// </summary>
        private (byte[] beatmap, List<byte[]> blocks) readState(byte[] state)
        {
            var blocks = new List<byte[]>();

            if (state.Length < 2 || (state[0] | (state[1] << 8)) != contributor_header_magic)
                return (state, blocks);

            using (var stream = new MemoryStream(state))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                reader.ReadUInt16();
                reader.ReadByte();
                int blockCount = reader.ReadInt32();
                int beatmapLength = reader.ReadInt32();

                if (blockCount < 0 || beatmapLength < 0 || beatmapLength > stream.Length - stream.Position)
                    return (state, blocks);

                byte[] beatmap = reader.ReadBytes(beatmapLength);

                for (int i = 0; i < blockCount; i++)
                {
                    if (stream.Length - stream.Position < sizeof(int))
                        return (state, new List<byte[]>());

                    int length = reader.ReadInt32();

                    if (length < 0 || length > stream.Length - stream.Position)
                        return (state, new List<byte[]>());

                    blocks.Add(reader.ReadBytes(length));
                }

                return (beatmap, blocks);
            }
        }

        public void RestoreState(int direction)
        {
            if (TransactionActive)
                return;

            if (savedStates.Count == 0)
                return;

            int newState = Math.Clamp(currentState + direction, 0, savedStates.Count - 1);
            if (currentState == newState)
                return;

            isRestoring = true;

            var previous = readState(savedStates[currentState]);
            var next = readState(savedStates[newState]);

            ApplyStateChange(previous.beatmap, next.beatmap);

            // A snapshot taken before all contributors were added carries fewer blocks; those contributors keep their
            // state rather than being reset to a default the older snapshot never recorded.
            for (int i = 0; i < contributors.Count && i < next.blocks.Count; i++)
            {
                byte[] nextBlock = next.blocks[i];

                // An unchanged block is left alone, so undoing a beatmap-only edit does not rebuild the contributor's
                // objects and drop its selection. When the older snapshot has no block at all, the contributor adopts the
                // newer one instead.
                if (i < previous.blocks.Count && previous.blocks[i].AsSpan().SequenceEqual(nextBlock))
                    continue;

                contributors[i].Restore(i < previous.blocks.Count ? previous.blocks[i] : nextBlock, nextBlock);
            }

            currentState = newState;

            isRestoring = false;

            OnStateChange?.Invoke();
            updateBindables();
        }

        /// <summary>
        /// Write a serialised copy of the currently tracked state to the provided stream.
        /// This will be stored as a state which can be restored in the future.
        /// </summary>
        /// <param name="stream">The stream which the state should be written to.</param>
        protected abstract void WriteCurrentStateToStream(MemoryStream stream);

        /// <summary>
        /// Given a previous and new state, apply any changes required to bring the current state in line with the new state.
        /// </summary>
        /// <param name="previousState">The previous (current before this call) serialised state.</param>
        /// <param name="newState">The new state to be applied.</param>
        protected abstract void ApplyStateChange(byte[] previousState, byte[] newState);

        private void updateBindables()
        {
            CanUndo.Value = savedStates.Count > 0 && currentState > 0;
            CanRedo.Value = currentState < savedStates.Count - 1;
        }
    }
}
