// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Exceptions;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Default implementation of IInvocationContext.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class InvocationContext : IInvocationContext
{
    private int _numberOfLlmCalls = 0;

    public required ISession Session { get; init; }
    public required string Branch { get; init; }
    public string? UserInput { get; init; }
    public IContent? UserMessage { get; init; }
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
        return new InvocationContext
        {
            Session = Session,
            Branch = newBranch,
            UserInput = UserInput,
            UserMessage = UserMessage,
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
            UserMessage = UserMessage,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            _numberOfLlmCalls = _numberOfLlmCalls
        };
    }

    public IInvocationContext WithUserMessage(IContent newUserMessage)
    {
        return new InvocationContext
        {
            Session = Session,
            Branch = Branch,
            UserInput = UserInput,
            UserMessage = newUserMessage,
            ArtifactService = ArtifactService,
            MemoryService = MemoryService,
            RunConfig = RunConfig,
            _numberOfLlmCalls = _numberOfLlmCalls
        };
    }
}
