// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Exceptions;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Compaction;
using NTG.Adk.Implementations.Sessions;
using NTG.Adk.Implementations.Events;

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
    private readonly IInvocationContextFactory? _contextFactory;

    /// <summary>
    /// Create a new Runner instance.
    /// </summary>
    /// <param name="agent">The root agent to execute</param>
    /// <param name="appName">Application name for session management</param>
    /// <param name="sessionService">Session service for state persistence</param>
    /// <param name="artifactService">Optional artifact service for file storage</param>
    /// <param name="memoryService">Optional memory service for long-term memory</param>
    /// <param name="runConfig">Optional run configuration (defaults to MaxLlmCalls=500)</param>
    /// <param name="contextFactory">Optional factory for creating invocation contexts (DI pattern)</param>
    public Runner(
        IAgent agent,
        string appName,
        ISessionService sessionService,
        IArtifactService? artifactService = null,
        IMemoryService? memoryService = null,
        RunConfig? runConfig = null,
        IInvocationContextFactory? contextFactory = null)
    {
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        AppName = appName ?? throw new ArgumentNullException(nameof(appName));
        SessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        ArtifactService = artifactService;
        MemoryService = memoryService;
        RunConfig = runConfig ?? new RunConfig();
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Run the agent asynchronously.
    /// Main execution method that creates/gets session and runs the agent.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier (will create if doesn't exist)</param>
    /// <param name="userInput">User input to process</param>
    /// <param name="initialState">Initial state for new sessions</param>
    /// <param name="metadata">Optional metadata for invocation (e.g., streaming callbacks)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of events from agent execution</returns>
    public async IAsyncEnumerable<IEvent> RunAsync(
        string userId,
        string sessionId,
        string? userInput = null,
        IReadOnlyDictionary<string, object>? initialState = null,
        IReadOnlyDictionary<string, object>? metadata = null,
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
        var context = _contextFactory?.Create(
            session,
            userInput: userInput,
            userMessage: null,
            artifactService: ArtifactService,
            memoryService: MemoryService,
            runConfig: RunConfig,
            metadata: metadata)
        ?? new InvocationContext
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            UserMessage = null,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            Metadata = metadata
        };

        // Run agent and yield events
        await foreach (var evt in Agent.RunAsync(context, cancellationToken))
        {
            // Append event to session
            await SessionService.AppendEventAsync(session, evt, cancellationToken);

            yield return evt;
        }

        // Run compaction after agent finishes (Python ADK approach)
        if (RunConfig.EventsCompactionConfig != null)
        {
            await CompactionService.RunCompactionIfNeededAsync(
                session,
                SessionService,
                RunConfig.EventsCompactionConfig,
                cancellationToken);
        }
    }

    /// <summary>
    /// Run the agent with rich content message (Boundary DTO convenience overload).
    /// </summary>
    public async IAsyncEnumerable<IEvent> RunAsync(
        string userId,
        string sessionId,
        Boundary.Events.Content userMessage,
        IReadOnlyDictionary<string, object>? initialState = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Convert Boundary DTO to IContent interface via adapter
        IContent contentAdapter = new ContentAdapter(userMessage);

        await foreach (var evt in RunAsync(userId, sessionId, contentAdapter, initialState, metadata, cancellationToken))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Run the agent with rich content message (supports multimodal - images, text, etc.).
    /// Equivalent to run_async(new_message=...) in Python ADK.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sessionId">Session identifier (will create if doesn't exist)</param>
    /// <param name="userMessage">Rich content message with text and/or images</param>
    /// <param name="initialState">Initial state for new sessions</param>
    /// <param name="metadata">Optional metadata for invocation (e.g., streaming callbacks)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of events from agent execution</returns>
    public async IAsyncEnumerable<IEvent> RunAsync(
        string userId,
        string sessionId,
        IContent userMessage,
        IReadOnlyDictionary<string, object>? initialState = null,
        IReadOnlyDictionary<string, object>? metadata = null,
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

        // Create invocation context with rich content
        var context = _contextFactory?.Create(
            session,
            userInput: null,
            userMessage: userMessage,
            artifactService: ArtifactService,
            memoryService: MemoryService,
            runConfig: RunConfig,
            metadata: metadata)
        ?? new InvocationContext
        {
            Session = session,
            Branch = "main",
            UserInput = null,
            UserMessage = userMessage,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            Metadata = metadata
        };

        // Run agent and yield events
        await foreach (var evt in Agent.RunAsync(context, cancellationToken))
        {
            // Append event to session
            await SessionService.AppendEventAsync(session, evt, cancellationToken);

            yield return evt;
        }

        // Run compaction after agent finishes (Python ADK approach)
        if (RunConfig.EventsCompactionConfig != null)
        {
            await CompactionService.RunCompactionIfNeededAsync(
                session,
                SessionService,
                RunConfig.EventsCompactionConfig,
                cancellationToken);
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
        var context = _contextFactory?.Create(
            session,
            userInput: userInput,
            userMessage: null,
            artifactService: ArtifactService,
            memoryService: MemoryService,
            runConfig: RunConfig)
        ?? new InvocationContext
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            UserMessage = null,
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

        // Run compaction after agent finishes (Python ADK approach)
        if (RunConfig.EventsCompactionConfig != null)
        {
            await CompactionService.RunCompactionIfNeededAsync(
                session,
                SessionService,
                RunConfig.EventsCompactionConfig,
                cancellationToken);
        }
    }
}
