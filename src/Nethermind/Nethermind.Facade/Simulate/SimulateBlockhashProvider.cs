// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.State;

namespace Nethermind.Facade.Simulate;

public sealed class SimulateBlockhashProvider(IBlockhashProvider blockhashProvider, IBlockTree blockTree)
    : IBlockhashProvider
{
    public Hash256? GetBlockhash(BlockHeader currentBlock, IWorldState worldState, in long number)
    {
        long bestKnown = blockTree.BestKnownNumber;
        return bestKnown < number && blockTree.BestSuggestedHeader is not null
            ? blockhashProvider.GetBlockhash(blockTree.BestSuggestedHeader!, worldState, in bestKnown)
            : blockhashProvider.GetBlockhash(currentBlock, worldState, in number);
    }
}
