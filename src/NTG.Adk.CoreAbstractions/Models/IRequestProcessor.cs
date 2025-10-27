// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Models;

/// <summary>
/// Port interface for processing LLM requests before execution.
/// Allows modification of system instructions, tools, and conversation history.
/// Must return new request instance to respect immutability.
/// </summary>
public interface IRequestProcessor
{
    /// <summary>
    /// Process and potentially transform the LLM request
    /// </summary>
    /// <param name="request">Original request</param>
    /// <param name="context">Invocation context</param>
    /// <returns>Transformed request (may be same or new instance)</returns>
    Task<ILlmRequest> ProcessAsync(ILlmRequest request, IInvocationContext context);

    /// <summary>
    /// Execution priority (lower number runs first)
    /// </summary>
    int Priority => 100;
}
