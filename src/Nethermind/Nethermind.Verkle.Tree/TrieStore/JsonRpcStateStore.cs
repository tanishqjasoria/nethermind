// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Verkle;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public class JsonRpcStateStore
{
    private readonly VerkleArchiveStore _archiveStore;
    private readonly VerkleStateStore _verkleStateStore;
    private readonly VerkleMemoryDb _keyValueStore;

    private VerkleCommitment StateStoreStateRoot { get; set; }

    public JsonRpcStateStore(VerkleArchiveStore archiveStore, VerkleStateStore verkleStateStore, VerkleMemoryDb keyValueStore)
    {
        _archiveStore = archiveStore;
        _verkleStateStore = verkleStateStore;
        _keyValueStore = keyValueStore;
        StateStoreStateRoot = _verkleStateStore.StateRoot;
    }

    public VerkleCommitment StateRoot
    {
        get
        {
            _keyValueStore.GetInternalNode(VerkleStateStore.RootNodeKey, out InternalNode? value);
            return value is null ? StateStoreStateRoot : new VerkleCommitment(value.Bytes);
        }
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null)
    {
        try
        {
            if (!_keyValueStore.GetLeaf(key, out byte[]? value))
                value = _verkleStateStore.GetLeaf(key, stateRoot ?? StateStoreStateRoot);
            return value;
        }
        catch (StateUnavailableExceptions)
        {
            return _archiveStore.GetLeaf(key, stateRoot!);
        }
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key, VerkleCommitment? stateRoot = null)
    {
        return _keyValueStore.GetInternalNode(key, out InternalNode? value)
            ? value
            : _verkleStateStore.GetInternalNode(key, stateRoot ?? StateStoreStateRoot);
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

    public VerkleCommitment GetStateRoot()
    {
        return _verkleStateStore.GetStateRoot();
    }
    public void MoveToStateRoot(VerkleCommitment stateRoot)
    {
        StateStoreStateRoot = stateRoot;
    }
}
