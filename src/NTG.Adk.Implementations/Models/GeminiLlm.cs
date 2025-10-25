// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using Google.Cloud.AIPlatform.V1;
using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.Implementations.Events;

using GeminiPart = Google.Cloud.AIPlatform.V1.Part;
using GeminiFunctionCall = Google.Cloud.AIPlatform.V1.FunctionCall;
using GeminiFunctionResponse = Google.Cloud.AIPlatform.V1.FunctionResponse;
using GeminiContent = Google.Cloud.AIPlatform.V1.Content;
using BoundaryPart = NTG.Adk.Boundary.Events.Part;
using BoundaryFunctionCall = NTG.Adk.Boundary.Events.FunctionCall;
using BoundaryFunctionResponse = NTG.Adk.Boundary.Events.FunctionResponse;

namespace NTG.Adk.Implementations.Models;

/// <summary>
/// Google Gemini LLM adapter.
/// Implements ILlm port using Google Cloud AI Platform API.
/// Equivalent to google.genai.Client in Python ADK.
/// </summary>
public class GeminiLlm : ILlm
{
    private readonly PredictionServiceClient _client;
    private readonly string _projectId;
    private readonly string _location;

    public string ModelName { get; }

    /// <summary>
    /// Create GeminiLlm with API key authentication
    /// </summary>
    public GeminiLlm(string modelName, string apiKey, string projectId = "default", string location = "us-central1")
    {
        ModelName = modelName;
        _projectId = projectId;
        _location = location;

        // TODO: Initialize client with API key
        // For now, use default credentials
        _client = PredictionServiceClient.Create();
    }

    /// <summary>
    /// Create GeminiLlm with default credentials
    /// </summary>
    public GeminiLlm(string modelName)
    {
        ModelName = modelName;
        _projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "default";
        _location = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_LOCATION") ?? "us-central1";

        _client = PredictionServiceClient.Create();
    }

    public async Task<ILlmResponse> GenerateAsync(
        ILlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Build Gemini request
        var geminiRequest = BuildGeminiRequest(request);

        // Call Gemini API
        var response = await _client.GenerateContentAsync(geminiRequest, cancellationToken);

        // Convert to ILlmResponse
        return ConvertResponse(response);
    }

    public async IAsyncEnumerable<ILlmResponse> GenerateStreamAsync(
        ILlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Build Gemini request
        var geminiRequest = BuildGeminiRequest(request);

        // Call Gemini streaming API
        var streamingCall = _client.StreamGenerateContent(geminiRequest);

        await foreach (var response in streamingCall.GetResponseStream().WithCancellation(cancellationToken))
        {
            yield return ConvertResponse(response);
        }
    }

    private GenerateContentRequest BuildGeminiRequest(ILlmRequest request)
    {
        var geminiRequest = new GenerateContentRequest
        {
            Model = $"projects/{_projectId}/locations/{_location}/publishers/google/models/{ModelName}",
        };

        // Add system instruction
        if (!string.IsNullOrEmpty(request.SystemInstruction))
        {
            geminiRequest.SystemInstruction = new GeminiContent
            {
                Parts = { new GeminiPart { Text = request.SystemInstruction } }
            };
        }

        // Add contents (message history)
        foreach (var content in request.Contents)
        {
            geminiRequest.Contents.Add(ConvertContent(content));
        }

        // Add tools
        if (request.Tools?.Count > 0)
        {
            var tool = new Tool();
            foreach (var functionDecl in request.Tools)
            {
                tool.FunctionDeclarations.Add(ConvertFunctionDeclaration(functionDecl));
            }
            geminiRequest.Tools.Add(tool);
        }

        // Add generation config
        if (request.Config != null)
        {
            geminiRequest.GenerationConfig = new GenerationConfig();

            if (request.Config.Temperature.HasValue)
                geminiRequest.GenerationConfig.Temperature = (float)request.Config.Temperature.Value;

            if (request.Config.TopP.HasValue)
                geminiRequest.GenerationConfig.TopP = (float)request.Config.TopP.Value;

            if (request.Config.TopK.HasValue)
                geminiRequest.GenerationConfig.TopK = (float)request.Config.TopK.Value;

            if (request.Config.MaxOutputTokens.HasValue)
                geminiRequest.GenerationConfig.MaxOutputTokens = request.Config.MaxOutputTokens.Value;

            if (request.Config.StopSequences?.Count > 0)
            {
                geminiRequest.GenerationConfig.StopSequences.AddRange(request.Config.StopSequences);
            }
        }

        return geminiRequest;
    }

    private GeminiContent ConvertContent(IContent content)
    {
        var geminiContent = new GeminiContent
        {
            Role = content.Role ?? "user"
        };

        foreach (var part in content.Parts)
        {
            geminiContent.Parts.Add(ConvertPart(part));
        }

        return geminiContent;
    }

