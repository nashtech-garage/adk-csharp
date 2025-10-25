# AutoFlowAgent - Multi-Agent Coordination Demo

Demonstrates **AutoFlow**, a powerful feature that enables LLMs to automatically delegate tasks to specialized agents based on the nature of the query.

## What is AutoFlow?

**AutoFlow** is a dynamic agent routing mechanism where:
1. A coordinator agent automatically gets a `transfer_to_agent` tool
2. The LLM analyzes incoming queries
3. The LLM decides which specialist to delegate to
4. Execution seamlessly transfers to the chosen agent
5. Results flow back through the event stream

**No hard-coded routing logic required** - the LLM makes intelligent delegation decisions!

## Architecture

```
Coordinator (AutoFlow enabled)
├─ MathSpecialist (handles calculations)
├─ StorySpecialist (handles creative writing)
└─ CodeSpecialist (handles programming)
```

## How AutoFlow Works

### 1. Enable AutoFlow (Default: True)
```csharp
var coordinator = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "Coordinator",
    Instruction = "Route questions to appropriate specialists...",
    EnableAutoFlow = true  // Auto-adds transfer_to_agent tool
};
```

### 2. Add Sub-Agents
```csharp
coordinator.AddSubAgents(mathSpecialist, storySpecialist, codeSpecialist);
```

When `EnableAutoFlow = true` and `SubAgents.Count > 0`, the framework automatically:
- Injects the `transfer_to_agent(agent_name)` tool
- Provides tool description to the LLM
- Handles routing when the tool is called

### 3. LLM Makes Decision
The coordinator's LLM sees:
```json
{
  "name": "transfer_to_agent",
  "description": "Transfer the question to another agent. Use when delegating to specialists.",
  "parameters": {
    "agent_name": "string"
  }
}
```

Based on the query, the LLM calls: `transfer_to_agent("MathSpecialist")`

### 4. Framework Routes Automatically
```csharp
// In BaseAgent.RunAsync()
if (!string.IsNullOrEmpty(evt.Actions?.TransferTo))
{
    var targetAgent = FindAgent(evt.Actions.TransferTo);
    await foreach (var evt in targetAgent.RunAsync(context))
    {
        yield return evt;  // Stream events from specialist
    }
}
```

## Running the Sample

### Option 1: With Gemini (Recommended)
```bash
# Set API key
export GOOGLE_CLOUD_API_KEY="your-gemini-api-key"

# Run demo
dotnet run
```

### Option 2: With OpenAI
```bash
# Set API key
export OPENAI_API_KEY="your-openai-api-key"

# Run demo
dotnet run
```

### Option 3: With MockLlm (No API Key)
```bash
# Just run - will use mock responses
dotnet run
```

## Example Output

```
╔════════════════════════════════════════════════════════════╗
║         NTG.Adk - AutoFlow Multi-Agent Demo               ║
╚════════════════════════════════════════════════════════════╝

Using LLM: GeminiLlm

─────────────────────────────────────────────────────────────
Query: Calculate the square root of 144 and explain the steps
─────────────────────────────────────────────────────────────

[Coordinator] 🔧 Calling: transfer_to_agent
    agent_name: MathSpecialist
[tool] ⚙️ Tool Response: Transferring to agent: MathSpecialist
    → Transferring to: MathSpecialist
[MathSpecialist] The square root of 144 is 12.

Steps:
1. We're looking for a number that, when multiplied by itself, equals 144
2. 12 × 12 = 144
3. Therefore, √144 = 12

─────────────────────────────────────────────────────────────
Query: Write a short story about a brave robot
─────────────────────────────────────────────────────────────

[Coordinator] 🔧 Calling: transfer_to_agent
    agent_name: StorySpecialist
[tool] ⚙️ Tool Response: Transferring to agent: StorySpecialist
    → Transferring to: StorySpecialist
[StorySpecialist] In a city of gleaming towers, R0-B3RT stood alone...

╔════════════════════════════════════════════════════════════╗
║                    Demo Complete                          ║
╚════════════════════════════════════════════════════════════╝

Key AutoFlow Features Demonstrated:
✓ Automatic transfer_to_agent tool injection
✓ LLM-driven delegation decisions
✓ Hierarchical agent routing (FindAgent)
✓ Seamless execution transfer between agents
✓ Event streaming across agent boundaries
```

## Key Concepts

### 1. Tool Auto-Injection
```csharp
// In LlmAgent.GetEffectiveTools()
if (EnableAutoFlow && SubAgents.Count > 0)
{
    if (!tools.Any(t => t.Name == "transfer_to_agent"))
    {
        tools.Add(BuiltInTools.CreateTransferToAgentTool());
    }
}
```

