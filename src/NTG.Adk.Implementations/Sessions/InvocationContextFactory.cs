// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Default factory for creating invocation contexts.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class InvocationContextFactory : IInvocationContextFactory
{
    public IInvocationContext Create(
        ISession session,
        string? userInput = null,
        IContent? userMessage = null,
        IArtifactService? artifactService = null,
        IMemoryService? memoryService = null,
        RunConfig? runConfig = null)
    {
        return new InvocationContext
        {
            Session = session,
            Branch = "main",
            UserInput = userInput,
            UserMessage = userMessage,
            ArtifactService = artifactService,
            MemoryService = memoryService,
            RunConfig = runConfig ?? new RunConfig()
        };
    }
}
