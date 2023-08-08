// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Stats.Model;
using Nethermind.Synchronization.FastSync;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.VerkleSync;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.Interfaces;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test.VerkleSync.VerkleSyncFeed;

public class VerkleSyncFeedTestsBase
{
    private const int TimeoutLength = 2000;

    protected static IBlockTree _blockTree;
    protected static IBlockTree BlockTree => LazyInitializer.EnsureInitialized(ref _blockTree, () => Build.A.BlockTree().OfChainLength(100).TestObject);

    protected ILogger _logger;
    protected ILogManager _logManager;

    private readonly int _defaultPeerCount;
    private readonly int _defaultPeerMaxRandomLatency;

    public VerkleSyncFeedTestsBase(int defaultPeerCount = 1, int defaultPeerMaxRandomLatency = 0)
    {
        _defaultPeerCount = defaultPeerCount;
        _defaultPeerMaxRandomLatency = defaultPeerMaxRandomLatency;
    }

    public static (string Name, Action<VerkleStateTree, IVerkleTrieStore, IDb> Action)[] Scenarios => VerkleTrieScenarios.Scenarios;

    [SetUp]
    public void Setup()
    {
        _logManager = LimboLogs.Instance;
        _logger = LimboTraceLogger.Instance;
        TrieScenarios.InitOnce();
    }

    [TearDown]
    public void TearDown()
    {
        (_logger as ConsoleAsyncLogger)?.Flush();
    }

    protected static void SetStorage(IVerkleTrieStore trieStore, Address address, byte i)
    {
        VerkleStateTree tree = new VerkleStateTree(trieStore, LimboLogs.Instance);
        for (int j = 0; j < i; j++)
        {
            StorageCell cell = new(address, i);
            tree.SetStorage(cell, ((UInt256)j).ToLittleEndian());
        }
    }

    protected class SafeContext
    {
        public ISyncModeSelector SyncModeSelector;
        public SyncPeerMock[] SyncPeerMocks;
        public ISyncPeerPool Pool;
        public Synchronization.VerkleSync.VerkleSyncFeed Feed;
        public VerkleSyncDispatcher StateSyncDispatcher;
    }

    public static string GetDbPathForTest()
    {
        string tempDir = Path.GetTempPath();
        string dbname = "VerkleTrie_TestID_" + TestContext.CurrentContext.Test.ID;
        return Path.Combine(tempDir, dbname);
    }

    public static IDbProvider GetDbProviderForVerkleTrees(DbMode dbMode)
    {
        switch (dbMode)
        {
            case DbMode.MemDb:
                return VerkleDbFactory.InitDatabase(dbMode, null);
            case DbMode.PersistantDb:
                return VerkleDbFactory.InitDatabase(dbMode, GetDbPathForTest());
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }
    }

    protected class DbContext
    {
        private readonly ILogger _logger;

        public DbContext(ILogger logger, ILogManager logManager, DbMode dbMode)
        {
            RemoteDbProvider = GetDbProviderForVerkleTrees(DbMode.MemDb);
            LocalDbProvider = GetDbProviderForVerkleTrees(DbMode.MemDb);

            _logger = logger;

            RemoteTrieStore = new VerkleStateStore(RemoteDbProvider, LimboLogs.Instance);
            LocalTrieStore = new VerkleStateStore(LocalDbProvider, LimboLogs.Instance);

            RemoteStateTree = new VerkleStateTree(RemoteTrieStore, LimboLogs.Instance);
            LocalStateTree = new VerkleStateTree(LocalTrieStore, LimboLogs.Instance);
        }

        public IDbProvider RemoteDbProvider;
        public IDbProvider LocalDbProvider;

        public IDb RemoteCodeDb => RemoteDbProvider.CodeDb;
        public IDb LocalCodeDb => LocalDbProvider.CodeDb;
        public IVerkleTrieStore RemoteTrieStore { get; }
        public IVerkleTrieStore LocalTrieStore { get; }

        public IDb RemoteLeafDb => RemoteDbProvider.LeafDb;
        public IDb LocalLeafDb => LocalDbProvider.LeafDb;

        public IDb RemoteInternalDb => RemoteDbProvider.InternalNodesDb;
        public IDb LocalInternalDb => LocalDbProvider.InternalNodesDb;
        public VerkleStateTree RemoteStateTree { get; }
        public VerkleStateTree LocalStateTree { get; }

