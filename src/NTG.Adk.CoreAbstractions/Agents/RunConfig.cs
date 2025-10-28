// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Compaction;

namespace NTG.Adk.CoreAbstractions.Agents;

/// <summary>
/// Configuration for agent execution.
/// Equivalent to google.adk.agents.RunConfig in Python.
/// </summary>
public class RunConfig
{
    /// <summary>
    /// Max number of LLM calls (including tool execution rounds) for a single invocation.
    /// Default: 500 (matches Python ADK)
    ///
    /// Valid Values:
    ///   - Greater than 0: Enforces the limit
    ///   - Less than or equal to 0: Allows unbounded LLM calls
    /// </summary>
    public int MaxLlmCalls { get; set; } = 500;

    /// <summary>
    /// Streaming mode for LLM responses.
    /// Default: StreamingMode.None (matches Python ADK - no streaming by default)
    ///
    /// Modes:
    ///   - None: Buffer complete response before returning
    ///   - Sse: Server-sent events - one-way token-by-token streaming
    ///   - Bidi: Bidirectional streaming
    /// </summary>
    public StreamingMode StreamingMode { get; set; } = StreamingMode.None;

    /// <summary>
    /// Sliding window compaction configuration.
    /// Default: null (no compaction - must be explicitly configured)
    /// </summary>
    public EventsCompactionConfig? EventsCompactionConfig { get; set; }
}
