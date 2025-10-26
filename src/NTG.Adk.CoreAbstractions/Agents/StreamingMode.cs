// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Agents;

/// <summary>
/// Streaming mode for LLM responses.
/// Equivalent to google.adk.agents.StreamingMode in Python.
/// </summary>
public enum StreamingMode
{
    /// <summary>
    /// No streaming - buffer complete response before returning.
    /// Default mode matching Python ADK.
    /// </summary>
    None,

    /// <summary>
    /// Server-sent events - one-way token-by-token streaming.
    /// LLM yields partial responses as tokens arrive.
    /// </summary>
    Sse,

    /// <summary>
    /// Bidirectional streaming - full duplex communication.
    /// </summary>
    Bidi
}
