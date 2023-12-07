// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public class ReadOnlyVerkleStateStore : IVerkleTrieStore, ISyncTrieStore
{
    private VerkleStateStore _verkleStateStore;
    private VerkleMemoryDb _keyValueStore;

    public ReadOnlyVerkleStateStore(VerkleStateStore verkleStateStore, VerkleMemoryDb keyValueStore)
    {
        _verkleStateStore = verkleStateStore;
        _keyValueStore = keyValueStore;
    }

    public VerkleCommitment StateRoot
    {
        get
        {
            _keyValueStore.GetInternalNode(VerkleStateStore.RootNodeKey, out InternalNode? value);
            return value is null ? _verkleStateStore.StateRoot : new VerkleCommitment(value.Bytes);
        }
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null)
    {
        return _keyValueStore.GetLeaf(key, out byte[]? value)
            ? value
            : _verkleStateStore.GetLeaf(key, stateRoot);
    }
    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null)
    {
        return _keyValueStore.GetInternalNode(key, out InternalNode? value)
            ? value
            : _verkleStateStore.GetInternalNode(key, stateRoot);
    }
    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
        _keyValueStore.SetLeaf(leafKey, leafValue);
    }
    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        _keyValueStore.SetInternalNode(internalNodeKey, internalNodeValue);
    }
    public void InsertBatch(long blockNumber, VerkleMemoryDb batch) { }

    public void ApplyDiffLayer(BatchChangeSet changeSet) { }

    public VerkleCommitment GetStateRoot()
    {
        return _verkleStateStore.GetStateRoot();
    }

    public bool HashStateForBlock(VerkleCommitment stateRoot)
    {
        return _verkleStateStore.HashStateForBlock(stateRoot);
    }

    public bool MoveToStateRoot(VerkleCommitment stateRoot)
    {
        return _verkleStateStore.MoveToStateRoot(stateRoot);
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(_verkleStateStore, keyValueStore);
    }
    public bool IsFullySynced(Keccak stateRoot)
    {
        return _verkleStateStore.IsFullySynced(stateRoot);
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange, byte[] toRange, long blockNumber)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, blockNumber);
    }

    public IEnumerable<PathWithSubTree> GetLeafRangeIterator(Stem fromRange, Stem toRange, VerkleCommitment stateRoot, long bytes)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, stateRoot, bytes);
    }
}

