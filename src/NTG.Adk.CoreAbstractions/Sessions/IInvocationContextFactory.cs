// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;

namespace NTG.Adk.CoreAbstractions.Sessions;

/// <summary>
/// Factory interface for creating invocation contexts.
/// Follows A.D.D V3: Port interface in CoreAbstractions.
/// Bootstrap layer wires concrete implementations.
/// </summary>
public interface IInvocationContextFactory
{
    /// <summary>
    /// Create an invocation context with the specified parameters.
    /// </summary>
    /// <param name="session">The session for this invocation</param>
    /// <param name="userInput">User input text (legacy API)</param>
    /// <param name="userMessage">User message with rich content (multimodal API)</param>
    /// <param name="artifactService">Optional artifact service</param>
    /// <param name="memoryService">Optional memory service</param>
    /// <param name="runConfig">Optional run configuration</param>
    /// <returns>A new invocation context instance</returns>
    IInvocationContext Create(
        ISession session,
        string? userInput = null,
        IContent? userMessage = null,
        IArtifactService? artifactService = null,
        IMemoryService? memoryService = null,
        RunConfig? runConfig = null);
}
