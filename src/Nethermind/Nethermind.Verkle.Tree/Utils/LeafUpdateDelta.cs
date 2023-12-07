// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;

namespace Nethermind.Verkle.Tree.Utils;

public struct LeafUpdateDelta
{
    public Banderwagon? DeltaC1 { get; private set; }
    public Banderwagon? DeltaC2 { get; private set; }

    public LeafUpdateDelta()
    {
        DeltaC1 = null;
        DeltaC2 = null;
    }

    public void UpdateDelta(Banderwagon deltaLeafCommitment, byte index)
    {
        if (index < 128)
        {
            if (DeltaC1 is null) DeltaC1 = deltaLeafCommitment;
            else DeltaC1 += deltaLeafCommitment;
        }
        else
        {
            if (DeltaC2 is null) DeltaC2 = deltaLeafCommitment;
            else DeltaC2 += deltaLeafCommitment;
        }
    }
}
