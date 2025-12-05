// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

namespace NTG.Adk.CoreAbstractions.Artifacts;

/// <summary>
/// Service for managing artifact storage and versioning.
/// Equivalent to google.adk.artifacts.BaseArtifactService in Python.
///
/// Artifacts are binary files (images, PDFs, generated documents, etc.)
/// that agents create, modify, or share during execution.
/// </summary>
public interface IArtifactService
{
    /// <summary>
    /// Save artifact data with automatic versioning.
    /// Returns the version number assigned to this save.
    /// </summary>
    Task<int> SaveArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        byte[] data,
        string? mimeType = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Load artifact data by version.
    /// If version is null, loads the latest version.
    /// Returns null if artifact not found.
    /// </summary>
    Task<byte[]?> LoadArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all artifact filenames for a given session.
    /// </summary>
    Task<IReadOnlyList<string>> ListArtifactKeysAsync(
        string appName,
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all versions of an artifact.
    /// </summary>
    Task DeleteArtifactAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all version numbers for a specific artifact.
    /// Returns versions in ascending order.
    /// </summary>
    Task<IReadOnlyList<int>> ListVersionsAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed metadata for a specific artifact version.
    /// Returns null if not found.
    /// </summary>
    Task<IArtifactMetadata?> GetArtifactMetadataAsync(
        string appName,
        string userId,
        string sessionId,
        string filename,
        int? version = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for an artifact version.
/// </summary>
public interface IArtifactMetadata
{
    string Filename { get; }
    int Version { get; }
    string? MimeType { get; }
    long SizeBytes { get; }
    DateTimeOffset CreatedAt { get; }
    IReadOnlyDictionary<string, object>? CustomMetadata { get; }
}
