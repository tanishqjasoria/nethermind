// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State.Tracing;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

[assembly: InternalsVisibleTo("Ethereum.Test.Base")]
[assembly: InternalsVisibleTo("Ethereum.Blockchain.Test")]
[assembly: InternalsVisibleTo("Nethermind.State.Test")]
[assembly: InternalsVisibleTo("Nethermind.Benchmark")]
[assembly: InternalsVisibleTo("Nethermind.Blockchain.Test")]
[assembly: InternalsVisibleTo("Nethermind.Synchronization.Test")]
[assembly: InternalsVisibleTo("Nethermind.Synchronization")]

namespace Nethermind.State
{
    public class WorldState : IWorldState
    {
        internal readonly StateProvider _stateProvider;
        internal readonly PersistentStorageProvider _persistentStorageProvider;
        private readonly TransientStorageProvider _transientStorageProvider;
        private readonly ITrieStore _trieStore;
        private readonly IKeyValueStore _codeDb;
        public StateType StateType => StateType.Merkle;
        public Hash256 StateRoot
        {
            get => _stateProvider.StateRoot;
            set
            {
                _stateProvider.StateRoot = value;
                _persistentStorageProvider.StateRoot = value;
            }
        }

        public WorldState(ITrieStore? trieStore, IKeyValueStore codeDb, ILogManager? logManager)
        {
            _trieStore = trieStore;
            _stateProvider = new StateProvider(trieStore.GetTrieStore(null), codeDb, logManager);
            _persistentStorageProvider = new PersistentStorageProvider(trieStore, _stateProvider, logManager);
            _transientStorageProvider = new TransientStorageProvider(logManager);
            _codeDb = codeDb;
        }

        internal WorldState(ITrieStore? trieStore, IKeyValueStore codeDb, ILogManager? logManager, StateTree stateTree, IStorageTreeFactory storageTreeFactory)
        {
            _stateProvider = new StateProvider(trieStore.GetTrieStore(null), codeDb, logManager, stateTree);
            _persistentStorageProvider = new PersistentStorageProvider(trieStore, _stateProvider, logManager, storageTreeFactory);
            _transientStorageProvider = new TransientStorageProvider(logManager);
            _codeDb = codeDb;
        }

        public Account GetAccount(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetAccount(address);
        }

        bool IAccountStateProvider.TryGetAccount(Address address, out AccountStruct account)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            account = _stateProvider.GetAccount(address).ToStruct();
            return !account.IsTotallyEmpty;
        }

        public bool IsContract(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.IsContract(address);
        }

        public byte[] GetOriginal(in StorageCell storageCell)
        {
            Span<byte> key = stackalloc byte[32];
            StorageTree.GetKey(storageCell.Index, in key);
            _codeDb[key] = storageCell.Index.ToBigEndian();
            return _persistentStorageProvider.GetOriginal(storageCell);
        }

        public void SweepLeaves(int blockNumber)
        {
        }

        public bool ValuePresentInTree(Hash256 key)
        {
            throw new NotImplementedException();
        }

        public ReadOnlySpan<byte> Get(in StorageCell storageCell)
        {
            Span<byte> key = stackalloc byte[32];
            StorageTree.GetKey(storageCell.Index, in key);
            _codeDb[key] = storageCell.Index.ToBigEndian();
            return _persistentStorageProvider.Get(storageCell);
        }
        public void Set(in StorageCell storageCell, byte[] newValue)
        {
            Span<byte> key = stackalloc byte[32];
            StorageTree.GetKey(storageCell.Index, in key);
            _codeDb[key] = storageCell.Index.ToBigEndian();
            _persistentStorageProvider.Set(storageCell, newValue);
        }
        public ReadOnlySpan<byte> GetTransientState(in StorageCell storageCell)
        {
            Span<byte> key = stackalloc byte[32];
            StorageTree.GetKey(storageCell.Index, in key);
            _codeDb[key] = storageCell.Index.ToBigEndian();
            return _transientStorageProvider.Get(storageCell);
        }
        public void SetTransientState(in StorageCell storageCell, byte[] newValue)
        {
            Span<byte> key = stackalloc byte[32];
            StorageTree.GetKey(storageCell.Index, in key);
            _codeDb[key] = storageCell.Index.ToBigEndian();
            _transientStorageProvider.Set(storageCell, newValue);
        }
        public void Reset()
        {
            _stateProvider.Reset();
            _persistentStorageProvider.Reset();
            _transientStorageProvider.Reset();
        }

