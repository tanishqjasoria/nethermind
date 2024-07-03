// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Synchronization.VerkleSync;

public interface IVerkleSyncProvider
{
    bool IsFinished(out VerkleSyncBatch? nextBatch);
    bool CanSync();

    AddRangeResult AddSubTreeRange(SubTreeRange request, SubTreesAndProofs response);
    AddRangeResult AddSubTreeRange(long blockNumber, Hash256 expectedRootHash, Stem startingStem, PathWithSubTree[] subTrees, byte[] proofs = null, Stem limitStem = null!);

    void RefreshLeafs(LeafToRefreshRequest request, byte[][] response);

    void RetryRequest(VerkleSyncBatch batch);

    bool IsVerkleGetRangesFinished();
    void UpdatePivot();
}
