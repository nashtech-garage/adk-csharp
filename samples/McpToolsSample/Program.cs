// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Boundary.Mcp;
using NTG.Adk.Implementations.Mcp;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

Console.WriteLine("========================================");
Console.WriteLine("  NTG.Adk - MCP Tools Demo");
Console.WriteLine("========================================\n");

// Demo 1: Connect to MCP Server and List Tools
Console.WriteLine("==> Demo 1: Connect to MCP Server via Stdio\n");

// Example: Connect to an MCP server (e.g., filesystem MCP server)
// To run this demo, you need an MCP server executable
// For example: npx -y @modelcontextprotocol/server-filesystem /tmp

var mcpServerCommand = Environment.GetEnvironmentVariable("MCP_SERVER_COMMAND");
var mcpServerArgs = Environment.GetEnvironmentVariable("MCP_SERVER_ARGS")?.Split(' ');

if (string.IsNullOrEmpty(mcpServerCommand))
{
    Console.WriteLine("Skipped: Set MCP_SERVER_COMMAND environment variable");
    Console.WriteLine("Example: npx (for Node.js MCP servers)");
    Console.WriteLine("         MCP_SERVER_ARGS='-y @modelcontextprotocol/server-filesystem /tmp'");
    Console.WriteLine("\nAlternatively, you can use any MCP server implementation.");
}
else
{
    Console.WriteLine($"Connecting to MCP server: {mcpServerCommand}");
    Console.WriteLine($"Arguments: {string.Join(" ", mcpServerArgs ?? Array.Empty<string>())}");

    // Create MCP connection params for stdio transport
    var connectionParams = new StdioConnectionParams
    {
        Command = mcpServerCommand,
        Arguments = mcpServerArgs ?? Array.Empty<string>()
    };

    // Create MCP toolset
    var mcpToolset = new McpToolset(connectionParams);

    try
    {
        // Connect to MCP server
        await mcpToolset.ConnectAsync();
        Console.WriteLine("✓ Connected to MCP server\n");

        // Get tools from MCP server
        var tools = await mcpToolset.GetToolsAsync();
        Console.WriteLine($"Available tools from MCP server: {tools.Count}\n");

        foreach (var tool in tools)
        {
            Console.WriteLine($"  - {tool.Name}");
            if (!string.IsNullOrEmpty(tool.Description))
            {
                Console.WriteLine($"    {tool.Description}");
            }
        }

        // Demo 2: Use MCP Tools with ADK Agent
        Console.WriteLine("\n==> Demo 2: Agent with MCP Tools\n");

        var llm = new MockLlm();
        var mcpAgent = new LlmAgent(llm, "mock-llm")
        {
            Name = "McpAssistant",
            Instruction = "You are an assistant with access to MCP server tools.",
            Tools = tools.ToList()
        };

        var runner = new InMemoryRunner(mcpAgent, appName: "McpToolsApp");

        Console.WriteLine($"Agent: {mcpAgent.Name} with {mcpAgent.Tools.Count} MCP tool(s)");
        Console.WriteLine("Query: List available tools and their capabilities\n");

        await foreach (var evt in runner.RunAsync(
            userId: "demo_user",
            sessionId: "demo_session_1",
            userInput: "What tools do you have available?"))
        {
            if (evt.Content?.Parts != null)
            {
                foreach (var part in evt.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        Console.WriteLine($"[{evt.Author}] {part.Text}");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Type: {ex.GetType().Name}");
    }
    finally
    {
        // Clean up MCP connection
        await mcpToolset.DisposeAsync();
        Console.WriteLine("\n✓ MCP connection closed");
    }
}

// Demo 3: MCP Toolset with Tool Name Prefix
Console.WriteLine("\n==> Demo 3: MCP Toolset with Tool Filtering & Prefix\n");

Console.WriteLine("McpToolset supports:");
Console.WriteLine("  - Tool filtering (predicate-based)");
Console.WriteLine("  - Tool name prefixing (namespace isolation)");
Console.WriteLine("  - Multiple transport types:");
Console.WriteLine("    • Stdio (process-based)");
Console.WriteLine("    • SSE (Server-Sent Events)");
Console.WriteLine("    • HTTP (Streamable HTTP)");

Console.WriteLine("\nExample: Filter tools by name pattern");
Console.WriteLine("var mcpToolset = new McpToolset(");
Console.WriteLine("    connectionParams,");
Console.WriteLine("    toolFilter: name => name.StartsWith(\"file_\"),");
Console.WriteLine("    toolNamePrefix: \"mcp_\");");

Console.WriteLine("\n========================================");
Console.WriteLine("  Demo completed!");
Console.WriteLine("========================================");