        public void ClearStorage(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _persistentStorageProvider.ClearStorage(address);
            _transientStorageProvider.ClearStorage(address);
        }
        public void RecalculateStateRoot()
        {
            _stateProvider.RecalculateStateRoot();
        }
        public void DeleteAccount(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.DeleteAccount(address);
        }
        public void CreateAccount(Address address, in UInt256 balance, in UInt256 nonce = default)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.CreateAccount(address, balance, nonce);
        }

        public void InsertCode(Address address, Hash256 codeHash, ReadOnlyMemory<byte> code, IReleaseSpec spec, bool isGenesis = false)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.InsertCode(address, codeHash, code, spec, isGenesis);
        }

        public void AddToBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.AddToBalance(address, balanceChange, spec);
        }
        public void AddToBalanceAndCreateIfNotExists(Address address, in UInt256 balanceChange, IReleaseSpec spec)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.AddToBalanceAndCreateIfNotExists(address, balanceChange, spec);
        }
        public void SubtractFromBalance(Address address, in UInt256 balanceChange, IReleaseSpec spec)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.SubtractFromBalance(address, balanceChange, spec);
        }
        public void UpdateStorageRoot(Address address, Hash256 storageRoot)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.UpdateStorageRoot(address, storageRoot);
        }
        public void IncrementNonce(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.IncrementNonce(address);
        }
        public void DecrementNonce(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            _stateProvider.DecrementNonce(address);
        }

        public void CommitTree(long blockNumber)
        {
            _persistentStorageProvider.CommitTrees(blockNumber);
            _stateProvider.CommitTree(blockNumber);
            _persistentStorageProvider.StateRoot = _stateProvider.StateRoot;
        }

        public void TouchCode(in ValueHash256 codeHash)
        {
            _stateProvider.TouchCode(codeHash);
        }

        public UInt256 GetNonce(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetNonce(address);
        }

        public UInt256 GetBalance(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetBalance(address);
        }

        public ValueHash256 GetStorageRoot(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetStorageRoot(address);
        }

        public byte[] GetCode(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetCode(address);
        }

        public byte[] GetCode(Hash256 codeHash) => _stateProvider.GetCode(codeHash);

        public byte[] GetCode(ValueHash256 codeHash) => _stateProvider.GetCode(codeHash);

        public Hash256 GetCodeHash(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.GetCodeHash(address);
        }

        ValueHash256 IAccountStateProvider.GetCodeHash(Address address)
        {
            return _stateProvider.GetCodeHash(address);
        }

        public void Accept(ITreeVisitor visitor, Hash256 stateRoot, VisitingOptions? visitingOptions = null)
        {
            _stateProvider.Accept(visitor, stateRoot, visitingOptions);
        }
        public bool AccountExists(Address address)
        {
            _codeDb[ValueKeccak.Compute(address.Bytes).BytesAsSpan] = address.Bytes;
            return _stateProvider.AccountExists(address);
        }
        public bool IsDeadAccount(Address address)
        {
            return _stateProvider.IsDeadAccount(address);
        }
        public bool IsEmptyAccount(Address address)
        {
            return _stateProvider.IsEmptyAccount(address);
        }

        public bool HasStateForRoot(Hash256 stateRoot)
        {
            return _trieStore.HasRoot(stateRoot);
        }

        public void Commit(IReleaseSpec releaseSpec, bool isGenesis = false)
        {
            _persistentStorageProvider.Commit();
            _transientStorageProvider.Commit();
            _stateProvider.Commit(releaseSpec, isGenesis);
        }
        public void Commit(IReleaseSpec releaseSpec, IWorldStateTracer tracer, bool isGenesis = false)
        {
            _persistentStorageProvider.Commit(tracer);
            _transientStorageProvider.Commit(tracer);
            _stateProvider.Commit(releaseSpec, tracer, isGenesis);
        }

        public Snapshot TakeSnapshot(bool newTransactionStart = false)
        {
            int persistentSnapshot = _persistentStorageProvider.TakeSnapshot(newTransactionStart);
            int transientSnapshot = _transientStorageProvider.TakeSnapshot(newTransactionStart);
            Snapshot.Storage storageSnapshot = new Snapshot.Storage(persistentSnapshot, transientSnapshot);
            int stateSnapshot = _stateProvider.TakeSnapshot();
            return new Snapshot(stateSnapshot, storageSnapshot);
        }

        public void Restore(Snapshot snapshot)
        {
            _persistentStorageProvider.Restore(snapshot.StorageSnapshot.PersistentStorageSnapshot);
            _transientStorageProvider.Restore(snapshot.StorageSnapshot.TransientStorageSnapshot);
            _stateProvider.Restore(snapshot.StateSnapshot);
        }

        internal void Restore(int state, int persistantStorage, int transientStorage)
        {
            Restore(new Snapshot(state, new Snapshot.Storage(persistantStorage, transientStorage)));
        }

        internal void SetNonce(Address address, in UInt256 nonce)
        {
            _stateProvider.SetNonce(address, nonce);
        }

        public void CreateAccountIfNotExists(Address address, in UInt256 balance, in UInt256 nonce = default)
        {
            _stateProvider.CreateAccountIfNotExists(address, balance, nonce);
        }

        public byte[] GetCodeChunk(Address codeOwner, UInt256 chunkId) => throw new NotImplementedException();
    }
}
