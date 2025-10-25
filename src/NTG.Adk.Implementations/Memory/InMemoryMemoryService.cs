// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using NTG.Adk.CoreAbstractions.Memory;

namespace NTG.Adk.Implementations.Memory;

/// <summary>
/// In-memory implementation of IMemoryService.
/// Equivalent to google.adk.memory.InMemoryMemoryService in Python.
///
/// Note: Not suitable for production - data is lost when process terminates.
/// Use for testing and development only.
/// </summary>
public class InMemoryMemoryService : IMemoryService
{
    // Three-level storage: app -> user -> key -> value
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, object>>> _storage = new();

    public async Task RememberAsync(
        string appName,
        string userId,
        string key,
        object value,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, key);
        ArgumentNullException.ThrowIfNull(value);

        var appStorage = _storage.GetOrAdd(appName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>());
        var userStorage = appStorage.GetOrAdd(userId, _ => new ConcurrentDictionary<string, object>());

        userStorage[key] = value;

        await Task.CompletedTask;
    }

    public async Task<T?> RecallAsync<T>(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, key);

        if (!_storage.TryGetValue(appName, out var appStorage))
            return default;

        if (!appStorage.TryGetValue(userId, out var userStorage))
            return default;

        if (!userStorage.TryGetValue(key, out var value))
            return default;

        if (value is T typedValue)
            return await Task.FromResult(typedValue);

        return default;
    }

    public async Task<bool> ContainsAsync(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, key);

        if (!_storage.TryGetValue(appName, out var appStorage))
            return false;

        if (!appStorage.TryGetValue(userId, out var userStorage))
            return false;

        return await Task.FromResult(userStorage.ContainsKey(key));
    }

    public async Task ForgetAsync(
        string appName,
        string userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, key);

        if (!_storage.TryGetValue(appName, out var appStorage))
            return;

        if (!appStorage.TryGetValue(userId, out var userStorage))
            return;

        userStorage.TryRemove(key, out _);

        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(
        string appName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, null);

        if (!_storage.TryGetValue(appName, out var appStorage))
            return Array.Empty<string>();

        if (!appStorage.TryGetValue(userId, out var userStorage))
            return Array.Empty<string>();

        return await Task.FromResult(userStorage.Keys.ToList());
    }

    public async Task ClearAsync(
        string appName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ValidateInputs(appName, userId, null);

        if (!_storage.TryGetValue(appName, out var appStorage))
            return;

        appStorage.TryRemove(userId, out _);

        await Task.CompletedTask;
    }

    // Validate inputs
    private static void ValidateInputs(string appName, string userId, string? key)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty", nameof(appName));

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        if (key != null && string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));
    }
}
