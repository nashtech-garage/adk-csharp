# NTG.Adk Architecture Documentation

## Abstract Driven Development (A.D.D) V3 Compliance

This document explains how NTG.Adk implements **A.D.D V3** fractal architecture pattern for building the Agent Development Kit in C#.

## The Five Layers

### Layer 1: Boundary

**Purpose**: External contracts - Pure data structures with no logic
**Dependencies**: NONE
**Location**: `src/NTG.Adk.Boundary/`

The Boundary layer contains all DTOs (Data Transfer Objects) and events that cross system boundaries:

```
Boundary/
├── Events/
│   ├── Event.cs                 # Main event DTO
│   ├── EventActions.cs          # Actions (escalate, transfer)
│   ├── Content.cs               # Content container (role + parts)
│   ├── Part.cs                  # Content part (text, function call, etc.)
│   ├── FunctionCall.cs          # LLM function call
│   └── FunctionResponse.cs      # Function execution result
├── Tools/
│   ├── FunctionDeclaration.cs   # Tool schema for LLM
│   └── Schema.cs                # JSON schema definition
├── Agents/
│   └── AgentConfig.cs           # Agent configuration DTO
└── Sessions/
    └── SessionStateDto.cs       # Session state snapshot
```

**Key Principle**: These are **pure data contracts**. No business logic, no framework dependencies. Can be serialized/deserialized across boundaries (HTTP, gRPC, message queues).

**Example**:
```csharp
// Pure DTO - just data
public record Event
{
    public required string Author { get; init; }
    public Content? Content { get; init; }
    public EventActions? Actions { get; init; }
}
```

### Layer 2: CoreAbstractions

**Purpose**: Ports (Interfaces) - Technology-agnostic contracts
**Dependencies**: NONE
**Location**: `src/NTG.Adk.CoreAbstractions/`

CoreAbstractions defines **all interfaces (ports)** that the system uses. This layer has ZERO dependencies - pure abstractions:

```
CoreAbstractions/
├── Agents/
│   └── IAgent.cs                # Port for all agents
├── Events/
│   └── IEvent.cs                # Event port + related interfaces
├── Sessions/
│   ├── ISession.cs              # Session management port
│   ├── IInvocationContext.cs    # Context passing port
│   └── ISessionState.cs         # State storage port
├── Tools/
│   └── ITool.cs                 # Tool port
└── Models/
    └── ILlm.cs                  # LLM provider port
```

**Key Principle**: **Dependency Inversion Principle (DIP)**. All concrete implementations depend on these interfaces, NEVER the reverse.

**Example**:
```csharp
// Port interface - no implementation
public interface IAgent
{
    string Name { get; }
    IAsyncEnumerable<IEvent> RunAsync(
        IInvocationContext context,
        CancellationToken cancellationToken = default);
}
```

**Why Zero Dependencies?**
- Operators call ports, not implementations
- Easy to swap implementations (mock for testing, real for production)
- Technology-agnostic (can replace Google AI with OpenAI without changing Operators)

### Layer 3: Implementations

**Purpose**: Adapters - Technology-specific implementations of ports
**Dependencies**: **CoreAbstractions ONLY**
**Location**: `src/NTG.Adk.Implementations/`

Implementations provide **concrete adapters** that implement the ports from CoreAbstractions:

```
Implementations/
├── Events/
│   └── EventAdapter.cs          # Wraps Boundary.Event as IEvent
├── Sessions/
│   ├── InMemorySession.cs       # ISession adapter (memory-based)
│   ├── InvocationContext.cs     # IInvocationContext implementation
│   └── InMemorySessionState.cs  # ISessionState adapter
├── Models/
│   ├── GeminiLlm.cs             # Google Gemini LLM adapter
│   ├── OpenAILlm.cs             # OpenAI LLM adapter
│   └── LiteLlm.cs               # LiteLLM adapter
└── Tools/
    ├── FunctionTool.cs          # ITool wrapper for functions
    ├── GoogleSearchTool.cs      # Google Search implementation
    └── HttpTool.cs              # HTTP client tool
```

**Key Principle**: **Adapters translate between external tech and internal ports**. Can have multiple implementations of the same port (e.g., InMemorySession vs SqlSession).

**Example**:
```csharp
// Adapter implementing IEvent port
public class EventAdapter : IEvent
{
    private readonly Event _dto; // Uses Boundary DTO

    public EventAdapter(Event dto) => _dto = dto;

    public string Author => _dto.Author;
    public IContent? Content => _dto.Content != null
        ? new ContentAdapter(_dto.Content)
        : null;
}
```

**Anti-Corruption Layer (ACL)**: Adapters prevent external API changes from affecting core business logic.

### Layer 4: Operators

**Purpose**: Business Orchestration - Agent workflows and coordination
**Dependencies**: **CoreAbstractions + Boundary**
**Location**: `src/NTG.Adk.Operators/`

Operators contain **all business logic** for agent orchestration. This is the heart of ADK:

```
Operators/
├── Agents/
│   ├── BaseAgent.cs             # Abstract agent base (uses IAgent)
│   └── LlmAgent.cs              # LLM-powered agent orchestrator
├── Workflows/
│   ├── SequentialAgent.cs       # Sequential execution orchestrator
│   ├── ParallelAgent.cs         # Parallel execution orchestrator
│   └── LoopAgent.cs             # Loop execution orchestrator
└── Flows/
    ├── AutoFlow.cs              # Multi-agent auto-delegation flow
    └── SingleFlow.cs            # Single LLM call flow
```

**Key Principle**: **Operators call Ports, NEVER Implementations directly**. This keeps business logic decoupled from technology.

