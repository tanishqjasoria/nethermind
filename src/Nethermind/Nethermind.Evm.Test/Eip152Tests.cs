// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Specs;
using Nethermind.Evm.Precompiles;
using NUnit.Framework;

namespace Nethermind.Evm.Test
{
    [TestFixture(VirtualMachineTestsStateProvider.MerkleTrie)]
    [TestFixture(VirtualMachineTestsStateProvider.VerkleTrie)]
    public class Eip152Tests : VirtualMachineTestsBase
    {
        private const int InputLength = 213;
        protected override long BlockNumber => MainnetSpecProvider.IstanbulBlockNumber + _blockNumberAdjustment;

        private int _blockNumberAdjustment;

        [TearDown]
        public void TearDown()
        {
            _blockNumberAdjustment = 0;
        }

        [Test]
        public void before_istanbul()
        {
            _blockNumberAdjustment = -1;
            Address precompileAddress = Blake2FPrecompile.Instance.Address;
            Assert.False(precompileAddress.IsPrecompile(Spec));
        }

        [Test]
        public void after_istanbul()
        {
            byte[] code = Prepare.EvmCode
                .CallWithInput(Blake2FPrecompile.Instance.Address, 1000L, new byte[InputLength])
                .Done;
            TestAllTracerWithOutput result = Execute(code);
            Assert.AreEqual(StatusCode.Success, result.StatusCode);
        }
        public Eip152Tests(VirtualMachineTestsStateProvider stateProvider) : base(stateProvider)
        {
        }
    }
}
