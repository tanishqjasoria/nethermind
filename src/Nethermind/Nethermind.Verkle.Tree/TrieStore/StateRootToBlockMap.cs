// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Core.Verkle;
using Nethermind.Db;

namespace Nethermind.Verkle.Tree.TrieStore;

public readonly struct StateRootToBlockMap
{
    private readonly IDb _stateRootToBlock;

    public StateRootToBlockMap(IDb stateRootToBlock)
    {
        _stateRootToBlock = stateRootToBlock;
    }

    public long this[VerkleCommitment key]
    {
        get
        {
            // if (Pedersen.Zero.Equals(key)) return -1;
            byte[]? encodedBlock = _stateRootToBlock[key.Bytes];
            return encodedBlock is null ? -2 : BinaryPrimitives.ReadInt64LittleEndian(encodedBlock);
        }
        set
        {
            Span<byte> encodedBlock = stackalloc byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(encodedBlock, value);
            if(!_stateRootToBlock.KeyExists(key.Bytes))
                _stateRootToBlock.Set(key.Bytes, encodedBlock.ToArray());
        }
    }
}
