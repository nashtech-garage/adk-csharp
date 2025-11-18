// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.ObjectPool;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Pooled factory for creating tool contexts with automatic pooling.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class PooledToolContextFactory : IToolContextFactory
{
    private readonly ObjectPool<ToolContext> _pool;

    public PooledToolContextFactory()
    {
        var policy = new ToolContextPoolPolicy();
        _pool = new DefaultObjectPool<ToolContext>(policy);
    }

    public IToolContext Create(
        ISession session,
        ISessionState state,
        IToolActions? actions = null)
    {
        var context = _pool.Get();

        // Re-initialize with new values
        var newContext = new ToolContext
        {
            Session = session,
            State = state,
            Actions = actions ?? new ToolActions()
        };

        _pool.Return(context);
        return new PooledToolContext(_pool, newContext);
    }

    private class ToolContextPoolPolicy : IPooledObjectPolicy<ToolContext>
    {
        public ToolContext Create()
        {
            return new ToolContext
            {
                Session = null!,
                State = null!,
                Actions = new ToolActions()
            };
        }

        public bool Return(ToolContext obj)
        {
            // No cleanup needed - reset happens in factory
            return true;
        }
    }
}
