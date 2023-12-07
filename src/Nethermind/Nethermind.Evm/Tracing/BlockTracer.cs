// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Evm.Tracing;

public abstract class BlockTracer : IBlockTracer
{
    public virtual bool IsTracingRewards => false;
    public bool IsTracingAccessWitness => false;
    public virtual void ReportReward(Address author, string rewardType, UInt256 rewardValue) { }
    public virtual void ReportWithdrawalWitness(VerkleWitness witness) { }
    public virtual void StartNewBlockTrace(Block block) { }
    public abstract ITxTracer StartNewTxTrace(Transaction? tx);
    public virtual void EndTxTrace() { }
    public virtual void EndBlockTrace() { }
}
