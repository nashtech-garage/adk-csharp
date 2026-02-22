// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
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
    private readonly RunConfig _runConfig;
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    public Runner(IAgent agent, IInvocationContextFactory? contextFactory = null, RunConfig? runConfig = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _contextFactory = contextFactory ?? new InvocationContextFactory();
        _runConfig = runConfig ?? new RunConfig { StreamingMode = StreamingMode.Sse };
    }

    /// <summary>
    /// Run the agent with user input and return final text result
    /// </summary>
    public async Task<string> RunAsync(
        string userInput,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var id = sessionId ?? Guid.NewGuid().ToString();
        var session = _sessions.GetOrAdd(id, _ => new InMemorySession(id));
        var context = _contextFactory.Create(session, userInput: userInput, runConfig: _runConfig);

        var finalText = new System.Text.StringBuilder();

        await foreach (var evt in _agent.RunAsync(context, cancellationToken))
        {
            // CRITICAL: Append event to session history so tool execution doesn't infinite loop
            if (!evt.Partial)
            {
                session.Events.Add(evt);
            }

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
        var id = sessionId ?? Guid.NewGuid().ToString();
        var session = _sessions.GetOrAdd(id, _ => new InMemorySession(id));
        var context = _contextFactory.Create(session, userInput: userInput, runConfig: _runConfig);

        await foreach (var evt in _agent.RunAsync(context, cancellationToken))
        {
            if (!evt.Partial)
            {
                session.Events.Add(evt);
            }
            yield return evt;
        }
    }
}
