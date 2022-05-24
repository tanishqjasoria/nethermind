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
// 

using System;
using System.Collections.Generic;
using Nethermind.Blockchain.Processing;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Rewards;
using Nethermind.Blockchain.Test.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.Specs.Forks;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NSubstitute;
using NUnit.Framework;
using System.Security;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc.Test.Modules;
using System.Threading.Tasks;
using System.Threading;
using FluentAssertions;
using Nethermind.Core.Test.Blockchain;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Trie;

namespace Nethermind.Blockchain.Test
{
    [TestFixture]
    public class VerkleBlockProcessorTests
    {
        [Test]
        public void Prepared_block_contains_author_field()
        {
            IDb codeDb = new MemDb();
            VerkleTrieStore trieStore = new (DatabaseScheme.MemoryDb, LimboLogs.Instance, "./db/verkle_db");
            VerkleStateProvider stateProvider = new VerkleStateProvider(trieStore, LimboLogs.Instance, codeDb);
            ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
            BlockProcessor processor = new(
                RinkebySpecProvider.Instance,
                TestBlockValidator.AlwaysValid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(transactionProcessor, stateProvider),
                stateProvider,
                new VerkleStorageProvider(stateProvider, LimboLogs.Instance),
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                LimboLogs.Instance);

            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).TestObject;
            Block block = Build.A.Block.WithHeader(header).TestObject;
            Block[] processedBlocks = processor.Process(
                Keccak.EmptyTreeHash,
                new List<Block> {block},
                ProcessingOptions.None,
                NullBlockTracer.Instance);
            Assert.AreEqual(1, processedBlocks.Length, "length");
            Assert.AreEqual(block.Author, processedBlocks[0].Author, "author");
        }
        
