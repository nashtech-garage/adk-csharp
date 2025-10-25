// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Collections.Concurrent;
using NTG.Adk.CoreAbstractions.Artifacts;

namespace NTG.Adk.Implementations.Artifacts;

/// <summary>
/// In-memory implementation of IArtifactService.
/// Stores artifacts in memory with automatic versioning.
/// Not suitable for production - use for testing and development only.
/// </summary>
public class InMemoryArtifactService : IArtifactService
{
    // Storage: appName -> userId -> sessionId -> filename -> list of versions
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, List<ArtifactVersion>>>>> _artifacts = new();

    public Task<int> SaveArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        byte[] data,
        string? mimeType = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var appArtifacts = _artifacts.GetOrAdd(appName, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, List<ArtifactVersion>>>>());
        var userArtifacts = appArtifacts.GetOrAdd(userId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, List<ArtifactVersion>>>());
        var sessionArtifacts = userArtifacts.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, List<ArtifactVersion>>());
        var versions = sessionArtifacts.GetOrAdd(filename, _ => new List<ArtifactVersion>());

        lock (versions)
        {
            var version = versions.Count + 1;
            var artifact = new ArtifactVersion
            {
                Filename = filename,
                Version = version,
                Data = (byte[])data.Clone(),
                MimeType = mimeType,
                SizeBytes = data.Length,
                CreatedAt = DateTimeOffset.UtcNow,
                CustomMetadata = metadata
            };
            versions.Add(artifact);
            return Task.FromResult(version);
        }
    }

    public Task<byte[]?> LoadArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        if (!_artifacts.TryGetValue(appName, out var appArtifacts))
            return Task.FromResult<byte[]?>(null);

        if (!appArtifacts.TryGetValue(userId, out var userArtifacts))
            return Task.FromResult<byte[]?>(null);

        if (!userArtifacts.TryGetValue(sessionId, out var sessionArtifacts))
            return Task.FromResult<byte[]?>(null);

        if (!sessionArtifacts.TryGetValue(filename, out var versions))
            return Task.FromResult<byte[]?>(null);

        lock (versions)
        {
            if (versions.Count == 0)
                return Task.FromResult<byte[]?>(null);

            ArtifactVersion? artifact;
            if (version.HasValue)
            {
                artifact = versions.FirstOrDefault(v => v.Version == version.Value);
                if (artifact == null)
                    return Task.FromResult<byte[]?>(null);
            }
            else
            {
                artifact = versions[^1]; // Latest version
            }

            return Task.FromResult<byte[]?>((byte[])artifact.Data.Clone());
        }
    }

    public Task<IReadOnlyList<string>> ListArtifactKeysAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_artifacts.TryGetValue(appName, out var appArtifacts))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        if (!appArtifacts.TryGetValue(userId, out var userArtifacts))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        if (!userArtifacts.TryGetValue(sessionId, out var sessionArtifacts))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var keys = sessionArtifacts.Keys.ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys);
    }

    public Task DeleteArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        CancellationToken cancellationToken = default)
    {
        if (_artifacts.TryGetValue(appName, out var appArtifacts) &&
            appArtifacts.TryGetValue(userId, out var userArtifacts) &&
            userArtifacts.TryGetValue(sessionId, out var sessionArtifacts))
        {
            sessionArtifacts.TryRemove(filename, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<int>> ListVersionsAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        CancellationToken cancellationToken = default)
    {
        if (!_artifacts.TryGetValue(appName, out var appArtifacts))
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        if (!appArtifacts.TryGetValue(userId, out var userArtifacts))
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        if (!userArtifacts.TryGetValue(sessionId, out var sessionArtifacts))
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        if (!sessionArtifacts.TryGetValue(filename, out var versions))
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        lock (versions)
        {
            var versionNumbers = versions.Select(v => v.Version).OrderBy(v => v).ToList();
            return Task.FromResult<IReadOnlyList<int>>(versionNumbers);
        }
    }

    public Task<IArtifactMetadata?> GetArtifactMetadataAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        if (!_artifacts.TryGetValue(appName, out var appArtifacts))
            return Task.FromResult<IArtifactMetadata?>(null);

        if (!appArtifacts.TryGetValue(userId, out var userArtifacts))
            return Task.FromResult<IArtifactMetadata?>(null);

        if (!userArtifacts.TryGetValue(sessionId, out var sessionArtifacts))
            return Task.FromResult<IArtifactMetadata?>(null);

        if (!sessionArtifacts.TryGetValue(filename, out var versions))
            return Task.FromResult<IArtifactMetadata?>(null);

        lock (versions)
        {
            if (versions.Count == 0)
                return Task.FromResult<IArtifactMetadata?>(null);

            ArtifactVersion? artifact;
            if (version.HasValue)
            {
                artifact = versions.FirstOrDefault(v => v.Version == version.Value);
                if (artifact == null)
                    return Task.FromResult<IArtifactMetadata?>(null);
            }
            else
            {
                artifact = versions[^1]; // Latest version
            }

            return Task.FromResult<IArtifactMetadata?>(artifact);
        }
    }

    private class ArtifactVersion : IArtifactMetadata
    {
        public required string Filename { get; init; }
        public required int Version { get; init; }
        public required byte[] Data { get; init; }
        public string? MimeType { get; init; }
        public required long SizeBytes { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public IReadOnlyDictionary<string, object>? CustomMetadata { get; init; }
    }
}
