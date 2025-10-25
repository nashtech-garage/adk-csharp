// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Boundary.Mcp;
using NTG.Adk.CoreAbstractions.Mcp;
using NTG.Adk.CoreAbstractions.Tools;
using ModelContextProtocol.Client;

namespace NTG.Adk.Implementations.Mcp;

/// <summary>
/// MCP toolset adapter that connects to MCP servers and provides tools.
/// Implements IMcpToolset port interface.
/// Based on Google ADK Python implementation.
/// </summary>
public sealed class McpToolset : IMcpToolset
{
    private readonly McpConnectionParams _connectionParams;
    private readonly Func<string, bool>? _toolFilter;
    private readonly string? _toolNamePrefix;
    private McpClient? _mcpClient;
    private bool _isDisposed;

    /// <summary>
    /// Creates a new MCP toolset.
    /// </summary>
    /// <param name="connectionParams">Connection parameters for MCP server</param>
    /// <param name="toolFilter">Optional predicate to filter tools by name</param>
    /// <param name="toolNamePrefix">Optional prefix to add to all tool names</param>
    public McpToolset(
        McpConnectionParams connectionParams,
        Func<string, bool>? toolFilter = null,
        string? toolNamePrefix = null)
    {
        _connectionParams = connectionParams ?? throw new ArgumentNullException(nameof(connectionParams));
        _toolFilter = toolFilter;
        _toolNamePrefix = toolNamePrefix;
    }

    public bool IsConnected => _mcpClient != null;

    /// <summary>
    /// Connect to the MCP server.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(McpToolset));

        if (_mcpClient != null)
            return; // Already connected

        // Create transport based on connection params
        IClientTransport transport = _connectionParams switch
        {
            StdioConnectionParams stdio => CreateStdioTransport(stdio),
            SseConnectionParams sse => CreateSseTransport(sse),
            HttpConnectionParams http => CreateHttpTransport(http),
            _ => throw new NotSupportedException(
                $"Connection type '{_connectionParams.Type}' not supported")
        };

        // Create MCP client (no CancellationToken parameter)
        _mcpClient = await McpClient.CreateAsync(transport);
    }

    /// <summary>
    /// Get all tools from the MCP server.
    /// </summary>
    public async Task<IReadOnlyList<ITool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(McpToolset));

        if (_mcpClient == null)
            throw new InvalidOperationException(
                "Not connected to MCP server. Call ConnectAsync first.");

        // List tools from MCP server (returns IList<McpClientTool>)
        // ListToolsAsync accepts optional JsonSerializerOptions (not CancellationToken)
        var mcpClientTools = await _mcpClient.ListToolsAsync();

        // Filter tools if predicate provided
        var filteredTools = _toolFilter != null
            ? mcpClientTools.Where(t => _toolFilter(t.Name)).ToList()
            : mcpClientTools.ToList();

        // Convert MCP tools to ADK tools
        var adkTools = filteredTools
            .Select(mcpTool => (ITool)new McpTool(mcpTool, _toolNamePrefix))
            .ToList();

        return adkTools.AsReadOnly();
    }

    /// <summary>
    /// Dispose resources and close MCP connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_mcpClient != null)
        {
            // McpClient implements IAsyncDisposable
            await _mcpClient.DisposeAsync();
            _mcpClient = null;
        }
    }

    // Transport factory methods

    private static IClientTransport CreateStdioTransport(StdioConnectionParams stdio)
    {
        var options = new StdioClientTransportOptions
        {
            Command = stdio.Command,
            Arguments = stdio.Arguments?.ToList() ?? new List<string>()
        };

        // Set environment variables if provided
        if (stdio.Environment != null)
        {
            options.EnvironmentVariables = new Dictionary<string, string?>();
            foreach (var kvp in stdio.Environment)
            {
                options.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        // Set working directory if provided
        if (!string.IsNullOrEmpty(stdio.WorkingDirectory))
        {
            options.WorkingDirectory = stdio.WorkingDirectory;
        }

        return new StdioClientTransport(options);
    }

    private static IClientTransport CreateSseTransport(SseConnectionParams sse)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = sse.Url,
            TransportMode = HttpTransportMode.Sse
        };

        // Set timeout if provided
        if (sse.Timeout.HasValue)
        {
            options.ConnectionTimeout = sse.Timeout.Value;
        }

        // Add headers if provided
        if (sse.Headers != null)
        {
            options.AdditionalHeaders = new Dictionary<string, string>(sse.Headers);
        }

        return new HttpClientTransport(options);
    }

    private static IClientTransport CreateHttpTransport(HttpConnectionParams http)
    {
        var options = new HttpClientTransportOptions
        {
            Endpoint = http.Url,
            TransportMode = HttpTransportMode.StreamableHttp
        };

        // Set timeout if provided
        if (http.Timeout.HasValue)
        {
            options.ConnectionTimeout = http.Timeout.Value;
        }

        // Add headers if provided
        if (http.Headers != null)
        {
            options.AdditionalHeaders = new Dictionary<string, string>(http.Headers);
        }

        return new HttpClientTransport(options);
    }
}
