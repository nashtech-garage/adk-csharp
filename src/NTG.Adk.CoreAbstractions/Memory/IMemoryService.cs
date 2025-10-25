// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Memory;

/// <summary>
/// Service for managing long-term agent memory.
/// Equivalent to google.adk.memory.BaseMemoryService in Python.
///
/// Memory service provides persistent key-value storage that can outlive
/// individual sessions, enabling agents to "remember" information across
/// multiple interactions.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Store a memory value.
    /// </summary>
    Task RememberAsync(
        string appName,
        string userId,
        string key,
        object value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a memory value.
    /// Returns null if key not found.
    /// </summary>
    Task<T?> RecallAsync<T>(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a memory key exists.
    /// </summary>
    Task<bool> ContainsAsync(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a memory key.
    /// </summary>
    Task ForgetAsync(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all memory keys for a user.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(
        string appName,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all memories for a user.
    /// </summary>
    Task ClearAsync(
        string appName,
        string userId,
        CancellationToken cancellationToken = default);
}
