// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using OpenAI;
using OpenAI.Chat;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.Implementations.Events;

namespace NTG.Adk.Implementations.Models;

/// <summary>
/// OpenAI LLM adapter.
/// Implements ILlm port using OpenAI API.
/// Equivalent to openai.Client in Python ADK.
/// </summary>
public class OpenAILlm : ILlm
{
    private readonly ChatClient _client;

    public string ModelName { get; }

    /// <summary>
    /// Create OpenAILlm with API key authentication
    /// </summary>
    public OpenAILlm(string modelName, string apiKey)
    {
        ModelName = modelName;
        var openAIClient = new OpenAIClient(apiKey);
        _client = openAIClient.GetChatClient(modelName);
    }

    /// <summary>
    /// Create OpenAILlm with default environment variable (OPENAI_API_KEY)
    /// </summary>
    public OpenAILlm(string modelName)
    {
        ModelName = modelName;
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable not set");
        var openAIClient = new OpenAIClient(apiKey);
        _client = openAIClient.GetChatClient(modelName);
    }

    public async Task<ILlmResponse> GenerateAsync(
        ILlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Build OpenAI messages
        var messages = BuildMessages(request);

        // Build options
        var options = BuildChatCompletionOptions(request);

        // Call OpenAI API
        var completion = await _client.CompleteChatAsync(messages, options, cancellationToken);

        // Convert to ILlmResponse
        return ConvertResponse(completion);
    }

