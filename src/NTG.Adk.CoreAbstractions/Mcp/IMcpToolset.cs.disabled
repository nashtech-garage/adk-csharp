// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.CoreAbstractions.Mcp;

/// <summary>
/// Port interface for MCP (Model Context Protocol) toolset.
/// Connects to MCP servers and retrieves tools.
/// Equivalent to google.adk.tools.McpToolset in Python.
/// </summary>
public interface IMcpToolset : IAsyncDisposable
{
    /// <summary>
    /// Get all tools from the connected MCP server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tools available from MCP server</returns>
    Task<IReadOnlyList<ITool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Connect to the MCP server.
    /// Must be called before GetToolsAsync.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if connected to MCP server.
    /// </summary>
    bool IsConnected { get; }
}
