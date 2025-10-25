// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using NTG.Adk.CoreAbstractions.Artifacts;

namespace NTG.Adk.Implementations.Artifacts;

/// <summary>
/// File-based implementation of IArtifactService.
/// Stores artifacts as files on disk with automatic versioning.
/// Suitable for single-machine production use and development.
/// </summary>
public class FileArtifactService : IArtifactService
{
    private readonly string _baseDirectory;

    /// <summary>
    /// Create a new FileArtifactService with a base directory for artifact storage.
    /// </summary>
    /// <param name="baseDirectory">Base directory where artifacts will be stored. Defaults to "./artifacts"</param>
    public FileArtifactService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<int> SaveArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        byte[] data,
        string? mimeType = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = GetArtifactDirectory(appName, userId, sessionId, filename);
        Directory.CreateDirectory(artifactDir);

        // Get next version number
        var existingVersions = Directory.GetFiles(artifactDir, "v*.dat");
        var version = existingVersions.Length + 1;

        // Save data file
        var dataPath = Path.Combine(artifactDir, $"v{version}.dat");
        await File.WriteAllBytesAsync(dataPath, data, cancellationToken);

        // Save metadata
        var metadataObj = new FileArtifactMetadata
        {
            Filename = filename,
            Version = version,
            MimeType = mimeType,
            SizeBytes = data.Length,
            CreatedAt = DateTimeOffset.UtcNow,
            CustomMetadata = metadata
        };

        var metadataPath = Path.Combine(artifactDir, $"v{version}.meta.json");
        var metadataJson = JsonSerializer.Serialize(metadataObj, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metadataPath, metadataJson, cancellationToken);

        return version;
    }

    public async Task<byte[]?> LoadArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = GetArtifactDirectory(appName, userId, sessionId, filename);
        if (!Directory.Exists(artifactDir))
            return null;

        // Get version to load
        var versionNumber = version ?? GetLatestVersion(artifactDir);
        if (versionNumber == 0)
            return null;

        var dataPath = Path.Combine(artifactDir, $"v{versionNumber}.dat");
        if (!File.Exists(dataPath))
            return null;

        return await File.ReadAllBytesAsync(dataPath, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListArtifactKeysAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionDir = GetSessionDirectory(appName, userId, sessionId);
        if (!Directory.Exists(sessionDir))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var artifactDirs = Directory.GetDirectories(sessionDir);
        var filenames = artifactDirs.Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
        return Task.FromResult<IReadOnlyList<string>>(filenames);
    }

    public Task DeleteArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = GetArtifactDirectory(appName, userId, sessionId, filename);
        if (Directory.Exists(artifactDir))
        {
            Directory.Delete(artifactDir, recursive: true);
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
        var artifactDir = GetArtifactDirectory(appName, userId, sessionId, filename);
        if (!Directory.Exists(artifactDir))
            return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        var dataFiles = Directory.GetFiles(artifactDir, "v*.dat");
        var versions = dataFiles
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(f => f.Substring(1)) // Remove 'v' prefix
            .Select(int.Parse)
            .OrderBy(v => v)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(versions);
    }

    public async Task<IArtifactMetadata?> GetArtifactMetadataAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default)
    {
        var artifactDir = GetArtifactDirectory(appName, userId, sessionId, filename);
        if (!Directory.Exists(artifactDir))
            return null;

        // Get version to load
        var versionNumber = version ?? GetLatestVersion(artifactDir);
        if (versionNumber == 0)
            return null;

        var metadataPath = Path.Combine(artifactDir, $"v{versionNumber}.meta.json");
        if (!File.Exists(metadataPath))
            return null;

        var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        return JsonSerializer.Deserialize<FileArtifactMetadata>(metadataJson);
    }

    // Get artifact directory: {baseDir}/{appName}/{userId}/{sessionId}/{filename}/
    private string GetArtifactDirectory(string appName, string userId, string sessionId, string filename)
    {
        return Path.Combine(
            _baseDirectory,
            SanitizePath(appName),
            SanitizePath(userId),
            SanitizePath(sessionId),
            SanitizePath(filename));
    }

    // Get session directory: {baseDir}/{appName}/{userId}/{sessionId}/
    private string GetSessionDirectory(string appName, string userId, string sessionId)
    {
        return Path.Combine(
            _baseDirectory,
            SanitizePath(appName),
            SanitizePath(userId),
            SanitizePath(sessionId));
    }

    // Sanitize path component to avoid directory traversal
    private static string SanitizePath(string pathComponent)
    {
        return pathComponent.Replace("..", "_").Replace("/", "_").Replace("\\", "_");
    }

    // Get latest version number from artifact directory
    private static int GetLatestVersion(string artifactDir)
    {
        var dataFiles = Directory.GetFiles(artifactDir, "v*.dat");
        if (dataFiles.Length == 0)
            return 0;

        return dataFiles
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(f => f.Substring(1)) // Remove 'v' prefix
            .Select(int.Parse)
            .Max();
    }

    private class FileArtifactMetadata : IArtifactMetadata
    {
        public required string Filename { get; init; }
        public required int Version { get; init; }
        public string? MimeType { get; init; }
        public required long SizeBytes { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public IReadOnlyDictionary<string, object>? CustomMetadata { get; init; }
    }
}
