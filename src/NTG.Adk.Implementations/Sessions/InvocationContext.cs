// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Artifacts;
using NTG.Adk.CoreAbstractions.Memory;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Sessions;

/// <summary>
/// Implementation of IInvocationContext.
/// Immutable record for context passing.
/// </summary>
public record InvocationContext : IInvocationContext
{
    public required ISession Session { get; init; }
    public required string Branch { get; init; }
    public string? UserInput { get; init; }
    public IArtifactService? ArtifactService { get; init; }
    public IMemoryService? MemoryService { get; init; }

    public IInvocationContext WithBranch(string newBranch) => this with { Branch = newBranch };

    public IInvocationContext WithUserInput(string newUserInput) => this with { UserInput = newUserInput };

    /// <summary>
    /// Create initial context
    /// </summary>
    public static InvocationContext Create(string? sessionId = null, string? userInput = null)
    {
        return new InvocationContext
        {
            Session = new InMemorySession(sessionId),
            Branch = "root",
            UserInput = userInput
        };
    }

    /// <summary>
    /// Create with existing session
    /// </summary>
    public static InvocationContext Create(ISession session, string? userInput = null)
    {
        return new InvocationContext
        {
            Session = session,
            Branch = "root",
            UserInput = userInput
        };
    }
}
