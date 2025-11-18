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
    /// <param name="services">Service collection</param>
    /// <param name="usePooling">Enable object pooling for performance (default: true)</param>
    public static IServiceCollection AddAdk(this IServiceCollection services, bool usePooling = true)
    {
        if (usePooling)
        {
            // Register pooled factories for better performance
            services.AddSingleton<IInvocationContextFactory, PooledInvocationContextFactory>();
            services.AddSingleton<IToolContextFactory, PooledToolContextFactory>();
        }
        else
        {
            // Register standard factories
            services.AddSingleton<IInvocationContextFactory, InvocationContextFactory>();
            services.AddSingleton<IToolContextFactory, ToolContextFactory>();
        }

        return services;
    }
}
