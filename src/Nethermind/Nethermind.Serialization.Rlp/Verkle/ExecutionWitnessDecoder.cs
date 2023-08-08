// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Verkle;

namespace Nethermind.Serialization.Rlp.Verkle;

public class ExecutionWitnessDecoder: IRlpStreamDecoder<ExecutionWitness>, IRlpValueDecoder<ExecutionWitness>
{
    private static readonly WitnessVerkleProofDecoder _proofDecoder = new WitnessVerkleProofDecoder();
    private static readonly StemStateDiffDecoder _diffDecoder = new StemStateDiffDecoder();
    public int GetLength(ExecutionWitness item, RlpBehaviors rlpBehaviors)=>
        Rlp.LengthOfSequence(GetContentLength(item).contentLength);

    public static (int contentLength, int diffLength) GetContentLength(ExecutionWitness item)
    {
        int contentLength = 0;

        int diffLength = item.StateDiff.Sum(diff => _diffDecoder.GetLength(diff, RlpBehaviors.None));
        contentLength += Rlp.LengthOfSequence(diffLength);

        contentLength += _proofDecoder.GetLength(item.VerkleProof, RlpBehaviors.None);

        return (contentLength, diffLength);
    }

    public ExecutionWitness Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();

        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        var resultLength = rlpStream.PeekNumberOfItemsRemaining(checkPosition);
        List<StemStateDiff> result = new (resultLength);
        for (int i = 0; i < resultLength; i++)
        {
            result.Add(_diffDecoder.Decode(rlpStream, rlpBehaviors));
        }

        var proof = _proofDecoder.Decode(rlpStream);

        return new ExecutionWitness(result, proof);
    }

    public void Encode(RlpStream stream, ExecutionWitness item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        (int contentLength, int diffLength) = GetContentLength(item);
        stream.StartSequence(contentLength);
        stream.StartSequence(diffLength);
        foreach (var diff in item.StateDiff)
        {
            _diffDecoder.Encode(stream, diff);
        }

        _proofDecoder.Encode(stream, item.VerkleProof);
    }

    public ExecutionWitness Decode(ref Rlp.ValueDecoderContext rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();

        int checkPosition = rlpStream.ReadSequenceLength() + rlpStream.Position;
        var resultLength = rlpStream.PeekNumberOfItemsRemaining(checkPosition);
        List<StemStateDiff> result = new (resultLength);
        for (int i = 0; i < resultLength; i++)
        {
            result.Add(_diffDecoder.Decode(ref rlpStream, rlpBehaviors));
        }

        var proof = _proofDecoder.Decode(ref rlpStream);

        return new ExecutionWitness(result, proof);
    }
}