### 2. Action Propagation
```csharp
// Tool sets action
tool_context.Actions.TransferToAgent = agent_name;

// Event carries action
return CreateFunctionResponseEvent(functionName, result, toolActions);

// BaseAgent intercepts action
if (!string.IsNullOrEmpty(evt.Actions?.TransferTo))
{
    // Route to target agent...
}
```

### 3. Hierarchical Agent Discovery
```csharp
public IAgent? FindAgent(string name)
{
    if (Name == name) return this;

    foreach (var subAgent in SubAgents)
    {
        var found = subAgent.FindAgent(name);  // Recursive search
        if (found != null) return found;
    }

    return null;
}
```

## Benefits of AutoFlow

### ✅ Dynamic Routing
LLM makes routing decisions based on query content - no hard-coded rules needed.

### ✅ Extensible
Add new specialists without changing coordinator code:
```csharp
coordinator.AddSubAgent(new TranslationSpecialist());
// LLM automatically learns about new specialist from description
```

### ✅ Composable
Specialists can have their own sub-agents:
```csharp
var mathSpecialist = new LlmAgent(...)
{
    EnableAutoFlow = true  // Math specialist can also delegate
};
mathSpecialist.AddSubAgents(algebraExpert, calculusExpert);
```

### ✅ Observable
All transfer events flow through the event stream:
```csharp
await foreach (var evt in coordinator.RunAsync(context))
{
    if (!string.IsNullOrEmpty(evt.Actions?.TransferTo))
    {
        Console.WriteLine($"Delegating to: {evt.Actions.TransferTo}");
    }
}
```

## Advanced Scenarios

### Chained Transfers
Agents can transfer to other agents in a chain:
```
User → Coordinator → MathSpecialist → AlgebraExpert
```

The framework handles multi-hop routing automatically.

### Conditional Transfer
LLM can choose to:
- Answer directly (simple queries)
- Transfer to specialist (complex queries)

```csharp
// Coordinator can answer simple greetings directly
// Or transfer complex questions to specialists
```

### Error Handling
If target agent not found:
```csharp
if (targetAgent == null)
{
    yield return CreateTextEvent(
        $"Error: Cannot transfer to '{targetAgentName}' - agent not found."
    );
    yield break;
}
```

## Comparison: With vs Without AutoFlow

### Without AutoFlow (Manual Routing)
```csharp
if (query.Contains("math"))
    result = await mathAgent.RunAsync(context);
else if (query.Contains("story"))
    result = await storyAgent.RunAsync(context);
else if (query.Contains("code"))
    result = await codeAgent.RunAsync(context);
// Hard-coded, brittle, not intelligent
```

### With AutoFlow (LLM-Driven)
```csharp
// LLM analyzes query semantics and delegates intelligently
await foreach (var evt in coordinator.RunAsync(context))
{
    yield return evt;  // Framework handles routing automatically
}
// Smart, flexible, extensible
```

## Related Documentation

- **Python ADK Reference**: See `google.adk.agents.LlmAgent` with `enable_autoflow=True`
- **Built-in Tools**: `src/NTG.Adk.Implementations/Tools/BuiltInTools.cs`
- **BaseAgent Routing**: `src/NTG.Adk.Operators/Agents/BaseAgent.cs` (RunAsync method)
- **LlmAgent AutoFlow**: `src/NTG.Adk.Operators/Agents/LlmAgent.cs` (GetEffectiveTools method)

## Troubleshooting

### AutoFlow Not Working?

**Check 1: EnableAutoFlow is true**
```csharp
coordinator.EnableAutoFlow = true  // Default, but verify
```

**Check 2: Sub-agents added**
```csharp
coordinator.AddSubAgents(specialist1, specialist2);
// Must have at least one sub-agent
```

**Check 3: Agent names match**
```csharp
// Tool call:        transfer_to_agent("MathSpecialist")
// Agent name must match exactly:
var specialist = new LlmAgent(...) { Name = "MathSpecialist" };
```

**Check 4: LLM has function calling support**
- Gemini: ✅ All models support function calling
- OpenAI: ✅ All chat models support function calling
- MockLlm: ⚠️ Returns canned responses, doesn't execute tools

### LLM Not Calling transfer_to_agent?

**Improve coordinator instructions:**
```csharp
Instruction = @"You MUST use transfer_to_agent for ALL user queries.
               Analyze the query and delegate to the appropriate specialist.
               Do NOT answer directly - always transfer."
```

**Add examples to instruction:**
```csharp
Instruction = @"Examples:
               - Math query → transfer_to_agent('MathSpecialist')
               - Story request → transfer_to_agent('StorySpecialist')
               - Code question → transfer_to_agent('CodeSpecialist')"
```

## License

Copyright 2025 NTG
Licensed under the Apache License, Version 2.0
