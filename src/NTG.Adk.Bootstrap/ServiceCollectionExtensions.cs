// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.DependencyInjection;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;
using NTG.Adk.Implementations.Sessions;
using NTG.Adk.Implementations.Tools;

namespace NTG.Adk.Bootstrap;

/// <summary>
/// DI extensions for NTG.Adk.
/// Follows A.D.D V3: Bootstrap wires implementations to ports.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register all ADK services and factories.
    /// </summary>
    public static IServiceCollection AddAdk(this IServiceCollection services)
    {
        // Register factories
        services.AddSingleton<IInvocationContextFactory, InvocationContextFactory>();
        services.AddSingleton<IToolContextFactory, ToolContextFactory>();

        return services;
    }
}
