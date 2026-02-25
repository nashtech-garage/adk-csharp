// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.CoreAbstractions.Models;

/// <summary>
/// Port interface for LLM providers.
/// Equivalent to google.adk.models.BaseLlm in Python.
/// </summary>
public interface ILlm
{
    /// <summary>
    /// Model name/identifier
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Generate content from a request
    /// </summary>
    Task<ILlmResponse> GenerateAsync(
        ILlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate content with streaming
    /// </summary>
    IAsyncEnumerable<ILlmResponse> GenerateStreamAsync(
        ILlmRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM request interface
/// </summary>
public interface ILlmRequest
{
    /// <summary>
    /// System instruction
    /// </summary>
    string? SystemInstruction { get; }

    /// <summary>
    /// Message history (contents)
    /// </summary>
    IReadOnlyList<IContent> Contents { get; }

    /// <summary>
    /// Available tools for function calling
    /// </summary>
    IReadOnlyList<IFunctionDeclaration>? Tools { get; }

    /// <summary>
    /// Tool choice mode (auto, required, none, or specific tool name)
    /// </summary>
    string? ToolChoice { get; }

    /// <summary>
    /// Generation config (temperature, top_p, etc.)
    /// </summary>
    IGenerationConfig? Config { get; }
}

/// <summary>
/// LLM response interface
/// </summary>
public interface ILlmResponse
{
    /// <summary>
    /// Generated content
    /// </summary>
    IContent? Content { get; }

    /// <summary>
    /// Response text (convenience accessor)
    /// </summary>
    string? Text { get; }

    /// <summary>
    /// Function calls from the model
    /// </summary>
    IReadOnlyList<IFunctionCall>? FunctionCalls { get; }

    /// <summary>
    /// Finish reason (stop, length, etc.)
    /// </summary>
    string? FinishReason { get; }

    /// <summary>
    /// Usage metadata (token counts)
    /// </summary>
    IUsageMetadata? Usage { get; }
}

/// <summary>
/// Generation config interface
/// </summary>
public interface IGenerationConfig
{
    double? Temperature { get; }
    double? TopP { get; }
    int? TopK { get; }
    int? MaxOutputTokens { get; }
    List<string>? StopSequences { get; }
    
    /// <summary>
    /// Reasoning effort level (low, medium, high) for models like OpenAI o1/o3
    /// </summary>
    string? ReasoningEffort { get; }
}

/// <summary>
/// Usage metadata interface
/// </summary>
public interface IUsageMetadata
{
    int PromptTokenCount { get; }
    int CandidatesTokenCount { get; }
    int TotalTokenCount { get; }
}