        [Test]
        public void Prepare_block_with_transactions()
        {
            IDb codeDb = new MemDb();
            VerkleTrieStore trieStore = new (DatabaseScheme.MemoryDb, LimboLogs.Instance, "./db/verkle_db");
            VerkleStateProvider stateProvider = new VerkleStateProvider(trieStore, LimboLogs.Instance, codeDb);
            ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
            BlockProcessor processor = new(
                RinkebySpecProvider.Instance,
                TestBlockValidator.AlwaysValid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(transactionProcessor, stateProvider),
                stateProvider,
                new VerkleStorageProvider(stateProvider, LimboLogs.Instance),
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                LimboLogs.Instance);

            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).TestObject;
            Block block = Build.A.Block.WithHeader(header).TestObject;
            Block[] processedBlocks = processor.Process(
                Keccak.EmptyTreeHash,
                new List<Block> {block},
                ProcessingOptions.None,
                NullBlockTracer.Instance);
            Assert.AreEqual(1, processedBlocks.Length, "length");
            Assert.AreEqual(block.Author, processedBlocks[0].Author, "author");
        }

        [Test]
        public void Can_store_a_witness()
        {
            IDb codeDb = new MemDb();
            
            VerkleTrieStore trieStore = new (DatabaseScheme.MemoryDb, LimboLogs.Instance, "./db/verkle_db");
            VerkleStateProvider stateProvider = new VerkleStateProvider(trieStore, LimboLogs.Instance, codeDb);
            ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
            IWitnessCollector witnessCollector = Substitute.For<IWitnessCollector>();
            BlockProcessor processor = new(
                RinkebySpecProvider.Instance,
                TestBlockValidator.AlwaysValid,
                NoBlockRewards.Instance,
                new BlockProcessor.BlockValidationTransactionsExecutor(transactionProcessor, stateProvider),
                stateProvider,
                new VerkleStorageProvider(stateProvider, LimboLogs.Instance),
                NullReceiptStorage.Instance,
                witnessCollector,
                LimboLogs.Instance);

            BlockHeader header = Build.A.BlockHeader.WithAuthor(TestItem.AddressD).TestObject;
            header.StateRoot = Keccak.Zero;
            Block block = Build.A.Block.WithHeader(header).TestObject;
            _ = processor.Process(
                Keccak.EmptyTreeHash,
                new List<Block> {block},
                ProcessingOptions.None,
                NullBlockTracer.Instance);

            witnessCollector.Received(1).Persist(block.Hash);
        }

        [Test]
        public void Recovers_state_on_cancel()
        {
            IDb codeDb = new MemDb();
            VerkleTrieStore trieStore = new (DatabaseScheme.MemoryDb, LimboLogs.Instance, "./db/verkle_db");
            VerkleStateProvider stateProvider = new VerkleStateProvider(trieStore, LimboLogs.Instance, codeDb);
            ITransactionProcessor transactionProcessor = Substitute.For<ITransactionProcessor>();
            BlockProcessor processor = new(
                RinkebySpecProvider.Instance,
                TestBlockValidator.AlwaysValid,
                new RewardCalculator(MainnetSpecProvider.Instance),
                new BlockProcessor.BlockValidationTransactionsExecutor(transactionProcessor, stateProvider),
                stateProvider,
                new VerkleStorageProvider(stateProvider, LimboLogs.Instance),
                NullReceiptStorage.Instance,
                NullWitnessCollector.Instance,
                LimboLogs.Instance);

            BlockHeader header = Build.A.BlockHeader.WithNumber(1).WithAuthor(TestItem.AddressD).TestObject;
            Block block = Build.A.Block.WithTransactions(1, MuirGlacier.Instance).WithHeader(header).TestObject;
            Assert.Throws<OperationCanceledException>(() => processor.Process(
                Keccak.EmptyTreeHash,
                new List<Block> {block},
                ProcessingOptions.None,
                AlwaysCancelBlockTracer.Instance));

            Assert.Throws<OperationCanceledException>(() => processor.Process(
                Keccak.EmptyTreeHash,
                new List<Block> {block},
                ProcessingOptions.None,
                AlwaysCancelBlockTracer.Instance));
        }

        [TestCase(20)]
        [TestCase(63)]
        [TestCase(64)]
        [TestCase(65)]
        [TestCase(127)]
        [TestCase(128)]
        [TestCase(129)]
        [TestCase(130)]
        [TestCase(1000)]
        [TestCase(2000)]
        public async Task Process_long_running_branch(int blocksAmount)
        {
            var address = TestItem.Addresses[0];
            var spec = new SingleReleaseSpecProvider(ConstantinopleFix.Instance, 1);
            var testRpc = await VerkleTestRpcBlockchain.ForTest(SealEngineType.NethDev)
                .Build(spec);
            testRpc.TestWallet.UnlockAccount(address, new SecureString());
            await testRpc.AddFunds(address, 1.Ether());
            await testRpc.AddBlock();
            var suggestedBlockResetEvent = new SemaphoreSlim(0);
            testRpc.BlockTree.NewHeadBlock += (s, e) =>
            {
                suggestedBlockResetEvent.Release(1);
            };

            var branchLength = blocksAmount + (int)testRpc.BlockTree.BestKnownNumber + 1;
            ((BlockTree)testRpc.BlockTree).AddBranch(branchLength, (int)testRpc.BlockTree.BestKnownNumber);
            (await suggestedBlockResetEvent.WaitAsync(VerkleTestBlockchain.DefaultTimeout * 10)).Should().BeTrue();
            Assert.AreEqual(branchLength - 1, (int)testRpc.BlockTree.BestKnownNumber);
        }
        
        [Test]
        public async Task Process_something()
        {
            var address = TestItem.Addresses[0];
            var address2 = new Address("0x368c3FBB093C385C5d2Eb50726AB7a0e212B3a77");
            var spec = new SingleReleaseSpecProvider(ConstantinopleFix.Instance, 1);
            var testRpc = await VerkleTestRpcBlockchain.ForTest(SealEngineType.NethDev)
                .Build(spec);
            testRpc.TestWallet.UnlockAccount(address, new SecureString());
            await testRpc.AddFunds(address, 1.Ether());
            await testRpc.AddFunds(address2, 1.Ether());
            var txn = Build.A.Transaction.WithValue(1).WithTo(new Address("0x71d2Dc1E106384B75F35fE9CbE88363899414cAE"))
                .WithNonce(5).SignedAndResolved(TestItem.PrivateKeyA).TestObject;
            await testRpc.AddBlock(txn);
            var suggestedBlockResetEvent = new SemaphoreSlim(0);
            testRpc.BlockTree.NewHeadBlock += (s, e) =>
            {
                suggestedBlockResetEvent.Release(1);
            };
        }
    }
}
