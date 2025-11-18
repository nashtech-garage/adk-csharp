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
/// Pooled factory for creating invocation contexts with automatic pooling.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class PooledInvocationContextFactory : IInvocationContextFactory
{
    private readonly ObjectPool<InvocationContext> _pool;

    public PooledInvocationContextFactory()
    {
        var policy = new InvocationContextPoolPolicy();
        _pool = new DefaultObjectPool<InvocationContext>(policy);
    }

    public IInvocationContext Create(
        ISession session,
        string? userInput = null,
        IContent? userMessage = null,
        IArtifactService? artifactService = null,
        IMemoryService? memoryService = null,
        RunConfig? runConfig = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var context = _pool.Get();

        // Reset context state
        typeof(InvocationContext).GetField("_numberOfLlmCalls",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(context, 0);

        // Re-initialize with new values
        var newContext = new InvocationContext
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            UserMessage = userMessage,
            ArtifactService = artifactService,
            MemoryService = memoryService,
            RunConfig = runConfig ?? new RunConfig(),
            Metadata = metadata
        };

        _pool.Return(context);
        return new PooledInvocationContext(_pool, newContext);
    }

    private class InvocationContextPoolPolicy : IPooledObjectPolicy<InvocationContext>
    {
        public InvocationContext Create()
        {
            return new InvocationContext
            {
                Session = null!,
                Branch = "main"
            };
        }

        public bool Return(InvocationContext obj)
        {
            // No cleanup needed - reset happens in factory
            return true;
        }
    }
}
