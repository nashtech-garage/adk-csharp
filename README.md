# NTG.Adk - Agent Development Kit for .NET

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0_LTS-512BD4)](https://dotnet.microsoft.com/)
[![A.D.D V3](https://img.shields.io/badge/Architecture-A.D.D_V3-green)](https://abstractdriven.com)

> **Production-ready C# implementation of Google's Agent Development Kit with 99% feature parity**

NTG.Adk is a complete C# port of [Google ADK Python](https://github.com/google/adk-python), following strict **[Abstract Driven Development (A.D.D) V3](https://abstractdriven.com/llms-full.txt)** architecture principles for enterprise-grade agent systems.

## ✨ Key Features

- 🏗️ **A.D.D V3 Architecture** - Five-layer fractal design with zero coupling
- 🤖 **Multi-Agent Orchestration** - Sequential, parallel, and loop workflows
- 🔄 **Session Management** - Multi-user with app/user/session state hierarchy
- 💾 **Artifact & Memory Services** - File storage and long-term agent memory
- ⚙️ **RunConfig** - Configurable limits and streaming (MaxLlmCalls: 500, StreamingMode)
- 📡 **Token Streaming** - Real-time SSE streaming with partial event detection
- 🌐 **A2A Protocol** - Seamless interoperability with Google Agent ecosystem
- 🔌 **MCP Protocol** - Connect to MCP servers and use their tools (stdio, SSE, HTTP)
- 🌐 **OpenAPI Toolset** - Auto-generate tools from any REST API (JSON/YAML specs)
- 🚀 **Runner Pattern** - Production-ready orchestration with integrated services
- 🧩 **LLM Adapters** - Gemini, OpenAI, and OpenAI-compatible endpoints (Ollama, LocalAI, vLLM)
- 🛠️ **Tool Ecosystem** - Function calling, custom tools, and built-in tools (Google Search, Code Execution)

## 📊 Status

**Version**: 1.6.3
**Target Framework**: .NET 8.0 LTS (supported until Nov 2026)
**Production Readiness**: 100% ✅
**Core Feature Parity with Python ADK**: 99% ✅ (Retrieval/RAG: 50%)
**A2A Interoperability**: 100% ✅
**MCP Protocol Support**: 100% ✅
**OpenAPI Toolset**: 100% ✅

See [docs/STATUS.md](docs/STATUS.md) for detailed metrics.

## ⚡ Quick Start

### Basic Agent

```csharp
using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

// Create agent with LLM
var llm = new GeminiLlm("gemini-2.0-flash-exp");
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "Assistant",
    Instruction = "You are a helpful assistant"
};

// Run with InMemoryRunner
var runner = new InMemoryRunner(agent, appName: "MyApp");

await foreach (var evt in runner.RunAsync("user001", "session001", "Hello!"))
{
    if (evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (part.Text != null)
                Console.WriteLine($"[{evt.Author}] {part.Text}");
        }
    }
}
```

### OpenAI-Compatible Endpoints (Ollama, LocalAI, vLLM)

```csharp
using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

// Use Ollama local models
var llm = new OpenAILlm(
    modelName: "llama3",
    apiKey: "ollama",  // Any string works
    endpoint: new Uri("http://localhost:11434/v1")
);

var agent = new LlmAgent(llm, "llama3")
{
    Name = "LocalAssistant",
    Instruction = "You are a helpful assistant running locally"
};

var runner = new InMemoryRunner(agent, appName: "LocalApp");
await foreach (var evt in runner.RunAsync("user001", "session001", "Hello!"))
{
    // Handle events
}
```

Supports: **Ollama**, **LocalAI**, **vLLM**, **LM Studio**, and any OpenAI-compatible endpoint.

### Multi-Agent Workflow

```csharp
using NTG.Adk.Operators.Workflows;

// Define agents
var validator = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "Validator",
    Instruction = "Validate input data",
    OutputKey = "validation"
};

var processor = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "Processor",
    Instruction = "Process validated data",
    OutputKey = "result"
};

// Sequential pipeline
var pipeline = new SequentialAgent("DataPipeline", [validator, processor]);

var runner = new InMemoryRunner(pipeline, appName: "PipelineApp");
await foreach (var evt in runner.RunAsync("user001", "session001", "Process this data"))
{
    // Handle events
}
```

### A2A Interoperability

```csharp
using NTG.Adk.Operators.A2A;

// Create ADK agent
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "A2AAgent",
    Instruction = "Answer questions via A2A protocol"
};

var runner = new InMemoryRunner(agent, appName: "A2AApp");

// Wrap with A2A executor
var a2aExecutor = new A2aAgentExecutor(runner);

// Handle A2A messages
var a2aMessage = new A2A.AgentMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Role = A2A.MessageRole.User,
    Parts = [new A2A.TextPart { Text = "Hello from A2A!" }]
};

await foreach (var a2aEvent in a2aExecutor.ExecuteAsync(
    a2aMessage,
    taskId: Guid.NewGuid().ToString(),
    contextId: "ADK/A2AApp/user001/session001"))
{
    // Handle A2A events (TaskStatusUpdateEvent, TaskArtifactUpdateEvent)
}
```

### MCP Protocol Integration

```csharp
using NTG.Adk.Boundary.Mcp;
using NTG.Adk.Implementations.Mcp;

// Connect to MCP server via stdio
var connectionParams = new StdioConnectionParams
{
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-filesystem", "/tmp"]
};

var mcpToolset = new McpToolset(connectionParams);

// Connect and get tools
await mcpToolset.ConnectAsync();
var tools = await mcpToolset.GetToolsAsync();

// Use MCP tools with agent
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "McpAssistant",
    Instruction = "You have access to MCP tools",
    Tools = tools.ToList()
};

var runner = new InMemoryRunner(agent, appName: "McpApp");
await foreach (var evt in runner.RunAsync("user001", "session001", "List files"))
{
    // Handle events
}
```

### OpenAPI Toolset

```csharp
using NTG.Adk.Implementations.Tools.OpenApi;
using NTG.Adk.Boundary.Tools.Auth;

// Load OpenAPI spec (JSON or YAML)
var openApiSpec = File.ReadAllText("petstore-openapi.json");

// Create toolset from spec
var toolset = new OpenAPIToolset(openApiSpec, "json");

// Optional: Add authentication
var authScheme = new ApiKeyAuthScheme
{
    In = "header",
    Name = "X-API-Key"
};
var authCredential = new ApiKeyCredential
{
    ApiKey = Environment.GetEnvironmentVariable("API_KEY")!
};
toolset = new OpenAPIToolset(openApiSpec, "json", authScheme, authCredential);

// Get all tools from spec
var tools = toolset.GetTools();

// Use with agent
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "ApiAgent",
    Instruction = "You can interact with the API using these tools",
    Tools = tools
};

var runner = new InMemoryRunner(agent, appName: "ApiApp");
await foreach (var evt in runner.RunAsync("user001", "session001", "List all pets"))
{
    // Handle events
}
```

## ⚙️ Advanced Configuration

### RunConfig - Execution Control

Configure agent execution limits and streaming (matches Python ADK):

```csharp
using NTG.Adk.CoreAbstractions.Agents;

// Default configuration (matches Python ADK defaults)
var runConfig = new RunConfig
{
    MaxLlmCalls = 500,                  // Max LLM calls per invocation (prevents infinite loops)
    StreamingMode = StreamingMode.None  // No streaming by default
};

var runner = new Runner(agent, "MyApp", sessionService, runConfig: runConfig);
```

### Token Streaming

Enable real-time token-by-token streaming:

```csharp
// Enable SSE streaming
var runConfig = new RunConfig
{
    StreamingMode = StreamingMode.Sse  // Server-sent events streaming
};

var runner = new Runner(agent, "MyApp", sessionService, runConfig: runConfig);

await foreach (var evt in runner.RunAsync("user001", "session001", "Hello"))
{
    if (evt.Partial)
    {
        // Streaming chunk - arrives in real-time as tokens are generated
        Console.Write(evt.Content?.Parts?.FirstOrDefault()?.Text ?? "");
    }
    else
    {
        // Complete response
        Console.WriteLine("\n[Complete]");
    }
}
```

**Streaming Modes:**
- `StreamingMode.None` - Buffer complete response (default, matches Python ADK)
- `StreamingMode.Sse` - Server-sent events, token-by-token streaming
- `StreamingMode.Bidi` - Bidirectional streaming (reserved for future use)

### LLM Call Limits

Prevent infinite loops with configurable limits:

```csharp
var runConfig = new RunConfig
{
    MaxLlmCalls = 100  // Custom limit
};

try
{
    await foreach (var evt in runner.RunAsync("user001", "session001", "Complex task"))
    {
        // Process events
    }
}
catch (LlmCallsLimitExceededError ex)
{
    Console.WriteLine($"Limit exceeded: {ex.Message}");
    // Output: "Max number of LLM calls limit of 100 exceeded"
}
```

**Default:** 500 calls per invocation (matches Python ADK)

## 📚 Documentation

- **[Getting Started Guide](docs/GETTING_STARTED.md)** - Detailed setup and usage
- **[Architecture](docs/ARCHITECTURE.md)** - A.D.D V3 five-layer design
- **[Features](docs/FEATURES.md)** - Complete feature list with examples
- **[Compatibility](docs/COMPATIBILITY.md)** - Python ADK API mapping
- **[Status](docs/STATUS.md)** - Current implementation status
- **[Changelog](docs/CHANGELOG.md)** - Version history

## 🏗️ Architecture Overview

NTG.Adk follows **A.D.D V3** strict five-layer architecture:

```
NTG.Adk/
├── Boundary/              # Layer 1: DTOs, Events (no dependencies)
├── CoreAbstractions/      # Layer 2: Interfaces/Ports (no dependencies)
├── Implementations/       # Layer 3: Adapters (depends on CoreAbstractions)
├── Operators/             # Layer 4: Business Logic (depends on CoreAbstractions + Boundary)
└── Bootstrap/             # Layer 5: Composition Root (depends on all)
```

**Key Principles:**
- ✅ Operators call ports (interfaces), never implementations
- ✅ Zero coupling between layers (except explicit dependencies)
- ✅ Dependency inversion at all boundaries
- ✅ Technology-agnostic core abstractions

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for details.

## 🔗 Python ADK Compatibility

NTG.Adk maintains 99% feature parity with Google ADK Python (100% core features):

| Python ADK | C# NTG.Adk | Layer |
|------------|------------|-------|
| `google.adk.agents.BaseAgent` | `IAgent` | Port (CoreAbstractions) |
| `google.adk.agents.LlmAgent` | `LlmAgent` | Operator |
| `google.adk.runners.Runner` | `Runner` | Operator |
| `google.adk.events.Event` | `Event` | Boundary DTO |
| `google.adk.tools.BaseTool` | `ITool` | Port (CoreAbstractions) |

See [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) for complete mapping.

## 📦 Project Structure

```
E:\repos\adk-csharp/
├── src/
│   ├── NTG.Adk.Boundary/           # Layer 1: DTOs
│   ├── NTG.Adk.CoreAbstractions/   # Layer 2: Ports
│   ├── NTG.Adk.Implementations/    # Layer 3: Adapters
│   ├── NTG.Adk.Operators/          # Layer 4: Orchestration
│   └── NTG.Adk.Bootstrap/          # Layer 5: DI/Entry
├── samples/
│   ├── HelloWorldAgent/            # Basic agent demo
│   ├── GeminiAgent/                # Gemini LLM integration
│   ├── OpenAIAgent/                # OpenAI integration
│   ├── AutoFlowAgent/              # AutoFlow orchestration
│   ├── StoryFlowAgent/             # Multi-agent workflow
│   ├── A2AInteropSample/           # A2A protocol demo
│   ├── McpToolsSample/             # MCP Protocol integration
│   ├── OpenApiToolsSample/         # OpenAPI Toolset demo
│   └── BuiltInToolsSample/         # Built-in tools demo
├── docs/                           # Documentation
└── README.md                       # This file
```

## 🧪 Samples

Explore working examples in the `samples/` directory:

1. **HelloWorldAgent** - Simple echo agent with InMemoryRunner
2. **GeminiAgent** - Google Gemini 2.0 Flash integration
3. **OpenAIAgent** - OpenAI GPT-4 integration
4. **AutoFlowAgent** - Dynamic multi-agent routing
5. **StoryFlowAgent** - Sequential story generation workflow
6. **A2AInteropSample** - A2A protocol interoperability
7. **McpToolsSample** - MCP Protocol integration (stdio, SSE, HTTP transports)
8. **OpenApiToolsSample** - OpenAPI Toolset with REST API integration
9. **BuiltInToolsSample** - Built-in tools (Google Search, Code Execution)

Run a sample:
```bash
cd samples/HelloWorldAgent
dotnet run
```

## 🔧 Requirements

- **.NET 8.0 LTS** (supported until November 2026)
- **C# 12** language features
- **Visual Studio 2022** or **VS Code** with C# Dev Kit

## 🛠️ Build

```bash
# Clone repository
git clone <repository-url>
cd adk-csharp

# Restore packages
dotnet restore

# Build solution
dotnet build

# Run tests (if available)
dotnet test

# Run a sample
cd samples/HelloWorldAgent
dotnet run
```

## 🗺️ Roadmap

### Phase 1 - Core Infrastructure ✅ COMPLETE
- ✅ Multi-Agent Orchestration (Sequential, Parallel, Loop)
- ✅ Session/Artifact/Memory Services (In-Memory)
- ✅ A2A Protocol Integration
- ✅ MCP Protocol Support (stdio, SSE, HTTP)
- ✅ OpenAPI Toolset (REST API auto-integration)
- ✅ Built-in Tools (Google Search, Code Execution)
- ✅ LLM Adapters (Gemini, OpenAI)
- ✅ .NET 8.0 LTS Migration

### Phase 2 - Persistent Storage (In Progress)
- ⏳ **DatabaseSessionService** - SQL persistence for sessions
  - PostgreSQL, MySQL, SQLite support
  - Production-ready session storage
  - Multi-instance deployment support
- ⏳ **FileArtifactService** - Local file system storage
  - File-based artifact persistence
  - Blob storage support (Azure, AWS S3, GCP)
  - Artifact versioning and cleanup

### Phase 3 - Advanced Features (Planned)
- 📋 **Planner System** - Agent reasoning and planning
  - BuiltInPlanner with extended thinking
  - PlanReActPlanner for ReAct pattern
  - Custom planner support
- 📋 **FilesRetrieval** - RAG and document search
  - Directory-based retrieval
  - Semantic search capabilities
  - LlamaIndex integration
- 📋 **AgentEvaluator** - Testing and quality assurance
  - Automated agent testing
  - Benchmark framework
  - Performance metrics

### Phase 4 - Cloud Integration (Future)
- 📋 **Cloud Storage Services**
  - GCS Artifact Service (Google Cloud Storage)
  - Azure Blob Artifact Service
  - AWS S3 Artifact Service
- 📋 **Vertex AI Integration**
  - Vertex AI RAG Retrieval
  - Vertex AI Search Tool
  - Vertex AI Example Store
- 📋 **Enterprise Features**
  - Authentication & Authorization
  - Rate limiting and quotas
  - Audit logging and monitoring

### Community Contributions Welcome! 🤝
We welcome contributions for:
- New LLM adapters (Anthropic Claude, Cohere, etc.)
- Additional built-in tools
- Cloud provider integrations
- Performance optimizations
- Documentation improvements

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## 📄 License

Apache 2.0 License - see [LICENSE](LICENSE) file.

## 🙏 Credits

- Based on [Google ADK Python](https://github.com/google/adk-python)
- Architecture: [Abstract Driven Development (A.D.D) V3](https://abstractdriven.com)
- A2A Protocol: [a2a-dotnet SDK](https://github.com/a2aproject/a2a-dotnet)

---

**Built with Abstract Driven Development (A.D.D) V3** 🚀
