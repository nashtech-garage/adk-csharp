// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text;
using System.Text.Json;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools.Retrieval;

/// <summary>
/// Agentic retrieval tool that uses LLM-powered query expansion instead of vector embeddings.
/// This approach leverages grep + LLM reasoning to achieve semantic search without embeddings.
///
/// **How it works:**
/// 1. LLM generates search terms (synonyms, related concepts, expansions)
/// 2. Multi-pass grep searches with expanded queries
/// 3. LLM ranks results by semantic relevance
/// 4. Iterative refinement based on initial results
///
/// **Advantages over vector embeddings:**
/// - No embedding model or vector database needed
/// - More interpretable (you can see the search terms)
/// - Lower infrastructure complexity
/// - Leverages LLM's semantic understanding at query time
/// - Can handle domain-specific terminology better
///
/// **Use case:** Document Q&A, code search, knowledge base retrieval
/// </summary>
public class AgenticFilesRetrievalTool : ITool
{
    private readonly string _inputDir;
    private readonly List<DocumentChunk> _documents;
    private readonly ILlm _llm;
    private readonly int _maxIterations;

    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// Creates a new AgenticFilesRetrievalTool with LLM-powered search.
    /// </summary>
    /// <param name="name">Tool name</param>
    /// <param name="description">Tool description</param>
    /// <param name="inputDir">Directory path containing files to index</param>
    /// <param name="llm">LLM for query expansion and relevance ranking</param>
    /// <param name="filePattern">File pattern to match (default: "*.txt")</param>
    /// <param name="searchOption">Search subdirectories (default: TopDirectoryOnly)</param>
    /// <param name="maxIterations">Max refinement iterations (default: 2)</param>
    public AgenticFilesRetrievalTool(
        string name,
        string description,
        string inputDir,
        ILlm llm,
        string filePattern = "*.txt",
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        int maxIterations = 2)
    {
        Name = name;
        Description = description;
        _inputDir = inputDir;
        _llm = llm;
        _maxIterations = maxIterations;
        _documents = new List<DocumentChunk>();

        // Load and index documents
        LoadDocuments(filePattern, searchOption);
    }

    public async Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        if (!args.TryGetValue("query", out var queryObj) || queryObj is not string query)
        {
            return new { error = "Missing required parameter: query" };
        }

        // Step 1: LLM generates expanded search terms
        var searchTerms = await GenerateSearchTermsAsync(query, cancellationToken);

        // Step 2: Multi-pass grep search with expanded terms
        var candidates = PerformExpandedSearch(searchTerms);

        if (candidates.Count == 0)
        {
            return new { result = "No relevant documents found.", documents = Array.Empty<object>() };
        }

        // Step 3: LLM ranks results by semantic relevance
        var rankedResults = await RankResultsByRelevanceAsync(query, candidates, cancellationToken);

        if (rankedResults.Count == 0)
        {
            return new { result = "No relevant documents found after ranking.", documents = Array.Empty<object>() };
        }

        // Return top result
        var topResult = rankedResults[0];
        return new
        {
            result = topResult.Content,
            file = topResult.FileName,
            relevance_score = topResult.RelevanceScore,
            search_terms_used = searchTerms,
            all_results = rankedResults.Take(5).Select(r => new
            {
                file = r.FileName,
                score = r.RelevanceScore,
                preview = r.Content.Length > 200 ? r.Content.Substring(0, 200) + "..." : r.Content
            }).ToArray()
        };
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
                    Description = "The search query or question to retrieve relevant documents"
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

