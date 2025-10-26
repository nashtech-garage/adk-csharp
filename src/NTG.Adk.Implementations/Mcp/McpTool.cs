// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Tools;
using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;

namespace NTG.Adk.Implementations.Mcp;

/// <summary>
/// Adapter that wraps an MCP McpClientTool as an ITool.
/// Enables ADK agents to call MCP server tools.
/// Based on Google ADK Python implementation.
/// </summary>
internal sealed class McpTool : ITool
{
    private readonly McpClientTool _mcpClientTool;
    private readonly FunctionDeclaration _declaration;
    private readonly string? _toolNamePrefix;

    public string Name { get; }
    public string? Description { get; }

    public McpTool(McpClientTool mcpClientTool, string? toolNamePrefix = null)
    {
        _mcpClientTool = mcpClientTool ?? throw new ArgumentNullException(nameof(mcpClientTool));
        _toolNamePrefix = toolNamePrefix;

        // Apply prefix to tool name
        Name = string.IsNullOrEmpty(toolNamePrefix)
            ? mcpClientTool.Name
            : $"{toolNamePrefix}{mcpClientTool.Name}";

        Description = mcpClientTool.Description;

        // Convert MCP tool schema to ADK function declaration
        _declaration = McpSchemaConverter.ConvertAIFunctionToDeclaration(mcpClientTool, Name);
    }

    public IFunctionDeclaration GetDeclaration()
    {
        return new FunctionDeclarationAdapter(_declaration);
    }

    public async Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert ADK args to AIFunctionArguments
            var aiArgs = new AIFunctionArguments();
            foreach (var kvp in args)
            {
                aiArgs[kvp.Key] = kvp.Value;
            }

            // Call MCP tool via AIFunction.InvokeAsync
            var result = await _mcpClientTool.InvokeAsync(aiArgs, cancellationToken);

            // Result is already an object, return directly
            return result ?? new { success = true };
        }
        catch (Exception ex)
        {
            // Return error result
            return new
            {
                success = false,
                error = ex.Message,
                errorType = ex.GetType().Name
            };
        }
    }
}

/// <summary>
/// Adapter for FunctionDeclaration DTO to IFunctionDeclaration port.
/// </summary>
internal sealed class FunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public FunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null ? new SchemaAdapter(_dto.Parameters) : null;
    public ISchema? Response => _dto.Response != null ? new SchemaAdapter(_dto.Response) : null;
}

internal sealed class SchemaAdapter : ISchema
{
    private readonly Schema _dto;

    public SchemaAdapter(Schema dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new SchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}

internal sealed class SchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public SchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
    public ISchemaProperty? Items => _dto.Items != null ? new SchemaPropertyAdapter(_dto.Items) : null;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new SchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}
