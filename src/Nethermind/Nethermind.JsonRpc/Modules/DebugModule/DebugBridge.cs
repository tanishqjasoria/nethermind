﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm.Tracing;
using Nethermind.Network;
using Nethermind.Store;

namespace Nethermind.JsonRpc.Modules.DebugModule
{
    public class DebugBridge : IDebugBridge
    {        
        private readonly ITracer _tracer;
        private Dictionary<string, IDb> _dbMappings;

        public DebugBridge(IReadOnlyDbProvider dbProvider, ITracer tracer, IBlockchainProcessor receiptsProcessor)
        {
            IBlockchainProcessor receiptsProcessor1 = receiptsProcessor ?? throw new ArgumentNullException(nameof(receiptsProcessor));
            receiptsProcessor1.ProcessingQueueEmpty += (sender, args) => _receiptProcessedEvent.Set();
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
            IDb blockInfosDb = dbProvider.BlockInfosDb ?? throw new ArgumentNullException(nameof(dbProvider.BlockInfosDb));
            IDb blocksDb = dbProvider.BlocksDb ?? throw new ArgumentNullException(nameof(dbProvider.BlocksDb));
            IDb receiptsDb = dbProvider.ReceiptsDb ?? throw new ArgumentNullException(nameof(dbProvider.ReceiptsDb));
            IDb codeDb = dbProvider.CodeDb ?? throw new ArgumentNullException(nameof(dbProvider.CodeDb));
            
            _dbMappings = new Dictionary<string, IDb>(StringComparer.InvariantCultureIgnoreCase)
            {
                {DbNames.State, dbProvider.StateDb},
                {DbNames.Storage, dbProvider.StateDb},
                {DbNames.BlockInfos, blockInfosDb},
                {DbNames.Blocks, blocksDb},
                {DbNames.Code, codeDb},
                {DbNames.Receipts, receiptsDb}
            };    
        }
        
        public byte[] GetDbValue(string dbName, byte[] key)
        {
            return _dbMappings[dbName][key];
        }
        
        public GethLikeTxTrace GetTransactionTrace(Keccak transactionHash)
        {
            return _tracer.Trace(transactionHash);
        }

        public GethLikeTxTrace GetTransactionTrace(UInt256 blockNumber, int index)
        {
            return _tracer.Trace(blockNumber, index);
        }

        public GethLikeTxTrace GetTransactionTrace(Keccak blockHash, int index)
        {
            return _tracer.Trace(blockHash, index);
        }

        public GethLikeTxTrace[] GetBlockTrace(Keccak blockHash)
        {
            return _tracer.TraceBlock(blockHash);
        }

        public GethLikeTxTrace[] GetBlockTrace(UInt256 blockNumber)
        {
            return _tracer.TraceBlock(blockNumber);
        }
        
        public GethLikeTxTrace[] GetBlockTrace(Rlp blockRlp)
        {
            return _tracer.TraceBlock(blockRlp);
        }
        
        private AutoResetEvent _receiptProcessedEvent = new AutoResetEvent(false);
       
    }
}