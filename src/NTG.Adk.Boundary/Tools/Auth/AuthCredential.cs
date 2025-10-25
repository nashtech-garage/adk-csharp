// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

namespace NTG.Adk.Boundary.Tools.Auth;

/// <summary>
/// Authentication credential types.
/// </summary>
public enum AuthCredentialType
{
    /// <summary>
    /// API Key credential
    /// </summary>
    ApiKey,

    /// <summary>
    /// HTTP Basic/Bearer credential
    /// </summary>
    Http,

    /// <summary>
    /// OAuth 2.0 credential
    /// </summary>
    OAuth2,

    /// <summary>
    /// OpenID Connect credential
    /// </summary>
    OpenIdConnect,

    /// <summary>
    /// Service Account credential (Google Cloud)
    /// </summary>
    ServiceAccount
}

/// <summary>
/// Base class for authentication credentials.
/// </summary>
public abstract record AuthCredential
{
    /// <summary>
    /// Type of authentication credential
    /// </summary>
    public abstract AuthCredentialType Type { get; }
}

/// <summary>
/// API Key authentication credential.
/// </summary>
public sealed record ApiKeyCredential : AuthCredential
{
    public override AuthCredentialType Type => AuthCredentialType.ApiKey;

    /// <summary>
    /// API key value
    /// </summary>
    public required string ApiKey { get; init; }
}

/// <summary>
/// HTTP authentication credential.
/// </summary>
public sealed record HttpCredential : AuthCredential
{
    public override AuthCredentialType Type => AuthCredentialType.Http;

    /// <summary>
    /// HTTP scheme (basic, bearer)
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// Token or credentials value
    /// </summary>
    public required string Value { get; init; }
}

/// <summary>
/// OAuth 2.0 authentication credential.
/// </summary>
public sealed record OAuth2Credential : AuthCredential
{
    public override AuthCredentialType Type => AuthCredentialType.OAuth2;

    /// <summary>
    /// OAuth2 client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// OAuth2 client secret
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Access token (if already obtained)
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Refresh token (if available)
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Requested scopes
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }
}

/// <summary>
/// OpenID Connect authentication credential.
/// </summary>
public sealed record OpenIdConnectCredential : AuthCredential
{
    public override AuthCredentialType Type => AuthCredentialType.OpenIdConnect;

    /// <summary>
    /// Client ID
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Client secret
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// ID token (if already obtained)
    /// </summary>
    public string? IdToken { get; init; }

    /// <summary>
    /// Access token (if already obtained)
    /// </summary>
    public string? AccessToken { get; init; }
}

/// <summary>
/// Service Account authentication credential.
/// </summary>
public sealed record ServiceAccountCredential : AuthCredential
{
    public override AuthCredentialType Type => AuthCredentialType.ServiceAccount;

    /// <summary>
    /// Service account JSON key content
    /// </summary>
    public required string JsonKeyContent { get; init; }

    /// <summary>
    /// Scopes for service account
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; init; }
}
