// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;

namespace NTG.Adk.CoreAbstractions.Tools;

/// <summary>
/// Port interface for providing tools dynamically based on invocation context.
/// Allows context-aware tool injection without modifying agent configuration.
/// </summary>
public interface IToolProvider
{
    /// <summary>
    /// Get tools for the given invocation context
    /// </summary>
    /// <param name="context">Current invocation context</param>
    /// <returns>Collection of tools to add</returns>
    IEnumerable<ITool> GetTools(IInvocationContext context);
}
