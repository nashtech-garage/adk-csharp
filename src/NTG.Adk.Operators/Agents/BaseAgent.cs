// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Events;

namespace NTG.Adk.Operators.Agents;

/// <summary>
/// Base agent operator class.
/// Orchestrates agent execution, following A.D.D V3: depends only on Ports (CoreAbstractions).
/// Equivalent to google.adk.agents.BaseAgent in Python.
/// </summary>
public abstract class BaseAgent : IAgent
{
    private IAgent? _parentAgent;
    private readonly List<IAgent> _subAgents = new();

    public string Name { get; set; } = string.Empty;
    public string? Description { get; init; }

    public IAgent? ParentAgent
    {
        get => _parentAgent;
        set => _parentAgent = value;
    }

    public IReadOnlyList<IAgent> SubAgents => _subAgents.AsReadOnly();

    /// <summary>
    /// Add a sub-agent to this agent
    /// </summary>
    public void AddSubAgent(IAgent agent)
    {
        if (agent.ParentAgent != null && agent.ParentAgent != this)
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' already has a parent '{agent.ParentAgent.Name}'. " +
                "An agent can only have one parent.");
        }

        agent.ParentAgent = this;
        _subAgents.Add(agent);
    }

    /// <summary>
    /// Add multiple sub-agents
    /// </summary>
    public void AddSubAgents(params IAgent[] agents)
    {
        foreach (var agent in agents)
        {
            AddSubAgent(agent);
        }
    }

    /// <summary>
    /// Find agent by name (recursive search through hierarchy)
    /// </summary>
    public IAgent? FindAgent(string name)
    {
        if (Name == name) return this;

        foreach (var subAgent in SubAgents)
        {
            var found = subAgent.FindAgent(name);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Main execution method - calls the implementation and handles AutoFlow transfers.
    /// </summary>
    public async IAsyncEnumerable<IEvent> RunAsync(
        IInvocationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Call the implementation method
        await foreach (var evt in RunAsyncImpl(context, cancellationToken))
        {
            yield return evt;

            // Check for transfer action (AutoFlow)
            if (!string.IsNullOrEmpty(evt.Actions?.TransferTo))
            {
                var targetAgentName = evt.Actions.TransferTo;
                var targetAgent = FindAgent(targetAgentName);

                if (targetAgent == null)
                {
                    // Agent not found - yield error event
                    yield return CreateTextEvent(
                        $"Error: Cannot transfer to agent '{targetAgentName}' - agent not found in hierarchy.");
                    yield break;
                }

                // Transfer execution to target agent
                await foreach (var transferredEvent in targetAgent.RunAsync(context, cancellationToken))
                {
                    yield return transferredEvent;

                    // Allow chaining: if transferred agent also transfers, continue the chain
                    if (!string.IsNullOrEmpty(transferredEvent.Actions?.TransferTo))
                    {
                        // The recursive call to RunAsync will handle the next transfer
                        break;
                    }
                }

                // Stop processing current agent after transfer
                yield break;
            }

            // Check for escalate action (exit loop)
            if (evt.Actions?.Escalate == true)
            {
                // Propagate escalation to parent
                yield break;
            }
        }
    }

    /// <summary>
    /// Implementation method for derived classes.
    /// Equivalent to _run_async_impl in Python ADK.
    /// </summary>
    protected abstract IAsyncEnumerable<IEvent> RunAsyncImpl(
        IInvocationContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Helper to create a text event
    /// </summary>
    protected IEvent CreateTextEvent(string text)
    {
        return EventAdapter.FromText(Name, text);
    }

    /// <summary>
    /// Helper to create an event from DTO
    /// </summary>
    protected IEvent CreateEvent(Event eventDto)
    {
        return new EventAdapter(eventDto);
    }
}
