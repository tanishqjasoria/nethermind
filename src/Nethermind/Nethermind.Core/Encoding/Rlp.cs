﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Microsoft.IO;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;

namespace Nethermind.Core.Encoding
{
    /// <summary>
    ///     https://github.com/ethereum/wiki/wiki/RLP
    /// </summary>
    //[DebuggerStepThrough]
    public class Rlp
    {
        public static readonly Rlp OfEmptyByteArray = new Rlp(128);

        public static readonly Rlp OfEmptySequence = new Rlp(192);

        /// <summary>
        /// This is not encoding - just a creation of an RLP object, e.g. passing 192 would mean an RLP of an empty sequence.
        /// </summary>
        internal Rlp(byte singleByte)
        {
            Bytes = new[] { singleByte };
        }

        public Rlp(byte[] bytes)
        {
            Bytes = bytes;
        }

        public byte[] Bytes { get; }

        public byte this[int index] => Bytes[index];

        public int Length => Bytes.Length;

        // TODO: discover decoders, use them for encoding as well
        private static readonly Dictionary<Type, IRlpDecoder> Decoders =
            new Dictionary<Type, IRlpDecoder>
            {
                [typeof(Account)] = new AccountDecoder(),
                [typeof(Block)] = new BlockDecoder(),
                [typeof(BlockHeader)] = new HeaderDecoder(),
                [typeof(BlockInfo)] = new BlockInfoDecoder(),
                [typeof(ChainLevelInfo)] = new ChainLevelDecoder(),
                [typeof(LogEntry)] = new LogEntryDecoder(),
                [typeof(NetworkNode)] = new NetworkNodeDecoder(),
                [typeof(Transaction)] = new TransactionDecoder(),
                [typeof(TransactionReceipt)] = new TransactionReceiptDecoder(),
            };

        public static T Decode<T>(Rlp oldRlp, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            return Decode<T>(oldRlp.Bytes.AsRlpContext(), rlpBehaviors);
        }

        public static T Decode<T>(byte[] bytes, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            return Decode<T>(bytes.AsRlpContext(), rlpBehaviors);
        }

        public static T[] DecodeArray<T>(DecoderContext context, RlpBehaviors rlpBehaviors = RlpBehaviors.None) // TODO: move inside the context
        {
            if (Decoders.ContainsKey(typeof(T)))
            {
                IRlpDecoder<T> decoder = (IRlpDecoder<T>)Decoders[typeof(T)];
                int checkPosition = context.ReadSequenceLength() + context.Position;
                T[] result = new T[context.ReadNumberOfItemsRemaining(checkPosition)];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = decoder.Decode(context, rlpBehaviors);
                }

                return result;
            }

            throw new RlpException($"{nameof(Rlp)} does not support decoding {typeof(T).Name}");
        }

        public static T Decode<T>(DecoderContext context, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            if (Decoders.ContainsKey(typeof(T)))
            {
                return ((IRlpDecoder<T>)Decoders[typeof(T)]).Decode(context, rlpBehaviors);
            }

            throw new RlpException($"{nameof(Rlp)} does not support decoding {typeof(T).Name}");
        }

        public static Rlp Encode<T>(T item, RlpBehaviors behaviors = RlpBehaviors.None)
        {
            if (item is Rlp rlp)
            {
                return Encode(new[] { rlp });
            }

            if (Decoders.ContainsKey(typeof(T)))
            {
                return ((IRlpDecoder<T>)Decoders[typeof(T)]).Encode(item, behaviors);
            }

            throw new RlpException($"{nameof(Rlp)} does not support encoding {typeof(T).Name}");
        }

        public static Rlp Encode<T>(T[] items, RlpBehaviors behaviors = RlpBehaviors.None)
        {
            if (Decoders.ContainsKey(typeof(T)))
            {
                IRlpDecoder<T> decoder = (IRlpDecoder<T>)Decoders[typeof(T)];
                Rlp[] rlpSequence = new Rlp[items.Length];
                for (int i = 0; i < items.Length; i++)
                {
                    rlpSequence[i] = decoder.Encode(items[i], behaviors);
                }

                return Encode(rlpSequence);
            }

            throw new RlpException($"{nameof(Rlp)} does not support decoding {typeof(T).Name}");
        }

