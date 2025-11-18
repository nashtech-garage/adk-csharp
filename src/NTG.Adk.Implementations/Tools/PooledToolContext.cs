// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.ObjectPool;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Pooled wrapper for IToolContext with automatic return via IDisposable.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public sealed class PooledToolContext : IToolContext, IDisposable
{
    private readonly ObjectPool<ToolContext> _pool;
    private readonly ToolContext _context;
    private bool _disposed;

    internal PooledToolContext(ObjectPool<ToolContext> pool, ToolContext context)
    {
        _pool = pool;
        _context = context;
    }

    public ISession Session => _context.Session;
    public ISessionState State => _context.State;
    public string? User => _context.User;
    public IReadOnlyDictionary<string, object>? Metadata => _context.Metadata;
    public IToolActions Actions => _context.Actions;

    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Return(_context);
            _disposed = true;
        }
    }
}
