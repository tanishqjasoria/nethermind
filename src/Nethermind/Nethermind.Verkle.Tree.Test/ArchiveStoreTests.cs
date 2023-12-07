// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections;
using FluentAssertions;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.History.V2;
using Nethermind.Verkle.Tree.TrieStore;

namespace Nethermind.Verkle.Tree.Test;

public class ArchiveStoreTests
{
    [TearDown]
    public void CleanTestData()
    {
        string dbPath = VerkleTestUtils.GetDbPathForTest();
        if (Directory.Exists(dbPath))
        {
            Directory.Delete(dbPath, true);
        }
    }

    public static IEnumerable MultiBlockTest
    {
        get
        {
            ulong maxBlock = 100;
            int maxChunk = 20;
            for (ulong i = 10; i < maxBlock; i = i + 10)
            {
                for (int j = 1; j < maxChunk; j++)
                {
                    yield return new TestCaseData(DbMode.MemDb, i, j);
                    yield return new TestCaseData(DbMode.PersistantDb, i, j);
                }
            }
        }
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGetMultiBlockReverseStateWithEliasFano(DbMode dbMode)
    {

        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                break;
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, VerkleTestUtils.GetDbPathForTest());
                break;
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }

        VerkleStateStore stateStore = new VerkleStateStore(provider, 0, LimboLogs.Instance);
        VerkleTree tree = new VerkleTree(stateStore, LimboLogs.Instance);

        VerkleArchiveStore archiveStore = new VerkleArchiveStore(stateStore, provider, LimboLogs.Instance);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._emptyArray);
        tree.Commit();
        tree.CommitTree(0);
        VerkleCommitment stateRoot0 = tree.StateRoot;
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._arrayAll0Last2);
        tree.Commit();
        tree.CommitTree(1);
        VerkleCommitment stateRoot1 = tree.StateRoot;
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._arrayAll0Last3);
        tree.Commit();
        tree.CommitTree(2);
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);

        archiveStore.GetLeaf(VerkleTestUtils._keyVersion, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyBalance, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyNonce, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeCommitment, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeSize, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);

        archiveStore.GetLeaf(VerkleTestUtils._keyVersion, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyBalance, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyNonce, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeCommitment, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeSize, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
    }

    [TestCase(DbMode.MemDb, (ulong)10, 10)]
    [TestCase(DbMode.MemDb, (ulong)20, 10)]
    [TestCase(DbMode.MemDb, (ulong)60, 10)]
    [TestCase(DbMode.MemDb, (ulong)500, 10)]
    [TestCase(DbMode.PersistantDb, (ulong)10, 10)]
    [TestCase(DbMode.PersistantDb, (ulong)20, 10)]
    [TestCase(DbMode.PersistantDb, (ulong)60, 10)]
    [TestCase(DbMode.PersistantDb, (ulong)10, 2)]
    [TestCase(DbMode.MemDb, (ulong)10, 2)]
    [TestCase(DbMode.PersistantDb, (ulong)20, 4)]
    [TestCase(DbMode.MemDb, (ulong)20, 4)]
    [TestCase(DbMode.PersistantDb, (ulong)30, 6)]
    [TestCase(DbMode.MemDb, (ulong)30, 6)]
    [TestCase(DbMode.PersistantDb, (ulong)40, 8)]
    [TestCase(DbMode.MemDb, (ulong)40, 8)]
    // [TestCaseSource(nameof(MultiBlockTest))]
    public void TestArchiveStoreForMultipleBlocks(DbMode dbMode, ulong numBlocks, int blockChunks)
    {
        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                break;
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, VerkleTestUtils.GetDbPathForTest());
                break;
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }

        VerkleStateStore stateStore = new VerkleStateStore(provider, 0, LimboLogs.Instance);
        VerkleTree tree = new VerkleTree(stateStore, LimboLogs.Instance);

        VerkleArchiveStore archiveStore =
            new VerkleArchiveStore(stateStore, provider, LimboLogs.Instance) { BlockChunks = blockChunks };

        Pedersen[] keys =
        {
            VerkleTestUtils._keyVersion, VerkleTestUtils._keyNonce, VerkleTestUtils._keyBalance,
            VerkleTestUtils._keyCodeSize, VerkleTestUtils._keyCodeCommitment
        };

        List<VerkleCommitment> stateRoots = new List<VerkleCommitment>();
        ulong i = 0;
        long block = 0;
        while (i < numBlocks)
        {
            foreach (Pedersen key in keys)
            {
                tree.Insert(key, new UInt256(i++).ToBigEndian());
                tree.Commit();
                tree.CommitTree(block++);
                stateRoots.Add(tree.StateRoot);
            }
        }

        i = 0;
        block = 0;
        while (i < numBlocks)
        {
            foreach (Pedersen key in keys)
            {
                byte[]? leaf = archiveStore.GetLeaf(key.BytesAsSpan, stateRoots[(int)block++]);
                leaf.Should().BeEquivalentTo(new UInt256(i++).ToBigEndian());
            }
        }
    }
}
