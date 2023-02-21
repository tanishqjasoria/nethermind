// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle;
using NUnit.Framework;

namespace Nethermind.Evm.Test
{
    public class VirtualMachineTestsBase
    {
        protected readonly VirtualMachineTestsStateProvider _stateProvider;

        public enum VirtualMachineTestsStateProvider
        {
            MerkleTrie,
            VerkleTrie
        }

        protected const string SampleHexData1 = "a01234";
        protected const string SampleHexData2 = "b15678";
        protected const string HexZero = "00";
        protected const long DefaultBlockGasLimit = 8000000;

        private IEthereumEcdsa _ethereumEcdsa;
        protected ITransactionProcessor _processor;

        protected VirtualMachine Machine { get; private set; }
        protected IWorldState WorldState { get; private set; }

        protected static Address Contract { get; } = new("0xd75a3a95360e44a3874e691fb48d77855f127069");
        protected static Address Sender { get; } = TestItem.AddressA;
        protected static Address Recipient { get; } = TestItem.AddressB;
        protected static Address Miner { get; } = TestItem.AddressD;

        protected static PrivateKey SenderKey { get; } = TestItem.PrivateKeyA;
        protected static PrivateKey RecipientKey { get; } = TestItem.PrivateKeyB;
        protected static PrivateKey MinerKey { get; } = TestItem.PrivateKeyD;

        protected virtual long BlockNumber => MainnetSpecProvider.ByzantiumBlockNumber;
        protected virtual ulong Timestamp => 0UL;
        protected virtual ISpecProvider SpecProvider => MainnetSpecProvider.Instance;
        protected IReleaseSpec Spec => SpecProvider.GetSpec(BlockNumber, Timestamp);

        protected virtual ILogManager GetLogManager()
        {
            return LimboLogs.Instance;
        }

        public VirtualMachineTestsBase(VirtualMachineTestsStateProvider stateProvider)
        {
            _stateProvider = stateProvider;
        }

        [SetUp]
        public virtual void Setup()
        {
            ILogManager logManager = GetLogManager();

            IDb codeDb = new MemDb();
            MemDb stateDb = new MemDb();
            if (_stateProvider == VirtualMachineTestsStateProvider.MerkleTrie)
            {
                WorldState = new WorldState(new TrieStore(stateDb, logManager), codeDb, logManager);
            }
            else
            {
                IDbProvider provider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
                WorldState = new VerkleWorldState(new VerkleStateTree(provider), codeDb, logManager);
            }

            _ethereumEcdsa = new EthereumEcdsa(SpecProvider.ChainId, logManager);
            IBlockhashProvider blockhashProvider = TestBlockhashProvider.Instance;
            Machine = new VirtualMachine(blockhashProvider, SpecProvider, logManager);
            _processor = new TransactionProcessor(SpecProvider, WorldState, Machine, logManager);
        }

        protected GethLikeTxTrace ExecuteAndTrace(params byte[] code)
        {
            GethLikeTxTracer tracer = new(GethTraceOptions.Default);
            (Block block, Transaction transaction) = PrepareTx(BlockNumber, 100000, code);
            _processor.Execute(transaction, block.Header, tracer);
            return tracer.BuildResult();
        }

        protected GethLikeTxTrace ExecuteAndTrace(long blockNumber, long gasLimit, params byte[] code)
        {
            GethLikeTxTracer tracer = new(GethTraceOptions.Default);
            (Block block, Transaction transaction) = PrepareTx(blockNumber, gasLimit, code);
            _processor.Execute(transaction, block.Header, tracer);
            return tracer.BuildResult();
        }

        protected TestAllTracerWithOutput Execute(long blockNumber, ulong timestamp, params byte[] code)
        {
            (Block block, Transaction transaction) = PrepareTx(blockNumber, 100000, code, timestamp: timestamp);
            TestAllTracerWithOutput tracer = CreateTracer();
            _processor.Execute(transaction, block.Header, tracer);
            return tracer;
        }

        protected TestAllTracerWithOutput Execute(params byte[] code)
        {
            return Execute(BlockNumber, Timestamp, code);
        }

