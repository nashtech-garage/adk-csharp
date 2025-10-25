// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Operators.Agents;

namespace NTG.Adk.Operators.Workflows;

/// <summary>
/// Executes sub-agents sequentially in a loop.
/// Equivalent to google.adk.agents.LoopAgent in Python.
///
/// Loop terminates when:
/// 1. max_iterations is reached, OR
/// 2. Any sub-agent returns an Event with escalate=True
///
/// Passes the same InvocationContext in each iteration, allowing
/// state to persist across loops.
/// </summary>
public class LoopAgent : BaseAgent
{
    /// <summary>
    /// Sub-agents to execute in each iteration
    /// </summary>
    public IReadOnlyList<IAgent> Agents { get; }

    /// <summary>
    /// Maximum number of loop iterations (optional)
    /// </summary>
    public int? MaxIterations { get; init; }

    public LoopAgent(
        string name,
        IEnumerable<IAgent> agents,
        int? maxIterations = null,
        string? description = null)
    {
        Name = name;
        Description = description ?? $"Loops through sub-agents (max {maxIterations ?? int.MaxValue} iterations)";
        Agents = agents.ToList();
        MaxIterations = maxIterations;

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
        var iteration = 0;
        var shouldContinue = true;

        while (shouldContinue && (!MaxIterations.HasValue || iteration < MaxIterations.Value))
        {
            cancellationToken.ThrowIfCancellationRequested();

            iteration++;

            // Execute each agent in sequence within this iteration
            foreach (var agent in Agents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Run agent and yield events
                await foreach (var evt in agent.RunAsync(context, cancellationToken))
                {
                    yield return evt;

                    // Check for escalation (break loop)
                    if (evt.Actions?.Escalate == true)
                    {
                        shouldContinue = false;
                        break;
                    }
                }

                // Exit inner loop if escalated
                if (!shouldContinue)
                    break;
            }
        }
    }
}
