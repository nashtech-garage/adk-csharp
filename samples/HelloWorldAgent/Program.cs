// Copyright 2025 NTG
// Simple Hello World Agent Demo with InMemoryRunner
// Demonstrates NTG.Adk with A.D.D V3 architecture + Runner pattern

using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

Console.WriteLine("=== NTG.Adk Hello World Agent with InMemoryRunner ===");
Console.WriteLine("A.D.D V3 Five-Layer Architecture + Runner Pattern Demo\n");

// Layer 3: Implementations - Create Mock LLM adapter
var llm = new MockLlm();

// Layer 4: Operators - Create agent with business logic
var agent = new LlmAgent(llm, "mock-llm")
{
    Name = "HelloAgent",
    Description = "A simple greeting agent",
    Instruction = "You are a friendly assistant. Greet the user warmly."
};

// Layer 4: Operators - Create InMemoryRunner (auto-initializes services)
var runner = new InMemoryRunner(agent, appName: "HelloWorldApp");

// Constants for session management
const string USER_ID = "user_001";
const string SESSION_ID = "session_001";

// Run the agent
Console.WriteLine("User: Hello, how are you?");
Console.WriteLine("Agent Response:");

await foreach (var evt in runner.RunAsync(USER_ID, SESSION_ID, "Hello, how are you?"))
{
    if (evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (part.Text != null)
            {
                Console.WriteLine($"[{evt.Author}] {part.Text}");
            }
        }
    }
}

Console.WriteLine("\n=== Architecture Layers ===");
Console.WriteLine("✅ Boundary: Event DTOs");
Console.WriteLine("✅ CoreAbstractions: IAgent, ILlm, ISessionService ports");
Console.WriteLine("✅ Implementations: MockLlm, InMemorySessionService adapters");
Console.WriteLine("✅ Operators: LlmAgent, InMemoryRunner orchestrators");
Console.WriteLine("✅ Bootstrap: Composition root (not needed with InMemoryRunner)");
