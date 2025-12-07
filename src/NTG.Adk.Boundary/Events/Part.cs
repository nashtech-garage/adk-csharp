// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Events;

/// <summary>
/// A single part of content (text, function call, function response, etc.).
/// Equivalent to google.genai.types.Part in Python.
/// </summary>
public record Part
{
    /// <summary>
    /// Text content
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Reasoning/thinking content (for models like DeepSeek R1, OpenAI o1/o3)
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// Function call
    /// </summary>
    public FunctionCall? FunctionCall { get; init; }

    /// <summary>
    /// Function response
    /// </summary>
    public FunctionResponse? FunctionResponse { get; init; }

    /// <summary>
    /// Binary data (images, etc.)
    /// </summary>
    public byte[]? InlineData { get; init; }

    /// <summary>
    /// MIME type for inline data
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Create a text part
    /// </summary>
    public static Part FromText(string text) => new() { Text = text };

    /// <summary>
    /// Create a reasoning part
    /// </summary>
    public static Part FromReasoning(string reasoning) => new() { Reasoning = reasoning };

    /// <summary>
    /// Create a function call part
    /// </summary>
    public static Part FromFunctionCall(FunctionCall functionCall) => new() { FunctionCall = functionCall };

    /// <summary>
    /// Create a function response part
    /// </summary>
    public static Part FromFunctionResponse(FunctionResponse functionResponse) => new() { FunctionResponse = functionResponse };

    /// <summary>
    /// Create a binary data part
    /// </summary>
    public static Part FromBytes(byte[] data, string mimeType) => new()
    {
        InlineData = data,
        MimeType = mimeType
    };
}