        protected virtual TestAllTracerWithOutput CreateTracer() => new();

        protected T Execute<T>(T tracer, params byte[] code) where T : ITxTracer
        {
            (Block block, Transaction transaction) = PrepareTx(BlockNumber, 100000, code, timestamp: Timestamp);
            _processor.Execute(transaction, block.Header, tracer);
            return tracer;
        }

        protected TestAllTracerWithOutput Execute(long blockNumber, long gasLimit, byte[] code, long blockGasLimit = DefaultBlockGasLimit, ulong timestamp = 0)
        {
            (Block block, Transaction transaction) = PrepareTx(blockNumber, gasLimit, code, blockGasLimit: blockGasLimit, timestamp: timestamp);
            TestAllTracerWithOutput tracer = CreateTracer();
            _processor.Execute(transaction, block.Header, tracer);
            return tracer;
        }

        protected (Block block, Transaction transaction) PrepareTx(
            long blockNumber,
            long gasLimit,
            byte[] code,
            SenderRecipientAndMiner senderRecipientAndMiner = null,
            int value = 1,
            long blockGasLimit = DefaultBlockGasLimit,
            ulong timestamp = 0)
        {
            senderRecipientAndMiner ??= SenderRecipientAndMiner.Default;
            if (!WorldState.AccountExists(senderRecipientAndMiner.Sender))
                WorldState.CreateAccount(senderRecipientAndMiner.Sender, 100.Ether());
            else
                WorldState.AddToBalance(senderRecipientAndMiner.Sender, 100.Ether(), SpecProvider.GenesisSpec);

            if (!WorldState.AccountExists(senderRecipientAndMiner.Recipient))
                WorldState.CreateAccount(senderRecipientAndMiner.Recipient, 100.Ether());
            else
                WorldState.AddToBalance(senderRecipientAndMiner.Recipient, 100.Ether(), SpecProvider.GenesisSpec);
            WorldState.InsertCode(senderRecipientAndMiner.Recipient, code, SpecProvider.GenesisSpec);

            GetLogManager().GetClassLogger().Debug("Committing initial state");
            WorldState.Commit(SpecProvider.GenesisSpec);
            GetLogManager().GetClassLogger().Debug("Committed initial state");
            GetLogManager().GetClassLogger().Debug("Committing initial tree");
            WorldState.CommitTree(0);
            GetLogManager().GetClassLogger().Debug("Committed initial tree");

            Transaction transaction = Build.A.Transaction
                .WithGasLimit(gasLimit)
                .WithGasPrice(1)
                .WithValue(value)
                .WithNonce(WorldState.GetNonce(senderRecipientAndMiner.Sender))
                .To(senderRecipientAndMiner.Recipient)
                .SignedAndResolved(_ethereumEcdsa, senderRecipientAndMiner.SenderKey)
                .TestObject;

            Block block = BuildBlock(blockNumber, senderRecipientAndMiner, transaction, blockGasLimit, timestamp);
            return (block, transaction);
        }

        protected (Block block, Transaction transaction) PrepareTx(long blockNumber, long gasLimit, byte[] code, byte[] input, UInt256 value, SenderRecipientAndMiner senderRecipientAndMiner = null)
        {
            senderRecipientAndMiner ??= SenderRecipientAndMiner.Default;
            WorldState.CreateAccount(senderRecipientAndMiner.Sender, 100.Ether());
            WorldState.CreateAccount(senderRecipientAndMiner.Recipient, 100.Ether());
            WorldState.InsertCode(senderRecipientAndMiner.Recipient, code, SpecProvider.GenesisSpec); ;

            WorldState.Commit(SpecProvider.GenesisSpec);

            Transaction transaction = Build.A.Transaction
                .WithGasLimit(gasLimit)
                .WithGasPrice(1)
                .WithData(input)
                .WithValue(value)
                .To(senderRecipientAndMiner.Recipient)
                .SignedAndResolved(_ethereumEcdsa, senderRecipientAndMiner.SenderKey)
                .TestObject;

            Block block = BuildBlock(blockNumber, senderRecipientAndMiner);
            return (block, transaction);
        }

