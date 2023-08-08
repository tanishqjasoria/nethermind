// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Verkle;

namespace Nethermind.Serialization.Rlp.Verkle;

public class StemStateDiffDecoder: IRlpStreamDecoder<StemStateDiff>, IRlpValueDecoder<StemStateDiff>
{
    private static readonly SuffixStateDiffDecoder _suffixStateDiffDecoder = SuffixStateDiffDecoder.Instance;
    public int GetLength(StemStateDiff item, RlpBehaviors rlpBehaviors)=>
        Rlp.LengthOfSequence(GetContentLength(item).contentLength);

    public static (int contentLength, int suffixDiffContentLength)  GetContentLength(StemStateDiff item)
    {
        int contentLength = 0;
        contentLength += Rlp.LengthOf(item.Stem.Bytes);

        int suffixDiffContentLength = item.SuffixDiffs.Sum(suffixDiff =>
            _suffixStateDiffDecoder.GetLength(suffixDiff, RlpBehaviors.None));

        contentLength += Rlp.LengthOfSequence(suffixDiffContentLength);
        return (contentLength, suffixDiffContentLength);
    }

    public StemStateDiff Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        Stem stem = new Stem(rlpStream.DecodeByteArray());
        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        SuffixStateDiff[] result = new SuffixStateDiff[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = _suffixStateDiffDecoder.Decode(rlpStream, rlpBehaviors);
        }

        return new StemStateDiff { Stem = stem, SuffixDiffs = new List<SuffixStateDiff>(result) };
    }

    public void Encode(RlpStream stream, StemStateDiff item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        (int contentLength, int suffixDiffContentLength) = GetContentLength(item);
        stream.StartSequence(contentLength);
        stream.Encode(item.Stem.Bytes);

        stream.StartSequence(suffixDiffContentLength);
        foreach (SuffixStateDiff suffixDiff in item.SuffixDiffs)
        {
              _suffixStateDiffDecoder.Encode(stream, suffixDiff);
        }
    }

    public StemStateDiff Decode(ref Rlp.ValueDecoderContext rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        Stem stem = new Stem(rlpStream.DecodeByteArray());
        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        SuffixStateDiff[] result = new SuffixStateDiff[rlpStream.PeekNumberOfItemsRemaining(checkPosition)];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = _suffixStateDiffDecoder.Decode(ref rlpStream, rlpBehaviors);
        }

        return new StemStateDiff { Stem = stem, SuffixDiffs = new List<SuffixStateDiff>(result) };
    }
}
