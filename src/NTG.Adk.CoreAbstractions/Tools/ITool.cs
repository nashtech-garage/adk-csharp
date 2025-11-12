// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Tools;

/// <summary>
/// Port interface for tools that agents can use.
/// Equivalent to google.adk.tools.BaseTool in Python.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name of the tool
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of what the tool does
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Get the function declaration for this tool (for LLM).
    /// Returns null for built-in tools that don't need declaration (e.g., GoogleSearch for Gemini).
    /// Equivalent to _get_declaration() in Python.
    /// </summary>
    IFunctionDeclaration? GetDeclaration();

    /// <summary>
    /// Execute the tool with given arguments
    /// </summary>
    /// <param name="args">Arguments from the LLM</param>
    /// <param name="context">Tool execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tool result</returns>
    Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Function declaration interface
/// </summary>
public interface IFunctionDeclaration
{
    string Name { get; }
    string? Description { get; }
    ISchema? Parameters { get; }
    ISchema? Response { get; }
}

/// <summary>
/// Schema interface
/// </summary>
public interface ISchema
{
    string Type { get; }
    IReadOnlyDictionary<string, ISchemaProperty>? Properties { get; }
    IReadOnlyList<string>? Required { get; }
}

/// <summary>
/// Schema property interface
/// </summary>
public interface ISchemaProperty
{
    string Type { get; }
    string? Description { get; }
    IReadOnlyList<string>? Enum { get; }
    ISchemaProperty? Items { get; }
    IReadOnlyDictionary<string, ISchemaProperty>? Properties { get; }
    IReadOnlyList<string>? Required { get; }
}

/// <summary>
/// Tool execution context
/// </summary>
public interface IToolContext
{
    /// <summary>
    /// The current session for this invocation.
    /// Provides access to session ID, app name, user ID, and session state.
    /// Matches Python ADK's context.session property.
    /// </summary>
    Sessions.ISession Session { get; }

    /// <summary>
    /// Session state (read/write access)
    /// </summary>
    Sessions.ISessionState State { get; }

    /// <summary>
    /// User making the request
    /// </summary>
    string? User { get; }

    /// <summary>
    /// Additional context metadata
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }

    /// <summary>
    /// Actions that tools can set to control execution flow.
    /// Tools can set actions.TransferTo to transfer to another agent.
    /// </summary>
    IToolActions Actions { get; }
}

/// <summary>
/// Actions that tools can set during execution
/// </summary>
public interface IToolActions
{
    /// <summary>
    /// Request transfer to another agent by name.
    /// Set this to transfer control to a different agent.
    /// </summary>
    string? TransferToAgent { get; set; }

    /// <summary>
    /// Request escalation (e.g., to exit a loop)
    /// </summary>
    bool Escalate { get; set; }

    /// <summary>
    /// Skip LLM summarization of tool result
    /// </summary>
    bool SkipSummarization { get; set; }
}
