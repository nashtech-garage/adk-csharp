# Agentic Retrieval: Grep + LLM > Vector Embeddings

## The Problem with Vector Embeddings

Traditional RAG (Retrieval Augmented Generation) systems rely on:
1. **Embedding Models** - Convert documents to vectors (expensive, requires ML infrastructure)
2. **Vector Databases** - Store and search embeddings (Pinecone, Weaviate, pgvector)
3. **Semantic Search** - Find similar vectors using cosine similarity

**Challenges:**
- High infrastructure complexity
- Expensive embedding computation
- Black-box similarity (hard to debug)
- Domain adaptation requires fine-tuning
- Cold start problem for new documents

## Agentic Retrieval Solution

**Core Idea:** Use grep (keyword search) + LLM reasoning instead of vector embeddings.

### Architecture

```
┌─────────────────┐
│  User Query     │
│  "How to retry?"│
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  Step 1: Query Expansion (LLM)      │
│  ---------------------------------- │
│  Input: "How to retry?"             │
│  Output: [                          │
│    "retry",                         │
│    "retrying",                      │
│    "retry logic",                   │
│    "exponential backoff",           │
│    "error handling",                │
│    "fault tolerance",               │
│    "transient failures",            │
│    "resilience"                     │
│  ]                                  │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  Step 2: Multi-Pass Grep Search     │
│  ---------------------------------- │
│  Search documents for ALL terms     │
│  - "retry" → 15 matches             │
│  - "exponential backoff" → 3 matches│
│  - "resilience" → 8 matches         │
│                                     │
│  Aggregate: 20 candidate documents  │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  Step 3: Semantic Ranking (LLM)     │
│  ---------------------------------- │
│  Show LLM excerpts from top 10      │
│  Ask: "Rank by relevance to query"  │
│                                     │
│  LLM Output: [                      │
│    {index: 3, score: 0.95},        │
│    {index: 1, score: 0.82},        │
│    {index: 7, score: 0.71}         │
│  ]                                  │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────┐
│  Top Result     │
│  Score: 0.95    │
└─────────────────┘
```

## Comparison

| Feature | Vector Embeddings | Agentic Retrieval |
|---------|------------------|-------------------|
| **Infrastructure** | Embedding model + Vector DB | Just grep + LLM |
| **Cost** | High (compute + storage) | Low (LLM calls only) |
| **Interpretability** | Black box similarity | Clear search terms |
| **Domain Adaptation** | Requires fine-tuning | LLM naturally adapts |
| **Cold Start** | Need to embed everything | Works immediately |
| **Accuracy** | Good for semantic | **Better with LLM reasoning** |
| **Latency** | Fast (vector search) | Moderate (LLM calls) |
| **Debugging** | Hard | Easy (see search terms) |

## When to Use Each Approach

### Use Agentic Retrieval When:
- ✅ Document collection < 10,000 files
- ✅ Need explainable search results
- ✅ Domain-specific terminology changes frequently
- ✅ Want to minimize infrastructure
- ✅ Have access to powerful LLM (GPT-4, Claude, Gemini)

### Use Vector Embeddings When:
- ⚠️ Document collection > 100,000 files
- ⚠️ Need sub-second latency
- ⚠️ Offline search required
- ⚠️ LLM cost is prohibitive

## Implementation Example

### Basic Usage

```csharp
using NTG.Adk.Implementations.Tools.Retrieval;
using NTG.Adk.Implementations.Models;

// Create agentic retrieval tool
var llm = new GeminiLlm("gemini-2.0-flash-exp");
var retrieval = new AgenticFilesRetrievalTool(
    name: "search_docs",
    description: "Search documentation for answers",
    inputDir: "./docs",
    llm: llm,
    filePattern: "*.md"
);

// Use in agent
var agent = new LlmAgent(
    name: "doc_assistant",
    model: llm,
    instruction: "Help users find information in docs",
    tools: new[] { retrieval }
);
```

### Advanced: Custom Query Expansion

```csharp
// Extend for domain-specific expansion
public class DomainSpecificRetrievalTool : AgenticFilesRetrievalTool
{
    protected override async Task<List<string>> GenerateSearchTermsAsync(
        string query,
        CancellationToken cancellationToken)
    {
        // Add domain glossary lookup
        var terms = await base.GenerateSearchTermsAsync(query, cancellationToken);

        // Add your domain synonyms
        if (query.Contains("retry"))
        {
            terms.AddRange(new[] { "idempotency", "circuit breaker", "saga" });
        }

        return terms;
    }
}
```

## Performance Characteristics

### Document Collection Size

| Size | Agentic Retrieval | Vector Embeddings |
|------|------------------|-------------------|
| **10 docs** | Excellent (< 2s) | Overkill |
| **100 docs** | Great (< 5s) | Good |
| **1,000 docs** | Good (< 10s) | Great |
| **10,000 docs** | Acceptable (< 30s) | Excellent |
| **100,000+ docs** | Slow | Required |

### Cost Comparison (1000 queries/day)

| Approach | Setup Cost | Monthly Cost | Total Year 1 |
|----------|-----------|--------------|--------------|
| **Agentic Retrieval** | $0 | ~$50 (LLM) | **$600** |
| **Vector Embeddings** | $2,000 | ~$200 (infra) | **$4,400** |

## Real-World Use Cases

### 1. Code Search
**Query:** "How do we handle authentication errors?"

**Agentic Expansion:**
- "authentication", "auth", "401 Unauthorized"
- "token expired", "refresh token"
- "login", "credentials", "bearer token"

**Result:** Finds relevant code even if exact terms don't match

### 2. Documentation Q&A
**Query:** "What's the retry policy?"

**Agentic Expansion:**
- "retry", "retries", "retrying"
- "exponential backoff", "jitter"
- "transient errors", "circuit breaker"

**Result:** Surfaces best practices and implementation details

### 3. Incident Response
**Query:** "Database connection timeouts"

**Agentic Expansion:**
- "timeout", "connection", "database"
- "pool exhaustion", "deadlock"
- "slow query", "latency"

**Result:** Finds similar past incidents and solutions

## Advanced Techniques

### 1. Iterative Refinement
```csharp
// If first search fails, LLM suggests refinements
if (results.Count == 0)
{
    var refinedQuery = await RefineQueryAsync(originalQuery);
    results = await SearchAsync(refinedQuery);
}
```

### 2. Multi-Modal Search
```csharp
// Combine keyword + semantic + metadata
var grepResults = PerformGrepSearch(terms);
var rankedResults = await RankByRelevanceAsync(query, grepResults);
var filteredResults = FilterByMetadata(rankedResults, metadata);
```

### 3. Cross-Document Reasoning
```csharp
// LLM synthesizes info from multiple documents
var topDocs = GetTopDocuments(query);
var answer = await SynthesizeAnswerAsync(query, topDocs);
```

## Conclusion

**Agentic Retrieval = Grep + LLM** is a practical, cost-effective alternative to vector embeddings for most use cases. It leverages:
- ✅ LLM's semantic understanding at query time
- ✅ Simple grep infrastructure (no ML ops)
- ✅ Interpretable results (you can see why docs matched)
- ✅ Natural domain adaptation (LLM knows your domain)

**When to upgrade to embeddings:** When you have 100K+ documents and need sub-second latency.

**Bottom line:** Start with agentic retrieval. Upgrade to embeddings only when you outgrow it.
