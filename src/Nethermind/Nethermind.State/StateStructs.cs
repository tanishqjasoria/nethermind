// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Db;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieStore;

namespace Nethermind.State;

public ref struct StateFactory
{
    public IDbProvider? DbProvider { get; set; }

    public IWorldState VerkleWorldState { get; set; }
    public IWorldState MerkleWorldState { get; set; }

    public IStateReader? MerkleStateReader { get; set; }
    public IStateReader? VerkleStateReader { get; set; }

    public IReadOnlyStateProvider? ChainHeadStateProvider { get; set; }

    public VerkleStateStore? VerkleTrieStore { get; set; }
    public ReadOnlyVerkleStateStore? ReadOnlyVerkleTrieStore { get; set; }
    public VerkleArchiveStore? VerkleArchiveStore { get; set; }

    public IWorldState? WorldState { get; set; }


    public ITrieStore? TrieStore { get; set; }
    public IReadOnlyTrieStore? ReadOnlyTrieStore { get; set; }




}
