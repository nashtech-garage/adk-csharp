// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Exceptions;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Implementation of IInvocationContext.
/// Includes LLM call tracking for enforcement.
/// </summary>
public class InvocationContext : IInvocationContext
{
    private int _numberOfLlmCalls = 0;

    public required ISession Session { get; init; }
    public required string Branch { get; init; }
    public string? UserInput { get; init; }
    public IArtifactService? ArtifactService { get; init; }
    public IMemoryService? MemoryService { get; init; }
    public RunConfig? RunConfig { get; init; }

    public int NumberOfLlmCalls => _numberOfLlmCalls;

    /// <summary>
    /// Increment LLM call counter and enforce limit.
    /// Throws LlmCallsLimitExceededError if limit exceeded.
    /// Equivalent to increment_and_enforce_llm_calls_limit in Python ADK.
    /// </summary>
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
        return new InvocationContext
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
        return new InvocationContext
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

    /// <summary>
    /// Create initial context
    /// </summary>
    public static InvocationContext Create(
        string? sessionId = null,
        string? userInput = null,
        RunConfig? runConfig = null)
    {
        return new InvocationContext
        {
            Session = new InMemorySession(sessionId),
            Branch = "root",
            UserInput = userInput,
            RunConfig = runConfig ?? new RunConfig()
        };
    }

    /// <summary>
    /// Create with existing session
    /// </summary>
    public static InvocationContext Create(
        ISession session,
        string? userInput = null,
        RunConfig? runConfig = null)
    {
        return new InvocationContext
        {
            Session = session,
            Branch = "root",
            UserInput = userInput,
            RunConfig = runConfig ?? new RunConfig()
        };
    }
}