**Example**:
```csharp
// Operator orchestrating via ports
public class LlmAgent : BaseAgent
{
    private readonly ILlm _llm; // Port dependency, not concrete class!

    public async IAsyncEnumerable<IEvent> RunAsync(IInvocationContext ctx)
    {
        // Business logic: orchestrate LLM calls
        var request = BuildRequest(ctx);
        var response = await _llm.GenerateAsync(request); // Uses port

        yield return CreateEvent(response);
    }
}
```

**Transaction Group Operator (TGO)**: For strong consistency, use TGO pattern (future enhancement).

### Layer 5: Bootstrap

**Purpose**: Composition Root - DI container setup and application entry
**Dependencies**: **ALL layers** (for wiring only, not runtime logic)
**Location**: `src/NTG.Adk.Bootstrap/`

Bootstrap **wires everything together** using Dependency Injection:

```
Bootstrap/
├── ServiceCollectionExtensions.cs  # DI registration
├── Runner.cs                       # Main entry point
└── AdkBuilder.cs                   # Fluent API for setup
```

**Key Principle**: **All dependencies are resolved here**. No other layer should contain DI configuration.

**Example**:
```csharp
// Bootstrap: Wire Implementations to Ports
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNtgAdk(this IServiceCollection services)
    {
        // Register adapters
        services.AddSingleton<ILlm, GeminiLlm>(); // Port → Implementation
        services.AddScoped<ISession, InMemorySession>();

        // Register operators
        services.AddTransient<LlmAgent>();
        services.AddTransient<SequentialAgent>();

        return services;
    }
}

// Usage
var services = new ServiceCollection();
services.AddNtgAdk();
var provider = services.BuildServiceProvider();

var runner = provider.GetRequiredService<Runner>();
```

## Dependency Rules Enforced

```
┌─────────────┐
│  Bootstrap  │ ◄─── Composition Root (depends on all)
└──────┬──────┘
       │
   ┌───▼───┐     ┌──────────┐
   │Operators├────►  Boundary │ (DTOs only)
   └───┬────┘     └──────────┘
       │
       │          ┌──────────────────┐
       └─────────►│CoreAbstractions  │ (Ports)
                  └────────┬─────────┘
                           ▲
                           │
                   ┌───────┴────────┐
                   │Implementations │ (Adapters)
                   └────────────────┘

Valid arrows:
✅ Operators → CoreAbstractions
✅ Operators → Boundary
✅ Implementations → CoreAbstractions
✅ Bootstrap → All (composition only)

Invalid arrows:
❌ Operators → Implementations (breaks DIP!)
❌ CoreAbstractions → Boundary
❌ CoreAbstractions → Implementations
```

## Fractal Nature

Every module at any depth has the **same 5 layers**. For example, if we add a "Tools" sub-module:

```
Implementations/
└── Tools/                        # Sub-module
    ├── Boundary/                 # Sub-module's DTOs
    ├── CoreAbstractions/         # Sub-module's ports
    ├── Implementations/          # Sub-module's adapters
    ├── Operators/                # Sub-module's logic
    └── Bootstrap/                # Sub-module's composition
```

This allows **infinite nesting** while maintaining consistency.

## Testing Strategy

### Unit Tests (Operators)

Test business logic using **mocked ports**:

```csharp
[Fact]
public async Task LlmAgent_Should_Call_Llm_Port()
{
    // Arrange: Mock the port
    var mockLlm = new Mock<ILlm>();
    mockLlm.Setup(x => x.GenerateAsync(It.IsAny<ILlmRequest>()))
           .ReturnsAsync(new MockLlmResponse());

    var agent = new LlmAgent { Model = "test" };
    agent.SetLlm(mockLlm.Object); // Inject port

    // Act
    var events = await agent.RunAsync(context).ToListAsync();

    // Assert
    mockLlm.Verify(x => x.GenerateAsync(It.IsAny<ILlmRequest>()), Times.Once);
}
```

**No need to test Implementations** - they're just thin adapters.

### Integration Tests (Bootstrap)

Test full stack with **real implementations**:

```csharp
[Fact]
public async Task End_To_End_Agent_Execution()
{
    // Arrange: Real DI container
    var services = new ServiceCollection();
    services.AddNtgAdk();
    services.AddSingleton<ILlm, GeminiLlm>(); // Real adapter

    var provider = services.BuildServiceProvider();
    var runner = provider.GetRequiredService<Runner>();

    // Act
    var result = await runner.RunAsync("Hello");

    // Assert
    Assert.NotNull(result);
}
```

## Benefits of This Architecture

1. **Testability**: Operators test business logic with mocked ports
2. **Flexibility**: Swap implementations without changing Operators
3. **Clarity**: Each layer has single responsibility
4. **Scalability**: Fractal pattern allows infinite nesting
5. **Maintainability**: Technology changes isolated to Implementations
6. **Compatibility**: Boundary DTOs enable cross-language compatibility

## LLM Context: Navigating the Codebase

When exploring this codebase as an LLM, ask:

1. **Where am I?** → Parse file path to determine layer
2. **What's my layer's job?** → Refer to layer responsibilities above
3. **What can I reference?** → Check dependency rules
4. **What ports exist?** → Look in CoreAbstractions
5. **What adapters exist?** → Look in Implementations

Example:
```
Path: src/NTG.Adk.Operators/Agents/LlmAgent.cs
Layer: Operators
Can depend on: CoreAbstractions (ILlm, IAgent), Boundary (Event DTOs)
Cannot depend on: Implementations (GeminiLlm directly)
```

---

**This architecture ensures NTG.Adk is maintainable, testable, and compatible with the original Python ADK while following enterprise-grade C# patterns.** 🏗️
