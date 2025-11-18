// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Tools;

/// <summary>
/// Factory interface for creating tool execution contexts.
/// Follows A.D.D V3: Port interface in CoreAbstractions.
/// Bootstrap layer wires concrete implementations.
/// </summary>
public interface IToolContextFactory
{
    /// <summary>
    /// Create a tool context with the specified parameters.
    /// </summary>
    /// <param name="session">The session for this tool execution</param>
    /// <param name="state">Session state for tool access</param>
    /// <param name="actions">Optional tool actions (default created if null)</param>
    /// <returns>A new tool context instance</returns>
    IToolContext Create(
        ISession session,
        ISessionState state,
        IToolActions? actions = null);
}
