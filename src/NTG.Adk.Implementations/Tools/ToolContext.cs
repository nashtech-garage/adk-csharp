// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Default implementation of IToolContext.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class ToolContext : IToolContext
{
    public required ISession Session { get; init; }
    public required ISessionState State { get; init; }
    public string? User { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    public required IToolActions Actions { get; init; }
}
