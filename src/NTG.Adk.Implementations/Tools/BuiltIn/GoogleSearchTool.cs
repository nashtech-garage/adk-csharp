// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Text.Json;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools.BuiltIn;

/// <summary>
/// Google Search tool for web search capabilities.
/// Uses Google Custom Search JSON API.
/// Equivalent to google.adk.tools.google_search_tool in Python.
/// </summary>
public sealed class GoogleSearchTool : ITool
{
    private readonly string _apiKey;
    private readonly string _searchEngineId;
    private readonly HttpClient _httpClient;

    public string Name => "google_search";
    public string? Description => "Search the web using Google. Returns top search results for a given query.";

    /// <summary>
    /// Creates a new Google Search tool.
    /// </summary>
    /// <param name="apiKey">Google Custom Search API key</param>
    /// <param name="searchEngineId">Custom Search Engine ID (cx parameter)</param>
    /// <param name="httpClient">Optional HttpClient for testing</param>
    public GoogleSearchTool(string apiKey, string searchEngineId, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(searchEngineId))
            throw new ArgumentException("Search engine ID cannot be null or empty", nameof(searchEngineId));

        _apiKey = apiKey;
        _searchEngineId = searchEngineId;
        _httpClient = httpClient ?? new HttpClient();
    }

    public IFunctionDeclaration GetDeclaration()
    {
        var schema = new Schema
        {
            Type = "object",
            Properties = new Dictionary<string, SchemaProperty>
            {
                ["query"] = new SchemaProperty
                {
                    Type = "string",
                    Description = "The search query string"
                },
                ["num_results"] = new SchemaProperty
                {
                    Type = "integer",
                    Description = "Number of results to return (1-10, default 5)"
                }
            },
            Required = new List<string> { "query" }
        };

        var declaration = new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = schema
        };

        return new GoogleSearchFunctionDeclarationAdapter(declaration);
    }

    public async Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        // Extract query parameter
        if (!args.TryGetValue("query", out var queryObj) || queryObj == null)
        {
            return new { success = false, error = "Missing required parameter: query" };
        }

        var query = queryObj.ToString()!;

        // Extract num_results parameter (default 5)
        var numResults = 5;
        if (args.TryGetValue("num_results", out var numObj) && numObj != null)
        {
            if (numObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            {
                numResults = jsonElement.GetInt32();
            }
            else if (numObj is int intVal)
            {
                numResults = intVal;
            }
            else if (int.TryParse(numObj.ToString(), out var parsed))
            {
                numResults = parsed;
            }
        }

        // Clamp to valid range
        numResults = Math.Clamp(numResults, 1, 10);

        try
        {
            // Build Google Custom Search API URL
            var url = $"https://www.googleapis.com/customsearch/v1?" +
                      $"key={Uri.EscapeDataString(_apiKey)}&" +
                      $"cx={Uri.EscapeDataString(_searchEngineId)}&" +
                      $"q={Uri.EscapeDataString(query)}&" +
                      $"num={numResults}";

            // Make request
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResult = JsonSerializer.Deserialize<GoogleSearchResponse>(jsonResponse);

            if (searchResult?.Items == null || searchResult.Items.Count == 0)
            {
                return new
                {
                    success = true,
                    query = query,
                    results = Array.Empty<object>(),
                    message = "No results found"
                };
            }

            // Format results
            var results = searchResult.Items.Select(item => new
            {
                title = item.Title,
                link = item.Link,
                snippet = item.Snippet,
                displayLink = item.DisplayLink
            }).ToList();

            return new
            {
                success = true,
                query = query,
                totalResults = searchResult.SearchInformation?.TotalResults,
                results = results
            };
        }
        catch (HttpRequestException ex)
        {
            return new
            {
                success = false,
                error = $"HTTP request failed: {ex.Message}",
                query = query
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = $"Search failed: {ex.Message}",
                query = query
            };
        }
    }

    // DTOs for Google Custom Search API response
    private sealed class GoogleSearchResponse
    {
        public SearchInformation? SearchInformation { get; set; }
        public List<SearchItem>? Items { get; set; }
    }

    private sealed class SearchInformation
    {
        public string? TotalResults { get; set; }
    }

    private sealed class SearchItem
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Snippet { get; set; }
        public string? DisplayLink { get; set; }
    }
}

// Adapter for FunctionDeclaration
internal sealed class GoogleSearchFunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public GoogleSearchFunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null
        ? new GoogleSearchSchemaAdapter(_dto.Parameters)
        : null;
}

internal sealed class GoogleSearchSchemaAdapter : ISchema
{
    private readonly Schema _dto;

    public GoogleSearchSchemaAdapter(Schema dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new GoogleSearchSchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}

internal sealed class GoogleSearchSchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public GoogleSearchSchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
}
