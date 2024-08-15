// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Abi;
using Nethermind.Blockchain.Contracts;
using Nethermind.Core;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Merge.AuRa.Contracts;

/// <summary>
/// Represents the smart contract for withdrawals as defined in the
/// <see href="https://github.com/gnosischain/specs/blob/master/execution/withdrawals.md#specification">specification</see>
/// of the Gnosis Chain withdrawals.
/// </summary>
public class WithdrawalContract : CallableContract, IWithdrawalContract
{
    private const long GasLimit = 30_000_000L;

    public WithdrawalContract(
        ITransactionProcessor transactionProcessor,
        IAbiEncoder abiEncoder,
        Address contractAddress)
        : base(transactionProcessor, abiEncoder, contractAddress) { }

    public void ExecuteWithdrawals(IWorldState worldState, BlockHeader blockHeader, UInt256 failedMaxCount,
        IList<ulong> amounts, IList<Address> addresses)
    {
        ArgumentNullException.ThrowIfNull(blockHeader);
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(addresses);

        Call(blockHeader, (string)(string)"executeSystemWithdrawals", Address.SystemUser, GasLimit, worldState, failedMaxCount, amounts, addresses);
    }
}