    public async IAsyncEnumerable<ILlmResponse> GenerateStreamAsync(
        ILlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        // Build OpenAI messages
        var messages = BuildMessages(request);

        // Build options
        var options = BuildChatCompletionOptions(request);

        // Call OpenAI streaming API
        var updates = _client.CompleteChatStreamingAsync(messages, options, cancellationToken);

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            yield return ConvertStreamingUpdate(update);
        }
    }

    private List<ChatMessage> BuildMessages(ILlmRequest request)
    {
        var messages = new List<ChatMessage>();

        // Add system instruction
        if (!string.IsNullOrEmpty(request.SystemInstruction))
        {
            messages.Add(new SystemChatMessage(request.SystemInstruction));
        }

        // Add content messages (message history)
        foreach (var content in request.Contents)
        {
            messages.Add(ConvertContent(content));
        }

        return messages;
    }

    private ChatCompletionOptions BuildChatCompletionOptions(ILlmRequest request)
    {
        var options = new ChatCompletionOptions();

        // Add tools
        if (request.Tools?.Count > 0)
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(ConvertFunctionDeclaration(tool));
            }
        }

        // Add generation config
        if (request.Config != null)
        {
            if (request.Config.Temperature.HasValue)
                options.Temperature = (float)request.Config.Temperature.Value;

            if (request.Config.TopP.HasValue)
                options.TopP = (float)request.Config.TopP.Value;

            if (request.Config.MaxOutputTokens.HasValue)
                options.MaxOutputTokenCount = request.Config.MaxOutputTokens.Value;

            if (request.Config.StopSequences?.Count > 0)
            {
                foreach (var seq in request.Config.StopSequences)
                {
                    options.StopSequences.Add(seq);
                }
            }
        }

        return options;
    }

    private ChatMessage ConvertContent(IContent content)
    {
        var role = content.Role switch
        {
            "user" => ChatMessageRole.User,
            "model" => ChatMessageRole.Assistant,
            "assistant" => ChatMessageRole.Assistant,
            "system" => ChatMessageRole.System,
            "tool" => ChatMessageRole.Tool,
            _ => ChatMessageRole.User
        };

        // Build message parts
        var textParts = new List<string>();
        var toolCalls = new List<ChatToolCall>();
        var toolCallId = string.Empty;

        foreach (var part in content.Parts)
        {
            if (part.Text != null)
            {
                textParts.Add(part.Text);
            }

            if (part.FunctionCall != null)
            {
                // OpenAI function calls
                var functionCallJson = System.Text.Json.JsonSerializer.Serialize(part.FunctionCall.Args);
                toolCalls.Add(ChatToolCall.CreateFunctionToolCall(
                    id: part.FunctionCall.Id ?? $"call_{Guid.NewGuid():N}",
                    functionName: part.FunctionCall.Name,
                    functionArguments: BinaryData.FromString(functionCallJson)
                ));
            }

            if (part.FunctionResponse != null)
            {
                // Tool response
                toolCallId = part.FunctionResponse.Id ?? string.Empty;
            }
        }

        // Create appropriate message type
        if (role == ChatMessageRole.Assistant && toolCalls.Count > 0)
        {
            return new AssistantChatMessage(toolCalls);
        }
        else if (role == ChatMessageRole.Tool)
        {
            var responseJson = System.Text.Json.JsonSerializer.Serialize(
                content.Parts.FirstOrDefault()?.FunctionResponse?.Response ?? new { }
            );
            return new ToolChatMessage(toolCallId, responseJson);
        }
        else if (role == ChatMessageRole.User)
        {
            return new UserChatMessage(string.Join("\n", textParts));
        }
        else if (role == ChatMessageRole.Assistant)
        {
            return new AssistantChatMessage(string.Join("\n", textParts));
        }
        else
        {
            return new SystemChatMessage(string.Join("\n", textParts));
        }
    }

    private ChatTool ConvertFunctionDeclaration(CoreAbstractions.Tools.IFunctionDeclaration funcDecl)
    {
        // Build JSON schema for OpenAI function
        var parametersJson = BuildParametersJson(funcDecl.Parameters);

        return ChatTool.CreateFunctionTool(
            functionName: funcDecl.Name,
            functionDescription: funcDecl.Description ?? string.Empty,
            functionParameters: BinaryData.FromString(parametersJson)
        );
    }

    private string BuildParametersJson(CoreAbstractions.Tools.ISchema? schema)
    {
        if (schema == null)
        {
            return "{}";
        }

        var schemaObj = new Dictionary<string, object>
        {
            ["type"] = schema.Type.ToLowerInvariant()
        };

        if (schema.Properties != null && schema.Properties.Count > 0)
        {
            var properties = new Dictionary<string, object>();
            foreach (var prop in schema.Properties)
            {
                var propObj = new Dictionary<string, object>
                {
                    ["type"] = prop.Value.Type.ToLowerInvariant()
                };
                if (!string.IsNullOrEmpty(prop.Value.Description))
                {
                    propObj["description"] = prop.Value.Description;
                }
                properties[prop.Key] = propObj;
            }
            schemaObj["properties"] = properties;
        }

        if (schema.Required != null && schema.Required.Count > 0)
        {
            schemaObj["required"] = schema.Required;
        }

        return System.Text.Json.JsonSerializer.Serialize(schemaObj);
    }

    private ILlmResponse ConvertResponse(ChatCompletion completion)
    {
        var choice = completion.Content.FirstOrDefault();
        if (choice == null)
        {
            return new OpenAILlmResponse
            {
                Content = null,
                Text = null,
                FunctionCalls = null,
                FinishReason = completion.FinishReason.ToString(),
                Usage = ConvertUsage(completion.Usage)
            };
        }

        // Extract text
        var text = string.Join("", completion.Content.Select(c => c.Text ?? string.Empty));

        // Extract function calls
        var functionCalls = completion.ToolCalls
            .Select(tc => new OpenAIFunctionCall(tc))
            .ToList<IFunctionCall>();

        // Convert to IContent
        var content = ConvertToIContent(completion);

        return new OpenAILlmResponse
        {
            Content = content,
            Text = !string.IsNullOrEmpty(text) ? text : null,
            FunctionCalls = functionCalls.Count > 0 ? functionCalls : null,
            FinishReason = completion.FinishReason.ToString(),
            Usage = ConvertUsage(completion.Usage)
        };
    }

    private ILlmResponse ConvertStreamingUpdate(StreamingChatCompletionUpdate update)
    {
        // Extract text
        var text = string.Join("", update.ContentUpdate.Select(c => c.Text ?? string.Empty));

        // Extract tool calls
        var functionCalls = update.ToolCallUpdates
            .Select(tc => new OpenAIFunctionCall(tc))
            .ToList<IFunctionCall>();

        // Convert to IContent
        var content = ConvertUpdateToIContent(update);

        return new OpenAILlmResponse
        {
            Content = content,
            Text = !string.IsNullOrEmpty(text) ? text : null,
            FunctionCalls = functionCalls.Count > 0 ? functionCalls : null,
            FinishReason = update.FinishReason?.ToString(),
            Usage = null // Streaming doesn't provide usage in each chunk
        };
    }

    private IContent ConvertToIContent(ChatCompletion completion)
    {
        var parts = new List<IPart>();

        // Add text parts
        foreach (var content in completion.Content)
        {
            if (!string.IsNullOrEmpty(content.Text))
            {
                parts.Add(new SimplePart { Text = content.Text });
            }
        }

        // Add function call parts
        foreach (var toolCall in completion.ToolCalls)
        {
            parts.Add(new SimplePart
            {
                FunctionCall = new OpenAIFunctionCall(toolCall)
            });
        }

        return new SimpleContent
        {
            Role = "model",
            Parts = parts
        };
    }

    private IContent ConvertUpdateToIContent(StreamingChatCompletionUpdate update)
    {
        var parts = new List<IPart>();

        // Add text parts
        foreach (var content in update.ContentUpdate)
        {
            if (!string.IsNullOrEmpty(content.Text))
            {
                parts.Add(new SimplePart { Text = content.Text });
            }
        }

        // Add tool call updates
        foreach (var toolCall in update.ToolCallUpdates)
        {
            parts.Add(new SimplePart
            {
                FunctionCall = new OpenAIFunctionCall(toolCall)
            });
        }

        return new SimpleContent
        {
            Role = "model",
            Parts = parts
        };
    }

    private IUsageMetadata? ConvertUsage(ChatTokenUsage? usage)
    {
        if (usage == null) return null;

        return new OpenAIUsageMetadata
        {
            PromptTokenCount = usage.InputTokenCount,
            CandidatesTokenCount = usage.OutputTokenCount,
            TotalTokenCount = usage.TotalTokenCount
        };
    }
}

