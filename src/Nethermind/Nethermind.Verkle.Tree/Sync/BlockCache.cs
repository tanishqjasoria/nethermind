// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.Sync;

public class BlockDiffCache: StackQueue<(long, ReadOnlyVerkleMemoryDb)>
{
    public BlockDiffCache(int capacity) : base(capacity) {}

    public void RemoveDiffs(long noOfDiffsToRemove)
    {
        for (int i = 0; i < noOfDiffsToRemove; i++)
        {
            Pop(out _);
        }
    }

    public byte[]? GetLeaf(byte[] key)
    {
        using StackEnumerator diffs = GetStackEnumerator();
        while (diffs.MoveNext())
        {
            if (diffs.Current.Item2.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
        }
        return null;
    }

    public byte[]? GetLeaf(byte[] key, long blockNumber)
    {
        using StackEnumerator diffs = GetStackEnumerator();
        while (diffs.MoveNext())
        {
            // TODO: find a better way to do this
            if (diffs.Current.Item1 > blockNumber) continue;
            if (diffs.Current.Item2.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
        }
        return null;
    }

    public InternalNode? GetInternalNode(byte[] key)
    {
        using StackEnumerator diffs = GetStackEnumerator();
        while (diffs.MoveNext())
        {
            if (diffs.Current.Item2.InternalTable.TryGetValue(key, out InternalNode? node)) return node!.Clone();
        }
        return null;
    }
    public InternalNode? GetInternalNode(byte[] key, long blockNumber)
    {
        using StackEnumerator diffs = GetStackEnumerator();
        while (diffs.MoveNext())
        {
            // TODO: fina a better way to do this
            if (diffs.Current.Item1 > blockNumber) continue;
            if (diffs.Current.Item2.InternalTable.TryGetValue(key, out InternalNode? node)) return node!.Clone();
        }
        return null;
    }
}
