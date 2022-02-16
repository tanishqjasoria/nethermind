//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Nethermind.Blockchain.Processing
{
    public class VerkleReadOnlyTxProcessingEnv : IReadOnlyTxProcessorSource
    {
        private readonly ReadOnlyDb _codeDb;
        public IStateReader StateReader { get; }
        public IStateProvider StateProvider { get; }
        public IStorageProvider StorageProvider { get; }
        public ITransactionProcessor TransactionProcessor { get; set; }
        public IBlockTree BlockTree { get; }
        public IReadOnlyDbProvider DbProvider { get; }
        public IBlockhashProvider BlockhashProvider { get; }
        public IVirtualMachine Machine { get; }

        public VerkleReadOnlyTxProcessingEnv(
            IDbProvider? dbProvider,
            VerkleStateProvider stateProvider,
            IBlockTree? blockTree,
            ISpecProvider? specProvider,
            ILogManager? logManager) 
            : this(dbProvider?.AsReadOnly(false), stateProvider, blockTree?.AsReadOnly(), specProvider, logManager)
        {
        }

        public VerkleReadOnlyTxProcessingEnv(
            IReadOnlyDbProvider? readOnlyDbProvider,
            VerkleStateProvider stateProvider,
            IReadOnlyBlockTree? readOnlyBlockTree,
            ISpecProvider? specProvider,
            ILogManager? logManager)
        {
            if (specProvider == null) throw new ArgumentNullException(nameof(specProvider));
            DbProvider = readOnlyDbProvider ?? throw new ArgumentNullException(nameof(readOnlyDbProvider));
            _codeDb = readOnlyDbProvider.CodeDb.AsReadOnly(true);
            
            StateProvider = stateProvider;
            StateReader = new VerkleStateReader(stateProvider.GetTree(), _codeDb, logManager);
            StorageProvider = new VerkleStorageProvider(stateProvider, logManager);
            IWorldState worldState = new WorldState(StateProvider, StorageProvider);
            
            BlockTree = readOnlyBlockTree ?? throw new ArgumentNullException(nameof(readOnlyBlockTree));
            BlockhashProvider = new BlockhashProvider(BlockTree, logManager);

            Machine = new VirtualMachine(BlockhashProvider, specProvider, logManager);
            TransactionProcessor = new TransactionProcessor(specProvider, worldState, Machine, logManager);
        }

        public void Reset()
        {
            StateProvider.Reset();
            StorageProvider.Reset();
            
            _codeDb.ClearTempChanges();
        }

        public IReadOnlyTransactionProcessor Build(Keccak stateRoot) => new ReadOnlyTransactionProcessor(TransactionProcessor, StateProvider, StorageProvider, stateRoot);
    }
}
