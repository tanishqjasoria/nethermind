// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Verkle;
using Nethermind.Verkle.Curve;

namespace Nethermind.Serialization.Rlp.Verkle;

public class WitnessVerkleProofDecoder: IRlpStreamDecoder<WitnessVerkleProof>, IRlpValueDecoder<WitnessVerkleProof>
{
    private static readonly IpaProofStructDecoder _ipaDecoder = new IpaProofStructDecoder();
    public int GetLength(WitnessVerkleProof item, RlpBehaviors rlpBehaviors) =>
        Rlp.LengthOfSequence(GetContentLength(item));

    public static int GetContentLength(WitnessVerkleProof item)
    {
        int contentLength = 0;

        int stemContentLength = item.OtherStems?.Length * 32 ?? 0;
        contentLength += Rlp.LengthOfSequence(stemContentLength);

        contentLength += Rlp.LengthOf(item.DepthExtensionPresent);

        int commitmentsLength = item.CommitmentsByPath.Length * 33;
        contentLength += Rlp.LengthOfSequence(commitmentsLength);

        contentLength += 33;

        contentLength += _ipaDecoder.GetLength(item.IpaProof, RlpBehaviors.None);

        return contentLength;
    }

    public WitnessVerkleProof Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        int otherStem = rlpStream.PeekNumberOfItemsRemaining(checkPosition);
        Stem[]? stems;
        if (otherStem > 0)
        {
            stems = new Stem[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
            for (int i = 0; i < stems.Length; i++)
            {
                stems[i] = new Stem(rlpStream.DecodeByteArray());
            }
        }
        else
        {
            stems = null;
        }

        var depth = rlpStream.DecodeByteArray();

        checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] commitments = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < commitments.Length; i++)
        {
            commitments[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }

        Banderwagon d = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;

        var proof = _ipaDecoder.Decode(rlpStream);

        return new WitnessVerkleProof(stems, depth, commitments, d, proof);
    }

    public void Encode(RlpStream stream, WitnessVerkleProof item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.StartSequence(GetContentLength(item));
        int stemContentLength = item.OtherStems?.Length * 32 ?? 0;
        stream.StartSequence(stemContentLength);
        if (item.OtherStems != null)
        {
            foreach (Stem? stem in item.OtherStems)
            {
                stream.Encode(stem.Bytes);
            }
        }

        stream.Encode(item.DepthExtensionPresent);

        int commitmentsLength = item.CommitmentsByPath.Length * 33;
        stream.StartSequence(commitmentsLength);
        foreach (Banderwagon commit in item.CommitmentsByPath)
        {
            stream.Encode(commit.ToBytes());
        }

        stream.Encode(item.D.ToBytes());

        _ipaDecoder.Encode(stream, item.IpaProof);
    }

    public WitnessVerkleProof Decode(ref Rlp.ValueDecoderContext rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        int otherStem = rlpStream.PeekNumberOfItemsRemaining(checkPosition);
        Stem[]? stems;
        if (otherStem > 0)
        {
            stems = new Stem[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
            for (int i = 0; i < stems.Length; i++)
            {
                stems[i] = new Stem(rlpStream.DecodeByteArray());
            }
        }
        else
        {
            stems = null;
        }

        var depth = rlpStream.DecodeByteArray();

        checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        Banderwagon[] commitments = new Banderwagon[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < commitments.Length; i++)
        {
            commitments[i] = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;
        }

        Banderwagon d = Banderwagon.FromBytes(rlpStream.DecodeByteArray(), subgroupCheck: false).Value;

        var proof = _ipaDecoder.Decode(ref rlpStream);

        return new WitnessVerkleProof(stems, depth, commitments, d, proof);
    }
}
