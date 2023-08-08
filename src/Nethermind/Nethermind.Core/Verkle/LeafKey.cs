// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethermind.Core.Verkle;

public readonly struct LeafKey:  IEquatable<LeafKey>, IComparable<LeafKey>
{
    public byte[] Bytes { get; }

    private LeafKey(byte[] bytes)
    {
        Bytes = bytes;
    }

    public int CompareTo(LeafKey other)
    {
        return Core.Extensions.Bytes.Comparer.Compare(Bytes, other.Bytes);
    }

    public bool Equals(LeafKey other)
    {
        if (ReferenceEquals(Bytes, other.Bytes))
        {
            return true;
        }

        if (Bytes is null) return other.Bytes is null;

        return other.Bytes is not null && Core.Extensions.Bytes.AreEqual(Bytes, other.Bytes);
    }

    public override bool Equals(object? obj)
    {
        return obj is LeafKey key && Equals(key);
    }

    public override int GetHashCode()
    {
        if (Bytes is null) return 0;

        long v0 = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetArrayDataReference(Bytes));
        long v1 = Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Bytes), sizeof(long)));
        long v2 = Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Bytes), sizeof(long) * 2));
        long v3 = Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Bytes), sizeof(long) * 3));

        v0 ^= v1;
        v2 ^= v3;
        v0 ^= v2;

        return (int)v0 ^ (int)(v0 >> 32);
    }
}
