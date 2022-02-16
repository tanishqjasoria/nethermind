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
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Metrics = Nethermind.Db.Metrics;


namespace Nethermind.State;

public class VerkleStateReader: IStateReader
{
    private readonly IDb _codeDb;
    private readonly ILogger _logger;
    private readonly VerkleStateTree _state;
    
    public VerkleStateReader(VerkleStateTree state, IDb? codeDb, ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<StateReader>() ?? throw new ArgumentNullException(nameof(logManager));
        _codeDb = codeDb ?? throw new ArgumentNullException(nameof(codeDb));
        _state = state;
    }
    
    public Account? GetAccount(Keccak stateRoot, Address address)
    {
        return GetState(stateRoot, address);
    }

    public byte[] GetStorage(Address address, in UInt256 index)
    {
        Metrics.StorageTreeReads++;
        return _state.GetStorageValue(new StorageCell(address, index));
    }
    
    public byte[] GetStorage(Keccak storageRoot, in UInt256 index)
    {
        if (storageRoot != Keccak.Zero)
        {
            throw new InvalidOperationException("verkle tree does not support storage root");
        }

        return new byte[32];

    }

    public UInt256 GetBalance(Keccak stateRoot, Address address)
    {
        return GetState(stateRoot, address)?.Balance ?? UInt256.Zero;
    }
    
    public byte[]? GetCode(Keccak codeHash)
    {
        if (codeHash == Keccak.OfAnEmptyString)
        {
            return Array.Empty<byte>();
        }

        return _codeDb[codeHash.Bytes];
    }
    
    public byte[] GetCode(Keccak stateRoot, Address address)
    {
        Account? account = GetState(stateRoot, address);
        return account is null ? Array.Empty<byte>() : GetCode(account.CodeHash);
    }
    
    public void RunTreeVisitor(ITreeVisitor treeVisitor, Keccak rootHash)
    {
        _state.Accept(treeVisitor, rootHash, treeVisitor.GetSupportedOptions());
    }

    
    private Account? GetState(Keccak stateRoot, Address address)
    {
        if (stateRoot == Keccak.EmptyTreeHash)
        {
            return null;
        }

        Metrics.StateTreeReads++;
        Account? account = _state.Get(address, stateRoot);
        return account;
    }

}
