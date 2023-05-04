// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Pruning
{
    public class NullTrieStore : IReadOnlyTrieStore
    {
        private NullTrieStore() { }

        public static NullTrieStore Instance { get; } = new();

        public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo) { }

        public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root) { }

        public void HackPersistOnShutdown() { }

        public IReadOnlyTrieStore AsReadOnly(IKeyValueStore keyValueStore)
        {
            return this;
        }

        public byte[]? TryLoadRlp(Span<byte> path, IKeyValueStore? keyValueStore)
        {
            throw new NotImplementedException();
        }
        public TrieNodeResolverCapability Capability => TrieNodeResolverCapability.Hash;

        public event EventHandler<ReorgBoundaryReached> ReorgBoundaryReached
        {
            add { }
            remove { }
        }

        public TrieNode FindCachedOrUnknown(Keccak hash)
        {
            return new(NodeType.Unknown, hash);
        }

        public TrieNode FindCachedOrUnknown(Keccak hash, Span<byte> nodePath, Span<byte> storagePrefix)
        {
            return new(NodeType.Unknown, nodePath, hash){StoreNibblePathPrefix = storagePrefix.ToArray()};
        }

        public TrieNode FindCachedOrUnknown(Span<byte> nodePath, Span<byte> storagePrefix, Keccak rootHash)
        {
            return new(NodeType.Unknown, nodePath){StoreNibblePathPrefix = storagePrefix.ToArray()};
        }

        public byte[] LoadRlp(Keccak hash)
        {
            return Array.Empty<byte>();
        }

        public bool IsPersisted(Keccak keccak) => true;

        public void Dispose() { }

        public byte[]? LoadRlp(Span<byte> nodePath, Keccak rootHash)
        {
            return Array.Empty<byte>();
        }

        public void SaveNodeDirectly(long blockNumber, TrieNode trieNode, IKeyValueStore? keyValueStore = null) { }

        public void ClearCache() { }

        public bool ExistsInDB(Keccak hash, byte[] nodePathNibbles) => false;

        public byte[]? this[ReadOnlySpan<byte> key] => null;
    }
}
