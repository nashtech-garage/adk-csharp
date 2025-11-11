// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text;
using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Compaction;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Tools;
using NTG.Adk.Implementations.Events;

namespace NTG.Adk.Operators.Compaction;

/// <summary>
/// LLM-based event summarizer for sliding window compaction.
/// Equivalent to google.adk.apps.LlmEventSummarizer in Python.
/// A.D.D V3: Operators layer (business logic orchestration)
/// </summary>
public class LlmEventSummarizer : IEventSummarizer
{
    private const string DefaultPromptTemplate =
        "The following is a conversation history between a user and an AI agent. " +
        "Please summarize the conversation, focusing on key information and decisions made, " +
        "as well as any unresolved questions or tasks. The summary should be concise and " +
        "capture the essence of the interaction.\n\n{conversation_history}";

    private readonly ILlm _llm;
    private readonly string _promptTemplate;

    public LlmEventSummarizer(ILlm llm, string? promptTemplate = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _promptTemplate = promptTemplate ?? DefaultPromptTemplate;
    }

    public async Task<IEvent?> MaybeSummarizeEventsAsync(
        IReadOnlyList<IEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events == null || events.Count == 0)
            return null;

        // Format events for prompt
        var conversationHistory = FormatEventsForPrompt(events);
        var prompt = _promptTemplate.Replace("{conversation_history}", conversationHistory);

        // Create LLM request
        var request = new LlmRequestImpl
        {
            SystemInstruction = null,
            Contents = new List<IContent>
            {
                new SimpleContent
                {
                    Role = "user",
                    Parts = new List<IPart>
                    {
                        new SimplePart { Text = prompt }
                    }
                }
            }
        };

        // Call LLM
        var response = await _llm.GenerateAsync(request, cancellationToken);
        if (response.Content == null)
            return null;

        // Calculate timestamps
        var startTimestamp = events[0].Timestamp.ToUnixTimeSeconds();
        var endTimestamp = events[events.Count - 1].Timestamp.ToUnixTimeSeconds();

        // Create compaction event
        var compaction = new EventCompaction
        {
            StartTimestamp = startTimestamp,
            EndTimestamp = endTimestamp,
            CompactedContent = Content.FromText(response.Text ?? "")
        };

        var actions = new EventActions
        {
            Compaction = compaction
        };

        var compactionEvent = new Event
        {
            Author = "user",
            Actions = actions,
            InvocationId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow
        };

        return EventAdapter.FromDto(compactionEvent);
    }

    private string FormatEventsForPrompt(IReadOnlyList<IEvent> events)
    {
        var sb = new StringBuilder();
        foreach (var evt in events)
        {
            if (evt.Content?.Parts == null) continue;

            foreach (var part in evt.Content.Parts)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    sb.AppendLine($"{evt.Author}: {part.Text}");
                }
            }
        }
        return sb.ToString();
    }
}

// Helper classes (internal to avoid polluting namespace)
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
    public IFunctionCall? FunctionCall { get; init; }
    public IFunctionResponse? FunctionResponse { get; init; }
    public byte[]? InlineData { get; init; }
    public string? MimeType { get; init; }
}
