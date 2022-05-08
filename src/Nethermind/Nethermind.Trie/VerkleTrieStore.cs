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
using System.Runtime.InteropServices;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;

namespace Nethermind.Trie;

public interface IVerkleTrieStore : ITrieStore
{
    
    void FinishBlockCommit(TrieType trieType, long blockNumber);
    IVerkleReadOnlyVerkleTrieStore AsReadOnly();

    public RustVerkle CreateTrie(CommitScheme commitScheme);
}

public interface IVerkleReadOnlyVerkleTrieStore : IVerkleTrieStore, IReadOnlyTrieStore
{
    void ClearTempChanges();
}

public class VerkleTrieStore: IVerkleTrieStore
{
    public readonly RustVerkleDb _verkleDb;
    private readonly ILogger _logger;
    
    public VerkleTrieStore(DatabaseScheme databaseScheme, ILogManager? logManager)
    {
        _verkleDb = RustVerkleLib.VerkleDbNew(databaseScheme);
        _logger = logManager?.GetClassLogger<VerkleTrieStore>() ?? throw new ArgumentNullException(nameof(logManager));
    }

    public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo)
    {
        
    }

    public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root)
    {
        throw new NotImplementedException();
    }

    public void FinishBlockCommit(TrieType trieType, long blockNumber)
    {
        // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
    }

    public void HackPersistOnShutdown()
    {
        // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
    }

    public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore) => AsReadOnly();

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add { }
        remove { }
    }

    public IVerkleReadOnlyVerkleTrieStore AsReadOnly()
    {
        RustVerkleDb roDb = RustVerkleLib.VerkleTrieGetReadOnlyDb(_verkleDb);
        return new ReadOnlyVerkleTrieStore(roDb);
    }
    
    public void Dispose()
    {
        if (_logger.IsDebug) _logger.Debug("Disposing trie");
        // RustVerkleLib.VerkleTrieFlush(_verkleTrie);
    }

    public RustVerkle CreateTrie(CommitScheme commitScheme)
    {
        return RustVerkleLib.VerkleTrieNewFromDb(_verkleDb, commitScheme);
    }

    public TrieNode FindCachedOrUnknown(Keccak hash)
    {
        throw new NotImplementedException();
    }

    public byte[]? LoadRlp(Keccak hash)
    {
        throw new NotImplementedException();
    }
}

public class ReadOnlyVerkleTrieStore: IVerkleReadOnlyVerkleTrieStore
{
    public readonly RustVerkleDb _verkleDb;
    private readonly ILogger _logger;
    
    public ReadOnlyVerkleTrieStore(DatabaseScheme databaseScheme, CommitScheme commitScheme)
    {
        _verkleDb = RustVerkleLib.VerkleDbNew(databaseScheme);
        _logger = SimpleConsoleLogger.Instance;
    }

    public ReadOnlyVerkleTrieStore(RustVerkleDb db)
    {
        _verkleDb = db;
        _logger = SimpleConsoleLogger.Instance;
    }

    public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo) { }
    public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root)
    {
        throw new NotImplementedException();
    }

    public void FinishBlockCommit(TrieType trieType, long blockNumber) { }

    public void HackPersistOnShutdown() { }
    public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore) => AsReadOnly();

    public event EventHandler<ReorgBoundaryReached> ReorgBoundaryReached
    {
        add { }
        remove { }
    }

    public IVerkleReadOnlyVerkleTrieStore AsReadOnly()
    {
        return new ReadOnlyVerkleTrieStore(_verkleDb);
    }
    
    public RustVerkle CreateTrie(CommitScheme commitScheme)
    {
        return RustVerkleLib.VerkleTrieNewFromDb(_verkleDb, commitScheme);
    }

    public void Dispose()
    {
        // RustVerkleLib.VerkleTrieClear(_verkleTrie);
    }
    
    
    public void ClearTempChanges()
    {
        RustVerkleLib.VerkleTrieClearTempChanges(_verkleDb);
    }

    public TrieNode FindCachedOrUnknown(Keccak hash)
    {
        throw new NotImplementedException();
    }

    public byte[]? LoadRlp(Keccak hash)
    {
        throw new NotImplementedException();
    }
}
