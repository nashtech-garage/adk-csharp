// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.ObjectPool;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Pooled wrapper for IInvocationContext with automatic return via IDisposable.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public sealed class PooledInvocationContext : IInvocationContext, IDisposable
{
    private readonly ObjectPool<InvocationContext> _pool;
    private readonly InvocationContext _context;
    private bool _disposed;

    internal PooledInvocationContext(ObjectPool<InvocationContext> pool, InvocationContext context)
    {
        _pool = pool;
        _context = context;
    }

    public ISession Session => _context.Session;
    public string Branch => _context.Branch;
    public string? UserInput => _context.UserInput;
    public IContent? UserMessage => _context.UserMessage;
    public IArtifactService? ArtifactService => _context.ArtifactService;
    public IMemoryService? MemoryService => _context.MemoryService;
    public RunConfig? RunConfig => _context.RunConfig;
    public IReadOnlyDictionary<string, object>? Metadata => _context.Metadata;
    public int NumberOfLlmCalls => _context.NumberOfLlmCalls;
    public string InvocationId => _context.InvocationId;

    public void IncrementAndEnforceLlmCallsLimit() => _context.IncrementAndEnforceLlmCallsLimit();

    public IInvocationContext WithBranch(string newBranch) => _context.WithBranch(newBranch);
    public IInvocationContext WithUserInput(string newUserInput) => _context.WithUserInput(newUserInput);
    public IInvocationContext WithUserMessage(IContent newUserMessage) => _context.WithUserMessage(newUserMessage);

    public void Dispose()
    {
        if (!_disposed)
        {
            _pool.Return(_context);
            _disposed = true;
        }
    }
}
