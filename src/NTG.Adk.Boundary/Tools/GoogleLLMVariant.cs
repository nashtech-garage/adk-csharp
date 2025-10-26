// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Tools;

/// <summary>
/// Google LLM API variants.
/// Equivalent to google.adk.tools._automatic_function_calling_util.GoogleLLMVariant in Python.
/// </summary>
public enum GoogleLLMVariant
{
    /// <summary>
    /// Gemini API (default) - public API without response schema
    /// </summary>
    GeminiApi,

    /// <summary>
    /// Vertex AI API - enterprise API with response schema support
    /// </summary>
    VertexAi
}
