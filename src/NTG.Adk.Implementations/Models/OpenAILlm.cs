// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;
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
    /// Create OpenAILlm with custom endpoint (for Ollama, LocalAI, vLLM, etc.)
    /// </summary>
    /// <param name="modelName">Model name (e.g., "llama3", "mistral")</param>
    /// <param name="apiKey">API key (use "ollama" or any string for Ollama)</param>
    /// <param name="endpoint">Custom endpoint URL (e.g., "http://localhost:11434/v1")</param>
    public OpenAILlm(string modelName, string apiKey, Uri endpoint)
    {
        ModelName = modelName;
        var options = new OpenAIClientOptions
        {
            Endpoint = endpoint
        };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
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
        var messages = BuildMessages(request);
        var options = BuildChatCompletionOptions(request);
        var updates = _client.CompleteChatStreamingAsync(messages, options, cancellationToken);

        // Accumulate tool call deltas by index; yield text immediately
        var toolCallMap = new SortedDictionary<int, (string Id, string Name, StringBuilder Args)>();
        // State machine for parsing <think>...</think> blocks across stream chunks
        var thinkState = new ThinkParseState();

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            // Accumulate tool call deltas (don't yield intermediate chunks)
            foreach (var tc in update.ToolCallUpdates)
            {
                if (!toolCallMap.TryGetValue(tc.Index, out var state))
                {
                    state = (tc.ToolCallId ?? "", tc.FunctionName ?? "", new StringBuilder());
                    toolCallMap[tc.Index] = state;
                }
                else if (!string.IsNullOrEmpty(tc.ToolCallId) || !string.IsNullOrEmpty(tc.FunctionName))
                {
                    toolCallMap[tc.Index] = (
                        !string.IsNullOrEmpty(tc.ToolCallId) ? tc.ToolCallId : state.Id,
                        !string.IsNullOrEmpty(tc.FunctionName) ? tc.FunctionName : state.Name,
                        state.Args
                    );
                    state = toolCallMap[tc.Index];
                }
                var argsUpdate = tc.FunctionArgumentsUpdate?.ToString();
                if (!string.IsNullOrEmpty(argsUpdate))
                    state.Args.Append(argsUpdate);
            }

            // Yield text/reasoning chunks immediately
            var contentParts = new List<IPart>();
            var textParts = new List<string>();
            foreach (var c in update.ContentUpdate)
            {
                // Native reasoning (o1/o3): pass through directly
                var reasoning = GetReasoningContent(c);
                if (!string.IsNullOrEmpty(reasoning))
                    contentParts.Add(new SimplePart { Reasoning = reasoning });
                // Text content: route <think> blocks to Reasoning, rest to Text
                if (!string.IsNullOrEmpty(c.Text))
                    ProcessTextChunk(c.Text, thinkState, contentParts, textParts);
            }
            if (contentParts.Count > 0)
            {
                yield return new OpenAILlmResponse
                {
                    Content = new SimpleContent { Role = "model", Parts = contentParts },
                    Text = string.Join("", textParts),
                    FunctionCalls = null,
                    FinishReason = update.FinishReason?.ToString(),
                    Usage = null
                };
            }
        }

        // Flush unclosed <think> block (truncated stream)
        if (thinkState.InBlock && thinkState.Buffer.Length > 0)
        {
            yield return new OpenAILlmResponse
            {
                Content = new SimpleContent { Role = "model", Parts = [new SimplePart { Reasoning = thinkState.Buffer.ToString() }] },
                Text = null,
                FunctionCalls = null,
                FinishReason = null,
                Usage = null
            };
        }

        // Yield assembled complete tool calls after streaming ends
        if (toolCallMap.Count > 0)
        {
            var calls = toolCallMap.Values
                .Select(s => (IFunctionCall)new AssembledFunctionCall(s.Id, s.Name, s.Args.ToString()))
                .ToList();
            yield return new OpenAILlmResponse
            {
                Content = null,
                Text = null,
                FunctionCalls = calls,
                FinishReason = "tool_calls",
                Usage = null
            };
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
            // Skip empty Parts list (streaming updates with no content)
            if (content.Parts == null || content.Parts.Count == 0)
                continue;

            // Skip content with only empty text parts (no function calls/responses/images)
            if (content.Parts.All(p =>
                string.IsNullOrWhiteSpace(p.Text) &&
                p.FunctionCall == null &&
                p.FunctionResponse == null &&
                p.InlineData == null))
                continue;

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

            // Set tool choice if specified
            if (!string.IsNullOrEmpty(request.ToolChoice))
            {
                options.ToolChoice = request.ToolChoice.ToLowerInvariant() switch
                {
                    "auto" => ChatToolChoice.CreateAutoChoice(),
                    "required" or "any" => ChatToolChoice.CreateRequiredChoice(),
                    "none" => ChatToolChoice.CreateNoneChoice(),
                    _ => ChatToolChoice.CreateFunctionChoice(request.ToolChoice)
                };
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
            
            if (!string.IsNullOrEmpty(request.Config.ReasoningEffort))
            {
                options.ReasoningEffortLevel = request.Config.ReasoningEffort.ToLowerInvariant() switch
                {
                    "low" => ChatReasoningEffortLevel.Low,
                    "medium" => ChatReasoningEffortLevel.Medium,
                    "high" => ChatReasoningEffortLevel.High,
                    _ => ChatReasoningEffortLevel.Medium
                };
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
        var multimodalParts = new List<ChatMessageContentPart>();
        var hasMultimodalContent = false;
        var toolCalls = new List<ChatToolCall>();
        var toolCallId = string.Empty;

        foreach (var part in content.Parts)
        {
            if (part.Text != null)
            {
                textParts.Add(part.Text);
                multimodalParts.Add(ChatMessageContentPart.CreateTextPart(part.Text));
            }

            if (part.InlineData != null && part.MimeType != null)
            {
                // Use BinaryData overload — Uri ctor normalizes data: URIs and corrupts base64
                hasMultimodalContent = true;
                multimodalParts.Add(ChatMessageContentPart.CreateImagePart(
                    imageBytes: BinaryData.FromBytes(part.InlineData),
                    imageBytesMediaType: part.MimeType
                ));
            }

            if (part.FunctionCall != null)
            {
                // OpenAI function calls
                var functionCallJson = part.FunctionCall.Args != null
                    ? System.Text.Json.JsonSerializer.Serialize(part.FunctionCall.Args)
                    : "{}";
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
            // Use multimodal content if images/binary data present
            if (hasMultimodalContent)
            {
                return new UserChatMessage(multimodalParts);
            }
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

    /// <summary>
    /// Extract reasoning content from ChatMessageContentPart.
    /// OpenAI reasoning models (o1, o3) and compatible APIs (DeepSeek R1) 
    /// may include reasoning in various ways depending on SDK version.
    /// </summary>
    private static string? GetReasoningContent(ChatMessageContentPart content)
    {
        // Try to access reasoning via reflection (for newer SDK versions)
        // The OpenAI SDK may expose reasoning as a property or via Kind
        try
        {
            // Check if this is a reasoning part by examining available properties
            var type = content.GetType();
            
            // Try "Reasoning" property (if SDK adds it)
            var reasoningProp = type.GetProperty("Reasoning");
            if (reasoningProp != null)
            {
                var value = reasoningProp.GetValue(content) as string;
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            
            // Try "Kind" property to check if it's a reasoning chunk
            var kindProp = type.GetProperty("Kind");
            if (kindProp != null)
            {
                var kind = kindProp.GetValue(content)?.ToString();
                if (kind?.Contains("Reasoning", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // If it's marked as reasoning, the Text might contain the reasoning
                    return content.Text;
                }
            }
        }
        catch
        {
            // Reflection failed, skip
        }
        
        return null;
    }

    /// <summary>
    /// Tracks <think>...</think> parse state across streaming chunks.
    /// </summary>
    private sealed class ThinkParseState
    {
        public bool InBlock { get; set; }
        public StringBuilder Buffer { get; } = new();
    }

    /// <summary>
    /// Routes text chunk content: <think> blocks → Reasoning parts, rest → Text parts.
    /// Handles blocks split across multiple stream chunks.
    /// </summary>
    private static void ProcessTextChunk(
        string chunk,
        ThinkParseState state,
        List<IPart> contentParts,
        List<string> textParts)
    {
        const string openTag  = "<think>";
        const string closeTag = "</think>";
        var pos = 0;
        while (pos < chunk.Length)
        {
            if (!state.InBlock)
            {
                var open = chunk.IndexOf(openTag, pos, StringComparison.OrdinalIgnoreCase);
                if (open < 0)
                {
                    var text = chunk[pos..];
                    textParts.Add(text);
                    contentParts.Add(new SimplePart { Text = text });
                    break;
                }
                if (open > pos)
                {
                    var text = chunk[pos..open];
                    textParts.Add(text);
                    contentParts.Add(new SimplePart { Text = text });
                }
                state.InBlock = true;
                pos = open + openTag.Length;
            }
            else
            {
                var close = chunk.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);
                if (close < 0)
                {
                    state.Buffer.Append(chunk[pos..]);
                    break;
                }
                state.Buffer.Append(chunk[pos..close]);
                var thinking = state.Buffer.ToString();
                if (thinking.Length > 0)
                    contentParts.Add(new SimplePart { Reasoning = thinking });
                state.Buffer.Clear();
                state.InBlock = false;
                pos = close + closeTag.Length;
            }
        }
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
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(argsJson);
                var dict = new Dictionary<string, object>();

                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    dict[property.Name] = ConvertElement(property.Value);
                }

                return dict;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static object ConvertElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString()!,
            System.Text.Json.JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null!,
            System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
            System.Text.Json.JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertElement(p.Value)),
            _ => element.GetRawText()
        };
    }

    public string? Id => _toolCall?.Id ?? _toolCallUpdate?.ToolCallId;
}

/// <summary>
/// Complete assembled function call from streaming deltas
/// </summary>
internal class AssembledFunctionCall : IFunctionCall
{
    private readonly string _argsJson;

    public AssembledFunctionCall(string id, string name, string argsJson)
    {
        Id = id;
        Name = name;
        _argsJson = argsJson;
    }

    public string Name { get; }
    public string? Id { get; }

    public IReadOnlyDictionary<string, object>? Args
    {
        get
        {
            if (string.IsNullOrEmpty(_argsJson)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(_argsJson);
                var dict = new Dictionary<string, object>();
                foreach (var prop in doc.RootElement.EnumerateObject())
                    dict[prop.Name] = OpenAIFunctionCall.ConvertElement(prop.Value);
                return dict;
            }
            catch { return null; }
        }
    }
}

internal class OpenAIUsageMetadata : IUsageMetadata
{
    public required int PromptTokenCount { get; init; }
    public required int CandidatesTokenCount { get; init; }
    public required int TotalTokenCount { get; init; }
}
