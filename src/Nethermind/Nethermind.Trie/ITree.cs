// Copyright 2022 Demerzel Solutions Limited
// Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.

using System;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie;
public interface ITree
{
    TreeType TreeType { get; }
    Keccak RootHash { get; }
    void Commit(long blockNumber);
    void UpdateRootHash();
    byte[]? Get(Span<byte> rawKey, Keccak? rootHash = null);
    void Set(Span<byte> rawKey, byte[] value);
    void Set(Span<byte> rawKey, Rlp? value);
    void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null);
}

public enum TreeType
{
    Merkle,
    Verkle
}
