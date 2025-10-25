// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Agents;

/// <summary>
/// Callback hooks for agent execution lifecycle.
/// Equivalent to before_model_callback and after_model_callback in Python ADK.
/// </summary>
public interface IAgentCallbacks
{
    /// <summary>
    /// Called before the LLM is invoked.
    /// Can modify the request or return early to skip LLM call.
    /// </summary>
    /// <param name="context">Callback context with session access</param>
    /// <param name="request">The LLM request (can be modified)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optional content to return instead of calling LLM. Null to proceed normally.</returns>
    Task<IContent?> BeforeModelAsync(
        ICallbackContext context,
        ILlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the LLM returns a response.
    /// Can modify or replace the response.
    /// </summary>
    /// <param name="context">Callback context with session access</param>
    /// <param name="response">The LLM response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Optional content to return instead of using LLM response. Null to use response normally.</returns>
    Task<IContent?> AfterModelAsync(
        ICallbackContext context,
        ILlmResponse response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before a tool is executed.
    /// </summary>
    /// <param name="context">Callback context</param>
    /// <param name="toolName">Tool name</param>
    /// <param name="args">Tool arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnToolStartAsync(
        ICallbackContext context,
        string toolName,
        IReadOnlyDictionary<string, object> args,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a tool completes execution.
    /// </summary>
    /// <param name="context">Callback context</param>
    /// <param name="toolName">Tool name</param>
    /// <param name="result">Tool result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task OnToolEndAsync(
        ICallbackContext context,
        string toolName,
        object result,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Callback context providing access to session and state.
/// Equivalent to google.adk.agents.CallbackContext in Python.
/// </summary>
public interface ICallbackContext
{
    /// <summary>
    /// Current session
    /// </summary>
    ISession Session { get; }

    /// <summary>
    /// Agent name
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// User input for this invocation
    /// </summary>
    string? UserInput { get; }

    /// <summary>
    /// Metadata
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }
}
