// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie.Pruning;

namespace Nethermind.Trie;

public partial class PatriciaTree : IPatriciaTree
{

    private readonly ILogger _logger;

    public const int OneNodeAvgMemoryEstimate = 384;

    /// <summary>
    ///     0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421
    /// </summary>
    public static readonly Keccak EmptyTreeHash = Keccak.EmptyTreeHash;
    private static readonly byte[] EmptyKeyPath = new byte[0];

    protected TrieType TrieType { get; set; }


    /// <summary>
    /// To save allocations this used to be static but this caused one of the hardest to reproduce issues
    /// when we decided to run some of the tree operations in parallel.
    /// </summary>
    private readonly Stack<StackedNode> _nodeStack = new();

    internal readonly ConcurrentQueue<Exception>? _commitExceptions;

    internal readonly ConcurrentQueue<NodeCommitInfo>? _currentCommit;

    private readonly ITrieStore _trieStore;

    private readonly bool _parallelBranches;

    private readonly bool _allowCommits;

    private Keccak _rootHash = Keccak.EmptyTreeHash;

    private TrieNode? _rootRef;


    /// <summary>
    /// Only used in EthereumTests
    /// </summary>
    internal TrieNode? Root
    {
        get
        {
            RootRef?.ResolveNode(TrieStore);
            return RootRef;
        }
    }

    public Keccak RootHash
    {
        get => _rootHash;
        set => SetRootHash(value, true);
    }


    public void UpdateRootHash()
    {
        RootRef?.ResolveKey(TrieStore, true);
        SetRootHash(RootRef?.Keccak ?? EmptyTreeHash, false);
    }

    public void SetRootHash(Keccak? value, bool resetObjects)
    {
        _rootHash = value ?? Keccak.EmptyTreeHash; // nulls were allowed before so for now we leave it this way
        if (_rootHash == Keccak.EmptyTreeHash)
        {
            RootRef = null;
        }
        else if (resetObjects)
        {
            switch (_trieStore.Capability)
            {
                case TrieNodeResolverCapability.Hash:
                    RootRef = TrieStore.FindCachedOrUnknown(_rootHash);
                    break;
                case TrieNodeResolverCapability.Path:
                    RootRef = TrieStore.FindCachedOrUnknown(Array.Empty<byte>());
                    RootRef.Keccak = _rootHash;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }
    }

    public ITrieStore TrieStore => _trieStore;

    public TrieNode? RootRef { get => _rootRef; set => _rootRef = value; }


    public PatriciaTree()
        : this(NullTrieStore.Instance, EmptyTreeHash, false, true, NullLogManager.Instance)
    {
    }

    public PatriciaTree(IKeyValueStoreWithBatching keyValueStore, TrieNodeResolverCapability capability = TrieNodeResolverCapability.Hash)
        : this(keyValueStore, EmptyTreeHash, false, true, NullLogManager.Instance)
    {
    }


    public PatriciaTree(ITrieStore trieStore, ILogManager logManager)
        : this(trieStore, EmptyTreeHash, false, true, logManager)
    {
    }

    public PatriciaTree(
        IKeyValueStoreWithBatching keyValueStore,
        Keccak rootHash,
        bool parallelBranches,
        bool allowCommits,
        ILogManager logManager,
        TrieNodeResolverCapability capability = TrieNodeResolverCapability.Hash)
        : this(
            trieStore: capability switch
            {
                TrieNodeResolverCapability.Hash => new TrieStore(keyValueStore, logManager),
                TrieNodeResolverCapability.Path => new TrieStoreByPath(keyValueStore, logManager),
                _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, null)
            },
            rootHash,
            parallelBranches,
            allowCommits,
            logManager)
    { }

    public PatriciaTree(
        ITrieStore? trieStore,
        Keccak rootHash,
        bool parallelBranches,
        bool allowCommits,
        ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<PatriciaTree>() ?? throw new ArgumentNullException(nameof(logManager));
        _trieStore = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
        _parallelBranches = parallelBranches;
        _allowCommits = allowCommits;
        RootHash = rootHash;

        // TODO: cannot do that without knowing whether the owning account is persisted or not
        // RootRef?.MarkPersistedRecursively(_logger);

        if (_allowCommits)
        {
            _currentCommit = new ConcurrentQueue<NodeCommitInfo>();
            _commitExceptions = new ConcurrentQueue<Exception>();
        }
    }

