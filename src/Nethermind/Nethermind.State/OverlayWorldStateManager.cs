// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.State;

public class OverlayWorldStateManager(
    IReadOnlyDbProvider dbProvider,
    OverlayTrieStore overlayTrieStore,
    ILogManager logManager,
    PreBlockCaches? caches = null)
    : IWorldStateManager
{
    public PreBlockCaches? Caches { get; } = caches;

    private readonly IDb _codeDb = dbProvider.GetDb<IDb>(DbNames.Code);

    private readonly StateReader _reader = new(overlayTrieStore, dbProvider.GetDb<IDb>(DbNames.Code), logManager);

    private readonly WorldState _state = new(overlayTrieStore, dbProvider.GetDb<IDb>(DbNames.Code), logManager);

    public IWorldState GlobalWorldState => _state;

    public IStateReader GlobalStateReader => _reader;

    public IReadOnlyTrieStore TrieStore { get; } = overlayTrieStore.AsReadOnly();

    public IScopedWorldStateManager CreateResettableWorldStateManager()
    {
        WorldState? worldState = Caches is not null
            ? new WorldState(
                new PreCachedTrieStore(overlayTrieStore, Caches.RlpCache),
                _codeDb,
                logManager,
                Caches)
            : new WorldState(
                overlayTrieStore,
                _codeDb,
                logManager);

        return new ScopedReadOnlyWorldStateManager(worldState, dbProvider, overlayTrieStore, logManager, Caches);
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add => overlayTrieStore.ReorgBoundaryReached += value;
        remove => overlayTrieStore.ReorgBoundaryReached -= value;
    }

    public IWorldState GetGlobalWorldState(BlockHeader blockHeader) => GlobalWorldState;
    public bool ClearCache() => Caches.Clear();

    public bool HasStateRoot(Hash256 root) => GlobalStateReader.HasStateForRoot(root);
}
