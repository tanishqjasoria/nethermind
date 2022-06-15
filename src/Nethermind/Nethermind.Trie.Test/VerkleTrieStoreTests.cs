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
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test;
using Nethermind.Logging;
using NUnit.Framework;

namespace Nethermind.Trie.Test;

[TestFixture, Parallelizable(ParallelScope.All)]
public class VerkleTrieStoreTests
{
    [Test]
    public void TestVerkleTrieCreate()
    {
        var NUM = 10000;
        byte[] one = 
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1
        };
        byte[] one32 = 
        {
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
        };

        string tempDir = Path.GetTempPath();
        string dbname = "VerkleTrie_TestID_" + TestContext.CurrentContext.Test.ID;
        string pathname = Path.Combine(tempDir, dbname);

        VerkleTree[] arr = new VerkleTree[NUM];
        Task[] TaskArr = new Task[NUM];

        VerkleTrieStore ts1 = new(DatabaseScheme.RocksDb, NullLogManager.Instance, pathname);
        VerkleTree vt1 = new (ts1);
        
        for (int i = 0; i < NUM; i++)
        {
            arr[i] = new (ts1.AsReadOnly());
        }
        
        vt1.SetValue(one, one32);
        vt1.UpdateRootHash();
        Keccak rootHash = vt1.RootHash;
        vt1.Commit(1);
        vt1.UpdateRootHash();
        
        for (int i = 0; i < NUM; i++)
        {
            TaskArr[i] =Task.Run(() =>
            {
                arr[i].UpdateRootHash();
                arr[i].GetValue(one).Should().Equal(one32);
                arr[i].RootHash.Should().BeEquivalentTo(rootHash);
            });
        }
    }
}
