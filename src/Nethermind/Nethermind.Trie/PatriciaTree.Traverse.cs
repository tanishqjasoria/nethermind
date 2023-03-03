// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using Nethermind.Core.Extensions;
using Nethermind.Trie.Pruning;

namespace Nethermind.Trie;

public partial class PatriciaTree
{
    private byte[]? TraverseNode(TrieNode node, TraverseContext traverseContext)
    {
        if (_logger.IsTrace)
            _logger.Trace(
                $"Traversing {node} to {(traverseContext.IsRead ? "READ" : traverseContext.IsDelete ? "DELETE" : "UPDATE")}");

        return node.NodeType switch
        {
            NodeType.Branch => TraverseBranch(node, traverseContext),
            NodeType.Extension => TraverseExtension(node, traverseContext),
            NodeType.Leaf => TraverseLeaf(node, traverseContext),
            NodeType.Unknown => throw new InvalidOperationException(
                $"Cannot traverse unresolved node {node.Keccak}"),
            _ => throw new NotSupportedException(
                $"Unknown node type {node.NodeType}")
        };
    }

    private byte[]? TraverseBranch(TrieNode node, TraverseContext traverseContext)
    {
        if (traverseContext.RemainingUpdatePathLength == 0)
        {
            /* all these cases when the path ends on the branch assume a trie with values in the branches
               which is not possible within the Ethereum protocol which has keys of the same length (64) */

            if (traverseContext.IsRead)
            {
                return node.Value;
            }

            if (traverseContext.IsDelete)
            {
                if (node.Value is null)
                {
                    return null;
                }

                ConnectNodes(null);
            }
            else if (Bytes.AreEqual(traverseContext.UpdateValue, node.Value))
            {
                return traverseContext.UpdateValue;
            }
            else
            {
                TrieNode withUpdatedValue = node.CloneWithChangedValue(traverseContext.UpdateValue);
                ConnectNodes(withUpdatedValue);
            }

            return traverseContext.UpdateValue;
        }
        TrieNode childNode = _trieStore.Capability switch
        {
            TrieNodeResolverCapability.Hash => node.GetChild(TrieStore, traverseContext.UpdatePath[traverseContext.CurrentIndex]),
            TrieNodeResolverCapability.Path => node.GetChild(TrieStore, traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + 1), traverseContext.UpdatePath[traverseContext.CurrentIndex]),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (traverseContext.IsUpdate)
        {
            _nodeStack.Push(new StackedNode(node, traverseContext.UpdatePath[traverseContext.CurrentIndex]));
        }

        traverseContext.CurrentIndex++;

        if (childNode is null)
        {
            if (traverseContext.IsRead)
            {
                return null;
            }

            if (traverseContext.IsDelete)
            {
                if (traverseContext.IgnoreMissingDelete)
                {
                    return null;
                }

                throw new TrieException(
                    $"Could not find the leaf node to delete: {traverseContext.UpdatePath.ToHexString(false)}");
            }

            byte[] leafPath = traverseContext.UpdatePath.Slice(
                traverseContext.CurrentIndex,
                traverseContext.UpdatePath.Length - traverseContext.CurrentIndex).ToArray();
            TrieNode leaf = _trieStore.Capability switch
            {
                TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateLeaf(leafPath, traverseContext.UpdateValue),
                TrieNodeResolverCapability.Path => TrieNodeFactory.CreateLeaf(leafPath, traverseContext.UpdateValue, traverseContext.GetCurrentPath()),
                _ => throw new ArgumentOutOfRangeException()
            };

            ConnectNodes(leaf);

            return traverseContext.UpdateValue;
        }

        childNode.ResolveNode(TrieStore);
        TrieNode nextNode = childNode;
        return TraverseNode(nextNode, traverseContext);
    }

