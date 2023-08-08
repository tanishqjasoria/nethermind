// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Verkle;

namespace Nethermind.Serialization.Rlp.Verkle;

public class SuffixStateDiffDecoder: IRlpStreamDecoder<SuffixStateDiff>, IRlpValueDecoder<SuffixStateDiff>
{
    public static SuffixStateDiffDecoder Instance => new SuffixStateDiffDecoder();

    public int GetLength(SuffixStateDiff item, RlpBehaviors rlpBehaviors) =>
        Rlp.LengthOfSequence(GetContentLength(item));

    public static int GetContentLength(SuffixStateDiff item)
    {
        return Rlp.LengthOf(item.Suffix) + Rlp.LengthOf(item.CurrentValue) + Rlp.LengthOf(item.NewValue);
    }

    public SuffixStateDiff Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        return new SuffixStateDiff()
        {
            Suffix = rlpStream.DecodeByte(),
            CurrentValue = rlpStream.DecodeByteArray(),
            NewValue = rlpStream.DecodeByteArray()
        };
    }

    public void Encode(RlpStream stream, SuffixStateDiff item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.StartSequence(GetContentLength(item));
        stream.Encode(item.Suffix);
        stream.Encode(item.CurrentValue);
        stream.Encode(item.NewValue);
    }

    public SuffixStateDiff Decode(ref Rlp.ValueDecoderContext rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        return new SuffixStateDiff()
        {
            Suffix = rlpStream.DecodeByte(),
            CurrentValue = rlpStream.DecodeByteArray(),
            NewValue = rlpStream.DecodeByteArray()
        };
    }
}
