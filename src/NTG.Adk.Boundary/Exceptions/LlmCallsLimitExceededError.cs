// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.Boundary.Exceptions;

/// <summary>
/// Exception thrown when the maximum number of LLM calls is exceeded.
/// Equivalent to google.adk.errors.LlmCallsLimitExceededError in Python.
/// </summary>
public class LlmCallsLimitExceededError : Exception
{
    public LlmCallsLimitExceededError(string message) : base(message)
    {
    }

    public LlmCallsLimitExceededError(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