    [DebuggerStepThrough]
    public byte[]? Get(Span<byte> rawKey, Keccak? rootHash = null)
    {
        try
        {
            int nibblesCount = 2 * rawKey.Length;
            byte[] array = null;
            Span<byte> nibbles = rawKey.Length <= 64
                ? stackalloc byte[nibblesCount]
                : array = ArrayPool<byte>.Shared.Rent(nibblesCount);
            Nibbles.BytesToNibbleBytes(rawKey, nibbles);
            var result = Run(nibbles, nibblesCount, Array.Empty<byte>(), false, startRootHash: rootHash);
            if (array is not null) ArrayPool<byte>.Shared.Return(array);
            return result;
        }
        catch (TrieException e)
        {
            throw new TrieException($"Failed to load key {rawKey.ToHexString()} from root hash {rootHash ?? RootHash}.", e);
        }
    }

    [DebuggerStepThrough]
    public void Set(Span<byte> rawKey, byte[] value)
    {
        if (_logger.IsTrace)
            _logger.Trace($"{(value.Length == 0 ? $"Deleting {rawKey.ToHexString()}" : $"Setting {rawKey.ToHexString()} = {value.ToHexString()}")}");

        int nibblesCount = 2 * rawKey.Length;
        byte[] array = null;
        Span<byte> nibbles = rawKey.Length <= 64
            ? stackalloc byte[nibblesCount]
            : array = ArrayPool<byte>.Shared.Rent(nibblesCount);
        Nibbles.BytesToNibbleBytes(rawKey, nibbles);
        Run(nibbles, nibblesCount, value, true);
        if (array is not null) ArrayPool<byte>.Shared.Return(array);
    }

    [DebuggerStepThrough]
    public void Set(Span<byte> rawKey, Rlp? value)
    {
        Set(rawKey, value is null ? Array.Empty<byte>() : value.Bytes);
    }

