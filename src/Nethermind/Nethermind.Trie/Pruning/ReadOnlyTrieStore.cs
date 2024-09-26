// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Reflection.Metadata.Ecma335;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Pruning
{
    /// <summary>
    /// Safe to be reused for the same wrapped store.
    /// </summary>
    public class ReadOnlyTrieStore : IReadOnlyTrieStore
    {
        private readonly TrieStore _trieStore;
        private readonly IKeyValueStore? _readOnlyStore;

        public ReadOnlyTrieStore(TrieStore trieStore, IKeyValueStore? readOnlyStore)
        {
            _trieStore = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
            _readOnlyStore = readOnlyStore;
        }

        public TrieNode FindCachedOrUnknown(Keccak hash) =>
            _trieStore.FindCachedOrUnknown(hash, true);

        public byte[] LoadRlp(Keccak hash) => _trieStore.LoadRlp(hash, _readOnlyStore);

        public bool IsPersisted(Keccak keccak) => _trieStore.IsPersisted(keccak);

        public TrieNodeResolverCapability Capability => TrieNodeResolverCapability.Hash;

        public IReadOnlyTrieStore AsReadOnly(IKeyValueStore keyValueStore)
        {
            return new ReadOnlyTrieStore(_trieStore, keyValueStore);
        }

        public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo) { }

        public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root) { }

        public void HackPersistOnShutdown() { }

        public event EventHandler<ReorgBoundaryReached> ReorgBoundaryReached
        {
            add { }
            remove { }
        }
        public void Dispose() { }

        public TrieNode FindCachedOrUnknown(Span<byte> nodePath)
        {
            throw new NotImplementedException();
        }

        public byte[]? LoadRlp(Span<byte> nodePath, Keccak rootHash)
        {
            throw new NotImplementedException();
        }

        public void SaveNodeDirectly(long blockNumber, TrieNode trieNode)
        {
            throw new NotImplementedException();
        }

        public byte[]? this[byte[] key] => _trieStore[key];
    }
}