        public static Rlp Encode(Transaction transaction)
        {
            return Encode(transaction, false);
        }

        public static Rlp Encode(Transaction transaction, bool forSigning, bool isEip155Enabled = false, int chainId = 0)
        {
            Rlp[] sequence = new Rlp[forSigning && !(isEip155Enabled && chainId != 0) ? 6 : 9];
            sequence[0] = Encode(transaction.Nonce);
            sequence[1] = Encode(transaction.GasPrice);
            sequence[2] = Encode(transaction.GasLimit);
            sequence[3] = Encode(transaction.To);
            sequence[4] = Encode(transaction.Value);
            sequence[5] = Encode(transaction.To == null ? transaction.Init : transaction.Data);

            if (forSigning)
            {
                if (isEip155Enabled && chainId != 0)
                {
                    sequence[6] = Encode(chainId);
                    sequence[7] = OfEmptyByteArray;
                    sequence[8] = OfEmptyByteArray;
                }
            }
            else
            {
                // TODO: below obviously fails when Signature is null
                sequence[6] = transaction.Signature == null ? OfEmptyByteArray : Encode(transaction.Signature.V);
                sequence[7] = Encode(transaction.Signature?.R.WithoutLeadingZeros()); // TODO: consider storing R and S differently
                sequence[8] = Encode(transaction.Signature?.S.WithoutLeadingZeros()); // TODO: consider storing R and S differently
            }

            return Encode(sequence);
        }

        private static Rlp EncodeNumber(long item)
        {
            long value = item;

            if (value >= 0)
            {
                // check test bytestring00 and zero - here is some inconsistency in tests
                if (value == 0L)
                {
                    return OfEmptyByteArray;
                }

                if (value < 128L)
                {
                    // ReSharper disable once PossibleInvalidCastException
                    return new Rlp(Convert.ToByte(value));
                }

                if (value <= byte.MaxValue)
                {
                    return Encode(new[] {Convert.ToByte(value)});
                }

                if (value <= short.MaxValue)
                {
                    return Encode(((short) value).ToBigEndianByteArray());
                }
                
                return Encode(new BigInteger(value));
            }

            return Encode(new BigInteger(value), 8);
        }

        public static Rlp Encode(bool value)
        {
            return value ? new Rlp(1) : OfEmptyByteArray;
        }

        public static Rlp Encode(byte value)
        {
            if (value == 0L)
            {
                return OfEmptyByteArray;
            }

            if (value < 128L)
            {
                return new Rlp(value);
            }

            return Encode(new[] { value });
        }

        public static Rlp Encode(long value)
        {
            return EncodeNumber(value);
        }

        // TODO: nonces only
        public static Rlp Encode(ulong value)
        {
            return Encode(value.ToBigEndianByteArray());
        }

        public static Rlp Encode(short value)
        {
            return EncodeNumber(value);
        }

        public static Rlp Encode(ushort value)
        {
            return EncodeNumber(value);
        }

        public static Rlp Encode(int value)
        {
            return EncodeNumber(value);
        }

        public static Rlp Encode(uint value)
        {
            return EncodeNumber(value);
        }

        public static Rlp Encode(BigInteger bigInteger, int outputLength = -1)
        {
            // TODO: use span here
            return bigInteger == 0 ? OfEmptyByteArray : Encode(bigInteger.ToBigEndianByteArray(outputLength));
        }

        public static Rlp Encode(string s)
        {
            if (s == null)
            {
                return OfEmptyByteArray;
            }

            return Encode(System.Text.Encoding.ASCII.GetBytes(s));
        }

