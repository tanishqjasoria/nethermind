// Copyright 2022 Demerzel Solutions Limited
// Licensed under the LGPL-3.0. For full terms, see LICENSE-LGPL in the project root.

using System;
using FluentAssertions;
using FluentAssertions.Collections;

namespace Nethermind.Trie.Test;

public static class SpanGenericsExtension
{
    public static GenericCollectionAssertions<T> Should<T>(this Span<T> value) => value.ToArray().Should();
    public static GenericCollectionAssertions<T> Should<T>(this ReadOnlySpan<T> value) => value.ToArray().Should();

    public static AndConstraint<GenericCollectionAssertions<T>> BeEquivalentTo<T>(this GenericCollectionAssertions<T> value, Span<byte> expectedValue) =>
        value.BeEquivalentTo(expectedValue.ToArray());
    public static AndConstraint<GenericCollectionAssertions<T>> BeEquivalentTo<T>(this GenericCollectionAssertions<T> value, ReadOnlySpan<byte> expectedValue) =>
        value.BeEquivalentTo(expectedValue.ToArray());

}
