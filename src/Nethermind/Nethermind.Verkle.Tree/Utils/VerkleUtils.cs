using System.Numerics;
using Nethermind.Int256;
using Nethermind.Verkle.Fields.FrEElement;

namespace Nethermind.Verkle.Tree.Utils;

public static class VerkleUtils
{
    private static FrE ValueExistsMarker { get; } = FrE.SetElement(BigInteger.Pow(2, 128));

    public static (FrE, FrE) BreakValueInLowHigh(byte[]? value)
    {
        if (value is null) return (FrE.Zero, FrE.Zero);
        if (value.Length != 32) throw new ArgumentException();
        UInt256 valueFr = new(value);
        FrE lowFr = FrE.SetElement(valueFr.u0, valueFr.u1) + ValueExistsMarker;
        FrE highFr = FrE.SetElement(valueFr.u2, valueFr.u3);
        return (lowFr, highFr);
    }

    public static (List<byte>, byte?, byte?) GetPathDifference(IEnumerable<byte> existingNodeKey, IEnumerable<byte> newNodeKey)
    {
        List<byte> samePathIndices = new List<byte>();
        foreach ((byte first, byte second) in existingNodeKey.Zip(newNodeKey))
        {
            if (first != second) return (samePathIndices, first, second);
            samePathIndices.Add(first);
        }
        return (samePathIndices, null, null);
    }
}
