// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.Implementations.Artifacts;
using NTG.Adk.Implementations.Memory;
using NTG.Adk.Implementations.Sessions;

namespace NTG.Adk.Operators.Runners;

/// <summary>
/// Lightweight runner with auto-initialized in-memory services.
/// Equivalent to google.adk.runners.InMemoryRunner in Python.
///
/// Perfect for testing, development, and single-machine execution.
/// All services (session, artifact, memory) are automatically created
/// with in-memory implementations - no external dependencies required.
/// </summary>
public class InMemoryRunner : Runner
{
    /// <summary>
    /// Create a new InMemoryRunner with auto-initialized services.
    /// </summary>
    /// <param name="agent">The root agent to execute</param>
    /// <param name="appName">Application name (defaults to "InMemoryRunner")</param>
    /// <param name="runConfig">Optional run configuration (streaming mode, max LLM calls)</param>
    public InMemoryRunner(
        IAgent agent,
        string? appName = null,
        RunConfig? runConfig = null)
        : base(
            agent: agent,
            appName: appName ?? "InMemoryRunner",
            sessionService: new InMemorySessionService(),
            artifactService: new InMemoryArtifactService(),
            memoryService: new InMemoryMemoryService(),
            runConfig: runConfig)
    {
    }

    /// <summary>
    /// Get the session service (for direct access if needed).
    /// </summary>
    public InMemorySessionService InMemorySessionService => (InMemorySessionService)SessionService;

    /// <summary>
    /// Get the artifact service (for direct access if needed).
    /// </summary>
    public InMemoryArtifactService InMemoryArtifactService => (InMemoryArtifactService)ArtifactService!;

    /// <summary>
    /// Get the memory service (for direct access if needed).
    /// </summary>
    public InMemoryMemoryService InMemoryMemoryService => (InMemoryMemoryService)MemoryService!;
}
