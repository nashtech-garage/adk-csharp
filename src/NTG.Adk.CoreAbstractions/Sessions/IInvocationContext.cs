// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Memory;

namespace NTG.Adk.CoreAbstractions.Sessions;

/// <summary>
/// Port interface for invocation context.
/// Equivalent to google.adk.agents.InvocationContext in Python.
/// </summary>
public interface IInvocationContext
{
    /// <summary>
    /// The session for this invocation
    /// </summary>
    ISession Session { get; }

    /// <summary>
    /// The current branch/path in the agent hierarchy
    /// Used by ParallelAgent to isolate contexts
    /// </summary>
    string Branch { get; }

    /// <summary>
    /// User input for this invocation
    /// </summary>
    string? UserInput { get; }

    /// <summary>
    /// Artifact service for file storage and versioning
    /// </summary>
    IArtifactService? ArtifactService { get; }

    /// <summary>
    /// Memory service for long-term agent memory
    /// </summary>
    IMemoryService? MemoryService { get; }

    /// <summary>
    /// Create a new context with a different branch
    /// </summary>
    IInvocationContext WithBranch(string newBranch);

    /// <summary>
    /// Create a new context with different user input
    /// </summary>
    IInvocationContext WithUserInput(string newUserInput);
}
