// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.Boundary.Tools.Auth;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools.OpenApi;

/// <summary>
/// A generic tool that interacts with a REST API endpoint.
/// Represents a single operation from an OpenAPI specification.
/// Based on Google ADK Python implementation.
/// </summary>
public sealed class RestApiTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _path;
    private readonly string _method;
    private readonly FunctionDeclaration _declaration;
    private AuthScheme? _authScheme;
    private AuthCredential? _authCredential;

    public string Name { get; }
    public string? Description { get; }

    /// <summary>
    /// Creates a new REST API tool.
    /// </summary>
    /// <param name="name">Tool name (usually operationId in snake_case)</param>
    /// <param name="description">Tool description</param>
    /// <param name="baseUrl">API base URL</param>
    /// <param name="path">API path (e.g., "/users/{userId}")</param>
    /// <param name="method">HTTP method (GET, POST, PUT, DELETE, etc.)</param>
    /// <param name="parameters">Function parameters schema</param>
    /// <param name="authScheme">Authentication scheme (optional)</param>
    /// <param name="authCredential">Authentication credential (optional)</param>
    /// <param name="httpClient">HTTP client (optional, for testing)</param>
    public RestApiTool(
        string name,
        string? description,
        string baseUrl,
        string path,
        string method,
        Schema parameters,
        AuthScheme? authScheme = null,
        AuthCredential? authCredential = null,
        HttpClient? httpClient = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description;
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _method = method?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(method));
        _authScheme = authScheme;
        _authCredential = authCredential;
        _httpClient = httpClient ?? new HttpClient();

        _declaration = new FunctionDeclaration
        {
            Name = name,
            Description = description,
            Parameters = parameters
        };
    }

    public IFunctionDeclaration GetDeclaration()
    {
        return new FunctionDeclarationAdapter(_declaration);
    }

    public async Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build request URL
            var url = BuildUrl(args);

            // Create HTTP request
            var request = new HttpRequestMessage(new HttpMethod(_method), url);

            // Apply authentication
            ApplyAuthentication(request);

            // Add request body for POST/PUT/PATCH
            if (_method is "POST" or "PUT" or "PATCH")
            {
                var body = BuildRequestBody(args);
                if (body != null)
                {
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(body),
                        Encoding.UTF8,
                        "application/json");
                }
            }

            // Execute request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Read response
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // Parse response
            object? responseData = null;
            if (!string.IsNullOrWhiteSpace(responseContent))
            {
                try
                {
                    responseData = JsonSerializer.Deserialize<object>(responseContent);
                }
                catch
                {
                    responseData = responseContent;
                }
            }

            return new
            {
                success = response.IsSuccessStatusCode,
                statusCode = (int)response.StatusCode,
                data = responseData
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message,
                errorType = ex.GetType().Name
            };
        }
    }

    /// <summary>
    /// Configure authentication scheme.
    /// </summary>
    public void ConfigureAuthScheme(AuthScheme authScheme)
    {
        _authScheme = authScheme;
    }

    /// <summary>
    /// Configure authentication credential.
    /// </summary>
    public void ConfigureAuthCredential(AuthCredential authCredential)
    {
        _authCredential = authCredential;
    }

    // Build full URL with path parameters and query parameters
    private string BuildUrl(IReadOnlyDictionary<string, object> args)
    {
        var url = _baseUrl.TrimEnd('/') + "/" + _path.TrimStart('/');

        // Replace path parameters (e.g., /users/{userId})
        foreach (var kvp in args)
        {
            var placeholder = $"{{{kvp.Key}}}";
            if (url.Contains(placeholder))
            {
                url = url.Replace(placeholder, Uri.EscapeDataString(kvp.Value.ToString()!));
            }
        }

        // Add query parameters for GET/DELETE
        if (_method is "GET" or "DELETE")
        {
            var queryParams = args
                .Where(kvp => !url.Contains($"{{{kvp.Key}}}")) // Exclude path params
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value.ToString()!)}")
                .ToList();

            if (queryParams.Any())
            {
                url += "?" + string.Join("&", queryParams);
            }
        }

        return url;
    }

    // Build request body for POST/PUT/PATCH
    private Dictionary<string, object>? BuildRequestBody(IReadOnlyDictionary<string, object> args)
    {
        // Exclude path parameters from body
        var bodyParams = args
            .Where(kvp => !_path.Contains($"{{{kvp.Key}}}"))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return bodyParams.Any() ? bodyParams : null;
    }

    // Apply authentication to request
    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (_authScheme == null || _authCredential == null)
            return;

        switch (_authScheme)
        {
            case ApiKeyAuthScheme apiKey when _authCredential is ApiKeyCredential apiKeyCred:
                ApplyApiKeyAuth(request, apiKey, apiKeyCred);
                break;

            case HttpAuthScheme http when _authCredential is HttpCredential httpCred:
                ApplyHttpAuth(request, http, httpCred);
                break;

            case OAuth2AuthScheme when _authCredential is OAuth2Credential oauth2Cred:
                ApplyOAuth2Auth(request, oauth2Cred);
                break;
        }
    }

    private void ApplyApiKeyAuth(HttpRequestMessage request, ApiKeyAuthScheme scheme, ApiKeyCredential credential)
    {
        switch (scheme.In.ToLowerInvariant())
        {
            case "header":
                request.Headers.Add(scheme.Name, credential.ApiKey);
                break;

            case "query":
                var separator = request.RequestUri!.Query.Contains('?') ? "&" : "?";
                var newUri = request.RequestUri + $"{separator}{Uri.EscapeDataString(scheme.Name)}={Uri.EscapeDataString(credential.ApiKey)}";
                request.RequestUri = new Uri(newUri);
                break;

            case "cookie":
                request.Headers.Add("Cookie", $"{scheme.Name}={credential.ApiKey}");
                break;
        }
    }

    private void ApplyHttpAuth(HttpRequestMessage request, HttpAuthScheme scheme, HttpCredential credential)
    {
        if (scheme.Scheme.Equals("bearer", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Authorization", $"Bearer {credential.Value}");
        }
        else if (scheme.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Authorization", $"Basic {credential.Value}");
        }
    }

    private void ApplyOAuth2Auth(HttpRequestMessage request, OAuth2Credential credential)
    {
        // Use access token if available
        if (!string.IsNullOrEmpty(credential.AccessToken))
        {
            request.Headers.Add("Authorization", $"Bearer {credential.AccessToken}");
        }
    }
}
