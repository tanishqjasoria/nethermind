// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Logging;
using Nethermind.Network.Contract.P2P;
using Nethermind.Network.P2P.EventArg;
using Nethermind.Network.P2P.ProtocolHandlers;
using Nethermind.Network.P2P.Subprotocols.Verkle.Messages;
using Nethermind.Network.Rlpx;
using Nethermind.Stats;
using Nethermind.Stats.Model;
using Nethermind.Verkle.Tree.Sync;

namespace Nethermind.Network.P2P.Subprotocols.Verkle;

public class VerkleProtocolHandler: ZeroProtocolHandlerBase, IVerkleSyncPeer
{
    private const int MaxBytesLimit = 2_000_000;
    private const int MinBytesLimit = 20_000;

    public static readonly TimeSpan UpperLatencyThreshold = TimeSpan.FromMilliseconds(2000);
    public static readonly TimeSpan LowerLatencyThreshold = TimeSpan.FromMilliseconds(1000);
    private const double BytesLimitAdjustmentFactor = 2;

    public override string Name => "verkle1";

    protected override TimeSpan InitTimeout => Timeouts.Eth;

    public override byte ProtocolVersion => 1;
    public override string ProtocolCode => Protocol.Verkle;
    public override int MessageIdSpaceSize => 8;

    private const string DisconnectMessage = "Serving verkle sync data in not implemented in this node.";

    private readonly MessageQueue<GetSubTreeRangeMessage, SubTreeRangeMessage> _getSubTreeRangeRequests;
    private readonly MessageQueue<GetLeafNodesMessage, LeafNodesMessage> _getLeafNodesRequests;
    private static readonly byte[] _emptyBytes = { 0 };

    private int _currentBytesLimit = MinBytesLimit;

    public VerkleProtocolHandler(ISession session,
        INodeStatsManager nodeStats,
        IMessageSerializationService serializer,
        ILogManager logManager): base(session, nodeStats, serializer, logManager)
    {
        _getLeafNodesRequests = new(Send);
        _getSubTreeRangeRequests = new(Send);
    }

    public override event EventHandler<ProtocolInitializedEventArgs> ProtocolInitialized;
    public override event EventHandler<ProtocolEventArgs>? SubprotocolRequested
    {
        add { }
        remove { }
    }

    public override void Init()
    {
        ProtocolInitialized?.Invoke(this, new ProtocolInitializedEventArgs(this));
    }

    public override void Dispose()
    {
    }

    public override void HandleMessage(ZeroPacket message)
    {
        int size = message.Content.ReadableBytes;

        switch (message.PacketType)
        {
            case VerkleMessageCode.GetSubTreeRange:
                GetSubTreeRangeMessage getSubTreeRangeMessage = Deserialize<GetSubTreeRangeMessage>(message.Content);
                ReportIn(getSubTreeRangeMessage, size);
                Handle(getSubTreeRangeMessage);
                break;
            case VerkleMessageCode.SubTreeRange:
                SubTreeRangeMessage subTreeRangeMessage = Deserialize<SubTreeRangeMessage>(message.Content);
                ReportIn(subTreeRangeMessage, size);
                Handle(subTreeRangeMessage, size);
                break;
            case VerkleMessageCode.GetLeafNodes:
                GetLeafNodesMessage getLeafNodesMessage = Deserialize<GetLeafNodesMessage>(message.Content);
                ReportIn(getLeafNodesMessage, size);
                Handle(getLeafNodesMessage);
                break;
            case VerkleMessageCode.LeafNodes:
                LeafNodesMessage leafNodesMessage = Deserialize<LeafNodesMessage>(message.Content);
                ReportIn(leafNodesMessage, size);
                Handle(leafNodesMessage, size);
                break;
        }
    }

    private void Handle(SubTreeRangeMessage msg, long size)
    {
        Metrics.VerkleSubTreeRangeReceived++;
        _getSubTreeRangeRequests.Handle(msg, size);
    }

    private void Handle(LeafNodesMessage msg, long size)
    {
        Metrics.VerkleLeafNodesReceived++;
        _getLeafNodesRequests.Handle(msg, size);
    }

    private void Handle(GetSubTreeRangeMessage msg)
    {
        Metrics.VerkleGetSubTreeRangeReceived++;
        SubTreeRangeMessage? response = FulfillSubTreeRangeMessage(msg);
        response.RequestId = msg.RequestId;
        Send(response);
    }

