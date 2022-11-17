// Copyright 2022 Demerzel Solutions Limited
// Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.

using System;
using System.Collections.Generic;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Tree;

namespace Nethermind.Trie;


public struct MemoryDb
{
    public readonly Dictionary<byte[], byte[]> LeafTable { get; }
    public readonly Dictionary<byte[], SuffixTree> StemTable { get; }
    public readonly Dictionary<byte[], InternalNode> BranchTable { get; }

    public MemoryDb()
    {
        LeafTable = new Dictionary<byte[], byte[]>(Bytes.EqualityComparer);
        StemTable = new Dictionary<byte[], SuffixTree>(Bytes.EqualityComparer);
        BranchTable = new Dictionary<byte[], InternalNode>(Bytes.EqualityComparer);
    }
}



public class VerkleTree : ITree
{
    private readonly MemoryDb _db;

    public TreeType TreeType => TreeType.Verkle;

    public Keccak RootHash => new Keccak(_db.BranchTable[Array.Empty<byte>()]._internalCommitment.PointAsField.ToBytes());

    public VerkleTree()
    {
        _db = new MemoryDb
        {
            BranchTable =
            {
                [Array.Empty<byte>()] = new BranchNode()
            }
        };
    }


    public void Commit(long blockNumber)
    {
        throw new NotImplementedException();
    }
    public void UpdateRootHash()
    {
        throw new NotImplementedException();
    }
    public byte[]? Get(Span<byte> rawKey, Keccak? rootHash = null)
    {
        throw new NotImplementedException();
    }
    public void Set(Span<byte> rawKey, byte[] value)
    {
        throw new NotImplementedException();
    }
    public void Set(Span<byte> rawKey, Rlp? value)
    {
        throw new NotImplementedException();
    }
    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null)
    {
        throw new NotImplementedException();
    }
}
