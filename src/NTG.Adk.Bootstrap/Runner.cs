// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Sessions;

namespace NTG.Adk.Bootstrap;

/// <summary>
/// Main entry point for running agents.
/// Bootstrap layer: composition root.
/// Equivalent to google.adk.runners.Runner in Python.
/// </summary>
public class Runner
{
    private readonly IAgent _agent;
    private readonly IInvocationContextFactory _contextFactory;

    public Runner(IAgent agent, IInvocationContextFactory? contextFactory = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _contextFactory = contextFactory ?? new InvocationContextFactory();
    }

    /// <summary>
    /// Run the agent with user input and return final text result
    /// </summary>
    public async Task<string> RunAsync(
        string userInput,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var session = new InMemorySession(sessionId);
        var context = _contextFactory.Create(session, userInput: userInput);

        var finalText = new System.Text.StringBuilder();

        await foreach (var evt in _agent.RunAsync(context, cancellationToken))
        {
            // Collect text from events
            if (evt.Content?.Parts != null)
            {
                foreach (var part in evt.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        finalText.AppendLine(part.Text);
                    }
                }
            }
        }

        return finalText.ToString();
    }

    /// <summary>
    /// Run the agent and stream events
    /// </summary>
    public async IAsyncEnumerable<IEvent> RunStreamAsync(
        string userInput,
        string? sessionId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var session = new InMemorySession(sessionId);
        var context = _contextFactory.Create(session, userInput: userInput);

        await foreach (var evt in _agent.RunAsync(context, cancellationToken))
        {
            yield return evt;
        }
    }
}
