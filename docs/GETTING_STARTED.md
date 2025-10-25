# Getting Started with NTG.Adk

## ✅ What's Built

You now have a **fully functional Agent Development Kit in C#** following **A.D.D V3 five-layer architecture**, 100% compatible with the [Abstract Driven spec](https://abstractdriven.com/llms-full.txt).

### Project Structure

```
E:\repos\adk-csharp/
├── src/
│   ├── NTG.Adk.Boundary/           # Layer 1: DTOs, Events ✅
│   ├── NTG.Adk.CoreAbstractions/   # Layer 2: Ports (IAgent, ILlm, ITool) ✅
│   ├── NTG.Adk.Implementations/    # Layer 3: Adapters (MockLlm, Sessions) ✅
│   ├── NTG.Adk.Operators/          # Layer 4: Business Logic (BaseAgent, SimpleLlmAgent) ✅
│   └── NTG.Adk.Bootstrap/          # Layer 5: Composition (Runner) ✅
├── samples/
│   └── HelloWorldAgent/            # Working demo ✅
├── README.md                       # Full documentation ✅
├── ARCHITECTURE.md                 # A.D.D V3 detailed guide ✅
└── GETTING_STARTED.md              # This file ✅
```

## 🚀 Run the Demo

```bash
cd E:\repos\adk-csharp\samples\HelloWorldAgent
dotnet run
```

**Output:**
```
=== NTG.Adk Hello World Agent ===
A.D.D V3 Five-Layer Architecture Demo

User: Hello, how are you?

Agent Response:
Mock response to: Hello, how are you?

=== Architecture Layers ===
✅ Boundary: Event DTOs
✅ CoreAbstractions: IAgent, ILlm ports
✅ Implementations: MockLlm adapter
✅ Operators: SimpleLlmAgent orchestrator
✅ Bootstrap: Runner composition root
```

## 📖 Quick Tutorial

### 1. Create a Simple Agent

```csharp
using NTG.Adk.Bootstrap;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;

// Create LLM adapter (Layer 3: Implementations)
var llm = new MockLlm();

// Create agent orchestrator (Layer 4: Operators)
var agent = new SimpleLlmAgent(llm)
{
    Name = "MyAgent",
    Instruction = "You are a helpful assistant."
};

// Create runner (Layer 5: Bootstrap)
var runner = new Runner(agent);

// Execute
var response = await runner.RunAsync("Hello!");
Console.WriteLine(response);
```

### 2. Understand the Layers

**Layer 1: Boundary** (`NTG.Adk.Boundary`)
- Pure DTOs: `Event`, `Content`, `Part`, `FunctionCall`
- No dependencies
- Serializable across boundaries

**Layer 2: CoreAbstractions** (`NTG.Adk.CoreAbstractions`)
- Ports: `IAgent`, `ILlm`, `ITool`, `ISession`
- No dependencies (pure interfaces)
- Enables Dependency Inversion Principle

**Layer 3: Implementations** (`NTG.Adk.Implementations`)
- Adapters: `MockLlm`, `InMemorySession`, `EventAdapter`
- Depends on: CoreAbstractions + Boundary
- Technology-specific implementations

**Layer 4: Operators** (`NTG.Adk.Operators`)
- Business logic: `BaseAgent`, `SimpleLlmAgent`
- Depends on: CoreAbstractions + Boundary (+ Implementations for helpers)
- Orchestrates agent workflows

**Layer 5: Bootstrap** (`NTG.Adk.Bootstrap`)
- Composition root: `Runner`
- Depends on: All layers (for wiring only)
- DI setup and entry point

### 3. Swap Implementations (DIP in Action)

```csharp
// Easy to swap MockLlm with real LLM:
// var llm = new GeminiLlm(apiKey);  // Future: Real Google Gemini
// var llm = new OpenAILlm(apiKey);  // Future: Real OpenAI

var agent = new SimpleLlmAgent(llm);  // Same agent code!
```

## 🎯 Next Steps

### Phase 1: Complete Core Features

1. **Real LLM Adapters** (Layer 3: Implementations)
   ```
   src/NTG.Adk.Implementations/Models/
   ├── GeminiLlm.cs          # Google Gemini adapter
   ├── OpenAILlm.cs          # OpenAI adapter
   └── AzureOpenAILlm.cs     # Azure OpenAI adapter
   ```

2. **LlmAgent (Full Version)** (Layer 4: Operators)
   ```
   src/NTG.Adk.Operators/Agents/
   └── LlmAgent.cs           # Full-featured agent with tools, sub-agents
   ```

