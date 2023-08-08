// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using FluentAssertions;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Db.Blooms;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using Nethermind.State;
using Nethermind.State.Repositories;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test.Producers
{
    [TestFixture]
    public class DevBlockProducerTests
    {
        [Test, Timeout(Timeout.MaxTestTime)]
        public void Test()
        {
            ISpecProvider specProvider = MainnetSpecProvider.Instance;
            DbProvider dbProvider = new(DbModeHint.Mem);
            dbProvider.RegisterDb(DbNames.BlockInfos, new MemDb());
            dbProvider.RegisterDb(DbNames.Blocks, new MemDb());
            dbProvider.RegisterDb(DbNames.Headers, new MemDb());
            dbProvider.RegisterDb(DbNames.State, new MemDb());
            dbProvider.RegisterDb(DbNames.Code, new MemDb());
            dbProvider.RegisterDb(DbNames.Metadata, new MemDb());

            BlockTree blockTree = new(
                dbProvider,
                new ChainLevelInfoRepository(dbProvider),
                specProvider,
                NullBloomStorage.Instance,
                LimboLogs.Instance);
            TrieStore trieStore = new(
                dbProvider.RegisteredDbs[DbNames.State],
                NoPruning.Instance,
                Archive.Instance,
                LimboLogs.Instance);
            WorldState stateProvider = new(
                trieStore,
                dbProvider.RegisteredDbs[DbNames.Code],
                LimboLogs.Instance);
            StateReader stateReader = new(trieStore, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            BlockhashProvider blockhashProvider = new(blockTree, LimboLogs.Instance);
            VirtualMachine virtualMachine = new(
                blockhashProvider,
                specProvider,
                LimboLogs.Instance);
            TransactionProcessor txProcessor = new(
                specProvider,
                stateProvider,
                virtualMachine,
                LimboLogs.Instance);
            BlockProcessor blockProcessor = new(
                specProvider,
                Always.Valid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(txProcessor, stateProvider),
                stateProvider,
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                LimboLogs.Instance);
            BlockchainProcessor blockchainProcessor = new(
                blockTree,
                blockProcessor,
                NullRecoveryStep.Instance,
                stateReader,
                LimboLogs.Instance,
                BlockchainProcessor.Options.Default);
            BuildBlocksWhenRequested trigger = new();
            ManualTimestamper timestamper = new ManualTimestamper();
            DevBlockProducer devBlockProducer = new(
                EmptyTxSource.Instance,
                blockchainProcessor,
                stateProvider,
                blockTree,
                trigger,
                timestamper,
                specProvider,
                new BlocksConfig(),
                LimboLogs.Instance);

            blockchainProcessor.Start();
            devBlockProducer.Start();
            ProducedBlockSuggester suggester = new ProducedBlockSuggester(blockTree, devBlockProducer);

            AutoResetEvent autoResetEvent = new(false);

            blockTree.NewHeadBlock += (s, e) => autoResetEvent.Set();
            blockTree.SuggestBlock(Build.A.Block.Genesis.TestObject);

            autoResetEvent.WaitOne(1000).Should().BeTrue("genesis");

            trigger.BuildBlock();
            autoResetEvent.WaitOne(1000).Should().BeTrue("1");
            blockTree.Head.Number.Should().Be(1);
        }

        [Test, Timeout(Timeout.MaxTestTime)]
        public void TestVerkle()
        {
            ISpecProvider specProvider = MainnetSpecProvider.Instance;
            DbProvider dbProvider = new(DbModeHint.Mem);
            dbProvider.RegisterDb(DbNames.BlockInfos, new MemDb());
            dbProvider.RegisterDb(DbNames.Blocks, new MemDb());
            dbProvider.RegisterDb(DbNames.Headers, new MemDb());
            dbProvider.RegisterDb(DbNames.State, new MemDb());
            dbProvider.RegisterDb(DbNames.Code, new MemDb());
            dbProvider.RegisterDb(DbNames.Metadata, new MemDb());
            dbProvider.RegisterDb(DbNames.Leaf, new MemDb());
            dbProvider.RegisterDb(DbNames.InternalNodes, new MemDb());
            dbProvider.RegisterDb(DbNames.ForwardDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.ReverseDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.StateRootToBlock, new MemDb());

            BlockTree blockTree = new(
                dbProvider,
                new ChainLevelInfoRepository(dbProvider),
                specProvider,
                NullBloomStorage.Instance,
                SimpleConsoleLogManager.Instance);
            VerkleStateTree stateTree = new (dbProvider, SimpleConsoleLogManager.Instance);
            VerkleStateReader stateReader = new(stateTree, dbProvider.GetDb<IDb>(DbNames.Code), SimpleConsoleLogManager.Instance);
            VerkleWorldState worldState = new (stateTree, dbProvider.RegisteredDbs[DbNames.Code], SimpleConsoleLogManager.Instance);
            BlockhashProvider blockHashProvider = new(blockTree, SimpleConsoleLogManager.Instance);
            VirtualMachine virtualMachine = new(
                blockHashProvider,
                specProvider,
                SimpleConsoleLogManager.Instance);
            TransactionProcessor txProcessor = new(
                specProvider,
                worldState,
                virtualMachine,
                SimpleConsoleLogManager.Instance);
            BlockProcessor blockProcessor = new(
                specProvider,
                Always.Valid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(txProcessor, worldState),
                worldState,
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                SimpleConsoleLogManager.Instance);
            BlockchainProcessor blockchainProcessor = new(
                blockTree,
                blockProcessor,
                NullRecoveryStep.Instance,
                stateReader,
                SimpleConsoleLogManager.Instance,
                BlockchainProcessor.Options.Default);
            BuildBlocksWhenRequested trigger = new();
            ManualTimestamper timestamper = new ManualTimestamper();
            DevBlockProducer devBlockProducer = new(
                EmptyTxSource.Instance,
                blockchainProcessor,
                worldState,
                blockTree,
                trigger,
                timestamper,
                specProvider,
                new BlocksConfig(),
                SimpleConsoleLogManager.Instance);

            blockchainProcessor.Start();
            devBlockProducer.Start();
            ProducedBlockSuggester suggester = new ProducedBlockSuggester(blockTree, devBlockProducer);

            AutoResetEvent autoResetEvent = new(false);

            blockTree.NewHeadBlock += (s, e) => autoResetEvent.Set();
            blockTree.SuggestBlock(Build.A.Block.Genesis.WithStateRoot(Keccak.Zero).TestObject);

            autoResetEvent.WaitOne(1000).Should().BeTrue("genesis");

            trigger.BuildBlock();
            autoResetEvent.WaitOne(1000).Should().BeTrue("1");
            blockTree.Head.Number.Should().Be(1);
        }

        [Test, Timeout(Timeout.MaxTestTime)]
        public void TestVerkleBlocksWithExecutionWitness()
        {
            ISpecProvider specProvider = new TestSpecProvider(Prague.Instance);
            DbProvider dbProvider = new(DbModeHint.Mem);
            dbProvider.RegisterDb(DbNames.BlockInfos, new MemDb());
            dbProvider.RegisterDb(DbNames.Blocks, new MemDb());
            dbProvider.RegisterDb(DbNames.Headers, new MemDb());
            dbProvider.RegisterDb(DbNames.State, new MemDb());
            dbProvider.RegisterDb(DbNames.Code, new MemDb());
            dbProvider.RegisterDb(DbNames.Metadata, new MemDb());
            dbProvider.RegisterDb(DbNames.Leaf, new MemDb());
            dbProvider.RegisterDb(DbNames.InternalNodes, new MemDb());
            dbProvider.RegisterDb(DbNames.ForwardDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.ReverseDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.StateRootToBlock, new MemDb());

            BlockTree blockTree = new(
                dbProvider,
                new ChainLevelInfoRepository(dbProvider),
                specProvider,
                NullBloomStorage.Instance,
                SimpleConsoleLogManager.Instance);
            VerkleStateTree stateTree = new (dbProvider, SimpleConsoleLogManager.Instance);
            VerkleStateReader stateReader = new(stateTree, dbProvider.GetDb<IDb>(DbNames.Code), SimpleConsoleLogManager.Instance);
            VerkleWorldState worldState = new (stateTree, dbProvider.RegisteredDbs[DbNames.Code], SimpleConsoleLogManager.Instance);

            worldState.CreateAccount(TestItem.AddressA, 1000.Ether());
            worldState.CreateAccount(TestItem.AddressB, 1000.Ether());
            worldState.CreateAccount(TestItem.AddressC, 1000.Ether());

            byte[] code = Bytes.FromHexString("0xabcd");
            worldState.InsertCode(TestItem.AddressA, code, specProvider.GenesisSpec);

            worldState.Set(new StorageCell(TestItem.AddressA, UInt256.One), Bytes.FromHexString("0xabcdef"));

            worldState.Commit(specProvider.GenesisSpec);
            worldState.CommitTree(0);

            BlockhashProvider blockHashProvider = new(blockTree, SimpleConsoleLogManager.Instance);
            VirtualMachine virtualMachine = new(
                blockHashProvider,
                specProvider,
                SimpleConsoleLogManager.Instance);
            TransactionProcessor txProcessor = new(
                specProvider,
                worldState,
                virtualMachine,
                SimpleConsoleLogManager.Instance);
            BlockProcessor blockProcessor = new(
                specProvider,
                Always.Valid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(txProcessor, worldState),
                worldState,
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                SimpleConsoleLogManager.Instance);
            BlockchainProcessor blockchainProcessor = new(
                blockTree,
                blockProcessor,
                NullRecoveryStep.Instance,
                stateReader,
                SimpleConsoleLogManager.Instance,
                BlockchainProcessor.Options.Default);
            BuildBlocksWhenRequested trigger = new();
            ManualTimestamper timestamper = new ManualTimestamper();
            DevBlockProducer devBlockProducer = new(
                EmptyTxSource.Instance,
                blockchainProcessor,
                worldState,
                blockTree,
                trigger,
                timestamper,
                specProvider,
                new BlocksConfig(),
                SimpleConsoleLogManager.Instance);

            blockchainProcessor.Start();
            devBlockProducer.Start();
            ProducedBlockSuggester suggester = new ProducedBlockSuggester(blockTree, devBlockProducer);

            AutoResetEvent autoResetEvent = new(false);

            blockTree.NewHeadBlock += (s, e) => autoResetEvent.Set();
            blockTree.SuggestBlock(Build.A.Block.Genesis.WithStateRoot(worldState.StateRoot).TestObject);

            autoResetEvent.WaitOne(1000).Should().BeTrue("genesis");

            trigger.BuildBlock();
            autoResetEvent.WaitOne(1000).Should().BeTrue("1");
            blockTree.Head.Number.Should().Be(1);
        }

        [Test, Timeout(Timeout.MaxTestTime)]
        public void TestVerkleBlocksWithExecutionWitnessAndStatelessValidation()
        {
            ISpecProvider specProvider = new TestSpecProvider(Prague.Instance);
            DbProvider dbProvider = new(DbModeHint.Mem);
            dbProvider.RegisterDb(DbNames.BlockInfos, new MemDb());
            dbProvider.RegisterDb(DbNames.Blocks, new MemDb());
            dbProvider.RegisterDb(DbNames.Headers, new MemDb());
            dbProvider.RegisterDb(DbNames.State, new MemDb());
            dbProvider.RegisterDb(DbNames.Code, new MemDb());
            dbProvider.RegisterDb(DbNames.Metadata, new MemDb());
            dbProvider.RegisterDb(DbNames.Leaf, new MemDb());
            dbProvider.RegisterDb(DbNames.InternalNodes, new MemDb());
            dbProvider.RegisterDb(DbNames.ForwardDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.ReverseDiff, new MemDb());
            dbProvider.RegisterDb(DbNames.StateRootToBlock, new MemDb());

            BlockTree blockTree = new(
                dbProvider,
                new ChainLevelInfoRepository(dbProvider),
                specProvider,
                NullBloomStorage.Instance,
                SimpleConsoleLogManager.Instance);
            VerkleStateTree stateTree = new (dbProvider, SimpleConsoleLogManager.Instance);
            VerkleStateReader stateReader = new(stateTree, dbProvider.GetDb<IDb>(DbNames.Code), SimpleConsoleLogManager.Instance);
            VerkleWorldState worldState = new (stateTree, dbProvider.RegisteredDbs[DbNames.Code], SimpleConsoleLogManager.Instance);
            BlockhashProvider blockHashProvider = new(blockTree, SimpleConsoleLogManager.Instance);
            VirtualMachine virtualMachine = new(
                blockHashProvider,
                specProvider,
                SimpleConsoleLogManager.Instance);
            TransactionProcessor txProcessor = new(
                specProvider,
                worldState,
                virtualMachine,
                SimpleConsoleLogManager.Instance);
            BlockProcessor blockProcessor = new(
                specProvider,
                Always.Valid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(txProcessor, worldState),
                worldState,
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                SimpleConsoleLogManager.Instance);
            BlockchainProcessor blockchainProcessor = new(
                blockTree,
                blockProcessor,
                NullRecoveryStep.Instance,
                stateReader,
                SimpleConsoleLogManager.Instance,
                BlockchainProcessor.Options.Default);
            BuildBlocksWhenRequested trigger = new();
            ManualTimestamper timestamper = new ManualTimestamper();
            DevBlockProducer devBlockProducer = new(
                EmptyTxSource.Instance,
                blockchainProcessor,
                worldState,
                blockTree,
                trigger,
                timestamper,
                specProvider,
                new BlocksConfig(),
                SimpleConsoleLogManager.Instance);

            blockchainProcessor.Start();
            devBlockProducer.Start();
            ProducedBlockSuggester suggester = new ProducedBlockSuggester(blockTree, devBlockProducer);

            AutoResetEvent autoResetEvent = new(false);

            blockTree.NewHeadBlock += (s, e) => autoResetEvent.Set();
            blockTree.SuggestBlock(Build.A.Block.Genesis.WithStateRoot(Keccak.Zero).TestObject);

            autoResetEvent.WaitOne(1000).Should().BeTrue("genesis");

            trigger.BuildBlock();
            autoResetEvent.WaitOne(1000).Should().BeTrue("1");
            blockTree.Head.Number.Should().Be(1);
        }
    }
}
