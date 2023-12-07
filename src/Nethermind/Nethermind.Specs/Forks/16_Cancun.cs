// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Core.Specs;

namespace Nethermind.Specs.Forks
{
    public class Cancun : Shanghai
    {
        private static IReleaseSpec _instance;

        protected Cancun()
        {
            Name = "Cancun";
            // TODO: temp fix for the verkle testnet
            IsEip1153Enabled = false;
            IsEip5656Enabled = false;
            IsEip4844Enabled = false;
            IsEip6780Enabled = false;
        }

        public static new IReleaseSpec Instance => LazyInitializer.EnsureInitialized(ref _instance, () => new Cancun());
    }
}
