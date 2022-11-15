// Copyright 2022 Demerzel Solutions Limited
// Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.

using FluentAssertions;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Utils;
using NUnit.Framework;

namespace Nethermind.Trie.Test;

public class VerkleUtilsTest
{

    private readonly byte[] treeKeyVersion =
    {
        24, 203, 148, 64, 90, 4, 172, 63, 91, 103, 58, 173, 125, 107, 176, 236, 255, 23, 135, 61, 129, 207, 123, 108,
        18, 153, 101, 247, 17, 48, 148, 0
    };

    private readonly byte[] treeKeyBalance =
    {
        24, 203, 148, 64, 90, 4, 172, 63, 91, 103, 58, 173, 125, 107, 176, 236, 255, 23, 135, 61, 129, 207, 123, 108,
        18, 153, 101, 247, 17, 48, 148, 1
    };

    private readonly byte[] treeKeyNonce =
    {
        24, 203, 148, 64, 90, 4, 172, 63, 91, 103, 58, 173, 125, 107, 176, 236, 255, 23, 135, 61, 129, 207, 123, 108,
        18, 153, 101, 247, 17, 48, 148, 2
    };

    private readonly byte[] treeKeyCodeKeccak =
    {
        24, 203, 148, 64, 90, 4, 172, 63, 91, 103, 58, 173, 125, 107, 176, 236, 255, 23, 135, 61, 129, 207, 123, 108,
        18, 153, 101, 247, 17, 48, 148, 3
    };

    private readonly byte[] treeKeyCodeSize =
    {
        24, 203, 148, 64, 90, 4, 172, 63, 91, 103, 58, 173, 125, 107, 176, 236, 255, 23, 135, 61, 129, 207, 123, 108,
        18, 153, 101, 247, 17, 48, 148, 4
    };

    [Test]
    public void CalculateTreeKeys()
    {
        byte[] keyPrefix = AccountHeader.GetTreeKeyPrefixAccount(TestItem.AddressA.Bytes);

        keyPrefix[31] = AccountHeader.Version;
        Assert.AreEqual(keyPrefix, treeKeyVersion);
        keyPrefix[31] = AccountHeader.Balance;
        Assert.AreEqual(keyPrefix, treeKeyBalance);
        keyPrefix[31] = AccountHeader.Nonce;
        Assert.AreEqual(keyPrefix, treeKeyNonce);
        keyPrefix[31] = AccountHeader.CodeHash;
        Assert.AreEqual(keyPrefix, treeKeyCodeKeccak);
        keyPrefix[31] = AccountHeader.CodeSize;
        Assert.AreEqual(keyPrefix, treeKeyCodeSize);
    }

    [Test]
    public void Set_Account_With_Code()
    {
        AccountHeader.CodeChunkEnumerator codeEnumerator;

        byte[] code = { 1, 2, 3, 4 };
        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code);

        codeEnumerator.TryGetNextChunk(out byte[] value);
        value.Should().NotBeNull();
        value.Slice(0, 5).Should().BeEquivalentTo(new byte[] { 0, 1, 2, 3, 4 });
        value.Slice(5, 27).Should().BeEquivalentTo(new byte[27]);

        codeEnumerator.TryGetNextChunk(out value).Should().BeFalse();
    }

    [Test]
    public void Set_Account_With_Code_Push_Opcodes()
    {
        AccountHeader.CodeChunkEnumerator codeEnumerator;

        byte[] code1 = { 97, 1, 2, 3, 4 };
        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code1);

        codeEnumerator.TryGetNextChunk(out byte[] value);
        value.Should().NotBeNull();
        value.Slice(0, 6).Should().BeEquivalentTo(new byte[] { 0, 97, 1, 2, 3, 4 });
        value.Slice(6, 26).Should().BeEquivalentTo(new byte[26]);

        byte[] code2 =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 100, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45
        };
        byte[] firstCodeChunk =
        {
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 100, 30
        };
        byte[] secondCodeChunk =
        {
            4, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45
        };

        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code2);

        codeEnumerator.TryGetNextChunk(out value);
        value.Should().NotBeNull();
        value.Should().BeEquivalentTo(firstCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Should().NotBeNull();
        value.Slice(0, 16).Should().BeEquivalentTo(secondCodeChunk);
        value.Slice(16, 16).Should().BeEquivalentTo(new byte[16]);

    }

    [Test]
    public void Set_Code_Edge_Cases_1()
    {
        AccountHeader.CodeChunkEnumerator codeEnumerator;

        byte[] code =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 29, 127, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65
        };
        byte[] firstCodeChunk =
        {
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 29, 127
        };
        byte[] secondCodeChunk =
        {
            31, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61
        };
        byte[] thirdCodeChunk =
        {
            1, 62, 63, 64, 65
        };
        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code);

        codeEnumerator.TryGetNextChunk(out byte[] value);
        value.Should().BeEquivalentTo(firstCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Should().BeEquivalentTo(secondCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Slice(0, 5).Should().BeEquivalentTo(thirdCodeChunk);
    }

    [Test]
    public void Set_Code_Edge_Cases_2()
    {
        AccountHeader.CodeChunkEnumerator codeEnumerator;
        byte[] code =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 29, 126, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65
        };
        byte[] firstCodeChunk =
        {
            0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27,
            28, 29, 126
        };
        byte[] secondCodeChunk =
        {
            31, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61
        };
        byte[] thirdCodeChunk =
        {
            0, 62, 63, 64, 65
        };

        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code);

        codeEnumerator.TryGetNextChunk(out byte[] value);
        value.Should().BeEquivalentTo(firstCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Should().BeEquivalentTo(secondCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Slice(0, 5).Should().BeEquivalentTo(thirdCodeChunk);

    }

    [Test]
    public void Set_Code_Edge_Cases_3()
    {
        AccountHeader.CodeChunkEnumerator codeEnumerator;
        byte[] code =
        {
            95, 1, 96, 3, 4, 97, 6, 7, 8, 98, 10, 11, 12, 13, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 112, 113,
            114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65
        };
        byte[] firstCodeChunk =
        {
            0, 95, 1, 96, 3, 4, 97, 6, 7, 8, 98, 10, 11, 12, 13, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 112, 113,
            114, 115, 116
        };
        byte[] secondCodeChunk =
        {
            19, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53,
            54, 55, 56, 57, 58, 59, 60, 61
        };
        byte[] thirdCodeChunk =
        {
            0, 62, 63, 64, 65
        };

        codeEnumerator = new AccountHeader.CodeChunkEnumerator(code);

        codeEnumerator.TryGetNextChunk(out byte[] value);
        value.Should().BeEquivalentTo(firstCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Should().BeEquivalentTo(secondCodeChunk);

        codeEnumerator.TryGetNextChunk(out value);
        value.Slice(0, 5).Should().BeEquivalentTo(thirdCodeChunk);
    }
}