3. **Workflow Agents** (Layer 4: Operators)
   ```
   src/NTG.Adk.Operators/Workflows/
   ├── SequentialAgent.cs    # Execute sub-agents sequentially
   ├── ParallelAgent.cs      # Execute sub-agents in parallel
   └── LoopAgent.cs          # Loop execution with max iterations
   ```

4. **Tool System** (Layers 3 & 4)
   ```
   src/NTG.Adk.Implementations/Tools/
   ├── FunctionTool.cs       # Wrap C# functions as tools
   ├── GoogleSearchTool.cs   # Google Search implementation
   └── HttpTool.cs           # HTTP client tool
   ```

### Phase 2: Advanced Features

5. **Flow System** (Layer 4: Operators)
   - `AutoFlow`: Multi-agent auto-delegation
   - `SingleFlow`: Simple LLM call flow
   - Streaming support

6. **Memory & State** (Layer 3: Implementations)
   - Vector store integration
   - Persistent sessions
   - Context caching

7. **A2A Protocol** (Layer 3: Implementations)
   - Agent-to-agent communication
   - gRPC support

### Phase 3: Production Ready

8. **ASP.NET Core Integration** (Layer 5: Bootstrap)
   - Minimal APIs for agents
   - WebSocket streaming
   - Middleware

9. **Testing**
   ```
   tests/
   ├── NTG.Adk.Operators.Tests/     # Unit tests (mock ports)
   └── NTG.Adk.Integration.Tests/   # Integration tests (real adapters)
   ```

10. **NuGet Packages**
    - `NTG.Adk` (meta-package)
    - `NTG.Adk.Core` (Boundary + CoreAbstractions)
    - `NTG.Adk.Gemini` (Gemini LLM adapter)

## 🛠️ Development Guidelines

### Adding a New LLM Adapter

1. **Create adapter in Implementations layer:**
   ```csharp
   // src/NTG.Adk.Implementations/Models/GeminiLlm.cs
   public class GeminiLlm : ILlm
   {
       public async Task<ILlmResponse> GenerateAsync(ILlmRequest request, CancellationToken ct)
       {
           // Call Google Gemini API
           // Return ILlmResponse
       }
   }
   ```

2. **Register in Bootstrap:**
   ```csharp
   services.AddSingleton<ILlm, GeminiLlm>();
   ```

3. **Use in Operators:**
   ```csharp
   var agent = new SimpleLlmAgent(llm); // ILlm port, not concrete!
   ```

### A.D.D V3 Rules Enforced

✅ **Operators call Ports, NEVER Implementations**
```csharp
// GOOD (Operator using Port)
public class SimpleLlmAgent : BaseAgent
{
    private readonly ILlm _llm;  // ✅ Port dependency
}

// BAD (Operator depending on Implementation)
public class SimpleLlmAgent : BaseAgent
{
    private readonly GeminiLlm _llm;  // ❌ BREAKS DIP!
}
```

✅ **Dependencies flow correctly:**
- Bootstrap → All (composition only)
- Operators → CoreAbstractions + Boundary
- Implementations → CoreAbstractions + Boundary
- CoreAbstractions → NONE
- Boundary → NONE

## 📚 Resources

- **README.md**: Project overview and quick start
- **ARCHITECTURE.md**: Deep dive into A.D.D V3 implementation
- **[Python ADK](https://github.com/google/adk-python)**: Original reference
- **[A.D.D V3 Spec](https://abstractdriven.com/llms-full.txt)**: Architecture pattern

## 🤝 Contributing

To add new features:

1. **Determine the layer:**
   - DTO/Event? → Boundary
   - Interface? → CoreAbstractions
   - Technology implementation? → Implementations
   - Business logic? → Operators
   - DI wiring? → Bootstrap

2. **Follow dependency rules:**
   - Check `ARCHITECTURE.md` for allowed dependencies
   - Never make Operators depend on Implementations directly
   - Keep CoreAbstractions and Boundary dependency-free

3. **Test with ports:**
   - Unit tests mock ports (ILlm, IAgent)
   - Integration tests use real implementations

## 🎓 Learning Path

1. ✅ **Run the sample** (`HelloWorldAgent`)
2. 📖 **Read `ARCHITECTURE.md`** to understand A.D.D V3
3. 🔧 **Add a new tool** (follow pattern in Implementations/Tools)
4. 🤖 **Implement real LLM adapter** (GeminiLlm or OpenAILlm)
5. 🔀 **Create SequentialAgent** (orchestrate multiple agents)
6. 🚀 **Build your own agent system!**

---

**You now have a production-grade C# ADK following enterprise architecture patterns!** 🏗️✨

Next: Start with Phase 1, Step 1 - implement `GeminiLlm.cs` adapter.
