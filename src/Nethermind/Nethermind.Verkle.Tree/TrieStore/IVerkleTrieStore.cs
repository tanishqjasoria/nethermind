// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public interface IVerkleTrieStore: IStoreWithReorgBoundary, IVerkleSyncTireStore
{
    VerkleCommitment StateRoot { get; }
    VerkleCommitment GetStateRoot();

    bool HashStateForBlock(VerkleCommitment stateRoot);
    bool MoveToStateRoot(VerkleCommitment stateRoot);

    byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null);

    void InsertBatch(long blockNumber, VerkleMemoryDb memDb);

    void ApplyDiffLayer(BatchChangeSet changeSet);

    ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore);
}
