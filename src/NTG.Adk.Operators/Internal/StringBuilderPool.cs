// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace NTG.Adk.Operators.Internal;

/// <summary>
/// Shared StringBuilder pool for reducing allocations.
/// Internal optimization - not part of public API.
/// </summary>
internal static class StringBuilderPool
{
    private static readonly ObjectPool<StringBuilder> Pool = new DefaultObjectPool<StringBuilder>(
        new StringBuilderPoolPolicy());

    /// <summary>
    /// Get StringBuilder from pool
    /// </summary>
    public static StringBuilder Get() => Pool.Get();

    /// <summary>
    /// Return StringBuilder to pool
    /// </summary>
    public static void Return(StringBuilder builder) => Pool.Return(builder);

    private class StringBuilderPoolPolicy : IPooledObjectPolicy<StringBuilder>
    {
        public StringBuilder Create() => new StringBuilder();

        public bool Return(StringBuilder obj)
        {
            if (obj.Capacity > 4096)
            {
                // Don't return very large builders
                return false;
            }

            obj.Clear();
            return true;
        }
    }
}