    private void Handle(GetLeafNodesMessage msg)
    {
        Metrics.VerkleGetLeafNodesReceived++;
        LeafNodesMessage? response = FulfillLeafNodesMessage(msg);
        response.RequestId = msg.RequestId;
        Send(response);
    }

    public override void DisconnectProtocol(DisconnectReason disconnectReason, string details)
    {
        Dispose();
    }

    public async Task<SubTreesAndProofs> GetSubTreeRange(SubTreeRange range, CancellationToken token)
    {
        var request = new GetSubTreeRangeMessage()
        {
            SubTreeRange = range,
            ResponseBytes = _currentBytesLimit
        };

        SubTreeRangeMessage response = await AdjustBytesLimit(() =>
            SendRequest(request, _getSubTreeRangeRequests, token));

        Metrics.VerkleGetSubTreeRangeSent++;

        return new SubTreesAndProofs(response.PathsWithSubTrees, response.Proofs);
    }

    public async Task<byte[][]> GetLeafNodes(GetLeafNodesRequest request, CancellationToken token)
    {
        GetLeafNodesMessage reqMsg = new()
        {
            RootHash = request.RootHash,
            Paths = request.LeafNodePaths,
            Bytes = _currentBytesLimit
        };

        LeafNodesMessage response = await AdjustBytesLimit(() =>
            SendRequest(reqMsg, _getLeafNodesRequests, token));

        Metrics.VerkleGetLeafNodesSent++;

        return response.Nodes;
    }

    public async Task<byte[][]> GetLeafNodes(LeafToRefreshRequest request, CancellationToken token)
    {
        GetLeafNodesMessage reqMsg = new()
        {
            RootHash = request.RootHash,
            Paths = request.Paths,
            Bytes = _currentBytesLimit
        };

        LeafNodesMessage response = await AdjustBytesLimit(() =>
            SendRequest(reqMsg, _getLeafNodesRequests, token));

        Metrics.VerkleGetLeafNodesSent++;

        return response.Nodes;
    }

    protected LeafNodesMessage FulfillLeafNodesMessage(GetLeafNodesMessage getTrieNodesMessage)
    {
        // var trieNodes = SyncServer.GetTrieNodes(getTrieNodesMessage.Paths, getTrieNodesMessage.RootHash);
        Metrics.VerkleLeafNodesSent++;
        return new LeafNodesMessage();
    }

    protected SubTreeRangeMessage FulfillSubTreeRangeMessage(GetSubTreeRangeMessage getAccountRangeMessage)
    {

        SubTreeRange? accountRange = getAccountRangeMessage.SubTreeRange;
        // (PathWithAccount[]? ranges, byte[][]? proofs) = SyncServer.GetAccountRanges(accountRange.RootHash, accountRange.StartingHash,
        //     accountRange.LimitHash, getAccountRangeMessage.ResponseBytes);
        SubTreeRangeMessage? response = new();
        Metrics.VerkleSubTreeRangeSent++;
        return response;
    }

    private async Task<TOut> SendRequest<TIn, TOut>(TIn msg, MessageQueue<TIn, TOut> requestQueue, CancellationToken token)
        where TIn : VerkleMessageBase
        where TOut : VerkleMessageBase
    {
        return await SendRequestGeneric(
            requestQueue,
            msg,
            TransferSpeedType.SnapRanges,
            static (request) => request.ToString(),
            token);
    }

    /// <summary>
    /// Adjust the _currentBytesLimit depending on the latency of the request and if the request failed.
    /// </summary>
    /// <param name="func"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    private async Task<T> AdjustBytesLimit<T>(Func<Task<T>> func)
    {
        // Record bytes limit so that in case multiple concurrent request happens, we do not multiply the
        // limit on top of other adjustment, so only the last adjustment will stick, which is fine.
        int startingBytesLimit = _currentBytesLimit;
        bool failed = false;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            return await func();
        }
        catch (Exception)
        {
            failed = true;
            throw;
        }
        finally
        {
            sw.Stop();
            if (failed)
            {
                _currentBytesLimit = MinBytesLimit;
            }
            else if (sw.Elapsed < LowerLatencyThreshold)
            {
                _currentBytesLimit = Math.Min((int)(startingBytesLimit * BytesLimitAdjustmentFactor), MaxBytesLimit);
            }
            else if (sw.Elapsed > UpperLatencyThreshold && startingBytesLimit > MinBytesLimit)
            {
                _currentBytesLimit = (int)(startingBytesLimit / BytesLimitAdjustmentFactor);
            }
        }
    }
}
