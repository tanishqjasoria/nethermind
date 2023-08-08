// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Tracing;
using Nethermind.Consensus.Validators;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.JsonRpc.Data;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree;
using Newtonsoft.Json;

namespace Nethermind.JsonRpc.Modules.Proof
{
    public class ProofModuleFactory : ModuleFactoryBase<IProofRpcModule>
    {
        private readonly IBlockPreprocessorStep _recoveryStep;
        private readonly IReceiptFinder _receiptFinder;
        private readonly ISpecProvider _specProvider;
        private readonly ILogManager _logManager;
        private readonly IReadOnlyBlockTree _blockTree;
        private readonly ReadOnlyDbProvider _dbProvider;
        private readonly IReadOnlyTrieStore _trieStore;
        private readonly ReadOnlyVerkleStateStore _verkleTrieStore;
        protected readonly StateType _stateType;

        public ProofModuleFactory(
            IDbProvider dbProvider,
            IBlockTree blockTree,
            IReadOnlyTrieStore trieStore,
            IBlockPreprocessorStep recoveryStep,
            IReceiptFinder receiptFinder,
            ISpecProvider specProvider,
            ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _recoveryStep = recoveryStep ?? throw new ArgumentNullException(nameof(recoveryStep));
            _receiptFinder = receiptFinder ?? throw new ArgumentNullException(nameof(receiptFinder));
            _specProvider = specProvider ?? throw new ArgumentNullException(nameof(specProvider));
            _dbProvider = dbProvider.AsReadOnly(false);
            _blockTree = blockTree.AsReadOnly();
            _trieStore = trieStore;
            _stateType = StateType.Merkle;
        }

        public ProofModuleFactory(
            IDbProvider dbProvider,
            IBlockTree blockTree,
            ReadOnlyVerkleStateStore trieStore,
            IBlockPreprocessorStep recoveryStep,
            IReceiptFinder receiptFinder,
            ISpecProvider specProvider,
            ILogManager logManager)
        {
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _recoveryStep = recoveryStep ?? throw new ArgumentNullException(nameof(recoveryStep));
            _receiptFinder = receiptFinder ?? throw new ArgumentNullException(nameof(receiptFinder));
            _specProvider = specProvider ?? throw new ArgumentNullException(nameof(specProvider));
            _dbProvider = dbProvider.AsReadOnly(false);
            _blockTree = blockTree.AsReadOnly();
            _verkleTrieStore = trieStore;
            _stateType = StateType.Verkle;
        }

        public override IProofRpcModule Create()
        {
            ReadOnlyTxProcessingEnv txProcessingEnv = _stateType switch
            {
                StateType.Merkle => new ReadOnlyTxProcessingEnv(_dbProvider, _trieStore, _blockTree, _specProvider, _logManager),
                StateType.Verkle => new ReadOnlyTxProcessingEnv(_dbProvider, _verkleTrieStore, _blockTree, _specProvider, _logManager),
                _ => throw new ArgumentOutOfRangeException()
            };

            ReadOnlyChainProcessingEnv chainProcessingEnv = new(
                txProcessingEnv, Always.Valid, _recoveryStep, NoBlockRewards.Instance, new InMemoryReceiptStorage(), _dbProvider, _specProvider, _logManager);

            Tracer tracer = new Tracer(txProcessingEnv.StateProvider, chainProcessingEnv.ChainProcessor);

            return new ProofRpcModule(tracer, _blockTree, _receiptFinder, _specProvider, _logManager);
        }

        private static readonly List<JsonConverter> _converters = new()
        {
            new ProofConverter()
        };

        public override IReadOnlyCollection<JsonConverter> GetConverters() => _converters;
    }
}
