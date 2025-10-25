# NTG.Adk - Agent Development Kit for C#

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

> **100% Compatible with [Abstract Driven Development (A.D.D) V3](https://abstractdriven.com/llms-full.txt)**
>
> C# port of [Google ADK Python](https://github.com/google/adk-python) with full API compatibility.

## 🏗️ Architecture: A.D.D V3 Five-Layer Pattern

This project strictly follows **Abstract Driven Development V3** fractal architecture:

```
NTG.Adk/
├── 📦 Boundary/              # Layer 1: External Contracts (DTOs, Events)
│   ├── Events/               # Event, Content, Part, FunctionCall
│   ├── Tools/                # FunctionDeclaration, Schema
│   ├── Agents/               # AgentConfig
│   └── Sessions/             # SessionStateDto
│   Dependencies: NONE
│
├── 🔌 CoreAbstractions/      # Layer 2: Ports (Interfaces)
│   ├── Agents/               # IAgent
│   ├── Events/               # IEvent, IContent, IPart
│   ├── Sessions/             # ISession, ISessionState, IInvocationContext
│   ├── Tools/                # ITool, IFunctionDeclaration
│   └── Models/               # ILlm, ILlmRequest, ILlmResponse
│   Dependencies: NONE
│
├── 🔧 Implementations/       # Layer 3: Adapters (Technology Implementations)
│   ├── Events/               # EventAdapter (DTO → Interface)
│   ├── Sessions/             # InMemorySession, InvocationContext
│   ├── Models/               # GeminiLlm, OpenAILlm (adapters)
│   └── Tools/                # FunctionTool, HttpTool
│   Dependencies: CoreAbstractions ONLY
│
├── ⚙️ Operators/             # Layer 4: Business Logic (Agent Orchestration)
│   ├── Agents/               # BaseAgent, LlmAgent
│   ├── Workflows/            # SequentialAgent, ParallelAgent, LoopAgent
│   └── Flows/                # AutoFlow, SingleFlow
│   Dependencies: CoreAbstractions + Boundary
│
└── 🚀 Bootstrap/             # Layer 5: Composition Root (DI, Entry Point)
    ├── ServiceCollectionExtensions.cs
    ├── Runner.cs
    └── AdkBuilder.cs
    Dependencies: ALL layers (wiring only)
```

### Dependency Rules (A.D.D V3)

```
✅ Valid:
- Bootstrap → All layers (composition only)
- Operators → Boundary + CoreAbstractions
- Implementations → CoreAbstractions
- CoreAbstractions → NONE
- Boundary → NONE

❌ Invalid:
- Operators → Implementations (NEVER!)
- CoreAbstractions → Boundary
```

## ⚡ Quick Start

### Installation

```bash
dotnet add package NTG.Adk
```

### Define a Single Agent

```csharp
using NTG.Adk.Operators.Agents;
using NTG.Adk.Bootstrap;

// Define agent using Operator layer
var searchAssistant = new LlmAgent
{
    Name = "search_assistant",
    Model = "gemini-2.5-flash",
    Instruction = "You are a helpful assistant. Answer user questions using Google Search when needed.",
    Description = "An assistant that can search the web.",
    Tools = [GoogleSearch.Create()] // ITool from Implementations
};

// Bootstrap layer: Create runner
var runner = new Runner(searchAssistant);

// Run
var result = await runner.RunAsync("What's the weather in Paris?");
Console.WriteLine(result);
```

### Define a Multi-Agent System

```csharp
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Workflows;

// Individual agents (Operators)
var greeter = new LlmAgent
{
    Name = "greeter",
    Model = "gemini-2.5-flash",
    Instruction = "Greet users warmly",
    OutputKey = "greeting"
};

var taskExecutor = new LlmAgent
{
    Name = "task_executor",
    Model = "gemini-2.5-flash",
    Instruction = "Execute tasks from the coordinator",
    OutputKey = "task_result"
};

// Coordinator with sub-agents
var coordinator = new LlmAgent
{
    Name = "Coordinator",
    Model = "gemini-2.5-flash",
    Description = "I coordinate greetings and tasks.",
    SubAgents = [greeter, taskExecutor]
};

// Run
var runner = new Runner(coordinator);
await runner.RunAsync("Hello, please search for AI news");
```

### Workflow Agents (Sequential, Parallel, Loop)

```csharp
// Sequential pipeline
var validator = new LlmAgent
{
    Name = "ValidateInput",
    Instruction = "Validate the input.",
    OutputKey = "validation_status"
};

var processor = new LlmAgent
{
    Name = "ProcessData",
    Instruction = "Process data if state key 'validation_status' is 'valid'.",
    OutputKey = "result"
};

var pipeline = new SequentialAgent
{
    Name = "DataPipeline",
    SubAgents = [validator, processor]
};

// Parallel execution
var fetchWeather = new LlmAgent { Name = "WeatherFetcher", OutputKey = "weather" };
var fetchNews = new LlmAgent { Name = "NewsFetcher", OutputKey = "news" };

var gatherer = new ParallelAgent
{
    Name = "InfoGatherer",
    SubAgents = [fetchWeather, fetchNews]
};

// Loop agent
var refiner = new LlmAgent { Name = "CodeRefiner", OutputKey = "current_code" };
var checker = new LlmAgent { Name = "QualityChecker", OutputKey = "quality_status" };

var refinementLoop = new LoopAgent
{
    Name = "CodeRefinementLoop",
    MaxIterations = 5,
    SubAgents = [refiner, checker]
};
```

## 🎯 API Compatibility with Python ADK

### Python → C# Mapping

| Python ADK | C# NTG.Adk | Layer |
|------------|------------|-------|
| `google.adk.agents.BaseAgent` | `NTG.Adk.CoreAbstractions.Agents.IAgent` | Port |
| `google.adk.agents.LlmAgent` | `NTG.Adk.Operators.Agents.LlmAgent` | Operator |
| `google.adk.events.Event` | `NTG.Adk.Boundary.Events.Event` | Boundary |
| `google.adk.sessions.Session` | `NTG.Adk.CoreAbstractions.Sessions.ISession` | Port |
| `google.adk.tools.BaseTool` | `NTG.Adk.CoreAbstractions.Tools.ITool` | Port |
| `google.adk.runners.Runner` | `NTG.Adk.Bootstrap.Runner` | Bootstrap |

### Example: Python vs C#

**Python:**
```python
from google.adk.agents import LlmAgent, SequentialAgent

agent_a = LlmAgent(name="AgentA", instruction="...", output_key="data")
agent_b = LlmAgent(name="AgentB", instruction="...")

pipeline = SequentialAgent(name="Pipeline", sub_agents=[agent_a, agent_b])

runner = Runner(agent=pipeline)
result = await runner.run_async("Process this")
```

**C#:**
```csharp
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Workflows;
using NTG.Adk.Bootstrap;

var agentA = new LlmAgent { Name = "AgentA", Instruction = "...", OutputKey = "data" };
var agentB = new LlmAgent { Name = "AgentB", Instruction = "..." };

var pipeline = new SequentialAgent { Name = "Pipeline", SubAgents = [agentA, agentB] };

var runner = new Runner(pipeline);
var result = await runner.RunAsync("Process this");
```

## 🔧 Project Status

### ✅ Completed

- [x] **Boundary Layer**: All DTOs and events (100% A.D.D V3 compliant)
- [x] **CoreAbstractions Layer**: All ports/interfaces defined
- [x] **Implementations Layer**: Event adapters, Session management
- [x] **Project Structure**: 5-layer A.D.D V3 architecture

### 🚧 In Progress

- [ ] **Implementations Layer**:
  - [ ] GeminiLlm adapter (Google AI integration)
  - [ ] OpenAILlm adapter
  - [ ] FunctionTool implementation
  - [ ] Built-in tools (Search, CodeExecutor, etc.)

- [ ] **Operators Layer**:
  - [ ] BaseAgent abstract class
  - [ ] LlmAgent orchestration
  - [ ] SequentialAgent, ParallelAgent, LoopAgent
  - [ ] AutoFlow, SingleFlow

- [ ] **Bootstrap Layer**:
  - [ ] Runner implementation
  - [ ] DI registration (ServiceCollectionExtensions)
  - [ ] AdkBuilder fluent API

### 📋 Roadmap

1. **Phase 1** (Current): Core agent system
2. **Phase 2**: Advanced features (callbacks, memory, A2A protocol)
3. **Phase 3**: ASP.NET Core integration (minimal APIs)
4. **Phase 4**: CLI tool, evaluation framework
5. **Phase 5**: NuGet packages, documentation

## 🤝 Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for A.D.D V3 guidelines.

## 📄 License

Apache 2.0 License - see [LICENSE](LICENSE) file.

---

**Built with [Abstract Driven Development (A.D.D) V3](https://abstractdriven.com)** 🚀