    private byte[]? TraverseLeaf(TrieNode node, TraverseContext traverseContext)
    {
        if (node.Key is null)
        {
            throw new InvalidDataException("An attempt to visit a node without a prefix path.");
        }

        Span<byte> remaining = traverseContext.GetRemainingUpdatePath();
        Span<byte> shorterPath;
        Span<byte> longerPath;
        if (traverseContext.RemainingUpdatePathLength - node.Key.Length < 0)
        {
            shorterPath = remaining;
            longerPath = node.Key;
        }
        else
        {
            shorterPath = node.Key;
            longerPath = remaining;
        }

        byte[] shorterPathValue;
        byte[] longerPathValue;

        if (Bytes.AreEqual(shorterPath, node.Key))
        {
            shorterPathValue = node.Value;
            longerPathValue = traverseContext.UpdateValue;
        }
        else
        {
            shorterPathValue = traverseContext.UpdateValue;
            longerPathValue = node.Value;
        }

        int extensionLength = FindCommonPrefixLength(shorterPath, longerPath);
        if (extensionLength == shorterPath.Length && extensionLength == longerPath.Length)
        {
            if (traverseContext.IsRead)
            {
                return node.Value;
            }

            if (traverseContext.IsDelete)
            {
                ConnectNodes(null);
                return traverseContext.UpdateValue;
            }

            if (!Bytes.AreEqual(node.Value, traverseContext.UpdateValue))
            {
                TrieNode withUpdatedValue = node.CloneWithChangedValue(traverseContext.UpdateValue);
                ConnectNodes(withUpdatedValue);
                return traverseContext.UpdateValue;
            }

            return traverseContext.UpdateValue;
        }

        if (traverseContext.IsRead)
        {
            return null;
        }

        if (traverseContext.IsDelete)
        {
            if (traverseContext.IgnoreMissingDelete)
            {
                return null;
            }

            throw new TrieException(
                $"Could not find the leaf node to delete: {traverseContext.UpdatePath.ToHexString(false)}");
        }

        if (extensionLength != 0)
        {
            Span<byte> extensionPath = longerPath.Slice(0, extensionLength);
            TrieNode extension = _trieStore.Capability switch
            {
                TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateExtension(extensionPath.ToArray()),
                TrieNodeResolverCapability.Path => TrieNodeFactory.CreateExtension(extensionPath.ToArray(), traverseContext.GetCurrentPath()),
                _ => throw new ArgumentOutOfRangeException()
            };
            _nodeStack.Push(new StackedNode(extension, 0));
        }

        TrieNode branch = _trieStore.Capability switch
        {
            TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateBranch(),
            TrieNodeResolverCapability.Path => TrieNodeFactory.CreateBranch(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength).ToArray()),
            _ => throw new ArgumentOutOfRangeException()
        };
        if (extensionLength == shorterPath.Length)
        {
            branch.Value = shorterPathValue;
        }
        else
        {
            Span<byte> shortLeafPath = shorterPath.Slice(extensionLength + 1, shorterPath.Length - extensionLength - 1);
            TrieNode shortLeaf;
            switch (_trieStore.Capability)
            {
                case TrieNodeResolverCapability.Hash:
                    shortLeaf = TrieNodeFactory.CreateLeaf(shortLeafPath.ToArray(), shorterPathValue);
                    break;
                case TrieNodeResolverCapability.Path:
                    if (shorterPath.Length == 64)
                    {
                        Span<byte> pathToShortLeaf = shorterPath.Slice(0, extensionLength + 1);
                        shortLeaf = TrieNodeFactory.CreateLeaf(shortLeafPath.ToArray(), shorterPathValue, pathToShortLeaf);
                    }
                    else
                    {
                        Span<byte> pathToShortLeaf = stackalloc byte[branch.PathToNode.Length + 1];
                        branch.PathToNode.CopyTo(pathToShortLeaf);
                        pathToShortLeaf[branch.PathToNode.Length] = shorterPath[extensionLength];
                        shortLeaf = TrieNodeFactory.CreateLeaf(shortLeafPath.ToArray(), shorterPathValue, pathToShortLeaf);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            branch.SetChild(shorterPath[extensionLength], shortLeaf);
        }

        Span<byte> leafPath = longerPath.Slice(extensionLength + 1, longerPath.Length - extensionLength - 1);
        TrieNode withUpdatedKeyAndValue;
        switch (_trieStore.Capability)
        {
            case TrieNodeResolverCapability.Hash:
                withUpdatedKeyAndValue = node.CloneWithChangedKeyAndValue(
                    leafPath.ToArray(), longerPathValue);
                break;
            case TrieNodeResolverCapability.Path:
                Span<byte> pathToLeaf = stackalloc byte[branch.PathToNode.Length + 1];
                branch.PathToNode.CopyTo(pathToLeaf);
                pathToLeaf[branch.PathToNode.Length] = longerPath[extensionLength];
                withUpdatedKeyAndValue = node.CloneWithChangedKeyAndValue(leafPath.ToArray(), longerPathValue, pathToLeaf.ToArray());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _nodeStack.Push(new StackedNode(branch, longerPath[extensionLength]));
        ConnectNodes(withUpdatedKeyAndValue);

        return traverseContext.UpdateValue;
    }

    private byte[]? TraverseExtension(TrieNode node, TraverseContext traverseContext)
    {
        if (node.Key is null)
        {
            throw new InvalidDataException("An attempt to visit a node without a prefix path.");
        }

        TrieNode originalNode = node;
        Span<byte> remaining = traverseContext.GetRemainingUpdatePath();

        int extensionLength = FindCommonPrefixLength(remaining, node.Key);
        if (extensionLength == node.Key.Length)
        {
            switch (_trieStore.Capability)
            {
                case TrieNodeResolverCapability.Hash:
                    break;
                case TrieNodeResolverCapability.Path:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            traverseContext.CurrentIndex += extensionLength;
            if (traverseContext.IsUpdate)
            {
                _nodeStack.Push(new StackedNode(node, 0));
            }

            TrieNode next = _trieStore.Capability switch
            {
                TrieNodeResolverCapability.Hash => node.GetChild(TrieStore, 0),
                TrieNodeResolverCapability.Path => node.GetChild(TrieStore, traverseContext.GetCurrentPath(), 0),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (next is null)
            {
                throw new TrieException(
                    $"Found an {nameof(NodeType.Extension)} {node.Keccak} that is missing a child.");
            }

            next.ResolveNode(TrieStore);
            return TraverseNode(next, traverseContext);
        }

        if (traverseContext.IsRead)
        {
            return null;
        }

        if (traverseContext.IsDelete)
        {
            if (traverseContext.IgnoreMissingDelete)
            {
                return null;
            }

            throw new TrieException(
                $"Could find the leaf node to delete: {traverseContext.UpdatePath.ToHexString()}");
        }

        byte[] pathBeforeUpdate = node.Key;
        if (extensionLength != 0)
        {
            byte[] extensionPath = node.Key.Slice(0, extensionLength);
            node = node.CloneWithChangedKey(extensionPath);
            _nodeStack.Push(new StackedNode(node, 0));
        }

        TrieNode branch = _trieStore.Capability switch
        {
            TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateBranch(),
            TrieNodeResolverCapability.Path => TrieNodeFactory.CreateBranch(traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength)),
            _ => throw new ArgumentOutOfRangeException()
        };

        if (extensionLength == remaining.Length)
        {
            branch.Value = traverseContext.UpdateValue;
        }
        else
        {
            byte[] path = remaining.Slice(extensionLength + 1, remaining.Length - extensionLength - 1).ToArray();
            TrieNode shortLeaf = _trieStore.Capability switch
            {
                TrieNodeResolverCapability.Hash => TrieNodeFactory.CreateLeaf(path, traverseContext.UpdateValue),
                TrieNodeResolverCapability.Path => TrieNodeFactory.CreateLeaf(path, traverseContext.UpdateValue, traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength + 1)),
                _ => throw new ArgumentOutOfRangeException()
            };
            branch.SetChild(remaining[extensionLength], shortLeaf);
        }

        TrieNode originalNodeChild = originalNode.GetChild(TrieStore, 0);
        if (originalNodeChild is null)
        {
            throw new InvalidDataException(
                $"Extension {originalNode.Keccak} has no child.");
        }

        if (pathBeforeUpdate.Length - extensionLength > 1)
        {
            byte[] extensionPath = pathBeforeUpdate.Slice(extensionLength + 1, pathBeforeUpdate.Length - extensionLength - 1);
            TrieNode secondExtension;
            switch (_trieStore.Capability)
            {
                case TrieNodeResolverCapability.Hash:
                    secondExtension = TrieNodeFactory.CreateExtension(extensionPath, originalNodeChild);
                    break;
                case TrieNodeResolverCapability.Path:
                    Span<byte> fullPath = traverseContext.UpdatePath.Slice(0, traverseContext.CurrentIndex + extensionLength + 1);
                    fullPath[traverseContext.CurrentIndex + extensionLength] = pathBeforeUpdate[extensionLength];
                    secondExtension
                        = TrieNodeFactory.CreateExtension(extensionPath, originalNodeChild, fullPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            branch.SetChild(pathBeforeUpdate[extensionLength], secondExtension);
        }
        else
        {
            TrieNode childNode = originalNodeChild;
            branch.SetChild(pathBeforeUpdate[extensionLength], childNode);
        }

        ConnectNodes(branch);
        return traverseContext.UpdateValue;
    }

    private void ConnectNodes(TrieNode? node)
    {
        bool isRoot = _nodeStack.Count == 0;
        TrieNode nextNode = node;

        while (!isRoot)
        {
            StackedNode parentOnStack = _nodeStack.Pop();
            node = parentOnStack.Node;

            isRoot = _nodeStack.Count == 0;

            if (node.IsLeaf)
            {
                throw new TrieException(
                    $"{nameof(NodeType.Leaf)} {node} cannot be a parent of {nextNode}");
            }

            if (node.IsBranch)
            {
                if (!(nextNode is null && !node.IsValidWithOneNodeLess))
                {
                    if (node.IsSealed)
                    {
                        node = node.Clone();
                    }

                    node.SetChild(parentOnStack.PathIndex, nextNode);
                    nextNode = node;
                }
                else
                {
                    if (node.Value!.Length != 0)
                    {
                        // this only happens when we have branches with values
                        // which is not possible in the Ethereum protocol where keys are of equal lengths
                        // (it is possible in the more general trie definition)
                        TrieNode leafFromBranch = TrieNodeFactory.CreateLeaf(Array.Empty<byte>(), node.Value);
                        if (_logger.IsTrace) _logger.Trace($"Converting {node} into {leafFromBranch}");
                        nextNode = leafFromBranch;
                    }
                    else
                    {
                        /* all the cases below are when we have a branch that becomes something else
                           as a result of deleting one of the last two children */
                        /* case 1) - extension from branch
                           this is particularly interesting - we create an extension from
                           the implicit path in the branch children positions (marked as P)
                           P B B B B B B B B B B B B B B B
                           B X - - - - - - - - - - - - - -
                           case 2) - extended extension
                           B B B B B B B B B B B B B B B B
                           E X - - - - - - - - - - - - - -
                           case 3) - extended leaf
                           B B B B B B B B B B B B B B B B
                           L X - - - - - - - - - - - - - - */

                        int childNodeIndex = 0;
                        for (int i = 0; i < 16; i++)
                        {
                            if (i != parentOnStack.PathIndex && !node.IsChildNull(i))
                            {
                                childNodeIndex = i;
                                break;
                            }
                        }

                        TrieNode childNode = node.GetChild(TrieStore, childNodeIndex);
                        if (childNode is null)
                        {
                            /* potential corrupted trie data state when we find a branch that has only one child */
                            throw new TrieException(
                                "Before updating branch should have had at least two non-empty children");
                        }

                        childNode.ResolveNode(TrieStore);
                        if (childNode.IsBranch)
                        {
                            TrieNode extensionFromBranch =
                                TrieNodeFactory.CreateExtension(new[] { (byte)childNodeIndex }, childNode);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending child {childNodeIndex} {childNode} of {node} into {extensionFromBranch}");

                            nextNode = extensionFromBranch;
                        }
                        else if (childNode.IsExtension)
                        {
                            /* to test this case we need something like this initially */
                            /* R
                               B B B B B B B B B B B B B B B B
                               E L - - - - - - - - - - - - - -
                               E - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            /* then we delete the leaf (marked as X) */
                            /* R
                               B B B B B B B B B B B B B B B B
                               E X - - - - - - - - - - - - - -
                               E - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            /* and we end up with an extended extension (marked with +)
                               replacing what was previously a top-level branch */
                            /* R
                               +
                               +
                               + - - - - - - - - - - - - - - -
                               B B B B B B B B B B B B B B B B
                               L L - - - - - - - - - - - - - - */

                            byte[] newKey = Bytes.Concat((byte)childNodeIndex, childNode.Key);

                            TrieNode extendedExtension = childNode.CloneWithChangedKey(newKey);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending child {childNodeIndex} {childNode} of {node} into {extendedExtension}");
                            nextNode = extendedExtension;
                        }
                        else if (childNode.IsLeaf)
                        {
                            byte[] newKey = Bytes.Concat((byte)childNodeIndex, childNode.Key);

                            TrieNode extendedLeaf = childNode.CloneWithChangedKey(newKey);
                            if (_logger.IsTrace)
                                _logger.Trace(
                                    $"Extending branch child {childNodeIndex} {childNode} into {extendedLeaf}");

                            if (_logger.IsTrace) _logger.Trace($"Decrementing ref on a leaf extended up to eat a branch {childNode}");
                            if (node.IsSealed)
                            {
                                if (_logger.IsTrace) _logger.Trace($"Decrementing ref on a branch replaced by a leaf {node}");
                            }

                            nextNode = extendedLeaf;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unknown node type {childNode.NodeType}");
                        }
                    }
                }
            }
            else if (node.IsExtension)
            {
                if (nextNode is null)
                {
                    throw new InvalidOperationException(
                        $"An attempt to set a null node as a child of the {node}");
                }

                if (nextNode.IsLeaf)
                {
                    byte[] newKey = Bytes.Concat(node.Key, nextNode.Key);
                    TrieNode extendedLeaf = nextNode.CloneWithChangedKey(newKey);
                    if (_logger.IsTrace)
                        _logger.Trace($"Combining {node} and {nextNode} into {extendedLeaf}");

                    nextNode = extendedLeaf;
                }
                else if (nextNode.IsExtension)
                {
                    /* to test this case we need something like this initially */
                    /* R
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       E L - - - - - - - - - - - - - -
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    /* then we delete the leaf (marked as X) */
                    /* R
                       B B B B B B B B B B B B B B B B
                       E X - - - - - - - - - - - - - -
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    /* and we end up with an extended extension replacing what was previously a top-level branch*/
                    /* R
                       E
                       E
                       E - - - - - - - - - - - - - - -
                       B B B B B B B B B B B B B B B B
                       L L - - - - - - - - - - - - - - */

                    byte[] newKey = Bytes.Concat(node.Key, nextNode.Key);
                    TrieNode extendedExtension = nextNode.CloneWithChangedKey(newKey);
                    if (_logger.IsTrace)
                        _logger.Trace($"Combining {node} and {nextNode} into {extendedExtension}");

                    nextNode = extendedExtension;
                }
                else if (nextNode.IsBranch)
                {
                    if (node.IsSealed)
                    {
                        node = node.Clone();
                    }

                    if (_logger.IsTrace) _logger.Trace($"Connecting {node} with {nextNode}");
                    node.SetChild(0, nextNode);
                    nextNode = node;
                }
                else
                {
                    throw new InvalidOperationException($"Unknown node type {nextNode.NodeType}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unknown node type {node.GetType().Name}");
            }
        }

        RootRef = nextNode;
    }
}
