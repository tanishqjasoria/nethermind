// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading;

namespace Nethermind.Trie
{
    public class TrieVisitContext : IDisposable
    {
        private SemaphoreSlim? _semaphore;
        private readonly int _maxDegreeOfParallelism = 1;

        public int Level { get; set; }
        public bool IsStorage { get; internal set; }
        public int? BranchChildIndex { get; internal set; }
        public bool ExpectAccounts { get; init; }

        public bool KeepTrackOfAbsolutePath { get; init; }

        private List<byte>? _absolutePathIndex;

        public List<byte> AbsolutePathIndex => _absolutePathIndex ??= new List<byte>();

        public int MaxDegreeOfParallelism
        {
            get => _maxDegreeOfParallelism;
            init => _maxDegreeOfParallelism = value == 0 ? Environment.ProcessorCount : value;
        }

        public AbsolutePathStruct AbsolutePathNext(byte[] path)
        {
            return new AbsolutePathStruct(!KeepTrackOfAbsolutePath ? null : AbsolutePathIndex, path);
        }

        public AbsolutePathStruct AbsolutePathNext(byte path)
        {
            return new AbsolutePathStruct(!KeepTrackOfAbsolutePath ? null : AbsolutePathIndex, path);
        }

        public SemaphoreSlim Semaphore
        {
            get
            {
                if (_semaphore is null)
                {
                    if (MaxDegreeOfParallelism == 1) throw new InvalidOperationException("Can not create semaphore for single threaded trie visitor.");
                    _semaphore = new SemaphoreSlim(MaxDegreeOfParallelism, MaxDegreeOfParallelism);
                }

                return _semaphore;
            }
        }

        public TrieVisitContext Clone() => (TrieVisitContext)MemberwiseClone();

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }


    public readonly ref struct AbsolutePathStruct
    {
        public AbsolutePathStruct(List<byte>? absolutePath, IReadOnlyCollection<byte>? path)
        {
            _absolutePath = absolutePath;
            _pathLength = path!.Count;
            _absolutePath?.AddRange(path!);
        }

        public AbsolutePathStruct(List<byte>? absolutePath, byte path)
        {
            _absolutePath = absolutePath;
            _pathLength = 1;
            _absolutePath?.Add(path);
        }

        private readonly List<byte>? _absolutePath;
        private readonly int _pathLength;

        public void Dispose()
        {
            if (_pathLength > 0)
                _absolutePath?.RemoveRange(_absolutePath.Count - _pathLength, _pathLength);
        }
    }
}
