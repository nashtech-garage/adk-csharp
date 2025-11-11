// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Net.Http;
using System.Text.Json;
using Microsoft.OpenApi;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.Boundary.Tools.Auth;
using NTG.Adk.CoreAbstractions.Tools;
using YamlDotNet.Serialization;

namespace NTG.Adk.Implementations.Tools.OpenApi;

/// <summary>
/// Parses OpenAPI specifications into a collection of RestApiTool instances.
/// Based on Google ADK Python implementation.
/// </summary>
public sealed class OpenAPIToolset
{
    private readonly OpenApiDocument _openApiDoc;
    private readonly List<RestApiTool> _tools;
    private readonly AuthScheme? _globalAuthScheme;
    private readonly AuthCredential? _globalAuthCredential;

    /// <summary>
    /// Initialize OpenAPIToolset from OpenAPI spec string.
    /// </summary>
    /// <param name="specStr">OpenAPI spec as JSON or YAML string</param>
    /// <param name="specStrType">Spec format: "json" or "yaml"</param>
    /// <param name="authScheme">Global auth scheme for all tools</param>
    /// <param name="authCredential">Global auth credential for all tools</param>
    public OpenAPIToolset(
        string specStr,
        string specStrType = "json",
        AuthScheme? authScheme = null,
        AuthCredential? authCredential = null)
    {
        if (string.IsNullOrWhiteSpace(specStr))
            throw new ArgumentException("Spec string cannot be empty", nameof(specStr));

        _globalAuthScheme = authScheme;
        _globalAuthCredential = authCredential;

        // Convert YAML to JSON if needed
        if (specStrType.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize(new StringReader(specStr));
            var serializer = new SerializerBuilder()
                .JsonCompatible()
                .Build();
            specStr = serializer.Serialize(yamlObject);
        }

        // Parse OpenAPI spec using v2.x API
        var result = OpenApiDocument.Parse(specStr);

        if (result.Document == null)
            throw new InvalidOperationException("Failed to parse OpenAPI spec: Document is null");

        _openApiDoc = result.Document;
        var diagnostic = result.Diagnostic;

        if (diagnostic?.Errors.Count > 0)
        {
            var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"Failed to parse OpenAPI spec: {errors}");
        }

        // Generate tools from operations
        _tools = GenerateTools();
    }

    /// <summary>
    /// Initialize OpenAPIToolset from OpenAPI spec dictionary.
    /// </summary>
    /// <param name="specDict">OpenAPI spec as dictionary</param>
    /// <param name="authScheme">Global auth scheme for all tools</param>
    /// <param name="authCredential">Global auth credential for all tools</param>
    public OpenAPIToolset(
        Dictionary<string, object> specDict,
        AuthScheme? authScheme = null,
        AuthCredential? authCredential = null)
        : this(
            JsonSerializer.Serialize(specDict),
            "json",
            authScheme,
            authCredential)
    {
    }

    /// <summary>
    /// Get all tools generated from the OpenAPI spec.
    /// </summary>
    public List<ITool> GetTools()
    {
        return _tools.Cast<ITool>().ToList();
    }

    /// <summary>
    /// Get a specific tool by name.
    /// </summary>
    public ITool? GetTool(string toolName)
    {
        return _tools.FirstOrDefault(t => t.Name == toolName);
    }

    // Generate RestApiTool instances from OpenAPI operations
    private List<RestApiTool> GenerateTools()
    {
        var tools = new List<RestApiTool>();

        // Get base URL from servers
        var baseUrl = _openApiDoc.Servers?.FirstOrDefault()?.Url ?? "http://localhost";

        // Iterate through all paths and operations
        foreach (var path in _openApiDoc.Paths)
        {
            if (path.Value.Operations == null)
                continue;

            foreach (var operation in path.Value.Operations)
            {
                var tool = CreateToolFromOperation(
                    baseUrl,
                    path.Key,
                    operation.Key,
                    operation.Value);

                if (tool != null)
                {
                    tools.Add(tool);
                }
            }
        }

        return tools;
    }

    // Create a RestApiTool from an OpenAPI operation
    private RestApiTool? CreateToolFromOperation(
        string baseUrl,
        string path,
        HttpMethod httpMethod,
        OpenApiOperation operation)
    {
        // Get operation ID (tool name)
        var operationId = operation.OperationId;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            // Generate name from path and method
            operationId = $"{httpMethod.Method}_{path.Replace("/", "_").Replace("{", "").Replace("}", "").Trim('_')}";
        }

        // Convert operationId to snake_case
        var toolName = ToSnakeCase(operationId);

        // Get description
        var description = operation.Summary ?? operation.Description ?? $"{httpMethod.Method} {path}";

        // Build parameter schema
        var schema = BuildParameterSchema(operation);

        // Create tool
        var tool = new RestApiTool(
            name: toolName,
            description: description,
            baseUrl: baseUrl,
            path: path,
            method: httpMethod.Method.ToUpperInvariant(),
            parameters: schema,
            authScheme: _globalAuthScheme,
            authCredential: _globalAuthCredential);

        return tool;
    }

    // Build ADK Schema from OpenAPI parameters
    private Schema BuildParameterSchema(OpenApiOperation operation)
    {
        var properties = new Dictionary<string, SchemaProperty>();
        var required = new List<string>();

        // Add parameters (path, query, header, cookie)
        foreach (var parameter in operation.Parameters ?? Enumerable.Empty<IOpenApiParameter>())
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;

            var propSchema = ConvertOpenApiSchemaToSchemaProperty(parameter.Schema);
            properties[parameter.Name] = propSchema with
            {
                Description = parameter.Description ?? propSchema.Description
            };

            if (parameter.Required)
            {
                required.Add(parameter.Name);
            }
        }

        // Add request body parameters
        if (operation.RequestBody?.Content != null)
        {
            var jsonContent = operation.RequestBody.Content
                .FirstOrDefault(c => c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

            if (jsonContent.Value?.Schema != null)
            {
                var bodySchema = jsonContent.Value.Schema;

                // Flatten request body properties into parameters
                foreach (var prop in bodySchema.Properties ?? new Dictionary<string, IOpenApiSchema>())
                {
                    properties[prop.Key] = ConvertOpenApiSchemaToSchemaProperty(prop.Value);

                    if (bodySchema.Required?.Contains(prop.Key) == true)
                    {
                        required.Add(prop.Key);
                    }
                }
            }
        }

        return new Schema
        {
            Type = "object",
            Properties = properties.Count > 0 ? properties : null,
            Required = required.Count > 0 ? required : null
        };
    }

    // Convert OpenApiSchema to ADK SchemaProperty
    private SchemaProperty ConvertOpenApiSchemaToSchemaProperty(IOpenApiSchema? openApiSchema)
    {
        if (openApiSchema == null)
        {
            return new SchemaProperty { Type = "string" };
        }

        var type = openApiSchema.Type switch
        {
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Number => "number",
            JsonSchemaType.Boolean => "boolean",
            JsonSchemaType.Array => "array",
            JsonSchemaType.Object => "object",
            JsonSchemaType.String => "string",
            _ => "string"
        };

        var enumValues = openApiSchema.Enum?
            .Select(e => e.ToString())
            .Where(e => !string.IsNullOrEmpty(e))
            .Select(e => e!)
            .ToList();

        return new SchemaProperty
        {
            Type = type,
            Description = openApiSchema.Description,
            Enum = enumValues?.Count > 0 ? enumValues : null
        };
    }

    // Convert PascalCase/camelCase to snake_case
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var result = new System.Text.StringBuilder();
        result.Append(char.ToLowerInvariant(input[0]));

        for (int i = 1; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
