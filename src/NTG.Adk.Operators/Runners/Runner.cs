// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Exceptions;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Sessions;

namespace NTG.Adk.Operators.Runners;

/// <summary>
/// Main orchestrator for agent execution.
/// Equivalent to google.adk.runners.Runner in Python.
///
/// The Runner manages session creation, invocation context setup,
/// and coordinates the execution of agents with integrated services.
/// </summary>
public class Runner
{
    public IAgent Agent { get; }
    public string AppName { get; }
    public ISessionService SessionService { get; }
    public IArtifactService? ArtifactService { get; }
    public IMemoryService? MemoryService { get; }
    public RunConfig RunConfig { get; }

    /// <summary>
    /// Create a new Runner instance.
    /// </summary>
    /// <param name="agent">The root agent to execute</param>
    /// <param name="appName">Application name for session management</param>
    /// <param name="sessionService">Session service for state persistence</param>
    /// <param name="artifactService">Optional artifact service for file storage</param>
    /// <param name="memoryService">Optional memory service for long-term memory</param>
    /// <param name="runConfig">Optional run configuration (defaults to MaxLlmCalls=500)</param>
    public Runner(
        IAgent agent,
        string appName,
        ISessionService sessionService,
        IArtifactService? artifactService = null,
        IMemoryService? memoryService = null,
        RunConfig? runConfig = null)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        SessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        ArtifactService = artifactService;
        MemoryService = memoryService;
        RunConfig = runConfig ?? new RunConfig();
    }

    /// <summary>
    /// Run the agent asynchronously.
    /// Main execution method that creates/gets session and runs the agent.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier (will create if doesn't exist)</param>
    /// <param name="userInput">User input to process</param>
    /// <param name="initialState">Initial state for new sessions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of events from agent execution</returns>
    public async IAsyncEnumerable<IEvent> RunAsync(
        string userId,
        string sessionId,
        string? userInput = null,
        IReadOnlyDictionary<string, object>? initialState = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Get or create session
        var session = await SessionService.GetSessionAsync(
            AppName,
            userId,
            sessionId,
            cancellationToken: cancellationToken);

        if (session == null)
        {
            // Create new session
            session = await SessionService.CreateSessionAsync(
                AppName,
                userId,
                initialState,
                sessionId,
                cancellationToken);
        }

        // Create invocation context
        var context = new InvocationContextImpl
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig
        };

        // Run agent and yield events
        await foreach (var evt in Agent.RunAsync(context, cancellationToken))
        {
            // Append event to session
            await SessionService.AppendEventAsync(session, evt, cancellationToken);

            yield return evt;
        }
    }

    /// <summary>
    /// Replay execution from a specific event in history.
    /// Useful for debugging or continuing from a checkpoint.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="fromEventIndex">Event index to replay from (0-based)</param>
    /// <param name="userInput">Optional new user input to continue with</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of events from replay point</returns>
    public async IAsyncEnumerable<IEvent> RewindAsync(
        string userId,
        string sessionId,
        int fromEventIndex,
        string? userInput = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Get session
        var session = await SessionService.GetSessionAsync(
            AppName,
            userId,
            sessionId,
            cancellationToken: cancellationToken);

        if (session == null)
        {
            throw new InvalidOperationException($"Session '{sessionId}' not found for user '{userId}'");
        }

        if (fromEventIndex < 0 || fromEventIndex >= session.Events.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(fromEventIndex),
                $"Event index {fromEventIndex} out of range (0-{session.Events.Count - 1})");
        }

        // First, yield historical events up to the rewind point
        for (int i = 0; i <= fromEventIndex; i++)
        {
            yield return session.Events[i];
        }

        // Create invocation context for continuation
        var context = new InvocationContextImpl
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig
        };

        // Continue execution from this point
        await foreach (var evt in Agent.RunAsync(context, cancellationToken))
        {
            // Append new event to session
            await SessionService.AppendEventAsync(session, evt, cancellationToken);

            yield return evt;
        }
    }
}

/// <summary>
/// Internal implementation of IInvocationContext.
/// </summary>
internal class InvocationContextImpl : IInvocationContext
{
    private int _numberOfLlmCalls = 0;

    public required ISession Session { get; init; }
    public required string Branch { get; init; }
    public string? UserInput { get; init; }
    public IArtifactService? ArtifactService { get; init; }
    public IMemoryService? MemoryService { get; init; }
    public RunConfig? RunConfig { get; init; }

    public int NumberOfLlmCalls => _numberOfLlmCalls;

    public void IncrementAndEnforceLlmCallsLimit()
    {
        _numberOfLlmCalls++;

        if (RunConfig != null
            && RunConfig.MaxLlmCalls > 0
            && _numberOfLlmCalls > RunConfig.MaxLlmCalls)
        {
            throw new LlmCallsLimitExceededError(
                $"Max number of LLM calls limit of {RunConfig.MaxLlmCalls} exceeded");
        }
    }

    public IInvocationContext WithBranch(string newBranch)
    {
        return new InvocationContextImpl
        {
            Session = Session,
            Branch = newBranch,
            UserInput = UserInput,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            _numberOfLlmCalls = _numberOfLlmCalls
        };
    }

    public IInvocationContext WithUserInput(string newUserInput)
    {
        return new InvocationContextImpl
        {
            Session = Session,
            Branch = Branch,
            UserInput = newUserInput,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            _numberOfLlmCalls = _numberOfLlmCalls
        };
    }
}
