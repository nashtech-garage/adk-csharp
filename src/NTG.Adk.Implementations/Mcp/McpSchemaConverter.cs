// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NTG.Adk.Boundary.Tools;
using Microsoft.Extensions.AI;

namespace NTG.Adk.Implementations.Mcp;

/// <summary>
/// Converts MCP AIFunction metadata to ADK function declaration schemas.
/// Based on Google ADK Python implementation.
/// </summary>
internal static class McpSchemaConverter
{
    /// <summary>
    /// Convert AIFunction (McpClientTool) to ADK FunctionDeclaration.
    /// </summary>
    public static FunctionDeclaration ConvertAIFunctionToDeclaration(AIFunction aiFunction, string? nameOverride = null)
    {
        // Build schema from AIFunction JsonSchema (JsonElement type)
        var schema = aiFunction.JsonSchema.ValueKind != JsonValueKind.Undefined &&
                     aiFunction.JsonSchema.ValueKind != JsonValueKind.Null
            ? ConvertJsonElementToSchema(aiFunction.JsonSchema)
            : new Schema
            {
                Type = "object",
                Properties = new Dictionary<string, SchemaProperty>(),
                Required = null
            };

        return new FunctionDeclaration
        {
            Name = nameOverride ?? aiFunction.Name,
            Description = aiFunction.Description,
            Parameters = schema
        };
    }

    /// <summary>
    /// Convert JsonElement schema to ADK Schema.
    /// </summary>
    private static Schema ConvertJsonElementToSchema(JsonElement jsonElement)
    {
        var schemaType = GetPropertyString(jsonElement, "type") ?? "object";
        var properties = new Dictionary<string, SchemaProperty>();
        var requiredList = new List<string>();

        // Extract properties
        if (jsonElement.TryGetProperty("properties", out var propsElement) &&
            propsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                properties[prop.Name] = ConvertJsonPropertyToSchemaProperty(prop.Value);
            }
        }

        // Extract required fields
        if (jsonElement.TryGetProperty("required", out var requiredElement) &&
            requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in requiredElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    requiredList.Add(item.GetString()!);
                }
            }
        }

        return new Schema
        {
            Type = schemaType,
            Properties = properties.Count > 0 ? properties : null,
            Required = requiredList.Count > 0 ? requiredList : null
        };
    }

    /// <summary>
    /// Convert MCP property JSON to ADK SchemaProperty.
    /// </summary>
    private static SchemaProperty ConvertJsonPropertyToSchemaProperty(JsonElement propJson)
    {
        var type = GetPropertyString(propJson, "type") ?? "string";
        var description = GetPropertyString(propJson, "description");
        var enumValues = new List<string>();

        // Extract enum values if present
        if (propJson.TryGetProperty("enum", out var enumElement) &&
            enumElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in enumElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    enumValues.Add(item.GetString()!);
                }
            }
        }

        return new SchemaProperty
        {
            Type = type,
            Description = description,
            Enum = enumValues.Count > 0 ? enumValues : null
        };
    }

    /// <summary>
    /// Helper to get string property from JsonElement.
    /// </summary>
    private static string? GetPropertyString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }
}
