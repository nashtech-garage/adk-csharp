// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.Implementations.Events;

namespace NTG.Adk.Implementations.Models;

/// <summary>
/// Mock LLM for testing.
/// Adapter implementing ILlm port.
/// </summary>
public class MockLlm : ILlm
{
    public string ModelName => "mock-llm";

    public Task<ILlmResponse> GenerateAsync(
        ILlmRequest request,
        CancellationToken cancellationToken = default)
    {
        // Simple echo response
        var userMessage = request.Contents.LastOrDefault();
        var responseText = $"Mock response to: {GetTextFromContent(userMessage)}";

        var response = new MockLlmResponse
        {
            Content = new ContentAdapter(Boundary.Events.Content.FromText(responseText, "model")),
            Text = responseText,
            FinishReason = "stop"
        };

        return Task.FromResult<ILlmResponse>(response);
    }

    public async IAsyncEnumerable<ILlmResponse> GenerateStreamAsync(
        ILlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var response = await GenerateAsync(request, cancellationToken);
        yield return response;
    }

    private string GetTextFromContent(IContent? content)
    {
        if (content?.Parts == null) return string.Empty;

        return string.Join(" ", content.Parts
            .Where(p => p.Text != null)
            .Select(p => p.Text));
    }
}

/// <summary>
/// Mock LLM response implementation
/// </summary>
internal class MockLlmResponse : ILlmResponse
{
    public required IContent? Content { get; init; }
    public required string? Text { get; init; }
    public IReadOnlyList<IFunctionCall>? FunctionCalls { get; init; }
    public required string? FinishReason { get; init; }
    public IUsageMetadata? Usage { get; init; }
}
