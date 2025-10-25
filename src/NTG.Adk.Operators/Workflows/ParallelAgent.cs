// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Threading.Channels;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Operators.Agents;

namespace NTG.Adk.Operators.Workflows;

/// <summary>
/// Executes sub-agents in parallel.
/// Equivalent to google.adk.agents.ParallelAgent in Python.
///
/// Each sub-agent gets a modified context with a different branch path,
/// but all share the same session.state (use distinct keys to avoid races).
/// </summary>
public class ParallelAgent : BaseAgent
{
    /// <summary>
    /// Sub-agents to execute in parallel
    /// </summary>
    public IReadOnlyList<IAgent> Agents { get; }

    public ParallelAgent(string name, IEnumerable<IAgent> agents, string? description = null)
    {
        Name = name;
        Description = description ?? "Executes sub-agents in parallel";
        Agents = agents.ToList();

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
        // Create a channel to collect events from all parallel tasks
        var channel = Channel.CreateUnbounded<IEvent>();

        // Start all agents in parallel
        var tasks = new List<Task>();

        foreach (var agent in Agents)
        {
            var agentTask = Task.Run(async () =>
            {
                try
                {
                    // Create child context with modified branch
                    var childContext = context.WithBranch($"{context.Branch}.{agent.Name}");

                    // Run agent and forward events to channel
                    await foreach (var evt in agent.RunAsync(childContext, cancellationToken))
                    {
                        await channel.Writer.WriteAsync(evt, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Handle errors gracefully
                    Console.Error.WriteLine($"Error in parallel agent {agent.Name}: {ex.Message}");
                }
            }, cancellationToken);

            tasks.Add(agentTask);
        }

        // Close channel when all tasks complete
        _ = Task.Run(async () =>
        {
            await Task.WhenAll(tasks);
            channel.Writer.Complete();
        }, cancellationToken);

        // Yield events as they arrive
        await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return evt;
        }
    }
}
