// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Verkle.Tree.History.V2;

public interface ILeafChangeSet
{
    public void InsertDiff(long blockNumber, IDictionary<byte[], byte[]?> leafTable);
    public byte[]? GetLeaf(long blockNumber, ReadOnlySpan<byte> key);
}
