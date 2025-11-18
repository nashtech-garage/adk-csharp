// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools;

/// <summary>
/// Default implementation of IToolActions.
/// Follows A.D.D V3: Adapter in Implementations layer.
/// </summary>
public class ToolActions : IToolActions
{
    public string? TransferToAgent { get; set; }
    public bool Escalate { get; set; }
    public bool SkipSummarization { get; set; }
}
