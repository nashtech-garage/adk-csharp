// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

namespace NTG.Adk.Boundary.Tools.Auth;

/// <summary>
/// Base class for authentication schemes.
/// Based on OpenAPI Security Scheme specification.
/// </summary>
public abstract record AuthScheme
{
    /// <summary>
    /// Type of authentication scheme
    /// </summary>
    public abstract string Type { get; }
}

/// <summary>
/// API Key authentication scheme.
/// </summary>
public sealed record ApiKeyAuthScheme : AuthScheme
{
    public override string Type => "apiKey";

    /// <summary>
    /// Location of the API key (header, query, cookie)
    /// </summary>
    public required string In { get; init; }

    /// <summary>
    /// Name of the parameter/header
    /// </summary>
    public required string Name { get; init; }
}

/// <summary>
/// HTTP authentication scheme (Basic, Bearer, etc.)
/// </summary>
public sealed record HttpAuthScheme : AuthScheme
{
    public override string Type => "http";

    /// <summary>
    /// HTTP auth scheme name (basic, bearer, digest, etc.)
    /// </summary>
    public required string Scheme { get; init; }

    /// <summary>
    /// Bearer format hint (e.g., "JWT")
    /// </summary>
    public string? BearerFormat { get; init; }
}

/// <summary>
/// OAuth 2.0 authentication scheme.
/// </summary>
public sealed record OAuth2AuthScheme : AuthScheme
{
    public override string Type => "oauth2";

    /// <summary>
    /// OAuth2 flows configuration
    /// </summary>
    public required OAuth2Flows Flows { get; init; }
}

/// <summary>
/// OAuth2 flows configuration.
/// </summary>
public sealed record OAuth2Flows
{
    /// <summary>
    /// Authorization Code flow
    /// </summary>
    public OAuth2Flow? AuthorizationCode { get; init; }

    /// <summary>
    /// Implicit flow
    /// </summary>
    public OAuth2Flow? Implicit { get; init; }

    /// <summary>
    /// Password flow (Resource Owner Password Credentials)
    /// </summary>
    public OAuth2Flow? Password { get; init; }

    /// <summary>
    /// Client Credentials flow
    /// </summary>
    public OAuth2Flow? ClientCredentials { get; init; }
}

/// <summary>
/// OAuth2 flow configuration.
/// </summary>
public sealed record OAuth2Flow
{
    /// <summary>
    /// Authorization URL (for authorizationCode and implicit)
    /// </summary>
    public string? AuthorizationUrl { get; init; }

    /// <summary>
    /// Token URL (for authorizationCode, password, clientCredentials)
    /// </summary>
    public string? TokenUrl { get; init; }

    /// <summary>
    /// Refresh URL (optional)
    /// </summary>
    public string? RefreshUrl { get; init; }

    /// <summary>
    /// Available scopes
    /// </summary>
    public IReadOnlyDictionary<string, string>? Scopes { get; init; }
}

/// <summary>
/// OpenID Connect authentication scheme.
/// </summary>
public sealed record OpenIdConnectAuthScheme : AuthScheme
{
    public override string Type => "openIdConnect";

    /// <summary>
    /// OpenID Connect URL to discover configuration
    /// </summary>
    public required string OpenIdConnectUrl { get; init; }
}