        public void CompareTrees(string stage, bool skipLogs = false)
        {
            if (!skipLogs) _logger.Info($"==================== {stage} ====================");
            LocalStateTree.StateRoot = RemoteStateTree.StateRoot;

            if (!skipLogs) _logger.Info("-------------------- REMOTE --------------------");
            VerkleTreeDumper dumper = new VerkleTreeDumper();
            RemoteStateTree.Accept(dumper, RemoteStateTree.StateRoot);
            string remote = dumper.ToString();
            if (!skipLogs) _logger.Info(remote);
            if (!skipLogs) _logger.Info("-------------------- LOCAL --------------------");
            dumper.Reset();
            LocalStateTree.Accept(dumper, LocalStateTree.StateRoot);
            string local = dumper.ToString();
            if (!skipLogs) _logger.Info(local);

            if (stage == "END")
            {
                Assert.That(local, Is.EqualTo(remote), $"{remote}{Environment.NewLine}{local}");
                VerkleTrieStatsCollector collector = new(LocalCodeDb, LimboLogs.Instance);
                LocalStateTree.Accept(collector, LocalStateTree.StateRoot);
                Assert.That(collector.Stats.MissingLeaf, Is.EqualTo(0));
            }

            //            Assert.AreEqual(dbContext._remoteCodeDb.Keys.OrderBy(k => k, Bytes.Comparer).ToArray(), dbContext._localCodeDb.Keys.OrderBy(k => k, Bytes.Comparer).ToArray(), "keys");
            //            Assert.AreEqual(dbContext._remoteCodeDb.Values.OrderBy(k => k, Bytes.Comparer).ToArray(), dbContext._localCodeDb.Values.OrderBy(k => k, Bytes.Comparer).ToArray(), "values");
            //
            //            Assert.AreEqual(dbContext._remoteDb.Keys.OrderBy(k => k, Bytes.Comparer).ToArray(), _localDb.Keys.OrderBy(k => k, Bytes.Comparer).ToArray(), "keys");
            //            Assert.AreEqual(dbContext._remoteDb.Values.OrderBy(k => k, Bytes.Comparer).ToArray(), _localDb.Values.OrderBy(k => k, Bytes.Comparer).ToArray(), "values");
        }
    }

    protected class SyncPeerMock : ISyncPeer
    {
        public static Func<IList<Keccak>, Task<byte[][]>> NotPreimage = request =>
        {
            var result = new byte[request.Count][];

            int i = 0;
            foreach (Keccak _ in request) result[i++] = new byte[] { 1, 2, 3 };

            return Task.FromResult(result);
        };

        public static Func<IList<Keccak>, Task<byte[][]>> EmptyArraysInResponses = request =>
        {
            var result = new byte[request.Count][];

            int i = 0;
            foreach (Keccak _ in request) result[i++] = new byte[0];

            return Task.FromResult(result);
        };

        private readonly IDb _codeDb;
        private readonly IDb _stateDb;

        private Func<IReadOnlyList<Keccak>, Task<byte[][]>> _executorResultFunction;

        private Keccak[] _filter;

        private readonly long _maxRandomizedLatencyMs;

        public SyncPeerMock(
            IDb stateDb,
            IDb codeDb,
            Func<IReadOnlyList<Keccak>, Task<byte[][]>> executorResultFunction = null,
            long? maxRandomizedLatencyMs = null,
            Node? node = null
        )
        {
            _stateDb = stateDb;
            _codeDb = codeDb;

            if (executorResultFunction is not null) _executorResultFunction = executorResultFunction;

            Node = node ?? new Node(TestItem.PublicKeyA, "127.0.0.1", 30302, true) { EthDetails = "eth66" };
            _maxRandomizedLatencyMs = maxRandomizedLatencyMs ?? 0;
        }

        public int MaxResponseLength { get; set; } = int.MaxValue;

        public Node Node { get; }
        public string ClientId => "executorMock";
        public Keccak HeadHash { get; set; }
        public long HeadNumber { get; set; }
        public UInt256 TotalDifficulty { get; set; }
        public bool IsInitialized { get; set; }
        public bool IsPriority { get; set; }
        public byte ProtocolVersion { get; }
        public string ProtocolCode { get; }

        public void Disconnect(InitiateDisconnectReason reason, string details)
        {
            throw new NotImplementedException();
        }

        public Task<BlockBody[]> GetBlockBodies(IReadOnlyList<Keccak> blockHashes, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<BlockHeader[]> GetBlockHeaders(Keccak blockHash, int maxBlocks, int skip, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<BlockHeader[]> GetBlockHeaders(long number, int maxBlocks, int skip, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<BlockHeader?> GetHeadBlockHeader(Keccak? hash, CancellationToken token)
        {
            return Task.FromResult(BlockTree.Head?.Header);
        }

        public void NotifyOfNewBlock(Block block, SendBlockMode mode)
        {
            throw new NotImplementedException();
        }

        public PublicKey Id => Node.Id;

        public void SendNewTransactions(IEnumerable<Transaction> txs, bool sendFullTx)
        {
            throw new NotImplementedException();
        }

        public Task<TxReceipt[][]> GetReceipts(IReadOnlyList<Keccak> blockHash, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async Task<byte[][]> GetNodeData(IReadOnlyList<Keccak> hashes, CancellationToken token)
        {
            if (_maxRandomizedLatencyMs != 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(TestContext.CurrentContext.Random.NextLong() % _maxRandomizedLatencyMs));
            }

            if (_executorResultFunction is not null) return await _executorResultFunction(hashes);

            var responses = new byte[hashes.Count][];

            int i = 0;
            foreach (Keccak item in hashes)
            {
                if (i >= MaxResponseLength) break;

                if (_filter is null || _filter.Contains(item)) responses[i] = _stateDb[item.Bytes] ?? _codeDb[item.Bytes];

                i++;
            }

            return responses;
        }

        public void SetFilter(Keccak[] availableHashes)
        {
            _filter = availableHashes;
        }

        public void RegisterSatelliteProtocol<T>(string protocol, T protocolHandler) where T : class
        {
            throw new NotImplementedException();
        }

        public bool TryGetSatelliteProtocol<T>(string protocol, out T protocolHandler) where T : class
        {
            protocolHandler = null;
            return false;
        }
    }

}
