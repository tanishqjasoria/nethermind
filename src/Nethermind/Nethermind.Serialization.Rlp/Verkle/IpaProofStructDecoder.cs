// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Proofs;

namespace Nethermind.Serialization.Rlp.Verkle;

public class IpaProofStructDecoder: IRlpStreamDecoder<IpaProofStruct>, IRlpValueDecoder<IpaProofStruct>
{
    public int GetLength(IpaProofStruct item, RlpBehaviors rlpBehaviors)=>
        Rlp.LengthOfSequence(GetContentLength(item));

    public static int GetContentLength(IpaProofStruct item)
    {
        int contentLength = 0;

        int cl = item.L.Length * 33;
        contentLength += Rlp.LengthOfSequence(cl);

        contentLength += 33;

        int cr = item.R.Length * 33;
        contentLength += Rlp.LengthOfSequence(cr);

        return contentLength;
    }

    public IpaProofStruct Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();

        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] resultL = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < resultL.Length; i++)
        {
            resultL[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }
        FrE fre = FrE.FromBytes(rlpStream.DecodeByteArray());

        checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] resultR = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < resultR.Length; i++)
        {
            resultR[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }

        return new IpaProofStruct(resultL, fre, resultR);
    }

    public void Encode(RlpStream stream, IpaProofStruct item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.StartSequence(GetContentLength(item));
        stream.StartSequence(item.L.Length * 33);
        foreach (Banderwagon point in item.L) stream.Encode(point.ToBytes());
        stream.Encode(item.A.ToBytes());
        stream.StartSequence(item.R.Length * 33);
        foreach (Banderwagon point in item.R) stream.Encode(point.ToBytes());
    }

    public IpaProofStruct Decode(ref Rlp.ValueDecoderContext rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();

        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] resultL = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < resultL.Length; i++)
        {
            resultL[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }
        FrE fre = FrE.FromBytes(rlpStream.DecodeByteArray());

        checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] resultR = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < resultR.Length; i++)
        {
            resultR[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }

        return new IpaProofStruct(resultL, fre, resultR);
    }
}
