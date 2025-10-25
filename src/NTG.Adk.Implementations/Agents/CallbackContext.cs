// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.Implementations.Agents;

/// <summary>
/// Implementation of ICallbackContext.
/// Provides context for callback execution.
/// </summary>
public class CallbackContext : ICallbackContext
{
    public required ISession Session { get; init; }
    public required string AgentName { get; init; }
    public string? UserInput { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    public static CallbackContext Create(
        ISession session,
        string agentName,
        string? userInput = null,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        return new CallbackContext
        {
            Session = session,
            AgentName = agentName,
            UserInput = userInput,
            Metadata = metadata
        };
    }
}
