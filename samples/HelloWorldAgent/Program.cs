// Copyright 2025 NTG
// Simple Hello World Agent Demo
// Demonstrates NTG.Adk with A.D.D V3 architecture

using NTG.Adk.Bootstrap;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;

Console.WriteLine("=== NTG.Adk Hello World Agent ===");
Console.WriteLine("A.D.D V3 Five-Layer Architecture Demo\n");

// Layer 3: Implementations - Create Mock LLM adapter
var llm = new MockLlm();

// Layer 4: Operators - Create agent with business logic
var agent = new LlmAgent(llm, "mock-llm")
{
    Name = "HelloAgent",
    Description = "A simple greeting agent",
    Instruction = "You are a friendly assistant. Greet the user warmly."
};

// Layer 5: Bootstrap - Create runner (composition root)
var runner = new Runner(agent);

// Run the agent
Console.WriteLine("User: Hello, how are you?");
var response = await runner.RunAsync("Hello, how are you?");
Console.WriteLine($"\nAgent Response:\n{response}");

Console.WriteLine("\n=== Architecture Layers ===");
Console.WriteLine("✅ Boundary: Event DTOs");
Console.WriteLine("✅ CoreAbstractions: IAgent, ILlm ports");
Console.WriteLine("✅ Implementations: MockLlm adapter");
Console.WriteLine("✅ Operators: SimpleLlmAgent orchestrator");
Console.WriteLine("✅ Bootstrap: Runner composition root");
