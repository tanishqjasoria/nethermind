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
using FluentAssertions;
using NUnit.Framework;
using System.Linq;

namespace Nethermind.Trie.Test;

[TestFixture, Parallelizable(ParallelScope.All)]
public class RustVerkleLibTest
{

    private readonly byte[] pedersenValue1 =
        StringToByteArray(
            "00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
    private readonly byte[] pedersenValue2 =
        StringToByteArray(
            "00020300000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
    private readonly byte[] pedersenValue3 =
        StringToByteArray(
            "0071562b71999873db5b286df957af199ec946170000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

    private readonly byte[] pedersenHash1 =
        StringToByteArray(
            "695921dca3b16c5cc850e94cdd63f573c467669e89cec88935d03474d6bdf9d4");
    private readonly byte[] pedersenHash2 =
        StringToByteArray(
            "5010fabfb319bf84136db68445972cdd5476ff2fbf3e5133330b3946b84b4e6a");
    private readonly byte[] pedersenHash3 =
        StringToByteArray(
            "6fc5ac021ff2468685885ad7fdb31a0c58d1ee93254a58c9e9e0809187c53e71");

    public static byte[] StringToByteArray(string hex) {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }

    [Test]
    public void TestPedersenHash()
    {
        RustVerkleLib.CalculatePedersenHash(pedersenValue1).Should().BeEquivalentTo(pedersenHash1);
        RustVerkleLib.CalculatePedersenHash(pedersenValue2).Should().BeEquivalentTo(pedersenHash2);
        RustVerkleLib.CalculatePedersenHash(pedersenValue3).Should().BeEquivalentTo(pedersenHash3);
    }
}