        public static void Encode(MemoryStream stream, byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                stream.Write(OfEmptyByteArray.Bytes);
            }
            else if (input.Length == 1 && input[0] < 128)
            {
                stream.WriteByte(input[0]);
            }
            else if (input.Length < 56)
            {
                byte smallPrefix = (byte)(input.Length + 128);
                stream.WriteByte(smallPrefix);
                stream.Write(input);
            }
            else
            {
                int lengthOfLength = LengthOfLength(input.Length);
                byte prefix = (byte)(183 + lengthOfLength);
                stream.WriteByte(prefix);
                SerializeLength(stream, input.Length);
                stream.Write(input);
            }
        }

        public static int Encode(Span<byte> buffer, int position, byte[] input)
        {
            if (input == null || input.Length == 0)
            {
                buffer[position++] = OfEmptyByteArray.Bytes[0];
                return position;
            }

            if (input.Length == 1 && input[0] < 128)
            {
                buffer[position++] = input[0];
                return position;
            }

            if (input.Length < 56)
            {
                byte smallPrefix = (byte)(input.Length + 128);
                buffer[position++] = smallPrefix;
            }
            else
            {
                int lengthOfLength = LengthOfLength(input.Length);
                byte prefix = (byte)(183 + lengthOfLength);
                buffer[position++] = prefix;
                SerializeLength(buffer, position, input.Length);
            }

            input.AsSpan().CopyTo(buffer.Slice(position, input.Length));
            position += input.Length;

            return position;
        }

        public static Rlp Encode(byte[] input)
        {
            if (input.Length == 0)
            {
                return OfEmptyByteArray;
            }

            if (input.Length == 1 && input[0] < 128)
            {
                return new Rlp(input[0]);
            }

            if (input.Length < 56)
            {
                byte smallPrefix = (byte)(input.Length + 128);
                return new Rlp(Extensions.Bytes.Concat(smallPrefix, input));
            }

            byte[] serializedLength = SerializeLength(input.Length);
            byte prefix = (byte)(183 + serializedLength.Length);
            return new Rlp(Extensions.Bytes.Concat(prefix, serializedLength, input));
        }

        public static void SerializeLength(MemoryStream stream, int value)
        {
            if (value < 1 << 8)
            {
                stream.WriteByte((byte)value);
            }
            else if (value < 1 << 16)
            {
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)value);
            }
            else if (value < 1 << 24)
            {
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)value);
            }
            else
            {
                stream.WriteByte((byte)(value >> 24));
                stream.WriteByte((byte)(value >> 16));
                stream.WriteByte((byte)(value >> 8));
                stream.WriteByte((byte)value);
            }
        }

        public static int SerializeLength(Span<byte> buffer, int position, int value)
        {
            if (value < 1 << 8)
            {
                buffer[position] = (byte)value;
                return position + 1;
            }

            if (value < 1 << 16)
            {
                buffer[position] = (byte)(value >> 8);
                buffer[position + 1] = ((byte)value);
                return position + 2;
            }

            if (value < 1 << 24)
            {
                buffer[position] = (byte)(value >> 16);
                buffer[position + 1] = ((byte)(value >> 8));
                buffer[position + 2] = ((byte)value);
                return position + 3;
            }

            buffer[position] = (byte)(value >> 24);
            buffer[position + 1] = (byte)(value >> 16);
            buffer[position + 2] = (byte)(value >> 8);
            buffer[position + 3] = (byte)value;
            return position + 4;
        }

        private static int LengthOfLength(int value)
        {
            if (value < 1 << 8)
            {
                return 1;
            }

            if (value < 1 << 16)
            {
                return 2;
            }

            if (value < 1 << 24)
            {
                return 3;
            }

            return 4;
        }

        public static byte[] SerializeLength(int value)
        {
            if (value < 1 << 8)
            {
                return new[] { (byte)value };
            }

            if (value < 1 << 16)
            {
                return new[]
                {
                    (byte)(value >> 8),
                    (byte)value,
                };
            }

            if (value < 1 << 24)
            {
                return new[]
                {
                    (byte)(value >> 16),
                    (byte)(value >> 8),
                    (byte)value,
                };
            }

            return new[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value
            };
        }

        public static Rlp Encode(Bloom bloom)
        {
            if (bloom == null)
            {
                return OfEmptyByteArray;
            }

            byte[] result = new byte[259];
            result[0] = 185;
            result[1] = 1;
            result[2] = 0;
            Buffer.BlockCopy(bloom.Bytes, 0, result, 3, 256);
            return new Rlp(result);
        }

        public static void Encode(MemoryStream stream, Keccak keccak)
        {
            if (keccak == null)
            {
                stream.Write(OfEmptyByteArray.Bytes);
            }
            else
            {
                stream.WriteByte(160);
                stream.Write(keccak.Bytes);
            }
        }

        public static Rlp Encode(Keccak keccak)
        {
            if (keccak == null)
            {
                return OfEmptyByteArray;
            }

            byte[] result = new byte[LengthOfKeccakRlp];
            result[0] = 160;
            Buffer.BlockCopy(keccak.Bytes, 0, result, 1, 32);
            return new Rlp(result);
        }

        public static Rlp Encode(Address address)
        {
            if (address == null)
            {
                return OfEmptyByteArray;
            }

            byte[] result = new byte[21];
            result[0] = 148;
            Buffer.BlockCopy(address.Bytes, 0, result, 1, 20);
            return new Rlp(result);
        }

        public static Rlp Encode(Keccak[] sequence)
        {
            Rlp[] rlpSequence = new Rlp[sequence.Length];
            for (int i = 0; i < sequence.Length; i++)
            {
                rlpSequence[i] = Encode(sequence[i]);
            }

            return Encode(rlpSequence);
        }

        public static MemoryStream StartEncoding()
        {
            return StreamManager.GetStream();
        }

        public static int GetSequenceRlpLength(int contentLength)
        {
            int totalLength = contentLength + 1;
            if (contentLength >= 56)
            {
                totalLength += LengthOfLength(contentLength);
            }

            return totalLength;
        }

        public static void StartSequence(MemoryStream stream, int sequenceLength)
        {
            byte prefix;
            long memorizedPosition1 = stream.Position;
            long memorizedPosition2 = memorizedPosition1 + 1;
            stream.Seek(memorizedPosition2, SeekOrigin.Begin);
            if (sequenceLength < 56)
            {
                prefix = (byte)(192 + sequenceLength);
            }
            else
            {
                SerializeLength(stream, sequenceLength);
                prefix = (byte)(247 + stream.Position - memorizedPosition2);
            }

            long memorizedPosition3 = stream.Position;
            stream.Seek(memorizedPosition1, SeekOrigin.Begin);
            stream.WriteByte(prefix);
            stream.Seek(memorizedPosition3, SeekOrigin.Begin);
        }

        public static int StartSequence(byte[] buffer, int position, int sequenceLength)
        {
            byte prefix;
            int beforeLength = position + 1;
            int afterLength = position + 1;
            if (sequenceLength < 56)
            {
                prefix = (byte)(192 + sequenceLength);
            }
            else
            {
                afterLength = SerializeLength(buffer, beforeLength, sequenceLength);
                prefix = (byte)(247 + afterLength - beforeLength);
            }

            buffer[position] = prefix;
            return afterLength;
        }

        public static Rlp Encode(params Rlp[] sequence)
        {
            int contentLength = 0;
            for (int i = 0; i < sequence.Length; i++)
            {
                contentLength += sequence[i].Length;
            }

            byte[] serializedLength = null;
            byte prefix;
            if (contentLength < 56)
            {
                prefix = (byte)(192 + contentLength);
            }
            else
            {
                serializedLength = SerializeLength(contentLength);
                prefix = (byte)(247 + serializedLength.Length);
            }

            int lengthOfPrefixAndSerializedLength = 1 + (serializedLength?.Length ?? 0);
            byte[] allBytes = new byte[lengthOfPrefixAndSerializedLength + contentLength];
            allBytes[0] = prefix;
            int offset = 1;
            if (serializedLength != null)
            {
                Buffer.BlockCopy(serializedLength, 0, allBytes, offset, serializedLength.Length);
                offset += serializedLength.Length;
            }

            for (int i = 0; i < sequence.Length; i++)
            {
                Buffer.BlockCopy(sequence[i].Bytes, 0, allBytes, offset, sequence[i].Length);
                offset += sequence[i].Length;
            }

            return new Rlp(allBytes);
        }

        private static readonly RecyclableMemoryStreamManager StreamManager = new RecyclableMemoryStreamManager();
        public const int LengthOfKeccakRlp = 33;
        public const int LengthOfAddressRlp = 21;
        public const int LengthOfBloomRlp = 259;
        public const int LengthOfEmptyArrayRlp = 1;
        public const int LengthOfEmptySequenceRlp = 1;

        public static int LengthOfByteArray(byte[] array)
        {
            if (array == null || array.Length == 0)
            {
                return 1;
            }

            if (array.Length == 1 && array[0] < 128)
            {
                return 1;
            }

            if (array.Length < 56)
            {
                return array.Length + 1;
            }

            return LengthOfLength(array.Length) + 1 + array.Length;
        }

        public class DecoderContext
        {
            public DecoderContext(byte[] data)
            {
                Data = data;
                Position = 0;
            }

            public byte[] Data { get; }

            public int Position { get; set; }

            public int Length => Data.Length;

            public bool IsSequenceNext()
            {
                return Data[Position] >= 192;
            }
            
//            public int ReadNumberOfItemsRemaining(int? beforePosition = null)
//            {
//                int positionStored = Position;
//                int numberOfItems = 0;
//                while (Position < (beforePosition ?? Data.Length))
//                {
//                   SkipItem();
//                }
//
//                Position = positionStored;
//                return numberOfItems;
//            }

            public int ReadNumberOfItemsRemaining(int? beforePosition = null)
            {
                int positionStored = Position;
                int numberOfItems = 0;
                while (Position < (beforePosition ?? Data.Length))
                {
                    int prefix = ReadByte();
                    if (prefix <= 128)
                    {
                    }
                    else if (prefix <= 183)
                    {
                        int length = prefix - 128;
                        Position += length;
                    }
                    else if (prefix < 192)
                    {
                        int lengthOfLength = prefix - 183;
                        int length = DeserializeLength(lengthOfLength);
                        if (length < 56)
                        {
                            throw new RlpException("Expected length greater or equal 56 and was {length}");
                        }

                        Position += length;
                    }
                    else
                    {
                        Position--;
                        int sequenceLength = ReadSequenceLength();
                        Position += sequenceLength;
                    }

                    numberOfItems++;
                }

                Position = positionStored;
                return numberOfItems;
            }

            public void SkipLength()
            {
                Position += PeekPrefixAndContentLength().PrefixLength;
            }

            public int PeekNextRlpLength()
            {
                (int a, int b) = PeekPrefixAndContentLength();
                return a + b;
            }

            public (int PrefixLength, int ContentLength) PeekPrefixAndContentLength()
            {
                int memorizedPosition = Position;
                (int prefixLength, int contentLengt) result;
                int prefix = ReadByte();
                if (prefix <= 128)
                {
                    result = (0, 1);
                }
                else if (prefix <= 183)
                {
                    result = (1, prefix - 128);
                }
                else if (prefix < 192)
                {
                    int lengthOfLength = prefix - 183;
                    if (lengthOfLength > 4)
                    {
                        // strange but needed to pass tests - seems that spec gives int64 length and tests int32 length
                        throw new RlpException("Expected length of lenth less or equal 4");
                    }

                    int length = DeserializeLength(lengthOfLength);
                    if (length < 56)
                    {
                        throw new RlpException("Expected length greater or equal 56 and was {length}");
                    }

                    result = (lengthOfLength + 1, length);
                }
                else if (prefix <= 247)
                {
                    result = (1, prefix - 192);
                }
                else
                {
                    int lengthOfContentLength = prefix - 247;
                    int contentLength = DeserializeLength(lengthOfContentLength);
                    if (contentLength < 56)
                    {
                        throw new RlpException($"Expected length greater or equal 56 and got {contentLength}");
                    }


                    result = (lengthOfContentLength + 1, contentLength);
                }

                Position = memorizedPosition;
                return result;
            }

            public int ReadSequenceLength()
            {
                int prefix = ReadByte();
                if (prefix < 192)
                {
                    throw new RlpException($"Expected a sequence prefix to be in the range of <192, 255> and got {prefix}");
                }

                if (prefix <= 247)
                {
                    return prefix - 192;
                }

                int lengthOfContentLength = prefix - 247;
                int contentLength = DeserializeLength(lengthOfContentLength);
                if (contentLength < 56)
                {
                    throw new RlpException($"Expected length greater or equal 56 and got {contentLength}");
                }

                return contentLength;
            }

            private int DeserializeLength(int lengthOfLength)
            {
                int result;
                if (Data[Position] == 0)
                {
                    throw new RlpException("Length starts with 0");
                }

                if (lengthOfLength == 1)
                {
                    result = Data[Position];
                }
                else if (lengthOfLength == 2)
                {
                    result = Data[Position + 1] | (Data[Position] << 8);
                }
                else if (lengthOfLength == 3)
                {
                    result = Data[Position + 2] | (Data[Position + 1] << 8) | (Data[Position] << 16);
                }
                else if (lengthOfLength == 4)
                {
                    result = Data[Position + 3] | (Data[Position + 2] << 8) | (Data[Position + 1] << 16) | (Data[Position] << 24);
                }
                else
                {
                    // strange but needed to pass tests - seems that spec gives int64 length and tests int32 length
                    throw new InvalidOperationException($"Invalid length of length = {lengthOfLength}");
                }

                Position += lengthOfLength;
                return result;
            }

            public byte ReadByte()
            {
                return Data[Position++];
            }

            public Span<byte> Read(int length)
            {
                Span<byte> data = Data.AsSpan(Position, length);
                Position += length;
                return data;
            }

            public void Check(int nextCheck)
            {
                if (Position != nextCheck)
                {
                    throw new RlpException($"Data checkpoint failed. Expected {nextCheck} and is {Position}");
                }
            }

            public Keccak DecodeKeccak()
            {
                int prefix = ReadByte();
                if (prefix == 128)
                {
                    return null;
                }

                if (prefix != 128 + 32)
                {
                    throw new RlpException($"Unexpected prefix of {prefix} when decoding {nameof(Keccak)}");
                }

                byte[] buffer = Read(32).ToArray();
                return new Keccak(buffer);
            }

            public Address DecodeAddress()
            {
                int prefix = ReadByte();
                if (prefix == 128)
                {
                    return null;
                }

                if (prefix != 128 + 20)
                {
                    throw new RlpException($"Unexpected prefix of {prefix} when decoding {nameof(Address)}");
                }

                byte[] buffer = Read(20).ToArray();
                return new Address(buffer);
            }

            public BigInteger DecodeUBigInt()
            {
                Span<byte> bytes = DecodeByteArraySpan();
                return bytes.ToUnsignedBigInteger();
            }

            public Bloom DecodeBloom()
            {
                Span<byte> bloomBytes = DecodeByteArraySpan();
                if (bloomBytes.Length == 0)
                {
                    return null;
                }

                Bloom bloom = bloomBytes.Length == 256 ? new Bloom(bloomBytes.ToBigEndianBitArray2048()) : throw new InvalidOperationException("Incorrect bloom RLP");
                return bloom;
            }

            public Span<byte> PeekNextItem()
            {
                int length = PeekNextRlpLength();
                Span<byte> item = Read(length);
                Position -= item.Length;
                return item;
            }

            public bool DecodeBool()
            {
                int prefix = ReadByte();
                if (prefix <= 128)
                {
                    return prefix == 1;
                }

                if (prefix <= 183)
                {
                    int length = prefix - 128;
                    if (length == 1 && Data[Position] < 128)
                    {
                        throw new RlpException($"Unexpected byte value {Data[Position]}");
                    }

                    bool result = Data[Position] == 1;
                    Position += length;
                    return result;
                }

                if (prefix < 192)
                {
                    int lengthOfLength = prefix - 183;
                    if (lengthOfLength > 4)
                    {
                        // strange but needed to pass tests - seems that spec gives int64 length and tests int32 length
                        throw new RlpException("Expected length of lenth less or equal 4");
                    }

                    int length = DeserializeLength(lengthOfLength);
                    if (length < 56)
                    {
                        throw new RlpException("Expected length greater or equal 56 and was {length}");
                    }

                    bool result = Data[Position] == 1;
                    Position += length;
                    return result;
                }

                throw new RlpException($"Unexpected prefix value of {prefix} when decoding a byte array.");
            }

            public T[] DecodeArray<T>(Func<DecoderContext, T> decodeItem)
            {
                int positionCheck = ReadSequenceLength() + Position;
                int count = ReadNumberOfItemsRemaining(positionCheck);
                T[] result = new T[count];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = decodeItem(this);
                }

                return result;
            }

            public string DecodeString()
            {
                Span<byte> bytes = DecodeByteArraySpan();
                return System.Text.Encoding.UTF8.GetString(bytes);
            }

            public byte DecodeByte()
            {
                Span<byte> bytes = DecodeByteArraySpan();
                return bytes.Length == 0 ? (byte)0 : bytes[0];
            }

            public int DecodeInt()
            {
                byte[] bytes = DecodeByteArray();
                return bytes.Length == 0 ? 0 : bytes.ToInt32();
            }

            public long DecodeLong()
            {
                byte[] bytes = DecodeByteArray();
                return bytes.Length == 0 ? 0L : bytes.ToInt64();
            }

            public byte[] DecodeByteArray()
            {
                return DecodeByteArraySpan().ToArray();
            }

            private Span<byte> DecodeByteArraySpan()
            {
                int prefix = ReadByte();
                if (prefix == 0)
                {
                    return new byte[] { 0 };
                }

                if (prefix < 128)
                {
                    return new[] { (byte)prefix };
                }

                if (prefix == 128)
                {
                    return Extensions.Bytes.Empty;
                }

                if (prefix <= 183)
                {
                    int length = prefix - 128;
                    Span<byte> buffer = Read(length);
                    if (length == 1 && buffer[0] < 128)
                    {
                        throw new RlpException($"Unexpected byte value {buffer[0]}");
                    }

                    return buffer;
                }

                if (prefix < 192)
                {
                    int lengthOfLength = prefix - 183;
                    if (lengthOfLength > 4)
                    {
                        // strange but needed to pass tests - seems that spec gives int64 length and tests int32 length
                        throw new RlpException("Expected length of lenth less or equal 4");
                    }

                    int length = DeserializeLength(lengthOfLength);
                    if (length < 56)
                    {
                        throw new RlpException("Expected length greater or equal 56 and was {length}");
                    }

                    return Read(length);
                }

                throw new RlpException($"Unexpected prefix value of {prefix} when decoding a byte array.");
            }

            public void SkipItem()
            {
                (int prefix, int content) = PeekPrefixAndContentLength();
                Position += prefix + content;
            }

            public void Reset()
            {
                Position = 0;
            }
        }

        public bool Equals(Rlp other)
        {
            if (other == null)
            {
                return false;
            }

            return Extensions.Bytes.UnsafeCompare(Bytes, other.Bytes);
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public string ToString(bool withZeroX)
        {
            return Bytes.ToHexString(withZeroX);
        }

        public int GetHashCode(Rlp obj)
        {
            return obj.Bytes.GetXxHashCode();
        }
    }
}