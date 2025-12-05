// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Operators.Internal;

/// <summary>
/// Shared LLM request implementation classes.
/// Internal optimization - not part of public API.
/// </summary>
internal class LlmRequestImpl : ILlmRequest
{
    public required string? SystemInstruction { get; init; }
    public required List<IContent> Contents { get; init; }
    IReadOnlyList<IContent> ILlmRequest.Contents => Contents;
    public IReadOnlyList<IFunctionDeclaration>? Tools { get; init; }
    public string? ToolChoice { get; init; }
    public IGenerationConfig? Config { get; init; }
}

internal class SimpleContent : IContent
{
    public required string? Role { get; init; }
    public required List<IPart> Parts { get; init; }
    IReadOnlyList<IPart> IContent.Parts => Parts;
}

internal class SimplePart : IPart
{
    public string? Text { get; init; }
    public string? Reasoning { get; init; }
    public IFunctionCall? FunctionCall { get; init; }
    public IFunctionResponse? FunctionResponse { get; init; }
    public byte[]? InlineData { get; init; }
    public string? MimeType { get; init; }
}