    public byte[]? Run(
            Span<byte> updatePath,
            int nibblesCount,
            byte[]? updateValue,
            bool isUpdate,
            bool ignoreMissingDelete = true,
            Keccak? startRootHash = null)
    {
        if (isUpdate && startRootHash is not null)
        {
            throw new InvalidOperationException("Only reads can be done in parallel on the Patricia tree");
        }

#if DEBUG
        if (nibblesCount != updatePath.Length)
        {
            throw new Exception("Does it ever happen?");
        }
#endif

        TraverseContext traverseContext =
            new(updatePath.Slice(0, nibblesCount), updateValue, isUpdate, ignoreMissingDelete);

        // lazy stack cleaning after the previous update
        if (traverseContext.IsUpdate)
        {
            _nodeStack.Clear();
        }

        byte[]? result;
        if (startRootHash is not null)
        {
            if (_logger.IsTrace) _logger.Trace($"Starting from {startRootHash} - {traverseContext.ToString()}");
            TrieNode startNode = TrieStore.FindCachedOrUnknown(startRootHash);
            startNode.ResolveNode(TrieStore);
            result = TraverseNode(startNode, traverseContext);
        }
        else
        {
            bool trieIsEmpty = RootRef is null;
            if (trieIsEmpty)
            {
                if (traverseContext.UpdateValue is not null)
                {
                    if (_logger.IsTrace) _logger.Trace($"Setting new leaf node with value {traverseContext.UpdateValue}");
                    byte[] key = updatePath.Slice(0, nibblesCount).ToArray();
                    RootRef = _trieStore.Capability switch
                    {
                        TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateLeaf(key, traverseContext.UpdateValue),
                        TrieNodeResolverCapability.Path => TrieNodeFactory.CreateLeaf(key, traverseContext.UpdateValue, EmptyKeyPath),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                }

                if (_logger.IsTrace) _logger.Trace($"Keeping the root as null in {traverseContext.ToString()}");
                result = traverseContext.UpdateValue;
            }
            else
            {
                RootRef.ResolveNode(TrieStore);
                if (_logger.IsTrace) _logger.Trace($"{traverseContext.ToString()}");
                result = TraverseNode(RootRef, traverseContext);
            }
        }

        return result;
    }

    public void Commit(long blockNumber, bool skipRoot = false)
    {
        if (_currentCommit is null)
        {
            throw new InvalidAsynchronousStateException(
                $"{nameof(_currentCommit)} is NULL when calling {nameof(Commit)}");
        }

        if (!_allowCommits)
        {
            throw new TrieException("Commits are not allowed on this trie.");
        }

        if (RootRef is not null && RootRef.IsDirty)
        {
            Commit(new NodeCommitInfo(RootRef), skipSelf: skipRoot);
            while (_currentCommit.TryDequeue(out NodeCommitInfo node))
            {
                if (_logger.IsTrace) _logger.Trace($"Committing {node} in {blockNumber}");
                TrieStore.CommitNode(blockNumber, node);
            }

            // reset objects
            RootRef!.ResolveKey(TrieStore, true);
            SetRootHash(RootRef.Keccak!, true);
        }

        TrieStore.FinishBlockCommit(TrieType, blockNumber, RootRef);
        if (_logger.IsDebug) _logger.Debug($"Finished committing block {blockNumber}");
    }

    private void Commit(NodeCommitInfo nodeCommitInfo, bool skipSelf = false)
    {
        if (_currentCommit is null)
        {
            throw new InvalidAsynchronousStateException(
                $"{nameof(_currentCommit)} is NULL when calling {nameof(Commit)}");
        }

        if (_commitExceptions is null)
        {
            throw new InvalidAsynchronousStateException(
                $"{nameof(_commitExceptions)} is NULL when calling {nameof(Commit)}");
        }

        TrieNode node = nodeCommitInfo.Node;
        if (node!.IsBranch)
        {
            // idea from EthereumJ - testing parallel branches
            if (!_parallelBranches || !nodeCommitInfo.IsRoot)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (node.IsChildDirty(i))
                    {
                        Commit(new NodeCommitInfo(node.GetChild(TrieStore, i)!, node, i));
                    }
                    else
                    {
                        if (_logger.IsTrace)
                        {
                            TrieNode child = node.GetChild(TrieStore, i);
                            if (child is not null)
                            {
                                _logger.Trace($"Skipping commit of {child}");
                            }
                        }
                    }
                }
            }
            else
            {
                List<NodeCommitInfo> nodesToCommit = new();
                for (int i = 0; i < 16; i++)
                {
                    if (node.IsChildDirty(i))
                    {
                        nodesToCommit.Add(new NodeCommitInfo(node.GetChild(TrieStore, i)!, node, i));
                    }
                    else
                    {
                        if (_logger.IsTrace)
                        {
                            TrieNode child = node.GetChild(TrieStore, i);
                            if (child is not null)
                            {
                                _logger.Trace($"Skipping commit of {child}");
                            }
                        }
                    }
                }

                if (nodesToCommit.Count >= 4)
                {
                    _commitExceptions.Clear();
                    Parallel.For(0, nodesToCommit.Count, i =>
                    {
                        try
                        {
                            Commit(nodesToCommit[i]);
                        }
                        catch (Exception e)
                        {
                            _commitExceptions!.Enqueue(e);
                        }
                    });

                    if (!_commitExceptions.IsEmpty)
                    {
                        throw new AggregateException(_commitExceptions);
                    }
                }
                else
                {
                    for (int i = 0; i < nodesToCommit.Count; i++)
                    {
                        Commit(nodesToCommit[i]);
                    }
                }
            }
        }
        else if (node.NodeType == NodeType.Extension)
        {
            TrieNode extensionChild = node.GetChild(TrieStore, 0);
            if (extensionChild is null)
            {
                throw new InvalidOperationException("An attempt to store an extension without a child.");
            }

            if (extensionChild.IsDirty)
            {
                Commit(new NodeCommitInfo(extensionChild, node, 0));
            }
            else
            {
                if (_logger.IsTrace) _logger.Trace($"Skipping commit of {extensionChild}");
            }
        }

