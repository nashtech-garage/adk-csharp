// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Reflection;
using System.Text.Json;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Wraps a C# function/method as an ITool.
/// Equivalent to google.adk.tools.FunctionTool in Python.
/// </summary>
public class FunctionTool : ITool
{
    private readonly Delegate _function;
    private readonly FunctionDeclaration _declaration;

    public string Name => _declaration.Name;
    public string? Description => _declaration.Description;

    private FunctionTool(Delegate function, FunctionDeclaration declaration)
    {
        _function = function;
        _declaration = declaration;
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
        // Convert args to method parameters
        var methodInfo = _function.Method;
        var parameters = methodInfo.GetParameters();
        var parameterValues = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            // Special cases: IToolContext, CancellationToken
            if (param.ParameterType == typeof(IToolContext))
            {
                parameterValues[i] = context;
                continue;
            }

            if (param.ParameterType == typeof(CancellationToken))
            {
                parameterValues[i] = cancellationToken;
                continue;
            }

            // Regular parameter from args
            if (args.TryGetValue(param.Name!, out var value))
            {
                parameterValues[i] = ConvertParameter(value, param.ParameterType);
            }
            else if (param.HasDefaultValue)
            {
                parameterValues[i] = param.DefaultValue;
            }
            else
            {
                throw new ArgumentException($"Missing required parameter: {param.Name}");
            }
        }

        // Invoke function
        var result = _function.DynamicInvoke(parameterValues);

        // Handle async results
        if (result is Task task)
        {
            await task;

            // Get result from Task<T>
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task) ?? new { success = true };
        }

        return result ?? new { success = true };
    }

    /// <summary>
    /// Create a FunctionTool from a delegate.
    /// </summary>
    public static FunctionTool Create<TResult>(
        Func<TResult> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    public static FunctionTool Create<T1, TResult>(
        Func<T1, TResult> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    public static FunctionTool Create<T1, T2, TResult>(
        Func<T1, T2, TResult> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    public static FunctionTool Create<T1, T2, T3, TResult>(
        Func<T1, T2, T3, TResult> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    // Async versions
    public static FunctionTool Create<TResult>(
        Func<Task<TResult>> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    public static FunctionTool Create<T1, TResult>(
        Func<T1, Task<TResult>> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    public static FunctionTool Create<T1, T2, TResult>(
        Func<T1, T2, Task<TResult>> function,
        string? name = null,
        string? description = null)
    {
        return CreateInternal(function, name, description);
    }

    private static FunctionTool CreateInternal(
        Delegate function,
        string? name,
        string? description)
    {
        var methodInfo = function.Method;
        var toolName = name ?? methodInfo.Name;

        // Build schema from method parameters
        var schema = BuildSchema(methodInfo);

        var declaration = new FunctionDeclaration
        {
            Name = toolName,
            Description = description ?? $"Executes {toolName}",
            Parameters = schema
        };

        return new FunctionTool(function, declaration);
    }

    private static Schema BuildSchema(MethodInfo methodInfo)
    {
        var properties = new Dictionary<string, SchemaProperty>();
        var required = new List<string>();

        foreach (var param in methodInfo.GetParameters())
        {
            // Skip special parameters
            if (param.ParameterType == typeof(IToolContext) ||
                param.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            var property = new SchemaProperty
            {
                Type = GetSchemaType(param.ParameterType),
                Description = $"Parameter {param.Name}"
            };

            properties[param.Name!] = property;

            if (!param.HasDefaultValue)
            {
                required.Add(param.Name!);
            }
        }

        return new Schema
        {
            Type = "object",
            Properties = properties,
            Required = required.Count > 0 ? required : null
        };
    }

    private static string GetSchemaType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
            return "integer";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return "number";
        if (type == typeof(bool)) return "boolean";
        if (type.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return "array";
        return "object";
    }

    private static object? ConvertParameter(object value, Type targetType)
    {
        if (value.GetType() == targetType)
            return value;

        // Handle JsonElement conversion
        if (value is JsonElement jsonElement)
        {
            return JsonSerializer.Deserialize(jsonElement.GetRawText(), targetType);
        }

        // Try direct conversion
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            // Fallback to JSON serialization
            var json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize(json, targetType);
        }
    }
}

/// <summary>
/// Adapter for FunctionDeclaration DTO to IFunctionDeclaration port
/// </summary>
internal class FunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public FunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null ? new SchemaAdapter(_dto.Parameters) : null;
}

internal class SchemaAdapter : ISchema
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

internal class SchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public SchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
}
