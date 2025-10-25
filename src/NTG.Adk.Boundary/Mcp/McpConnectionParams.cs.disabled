// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

namespace NTG.Adk.Boundary.Mcp;

/// <summary>
/// Base class for MCP connection parameters.
/// Based on Google ADK Python implementation.
/// </summary>
public abstract record McpConnectionParams
{
    /// <summary>
    /// Type of connection (stdio, sse, http)
    /// </summary>
    public abstract string Type { get; }
}

/// <summary>
/// Stdio connection parameters for MCP servers.
/// Launches MCP server as subprocess.
/// </summary>
public sealed record StdioConnectionParams : McpConnectionParams
{
    public override string Type => "stdio";

    /// <summary>
    /// Command to execute (e.g., "node", "python", "dotnet")
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Arguments for the command
    /// </summary>
    public string[]? Arguments { get; init; }

    /// <summary>
    /// Environment variables for the process
    /// </summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>
    /// Working directory for the process
    /// </summary>
    public string? WorkingDirectory { get; init; }
}

/// <summary>
/// SSE (Server-Sent Events) connection parameters for MCP servers.
/// Connects to MCP server via HTTP SSE.
/// </summary>
public sealed record SseConnectionParams : McpConnectionParams
{
    public override string Type => "sse";

    /// <summary>
    /// SSE endpoint URL
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// HTTP headers for authentication
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Timeout for connection
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// HTTP connection parameters for MCP servers.
/// Connects to MCP server via standard HTTP.
/// </summary>
public sealed record HttpConnectionParams : McpConnectionParams
{
    public override string Type => "http";

    /// <summary>
    /// HTTP endpoint URL
    /// </summary>
    public required Uri Url { get; init; }

    /// <summary>
    /// HTTP headers for authentication
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Timeout for requests
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
