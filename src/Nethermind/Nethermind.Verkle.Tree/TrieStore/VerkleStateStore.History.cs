// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public partial class VerkleStateStore
{
    public event EventHandler<InsertBatchCompletedV1>? InsertBatchCompletedV1;
    public event EventHandler<InsertBatchCompletedV2>? InsertBatchCompletedV2;
    /// <summary>
    ///  maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int BlockCacheSize { get; }
    private BlockDiffCache? BlockCache { get; }

    [Obsolete("should not be used - can be used in extreme cases to correct state")]
    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        if (changeSet.FromBlockNumber != LastPersistedBlockNumber)
        {
            throw new ArgumentException(
                $"This case should not be possible. Diff fromBlock should be equal to persisted block number. FullStateBlock:{LastPersistedBlockNumber}!=fromBlock:{changeSet.FromBlockNumber}",
                nameof(changeSet.FromBlockNumber));
        }

        VerkleMemoryDb reverseDiff = changeSet.DiffLayer;

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out byte[]? node);
            if (node is null)
            {
                Storage.RemoveLeaf(entry.Key);
            }
            else
            {
                Storage.SetLeaf(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.InternalTable)
        {
            reverseDiff.GetInternalNode(entry.Key, out InternalNode? node);
            if (node is null)
            {
                Storage.RemoveInternalNode(entry.Key);
            }
            else
            {
                Storage.SetInternalNode(entry.Key, node);
            }
        }
        LastPersistedBlockNumber = changeSet.ToBlockNumber;
    }

    public readonly StateRootToBlockMap StateRootToBlocks;
}
