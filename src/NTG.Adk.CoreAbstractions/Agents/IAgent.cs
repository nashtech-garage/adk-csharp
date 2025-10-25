// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Agents;

/// <summary>
/// Port interface for all agents.
/// Equivalent to BaseAgent interface contract in Python ADK.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique name of this agent
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what this agent does
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Parent agent in the hierarchy
    /// </summary>
    IAgent? ParentAgent { get; set; }

    /// <summary>
    /// Sub-agents that this agent orchestrates
    /// </summary>
    IReadOnlyList<IAgent> SubAgents { get; }

    /// <summary>
    /// Run the agent asynchronously.
    /// Returns a stream of events.
    /// </summary>
    /// <param name="context">The invocation context containing session, state, etc.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async stream of events</returns>
    IAsyncEnumerable<IEvent> RunAsync(
        IInvocationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find a descendant agent by name (recursive search)
    /// </summary>
    IAgent? FindAgent(string name);
}