        protected (Block block, Transaction transaction) PrepareInitTx(long blockNumber, long gasLimit, byte[] code, SenderRecipientAndMiner senderRecipientAndMiner = null)
        {
            senderRecipientAndMiner ??= SenderRecipientAndMiner.Default;
            WorldState.CreateAccount(senderRecipientAndMiner.Sender, 100.Ether());
            WorldState.Commit(SpecProvider.GenesisSpec);

            Transaction transaction = Build.A.Transaction
                .WithTo(null)
                .WithGasLimit(gasLimit)
                .WithGasPrice(1)
                .WithCode(code)
                .SignedAndResolved(_ethereumEcdsa, senderRecipientAndMiner.SenderKey)
                .TestObject;

            Block block = BuildBlock(blockNumber, senderRecipientAndMiner);
            return (block, transaction);
        }

        protected Block BuildBlock(long blockNumber, SenderRecipientAndMiner senderRecipientAndMiner)
        {
            return BuildBlock(blockNumber, senderRecipientAndMiner, null);
        }

        protected virtual Block BuildBlock(long blockNumber, SenderRecipientAndMiner senderRecipientAndMiner,
            Transaction tx, long blockGasLimit = DefaultBlockGasLimit,
            ulong timestamp = 0)
        {
            senderRecipientAndMiner ??= SenderRecipientAndMiner.Default;
            return Build.A.Block.WithNumber(blockNumber)
                .WithTransactions(tx is null ? new Transaction[0] : new[] { tx })
                .WithGasLimit(blockGasLimit)
                .WithBeneficiary(senderRecipientAndMiner.Miner)
                .WithTimestamp(timestamp)
                .TestObject;
        }

        protected void AssertGas(TestAllTracerWithOutput receipt, long gas)
        {
            Assert.AreEqual(gas, receipt.GasSpent, "gas");
        }

        protected void AssertStorage(UInt256 address, Address value)
        {
            Assert.AreEqual(value.Bytes.PadLeft(32), WorldState.Get(new StorageCell(Recipient, address)).PadLeft(32), "storage");
        }

        protected void AssertStorage(UInt256 address, Keccak value)
        {
            Assert.AreEqual(value.Bytes, WorldState.Get(new StorageCell(Recipient, address)).PadLeft(32), "storage");
        }

        protected void AssertStorage(UInt256 address, ReadOnlySpan<byte> value)
        {
            Assert.AreEqual(new ZeroPaddedSpan(value, 32 - value.Length, PadDirection.Left).ToArray(), WorldState.Get(new StorageCell(Recipient, address)).PadLeft(32), "storage");
        }

        protected void AssertStorage(UInt256 address, BigInteger expectedValue)
        {
            byte[] actualValue = WorldState.Get(new StorageCell(Recipient, address));
            byte[] expected = expectedValue < 0 ? expectedValue.ToBigEndianByteArray(32) : expectedValue.ToBigEndianByteArray();
            Assert.AreEqual(expected, actualValue, "storage");
        }

        protected void AssertStorage(UInt256 address, UInt256 expectedValue)
        {
            byte[] bytes = ((BigInteger)expectedValue).ToBigEndianByteArray();

            byte[] actualValue = WorldState.Get(new StorageCell(Recipient, address));
            Assert.AreEqual(bytes, actualValue, "storage");
        }

        private static int _callIndex = -1;

        protected void AssertStorage(StorageCell storageCell, UInt256 expectedValue)
        {
            _callIndex++;
            if (!WorldState.AccountExists(storageCell.Address))
            {
                Assert.AreEqual(expectedValue.ToBigEndian().WithoutLeadingZeros().ToArray(), new byte[] { 0 }, $"storage {storageCell}, call {_callIndex}");
            }
            else
            {
                byte[] actualValue = WorldState.Get(storageCell);
                Assert.AreEqual(expectedValue.ToBigEndian().WithoutLeadingZeros().ToArray(), actualValue, $"storage {storageCell}, call {_callIndex}");
            }
        }

        protected void AssertCodeHash(Address address, Keccak codeHash)
        {
            Assert.AreEqual(codeHash, WorldState.GetCodeHash(address), "code hash");
        }
    }
}
