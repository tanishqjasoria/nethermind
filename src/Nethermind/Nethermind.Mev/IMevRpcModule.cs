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

using System.Threading.Tasks;
using Nethermind.Blockchain.Find;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Data;
using Nethermind.Mev.Data;

namespace Nethermind.Mev
{
    [RpcModule(ModuleType.Mev)]
    public interface IMevRpcModule : IRpcModule
    {        
        [JsonRpcMethod(Description = "Adds bundle to the tx pool.", IsImplemented = true)]
        ResultWrapper<bool> eth_sendBundle(MevBundleRpc mevBundleRpc);
        
        [JsonRpcMethod(Description = "Simulates the bundle behaviour.", IsImplemented = true)]
        ResultWrapper<TxsResults> eth_callBundle(MevCallBundleRpc mevBundleRpc);

        [JsonRpcMethod(
            Description = "Publishes the MEV bundle using a carrier transaction encrypted for the specified validator.",
            IsImplemented = true)]
        Task<ResultWrapper<Keccak>> eth_publishBundle(
            PublicKey targetValidator,
            TransactionForRpc carrier,
            TransactionForRpc[] bundle);
    }
}
