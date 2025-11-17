// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
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
    /// User input for this invocation (legacy string API)
    /// </summary>
    string? UserInput { get; }

    /// <summary>
    /// User message for this invocation (rich content API - supports multimodal)
    /// Equivalent to new_message in Python ADK
    /// </summary>
    Events.IContent? UserMessage { get; }

    /// <summary>
    /// Artifact service for file storage and versioning
    /// </summary>
    IArtifactService? ArtifactService { get; }

    /// <summary>
    /// Memory service for long-term agent memory
    /// </summary>
    IMemoryService? MemoryService { get; }

    /// <summary>
    /// Configuration for agent execution (includes MaxLlmCalls limit)
    /// </summary>
    RunConfig? RunConfig { get; }

    /// <summary>
    /// Number of LLM calls made in this invocation
    /// </summary>
    int NumberOfLlmCalls { get; }

    /// <summary>
    /// Increment LLM call counter and enforce limit.
    /// Throws LlmCallsLimitExceededError if limit exceeded.
    /// </summary>
    void IncrementAndEnforceLlmCallsLimit();

    /// <summary>
    /// Create a new context with a different branch
    /// </summary>
    IInvocationContext WithBranch(string newBranch);

    /// <summary>
    /// Create a new context with different user input
    /// </summary>
    IInvocationContext WithUserInput(string newUserInput);

    /// <summary>
    /// Create a new context with different user message (rich content)
    /// </summary>
    IInvocationContext WithUserMessage(Events.IContent newUserMessage);
}
