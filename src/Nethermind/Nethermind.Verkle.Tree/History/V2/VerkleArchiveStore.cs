// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices.ComTypes;
using Nethermind.Core.Collections.EliasFano;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.TrieStore;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.History.V2;

public class VerkleArchiveStore
{
    public int BlockChunks
    {
        get => _historyOfAccounts.BlocksChunks;
        set => _historyOfAccounts.BlocksChunks = value;
    }

    private readonly VerkleStateStore _stateStore;
    private readonly HistoryOfAccounts _historyOfAccounts;
    private ILeafChangeSet ChangeSet { get; }

    public VerkleArchiveStore(VerkleStateStore stateStore, IDbProvider dbProvider, ILogManager logManager)
    {
        _stateStore = stateStore;
        _historyOfAccounts = new HistoryOfAccounts(dbProvider.HistoryOfAccounts);
        _stateStore.InsertBatchCompletedV2 += OnPersistNewBlock;
        ChangeSet = new LeafChangeSet(dbProvider, logManager);
    }

    private void OnPersistNewBlock(object? sender, InsertBatchCompletedV2 insertBatchCompleted)
    {
        long blockNumber = insertBatchCompleted.BlockNumber;
        ChangeSet.InsertDiff(blockNumber, insertBatchCompleted.LeafTable);
        foreach (KeyValuePair<byte[], byte[]?> keyVal in insertBatchCompleted.LeafTable)
            _historyOfAccounts.AppendHistoryBlockNumberForKey(new Pedersen(keyVal.Key), (ulong)blockNumber);
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment rootHash)
    {
        ulong blockNumber = (ulong)_stateStore.StateRootToBlocks[rootHash];
        EliasFano? requiredShard = _historyOfAccounts.GetAppropriateShard(key.ToArray(), blockNumber + 1);
        if (requiredShard is null) return _stateStore.GetLeaf(key);
        Console.WriteLine($"RequiredShard:{string.Join(",", requiredShard.Value.GetEnumerator(0).ToArray())}");
        ulong? requiredBlock = requiredShard.Value.Successor(blockNumber + 1);
        return requiredBlock is null ? _stateStore.GetLeaf(key) : ChangeSet.GetLeaf((long)requiredBlock!.Value, key);
    }
}
