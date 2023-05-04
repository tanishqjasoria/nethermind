// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.State
{
    public class StateTree : PatriciaTree, IStateTree
    {
        private readonly AccountDecoder _decoder = new();

        private static readonly UInt256 CacheSize = 1024;

        private static readonly int CacheSizeInt = (int)CacheSize;

        private static readonly Dictionary<UInt256, byte[]> Cache = new(CacheSizeInt);

        private static readonly Rlp EmptyAccountRlp = Rlp.Encode(Account.TotallyEmpty);

        static StateTree()
        {
            Span<byte> buffer = stackalloc byte[32];
            for (int i = 0; i < CacheSizeInt; i++)
            {
                UInt256 index = (UInt256)i;
                index.ToBigEndian(buffer);
                Cache[index] = Keccak.Compute(buffer).Bytes;
            }
        }

        [DebuggerStepThrough]
        public StateTree()
            : base(new MemDb(), Keccak.EmptyTreeHash, true, true, NullLogManager.Instance)
        {
            TrieType = TrieType.State;
        }

        [DebuggerStepThrough]
        public StateTree(ITrieStore? store, ILogManager? logManager)
            : base(store, Keccak.EmptyTreeHash, true, true, logManager)
        {
            Debug.Assert(store.Capability == TrieNodeResolverCapability.Hash);
            TrieType = TrieType.State;
        }

        [DebuggerStepThrough]
        public Account? Get(Address address, Keccak? rootHash = null)
        {
            byte[]? bytes = Get(ValueKeccak.Compute(address.Bytes).BytesAsSpan, rootHash);
            return bytes is null ? null : _decoder.Decode(bytes.AsRlpStream());

        }

        [DebuggerStepThrough]
        internal Account? Get(Keccak keccak) // for testing
        {
            byte[]? bytes = Get(keccak.Bytes);
            return bytes is null ? null : _decoder.Decode(bytes.AsRlpStream());

        }

        public void Set(Address address, Account? account)
        {
            ValueKeccak keccak = ValueKeccak.Compute(address.Bytes);
            Set(keccak.BytesAsSpan, account is null ? null : account.IsTotallyEmpty ? EmptyAccountRlp : Rlp.Encode(account));
        }

        [DebuggerStepThrough]
        public Rlp? Set(Keccak keccak, Account? account)
        {
            Rlp rlp = account is null ? null : account.IsTotallyEmpty ? EmptyAccountRlp : Rlp.Encode(account);

            Set(keccak.Bytes, rlp);
            return rlp;
        }

        public byte[] GetStorage(in UInt256 index, in Address? accountAddress, Keccak? root = null)
        {
            Span<byte> key = stackalloc byte[32];
            GetStorageKey(index, key);
            byte[]? value = Get(key, root);


            if (value is null)
            {
                return new byte[] { 0 };
            }

            Rlp.ValueDecoderContext rlp = value.AsRlpValueContext();
            return rlp.DecodeByteArray();
        }
        public void SetStorage(in UInt256 index, byte[] value, in Address? accountAddress)
        {
            Span<byte> key = stackalloc byte[32];
            GetStorageKey(index, key);
            SetInternal(key, value);
        }
        public void SetStorage(Keccak key, byte[] value, in Address? accountAddress, bool rlpEncode = true)
        {
            SetInternal(key.Bytes, value, rlpEncode);
        }

        private static void GetStorageKey(in UInt256 index, in Span<byte> key)
        {
            if (index < CacheSize)
            {
                Cache[index].CopyTo(key);
                return;
            }

            index.ToBigEndian(key);

            // in situ calculation
            KeccakHash.ComputeHashBytesToSpan(key, key);
        }

        private void SetInternal(Span<byte> rawKey, byte[] value, bool rlpEncode = true)
        {
            if (value.IsZero())
            {
                Set(rawKey, Array.Empty<byte>());
            }
            else
            {
                Rlp rlpEncoded = rlpEncode ? Rlp.Encode(value) : new Rlp(value);
                Set(rawKey, rlpEncoded);
            }
        }
    }
}
