// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Default factory for creating tool contexts.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class ToolContextFactory : IToolContextFactory
{
    public IToolContext Create(
        ISession session,
        ISessionState state,
        IToolActions? actions = null)
    {
        return new ToolContext
        {
            Session = session,
            State = state,
            Actions = actions ?? new ToolActions()
        };
    }
}
