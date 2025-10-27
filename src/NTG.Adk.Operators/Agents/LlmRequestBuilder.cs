// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Operators.Agents;

/// <summary>
/// Helper for creating modified ILlmRequest instances
/// </summary>
public static class LlmRequestBuilder
{
    /// <summary>
    /// Create new request with modified tools
    /// </summary>
    public static ILlmRequest WithTools(
        this ILlmRequest request,
        IReadOnlyList<IFunctionDeclaration> tools)
    {
        return new LlmRequestImpl
        {
            SystemInstruction = request.SystemInstruction,
            Contents = request.Contents.ToList(),
            Tools = tools,
            Config = request.Config
        };
    }

    /// <summary>
    /// Create new request with appended system instruction
    /// </summary>
    public static ILlmRequest WithAppendedInstruction(
        this ILlmRequest request,
        string additionalInstruction)
    {
        return new LlmRequestImpl
        {
            SystemInstruction = request.SystemInstruction + "\n\n" + additionalInstruction,
            Contents = request.Contents.ToList(),
            Tools = request.Tools,
            Config = request.Config
        };
    }
}
