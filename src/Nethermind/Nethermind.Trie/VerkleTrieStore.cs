// // Copyright 2022 Demerzel Solutions Limited
// // Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.
//
// using System;
// using System.Runtime.InteropServices;
// using Nethermind.Core;
// using Nethermind.Core.Crypto;
// using Nethermind.Core.Extensions;
// using Nethermind.Logging;
// using Nethermind.Trie.Pruning;
//
// namespace Nethermind.Trie;
//
// public interface IVerkleTrieStore : ITrieStore
// {
//
//     void FinishBlockCommit(TrieType trieType, long blockNumber);
//     IVerkleReadOnlyVerkleTrieStore AsReadOnly();
//
//     public RustVerkle CreateTrie(CommitScheme commitScheme);
//     void ClearTempChanges();
// }
//
// public interface IVerkleReadOnlyVerkleTrieStore : IVerkleTrieStore, IReadOnlyTrieStore
// {
//     void ClearTempChanges();
// }
//
// public class VerkleTrieStore: IVerkleTrieStore
// {
//     public readonly RustVerkleDb _verkleDb;
//     private readonly ILogger _logger;
//
//     public VerkleTrieStore(DatabaseScheme databaseScheme, ILogManager? logManager, string pathname = "./db/verkle_db")
//     {
//         _verkleDb = RustVerkleLib.VerkleDbNew(databaseScheme, pathname);
//         _logger = logManager?.GetClassLogger<VerkleTrieStore>() ?? throw new ArgumentNullException(nameof(logManager));
//     }
//
//     public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo)
//     {
//
//     }
//
//     public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root)
//     {
//         throw new NotImplementedException();
//     }
//
//     public bool IsPersisted(Keccak keccak)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void FinishBlockCommit(TrieType trieType, long blockNumber)
//     {
//         // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
//     }
//
//     public void HackPersistOnShutdown()
//     {
//         // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
//     }
//
//     public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore) => AsReadOnly();
//
//     public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
//     {
//         add { }
//         remove { }
//     }
//
//     public IVerkleReadOnlyVerkleTrieStore AsReadOnly()
//     {
//         RustVerkleDb roDb = RustVerkleLib.VerkleTrieGetReadOnlyDb(_verkleDb);
//         return new ReadOnlyVerkleTrieStore(roDb);
//     }
//
//     public void Dispose()
//     {
//         if (_logger.IsDebug) _logger.Debug("Disposing trie");
//         // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
//     }
//
//     public RustVerkle CreateTrie(CommitScheme commitScheme)
//     {
//         return RustVerkleLib.VerkleTrieNewFromDb(_verkleDb, commitScheme);
//     }
//
//     public void ClearTempChanges()
//     {
//     }
//
//     public TrieNode FindCachedOrUnknown(Keccak hash)
//     {
//         throw new NotImplementedException();
//     }
//
//     public byte[]? LoadRlp(Keccak hash)
//     {
//         throw new NotImplementedException();
//     }
// }
//
// public class ReadOnlyVerkleTrieStore: IVerkleReadOnlyVerkleTrieStore
// {
//     public readonly RustVerkleDb _verkleDb;
//     private readonly ILogger _logger;
//
//     public ReadOnlyVerkleTrieStore(DatabaseScheme databaseScheme, string pathname)
//     {
//         _verkleDb = RustVerkleLib.VerkleDbNew(databaseScheme, pathname);
//         _logger = SimpleConsoleLogger.Instance;
//     }
//
//     public ReadOnlyVerkleTrieStore(RustVerkleDb db)
//     {
//         _verkleDb = db;
//         _logger = SimpleConsoleLogger.Instance;
//     }
//
//     public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo) { }
//     public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root)
//     {
//         throw new NotImplementedException();
//     }
//
//     public bool IsPersisted(Keccak keccak)
//     {
//         throw new NotImplementedException();
//     }
//
//     public void FinishBlockCommit(TrieType trieType, long blockNumber) { }
//
//     public void HackPersistOnShutdown() { }
//     public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore) => AsReadOnly();
//
//     public event EventHandler<ReorgBoundaryReached> ReorgBoundaryReached
//     {
//         add { }
//         remove { }
//     }
//
//     public IVerkleReadOnlyVerkleTrieStore AsReadOnly()
//     {
//         return new ReadOnlyVerkleTrieStore(_verkleDb);
//     }
//
//     public RustVerkle CreateTrie(CommitScheme commitScheme)
//     {
//         return RustVerkleLib.VerkleTrieNewFromDb(_verkleDb, commitScheme);
//     }
//
//     public void Dispose()
//     {
//         // RustVerkleLib.VerkleTrieClear(_verkleTrie);
//     }
//
//
//     public void ClearTempChanges()
//     {
//         RustVerkleLib.VerkleTrieClearTempChanges(_verkleDb);
//     }
//
//     public TrieNode FindCachedOrUnknown(Keccak hash)
//     {
//         throw new NotImplementedException();
//     }
//
//     public byte[]? LoadRlp(Keccak hash)
//     {
//         throw new NotImplementedException();
//     }
// }
