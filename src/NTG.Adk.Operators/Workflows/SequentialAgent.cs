// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Operators.Agents;

namespace NTG.Adk.Operators.Workflows;

/// <summary>
/// Executes sub-agents sequentially in order.
/// Equivalent to google.adk.agents.SequentialAgent in Python.
///
/// Passes the same InvocationContext to all sub-agents, allowing them
/// to share state via ctx.Session.State.
/// </summary>
public class SequentialAgent : BaseAgent
{
    /// <summary>
    /// Sub-agents to execute in sequence
    /// </summary>
    public IReadOnlyList<IAgent> Agents { get; }

    public SequentialAgent(string name, IEnumerable<IAgent> agents, string? description = null)
    {
        Name = name;
        Description = description ?? "Executes sub-agents sequentially";
        Agents = agents.ToList();

        // Add all agents as sub-agents
        foreach (var agent in Agents)
        {
            AddSubAgent(agent);
        }
    }

    protected override async IAsyncEnumerable<IEvent> RunAsyncImpl(
        IInvocationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        // Execute each agent in sequence, passing the same context
        foreach (var agent in Agents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Run agent and yield all events
            await foreach (var evt in agent.RunAsync(context, cancellationToken))
            {
                yield return evt;

                // Check for escalation
                if (evt.Actions?.Escalate == true)
                {
                    // Stop execution and propagate escalation
                    yield break;
                }
            }
        }
    }
}