        return new AgenticFilesRetrievalFunctionDeclarationAdapter(declaration);
    }

    /// <summary>
    /// Use LLM to generate expanded search terms (synonyms, related concepts)
    /// </summary>
    private async Task<List<string>> GenerateSearchTermsAsync(string query, CancellationToken cancellationToken)
    {
        var prompt = $@"Given the search query: ""{query}""

Generate 8-12 search terms that would help find relevant documents. Include:
1. Key terms from the original query
2. Synonyms and variations
3. Related concepts
4. Technical terms if applicable
5. Common phrasings

Return ONLY a JSON array of strings, no explanation.
Example: [""term1"", ""term2"", ""term3""]";

        var request = new SimpleLlmRequest
        {
            SystemInstruction = "You are a search query expansion assistant. Generate diverse search terms.",
            Contents = new List<IContent>
            {
                new SimpleContent
                {
                    Role = "user",
                    Parts = new List<IPart> { new SimplePart { Text = prompt } }
                }
            },
            Config = new SimpleGenerationConfig
            {
                Temperature = 0.3,
                MaxOutputTokens = 200
            }
        };

        var response = await _llm.GenerateAsync(request, cancellationToken);
        var responseText = response.Text ?? "[]";

        // Parse JSON array of search terms
        try
        {
            // Extract JSON array from response (handle markdown code blocks)
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var terms = JsonSerializer.Deserialize<List<string>>(jsonStr);
                return terms ?? new List<string> { query };
            }
        }
        catch
        {
            // Fallback to original query
        }

        // Fallback: split query into terms
        return query.Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();
    }

    /// <summary>
    /// Perform expanded search using all generated terms
    /// </summary>
    private List<SearchCandidate> PerformExpandedSearch(List<string> searchTerms)
    {
        var candidatesDict = new Dictionary<string, SearchCandidate>();

        foreach (var doc in _documents)
        {
            var contentLower = doc.Content.ToLowerInvariant();
            var matchedTerms = new List<string>();
            var totalScore = 0.0;

            // Check each search term
            foreach (var term in searchTerms)
            {
                var termLower = term.ToLowerInvariant();
                var count = CountOccurrences(contentLower, termLower);

                if (count > 0)
                {
                    matchedTerms.Add(term);
                    totalScore += count * (term.Length > 3 ? 2.0 : 1.0);
                }
            }

            // Only include documents that match at least one term
            if (matchedTerms.Count > 0)
            {
                var key = $"{doc.FileName}_{doc.ChunkIndex}";
                candidatesDict[key] = new SearchCandidate
                {
                    FileName = doc.FileName,
                    Content = doc.Content,
                    ChunkIndex = doc.ChunkIndex,
                    GrepScore = totalScore,
                    MatchedTerms = matchedTerms
                };
            }
        }

        return candidatesDict.Values
            .OrderByDescending(c => c.GrepScore)
            .Take(20) // Top 20 candidates for LLM ranking
            .ToList();
    }

    /// <summary>
    /// Use LLM to rank results by semantic relevance
    /// </summary>
    private async Task<List<RankedResult>> RankResultsByRelevanceAsync(
        string query,
        List<SearchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return new List<RankedResult>();

        // Build ranking prompt with excerpts
        var candidatesText = new StringBuilder();
        for (int i = 0; i < candidates.Count && i < 10; i++)
        {
            var candidate = candidates[i];
            var preview = candidate.Content.Length > 300
                ? candidate.Content.Substring(0, 300) + "..."
                : candidate.Content;
            candidatesText.AppendLine($"[{i}] {candidate.FileName}:");
            candidatesText.AppendLine(preview);
            candidatesText.AppendLine();
        }

        var prompt = $@"Query: ""{query}""

Rank these document excerpts by relevance to the query. Return ONLY a JSON array of objects with 'index' and 'score' (0.0-1.0).

Documents:
{candidatesText}

Example response: [{{""index"": 2, ""score"": 0.95}}, {{""index"": 0, ""score"": 0.75}}]";

        var request = new SimpleLlmRequest
        {
            SystemInstruction = "You are a document relevance ranking assistant. Rank documents by how well they answer the query.",
            Contents = new List<IContent>
            {
                new SimpleContent
                {
                    Role = "user",
                    Parts = new List<IPart> { new SimplePart { Text = prompt } }
                }
            },
            Config = new SimpleGenerationConfig
            {
                Temperature = 0.1,
                MaxOutputTokens = 300
            }
        };

        var response = await _llm.GenerateAsync(request, cancellationToken);
        var responseText = response.Text ?? "[]";

        // Parse ranking response
        var rankings = new List<(int index, double score)>();
        try
        {
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var rankingsJson = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonStr);

                if (rankingsJson != null)
                {
                    foreach (var item in rankingsJson)
                    {
                        if (item.TryGetValue("index", out var idxElem) &&
                            item.TryGetValue("score", out var scoreElem))
                        {
                            var idx = idxElem.GetInt32();
                            var score = scoreElem.GetDouble();
                            rankings.Add((idx, score));
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback: use grep scores
            for (int i = 0; i < Math.Min(candidates.Count, 10); i++)
            {
                rankings.Add((i, 1.0 - (i * 0.1)));
            }
        }

        // Build ranked results
        var rankedResults = new List<RankedResult>();
        foreach (var (index, score) in rankings)
        {
            if (index >= 0 && index < candidates.Count)
            {
                var candidate = candidates[index];
                rankedResults.Add(new RankedResult
                {
                    FileName = candidate.FileName,
                    Content = candidate.Content,
                    ChunkIndex = candidate.ChunkIndex,
                    RelevanceScore = score,
                    GrepScore = candidate.GrepScore,
                    MatchedTerms = candidate.MatchedTerms
                });
            }
        }

        return rankedResults.OrderByDescending(r => r.RelevanceScore).ToList();
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

                // Split into chunks
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
                Console.WriteLine($"Warning: Could not read file {file}: {ex.Message}");
            }
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

    private class SearchCandidate
    {
        public required string FileName { get; init; }
        public required string Content { get; init; }
        public required int ChunkIndex { get; init; }
        public required double GrepScore { get; init; }
        public required List<string> MatchedTerms { get; init; }
    }

    private class RankedResult
    {
        public required string FileName { get; init; }
        public required string Content { get; init; }
        public required int ChunkIndex { get; init; }
        public required double RelevanceScore { get; init; }
        public required double GrepScore { get; init; }
        public required List<string> MatchedTerms { get; init; }
    }
}

// Simple implementations for LLM request
internal class SimpleLlmRequest : ILlmRequest
{
    public required string? SystemInstruction { get; init; }
    public required List<IContent> Contents { get; init; }
    IReadOnlyList<IContent> ILlmRequest.Contents => Contents;
    public IReadOnlyList<IFunctionDeclaration>? Tools => null;
    public string? ToolChoice => null;
    public IGenerationConfig? Config { get; init; }
}

internal class SimpleContent : IContent
{
    public required string? Role { get; init; }
    public required List<IPart> Parts { get; init; }
    IReadOnlyList<IPart> IContent.Parts => Parts;
}

internal class SimplePart : IPart
{
    public string? Text { get; init; }
    public string? Reasoning => null;
    public IFunctionCall? FunctionCall => null;
    public IFunctionResponse? FunctionResponse => null;
    public byte[]? InlineData => null;
    public string? MimeType => null;
}

internal class SimpleGenerationConfig : IGenerationConfig
{
    public double? Temperature { get; init; }
    public double? TopP => null;
    public int? TopK => null;
    public int? MaxOutputTokens { get; init; }
    public List<string>? StopSequences => null;
    public string? ReasoningEffort => null;
}

// Adapter classes
internal sealed class AgenticFilesRetrievalFunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public AgenticFilesRetrievalFunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null
        ? new AgenticFilesRetrievalSchemaAdapter(_dto.Parameters)
        : null;
    public ISchema? Response => _dto.Response != null
        ? new AgenticFilesRetrievalSchemaAdapter(_dto.Response)
        : null;
}

internal sealed class AgenticFilesRetrievalSchemaAdapter : ISchema
{
    private readonly Schema _dto;

    public AgenticFilesRetrievalSchemaAdapter(Schema dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public IReadOnlyDictionary<string, ISchemaProperty>? Properties => _dto.Properties?
        .ToDictionary(kvp => kvp.Key, kvp => (ISchemaProperty)new AgenticFilesRetrievalSchemaPropertyAdapter(kvp.Value));
    public IReadOnlyList<string>? Required => _dto.Required;
}

internal sealed class AgenticFilesRetrievalSchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public AgenticFilesRetrievalSchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
    public ISchemaProperty? Items => _dto.Items != null ? new AgenticFilesRetrievalSchemaPropertyAdapter(_dto.Items) : null;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new AgenticFilesRetrievalSchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}