    private GeminiPart ConvertPart(IPart part)
    {
        if (part.Text != null)
        {
            return new GeminiPart { Text = part.Text };
        }

        if (part.FunctionCall != null)
        {
            return new GeminiPart
            {
                FunctionCall = new GeminiFunctionCall
                {
                    Name = part.FunctionCall.Name,
                    Args = ConvertArgs(part.FunctionCall.Args)
                }
            };
        }

        if (part.FunctionResponse != null)
        {
            return new GeminiPart
            {
                FunctionResponse = new GeminiFunctionResponse
                {
                    Name = part.FunctionResponse.Name,
                    Response = ConvertResponse(part.FunctionResponse.Response)
                }
            };
        }

        if (part.InlineData != null)
        {
            return new GeminiPart
            {
                InlineData = new Blob
                {
                    MimeType = part.MimeType ?? "application/octet-stream",
                    Data = Google.Protobuf.ByteString.CopyFrom(part.InlineData)
                }
            };
        }

        return new GeminiPart { Text = "" };
    }

    private FunctionDeclaration ConvertFunctionDeclaration(CoreAbstractions.Tools.IFunctionDeclaration funcDecl)
    {
        var declaration = new FunctionDeclaration
        {
            Name = funcDecl.Name,
            Description = funcDecl.Description ?? ""
        };

        if (funcDecl.Parameters != null)
        {
            declaration.Parameters = ConvertSchema(funcDecl.Parameters);
        }

        return declaration;
    }

    private Google.Cloud.AIPlatform.V1.OpenApiSchema ConvertSchema(CoreAbstractions.Tools.ISchema schema)
    {
        var geminiSchema = new Google.Cloud.AIPlatform.V1.OpenApiSchema
        {
            Type = ConvertSchemaType(schema.Type)
        };

        if (schema.Properties != null)
        {
            foreach (var prop in schema.Properties)
            {
                geminiSchema.Properties.Add(prop.Key, new Google.Cloud.AIPlatform.V1.OpenApiSchema
                {
                    Type = ConvertSchemaType(prop.Value.Type),
                    Description = prop.Value.Description ?? ""
                });
            }
        }

        if (schema.Required != null)
        {
            geminiSchema.Required.AddRange(schema.Required);
        }

        return geminiSchema;
    }

    private Google.Cloud.AIPlatform.V1.Type ConvertSchemaType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "string" => Google.Cloud.AIPlatform.V1.Type.String,
            "number" => Google.Cloud.AIPlatform.V1.Type.Number,
            "integer" => Google.Cloud.AIPlatform.V1.Type.Integer,
            "boolean" => Google.Cloud.AIPlatform.V1.Type.Boolean,
            "array" => Google.Cloud.AIPlatform.V1.Type.Array,
            "object" => Google.Cloud.AIPlatform.V1.Type.Object,
            _ => Google.Cloud.AIPlatform.V1.Type.String
        };
    }

    private Google.Protobuf.WellKnownTypes.Struct ConvertArgs(IReadOnlyDictionary<string, object>? args)
    {
        var structValue = new Google.Protobuf.WellKnownTypes.Struct();
        if (args == null) return structValue;

        foreach (var arg in args)
        {
            structValue.Fields[arg.Key] = ConvertToValue(arg.Value);
        }

        return structValue;
    }

    private Google.Protobuf.WellKnownTypes.Struct ConvertResponse(object response)
    {
        var structValue = new Google.Protobuf.WellKnownTypes.Struct();

        if (response is Dictionary<string, object> dict)
        {
            foreach (var item in dict)
            {
                structValue.Fields[item.Key] = ConvertToValue(item.Value);
            }
        }
        else
        {
            structValue.Fields["result"] = ConvertToValue(response);
        }

        return structValue;
    }

    private Google.Protobuf.WellKnownTypes.Value ConvertToValue(object obj)
    {
        return obj switch
        {
            string s => Google.Protobuf.WellKnownTypes.Value.ForString(s),
            int i => Google.Protobuf.WellKnownTypes.Value.ForNumber(i),
            long l => Google.Protobuf.WellKnownTypes.Value.ForNumber(l),
            double d => Google.Protobuf.WellKnownTypes.Value.ForNumber(d),
            float f => Google.Protobuf.WellKnownTypes.Value.ForNumber(f),
            bool b => Google.Protobuf.WellKnownTypes.Value.ForBool(b),
            null => Google.Protobuf.WellKnownTypes.Value.ForNull(),
            _ => Google.Protobuf.WellKnownTypes.Value.ForString(obj.ToString() ?? "")
        };
    }

    private ILlmResponse ConvertResponse(GenerateContentResponse response)
    {
        var candidate = response.Candidates.FirstOrDefault();
        if (candidate == null)
        {
            return new GeminiLlmResponse
            {
                Content = null,
                Text = null,
                FunctionCalls = null,
                FinishReason = "no_candidates",
                Usage = null
            };
        }

        // Extract text
        var textParts = candidate.Content.Parts.Where(p => !string.IsNullOrEmpty(p.Text)).ToList();
        var text = textParts.Count > 0 ? string.Join("", textParts.Select(p => p.Text)) : null;

        // Extract function calls
        var functionCalls = candidate.Content.Parts
            .Where(p => p.FunctionCall != null)
            .Select(p => new GeminiLlmFunctionCall(p.FunctionCall))
            .ToList<IFunctionCall>();

        // Convert content
        var content = ConvertToIContent(candidate.Content);

        return new GeminiLlmResponse
        {
            Content = content,
            Text = text,
            FunctionCalls = functionCalls.Count > 0 ? functionCalls : null,
            FinishReason = candidate.FinishReason.ToString(),
            Usage = ConvertUsageMetadata(response.UsageMetadata)
        };
    }

    private IContent ConvertToIContent(GeminiContent content)
    {
        var parts = content.Parts.Select(ConvertToIPart).ToList();

        return new SimpleContent
        {
            Role = content.Role,
            Parts = parts
        };
    }

    private IPart ConvertToIPart(GeminiPart part)
    {
        if (!string.IsNullOrEmpty(part.Text))
        {
            return new SimplePart { Text = part.Text };
        }

        if (part.FunctionCall != null)
        {
            return new SimplePart
            {
                FunctionCall = new GeminiLlmFunctionCall(part.FunctionCall)
            };
        }

        if (part.FunctionResponse != null)
        {
            return new SimplePart
            {
                FunctionResponse = new GeminiLlmFunctionResponse(part.FunctionResponse)
            };
        }

        if (part.InlineData != null)
        {
            return new SimplePart
            {
                InlineData = part.InlineData.Data.ToByteArray(),
                MimeType = part.InlineData.MimeType
            };
        }

        return new SimplePart { Text = "" };
    }

    private IUsageMetadata? ConvertUsageMetadata(GenerateContentResponse.Types.UsageMetadata? usage)
    {
        if (usage == null) return null;

        return new GeminiUsageMetadata
        {
            PromptTokenCount = usage.PromptTokenCount,
            CandidatesTokenCount = usage.CandidatesTokenCount,
            TotalTokenCount = usage.TotalTokenCount
        };
    }
}

