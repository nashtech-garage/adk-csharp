// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools.Retrieval;

/// <summary>
/// Retrieval tool that loads and searches files from a directory.
/// Equivalent to google.adk.tools.retrieval.FilesRetrieval in Python.
///
/// This tool indexes text files from a directory and provides keyword-based
/// retrieval capabilities. It's useful for RAG (Retrieval Augmented Generation)
/// scenarios where agents need to search through documentation or code.
///
/// Note: This is a simple keyword-based implementation. For production use
/// with large document collections, consider using vector embeddings and
/// semantic search libraries.
/// </summary>
public class FilesRetrievalTool : ITool
{
    private readonly string _inputDir;
    private readonly List<DocumentChunk> _documents;

    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// Creates a new FilesRetrievalTool that indexes files from the specified directory.
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <param name="description">Tool description</param>
    /// <param name="inputDir">Directory path containing files to index</param>
    /// <param name="filePattern">File pattern to match (default: "*.txt")</param>
    /// <param name="searchOption">Search subdirectories (default: TopDirectoryOnly)</param>
    public FilesRetrievalTool(
        string name,
        string description,
        string inputDir,
        string filePattern = "*.txt",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        Name = name;
        Description = description;
        _inputDir = inputDir;
        _documents = new List<DocumentChunk>();

        // Load and index documents
        LoadDocuments(filePattern, searchOption);
    }

    public Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!args.TryGetValue("query", out var queryObj) || queryObj is not string query)
        {
            return Task.FromResult<object>(new { error = "Missing required parameter: query" });
        }

        // Perform keyword search
        var results = SearchDocuments(query);

        if (results.Count == 0)
        {
            return Task.FromResult<object>(new { result = "No relevant documents found.", documents = Array.Empty<object>() });
        }

        // Return top result's content (matching Python ADK behavior)
        var topResult = results[0];
        return Task.FromResult<object>(new
        {
            result = topResult.Content,
            file = topResult.FileName,
            score = topResult.Score,
            all_results = results.Take(5).Select(r => new
            {
                file = r.FileName,
                score = r.Score,
                preview = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content
            }).ToArray()
        });
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
                    Description = "The search query to retrieve relevant documents"
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

        return new FilesRetrievalFunctionDeclarationAdapter(declaration);
    }

    private void LoadDocuments(string filePattern, SearchOption searchOption)
    {
        if (!Directory.Exists(_inputDir))
        {
            throw new DirectoryNotFoundException($"Directory not found: {_inputDir}");
        }

        var files = Directory.GetFiles(_inputDir, filePattern, searchOption);

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                var fileName = Path.GetFileName(file);

                // Split into chunks if content is too large
                const int chunkSize = 2000;
                if (content.Length <= chunkSize)
                {
                    _documents.Add(new DocumentChunk
                    {
                        FileName = fileName,
                        Content = content,
                        ChunkIndex = 0
                    });
                }
                else
                {
                    var chunks = SplitIntoChunks(content, chunkSize);
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        _documents.Add(new DocumentChunk
                        {
                            FileName = fileName,
                            Content = chunks[i],
                            ChunkIndex = i
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Skip files that can't be read
                Console.WriteLine($"Warning: Could not read file {file}: {ex.Message}");
            }
        }

        if (_documents.Count == 0)
        {
            Console.WriteLine($"Warning: No documents loaded from {_inputDir}");
        }
    }

    private List<string> SplitIntoChunks(string content, int chunkSize)
    {
        var chunks = new List<string>();
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();

        foreach (var line in lines)
        {
            if (currentChunk.Length + line.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
                currentChunk.Clear();
            }
            currentChunk.AppendLine(line);
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
    }

    private List<SearchResult> SearchDocuments(string query)
    {
        var queryTerms = query.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var results = new List<SearchResult>();

        foreach (var doc in _documents)
        {
            var contentLower = doc.Content.ToLowerInvariant();
            var score = 0.0;

            // Simple keyword matching with term frequency
            foreach (var term in queryTerms)
            {
                var count = CountOccurrences(contentLower, term);
                score += count * (term.Length > 3 ? 2.0 : 1.0); // Weight longer terms more
            }

            if (score > 0)
            {
                results.Add(new SearchResult
                {
                    FileName = doc.FileName,
                    Content = doc.Content,
                    ChunkIndex = doc.ChunkIndex,
                    Score = score
                });
            }
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    private int CountOccurrences(string text, string term)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += term.Length;
        }

        return count;
    }

    private class DocumentChunk
    {
        public required string FileName { get; init; }
        public required string Content { get; init; }
        public required int ChunkIndex { get; init; }
    }

    private class SearchResult
    {
        public required string FileName { get; init; }
        public required string Content { get; init; }
        public required int ChunkIndex { get; init; }
        public required double Score { get; init; }
    }
}

// Adapter classes to bridge Boundary DTOs to CoreAbstractions ports
internal sealed class FilesRetrievalFunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public FilesRetrievalFunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null
        ? new FilesRetrievalSchemaAdapter(_dto.Parameters)
        : null;
    public ISchema? Response => _dto.Response != null
        ? new FilesRetrievalSchemaAdapter(_dto.Response)
        : null;
}

internal sealed class FilesRetrievalSchemaAdapter : ISchema
{
    private readonly Schema _dto;

    public FilesRetrievalSchemaAdapter(Schema dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public IReadOnlyDictionary<string, ISchemaProperty>? Properties => _dto.Properties?
        .ToDictionary(kvp => kvp.Key, kvp => (ISchemaProperty)new FilesRetrievalSchemaPropertyAdapter(kvp.Value));
    public IReadOnlyList<string>? Required => _dto.Required;
}

internal sealed class FilesRetrievalSchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public FilesRetrievalSchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
    public ISchemaProperty? Items => _dto.Items != null ? new FilesRetrievalSchemaPropertyAdapter(_dto.Items) : null;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new FilesRetrievalSchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}
