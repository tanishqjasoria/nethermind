// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading.Tasks;
using Nethermind.Core.Test.Blockchain;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test.Producers;

public class VerkleBlockProducerTests
{
    [Test]
    public async Task TestBlockProductionWithWitness()
    {
        TestVerkleBlockchain chain = await TestVerkleBlockchain.Create();
        await chain.BuildSomeBlocksWithDeployment(5);
        await chain.BuildSomeBlocks(100);
    }
}