        node.ResolveKey(TrieStore, nodeCommitInfo.IsRoot);
        node.Seal();

        if (node.FullRlp?.Length >= 32)
        {
            if (!skipSelf)
            {
                _currentCommit.Enqueue(nodeCommitInfo);
            }
        }
        else
        {
            if (_logger.IsTrace) _logger.Trace($"Skipping commit of an inlined {node}");
        }
    }

    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null)
    {
        if (visitor is null) throw new ArgumentNullException(nameof(visitor));
        if (rootHash is null) throw new ArgumentNullException(nameof(rootHash));
        visitingOptions ??= VisitingOptions.Default;

        using TrieVisitContext trieVisitContext = new()
        {
            // hacky but other solutions are not much better, something nicer would require a bit of thinking
            // we introduced a notion of an account on the visit context level which should have no knowledge of account really
            // but we know that we have multiple optimizations and assumptions on trees
            ExpectAccounts = visitingOptions.ExpectAccounts,
            MaxDegreeOfParallelism = visitingOptions.MaxDegreeOfParallelism
        };

        TrieNode rootRef = null;
        if (!rootHash.Equals(Keccak.EmptyTreeHash))
        {
            rootRef = RootHash == rootHash ? RootRef : TrieStore.FindCachedOrUnknown(rootHash);
            try
            {
                rootRef!.ResolveNode(TrieStore);
            }
            catch (TrieException)
            {
                visitor.VisitMissingNode(rootHash, trieVisitContext);
                return;
            }
        }

        visitor.VisitTree(rootHash, trieVisitContext);
        rootRef?.Accept(visitor, TrieStore, trieVisitContext);
    }



    private static int FindCommonPrefixLength(Span<byte> shorterPath, Span<byte> longerPath)
    {
        int commonPrefixLength = 0;
        int maxLength = Math.Min(shorterPath.Length, longerPath.Length);
        for (int i = 0; i < maxLength && shorterPath[i] == longerPath[i]; i++, commonPrefixLength++)
        {
            // just finding the common part of the path
        }

        return commonPrefixLength;
    }

    private readonly struct StackedNode
    {
        public StackedNode(TrieNode node, int pathIndex)
        {
            Node = node;
            PathIndex = pathIndex;
        }

        public TrieNode Node { get; }
        public int PathIndex { get; }

        public override string ToString()
        {
            return $"{PathIndex} {Node}";
        }
    }

    private ref struct TraverseContext
    {
        public Span<byte> UpdatePath { get; }
        public byte[]? UpdateValue { get; }
        public bool IsUpdate { get; }
        public bool IsRead => !IsUpdate;
        public bool IsDelete => IsUpdate && UpdateValue is null;
        public bool IgnoreMissingDelete { get; }
        public int CurrentIndex { get; set; }
        public int RemainingUpdatePathLength => UpdatePath.Length - CurrentIndex;

        public Span<byte> GetRemainingUpdatePath()
        {
            return UpdatePath.Slice(CurrentIndex, RemainingUpdatePathLength);
        }

        public Span<byte> GetCurrentPath()
        {
            return UpdatePath.Slice(0, CurrentIndex);
        }

        public TraverseContext(
            Span<byte> updatePath,
            byte[]? updateValue,
            bool isUpdate,
            bool ignoreMissingDelete = true)
        {
            UpdatePath = updatePath;
            if (updateValue is not null && updateValue.Length == 0)
            {
                updateValue = null;
            }

            UpdateValue = updateValue;
            IsUpdate = isUpdate;
            IgnoreMissingDelete = ignoreMissingDelete;
            CurrentIndex = 0;
        }

        public override string ToString()
        {
            return $"{(IsDelete ? "DELETE" : IsUpdate ? "UPDATE" : "READ")} {UpdatePath.ToHexString()}{(IsRead ? string.Empty : $" -> {UpdateValue}")}";
        }
    }
}