// Internal implementation classes
internal class OpenAILlmResponse : ILlmResponse
{
    public required IContent? Content { get; init; }
    public required string? Text { get; init; }
    public required IReadOnlyList<IFunctionCall>? FunctionCalls { get; init; }
    public required string? FinishReason { get; init; }
    public required IUsageMetadata? Usage { get; init; }
}

internal class OpenAIFunctionCall : IFunctionCall
{
    private readonly ChatToolCall? _toolCall;
    private readonly StreamingChatToolCallUpdate? _toolCallUpdate;

    public OpenAIFunctionCall(ChatToolCall toolCall)
    {
        _toolCall = toolCall;
    }

    public OpenAIFunctionCall(StreamingChatToolCallUpdate toolCallUpdate)
    {
        _toolCallUpdate = toolCallUpdate;
    }

    public string Name => _toolCall?.FunctionName ?? _toolCallUpdate?.FunctionName ?? string.Empty;

    public IReadOnlyDictionary<string, object>? Args
    {
        get
        {
            var argsJson = _toolCall?.FunctionArguments.ToString() ?? _toolCallUpdate?.FunctionArgumentsUpdate.ToString();
            if (string.IsNullOrEmpty(argsJson)) return null;

            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson);
                return dict;
            }
            catch
            {
                return null;
            }
        }
    }

    public string? Id => _toolCall?.Id ?? _toolCallUpdate?.ToolCallId;
}

internal class OpenAIUsageMetadata : IUsageMetadata
{
    public required int PromptTokenCount { get; init; }
    public required int CandidatesTokenCount { get; init; }
    public required int TotalTokenCount { get; init; }
}