// Internal implementation classes
internal class GeminiLlmResponse : ILlmResponse
{
    public required IContent? Content { get; init; }
    public required string? Text { get; init; }
    public required IReadOnlyList<IFunctionCall>? FunctionCalls { get; init; }
    public required string? FinishReason { get; init; }
    public required IUsageMetadata? Usage { get; init; }
}

internal class GeminiLlmFunctionCall : IFunctionCall
{
    private readonly GeminiFunctionCall _functionCall;

    public GeminiLlmFunctionCall(GeminiFunctionCall functionCall)
    {
        _functionCall = functionCall;
    }

    public string Name => _functionCall.Name;

    public IReadOnlyDictionary<string, object>? Args =>
        _functionCall.Args?.Fields.ToDictionary(
            kvp => kvp.Key,
            kvp => ConvertValue(kvp.Value));

    public string? Id => null; // Gemini API doesn't provide function call ID

    private object ConvertValue(Google.Protobuf.WellKnownTypes.Value value)
    {
        return value.KindCase switch
        {
            Google.Protobuf.WellKnownTypes.Value.KindOneofCase.StringValue => value.StringValue,
            Google.Protobuf.WellKnownTypes.Value.KindOneofCase.NumberValue => value.NumberValue,
            Google.Protobuf.WellKnownTypes.Value.KindOneofCase.BoolValue => value.BoolValue,
            Google.Protobuf.WellKnownTypes.Value.KindOneofCase.NullValue => null!,
            _ => value.ToString()
        };
    }
}

internal class GeminiLlmFunctionResponse : IFunctionResponse
{
    private readonly GeminiFunctionResponse _response;

    public GeminiLlmFunctionResponse(GeminiFunctionResponse response)
    {
        _response = response;
    }

    public string Name => _response.Name;
    public object Response => _response.Response;
    public string? Id => null; // Gemini API doesn't provide function response ID
    public string? Error => null; // Gemini API doesn't return errors in FunctionResponse
}

internal class GeminiUsageMetadata : IUsageMetadata
{
    public required int PromptTokenCount { get; init; }
    public required int CandidatesTokenCount { get; init; }
    public required int TotalTokenCount { get; init; }
}

// Reuse from LlmAgent.cs
internal class SimpleContent : IContent
{
    public required string? Role { get; init; }
    public required IReadOnlyList<IPart> Parts { get; init; }
}

internal class SimplePart : IPart
{
    public string? Text { get; init; }
    public IFunctionCall? FunctionCall { get; init; }
    public IFunctionResponse? FunctionResponse { get; init; }
    public byte[]? InlineData { get; init; }
    public string? MimeType { get; init; }
}
